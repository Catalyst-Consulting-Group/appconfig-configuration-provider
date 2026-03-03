using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CatConsult.AppConfigConfigurationProvider.Secrets;
using FluentAssertions;
using Moq;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class SecretsManagerSecretResolverTests
{
    private const string TestArn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret";
    private const string TestSecretValue = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
    private const int DefaultCacheTtlSeconds = 300;

    private readonly Mock<IAmazonSecretsManager> _mockClient = new();

    [Fact]
    public async Task ResolveSecretAsync_FetchesSecretFromSecretsManager()
    {
        SetupMockResponse(TestArn, TestSecretValue);

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var result = await sut.ResolveSecretAsync(TestArn);

        result.Should().Be(TestSecretValue);
        VerifyGetSecretValueCalled(Times.Once()); // Verify only one AWS call was made
    }

    [Fact]
    public async Task ResolveSecretAsync_CachedValueReturnedWithinTtl()
    {
        SetupMockResponse(TestArn, TestSecretValue);

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        // First call: fetches from AWS
        var result1 = await sut.ResolveSecretAsync(TestArn);
        // Second call: should return cached value
        var result2 = await sut.ResolveSecretAsync(TestArn);

        result1.Should().Be(TestSecretValue);
        result2.Should().Be(TestSecretValue);

        VerifyGetSecretValueCalled(Times.Once()); // Verify only one AWS call was made
    }

    [Fact]
    public async Task ResolveSecretAsync_RefetchesAfterTtlExpires()
    {
        const string updatedSecret = "{\"username\":\"admin\",\"password\":\"n3wP@ss\"}";

        // Use a short TTL so it expires between calls
        const int shortTtlSeconds = 1;

        _mockClient
            .SetupSequence(c => c.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == TestArn),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = TestSecretValue })
            .ReturnsAsync(new GetSecretValueResponse { SecretString = updatedSecret });

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, shortTtlSeconds);

        var result1 = await sut.ResolveSecretAsync(TestArn);
        result1.Should().Be(TestSecretValue);

        // Wait for TTL to expire
        await Task.Delay(TimeSpan.FromSeconds(shortTtlSeconds + 0.5));

        var result2 = await sut.ResolveSecretAsync(TestArn);
        result2.Should().Be(updatedSecret);

        VerifyGetSecretValueCalled(Times.Exactly(2)); // Verify exactly 2 AWS calls were made
    }

    // Verifies that each ARN gets its own independent cache entry, resolving two different ARNs should return two different secret values
    [Fact]
    public async Task ResolveSecretAsync_DifferentArns_CachedIndependently()
    {
        const string arn2 = "arn:aws:secretsmanager:us-east-1:123456789012:secret:other-secret";
        const string secret2 = "other-secret-value";

        SetupMockResponse(TestArn, TestSecretValue);
        SetupMockResponse(arn2, secret2);

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var result1 = await sut.ResolveSecretAsync(TestArn);
        var result2 = await sut.ResolveSecretAsync(arn2);

        result1.Should().Be(TestSecretValue);
        result2.Should().Be(secret2);
    }

    // Verifies that the lock prevents redundant AWS calls, 10 concurrent requests for the same ARN should result in only a single Secrets Manager fetch
    [Fact]
    public async Task ResolveSecretAsync_ConcurrentCalls_OnlySingleFetch()
    {
        var callCount = 0;

        _mockClient
            .Setup(c => c.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == TestArn),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return new GetSecretValueResponse { SecretString = TestSecretValue };
            });

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        // Fire multiple concurrent resolve calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.ResolveSecretAsync(TestArn))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().AllBe(TestSecretValue);

        // Only one AWS call should be made
        callCount.Should().Be(1);
    }

    // Verifies that AWS access errors propagate to the caller rather than being handled, app should fail at startup with a clear error if it can't access a secret
    [Fact]
    public async Task ResolveSecretAsync_AccessDenied_PropagatesException()
    {
        _mockClient
            .Setup(c => c.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new AmazonSecretsManagerException("Access denied"));

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var act = () => sut.ResolveSecretAsync(TestArn);

        await act.Should().ThrowAsync<AmazonSecretsManagerException>()
            .WithMessage("*Access denied*");
    }

    // Verifies that AWS missing/nonexistent secret errors propagate to the caller rather than being handled, app should fail at startup with a clear error if it can't find a secret
    [Fact]
    public async Task ResolveSecretAsync_SecretNotFound_PropagatesException()
    {
        _mockClient
            .Setup(c => c.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new ResourceNotFoundException("Secret not found"));

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var act = () => sut.ResolveSecretAsync(TestArn);

        await act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Fact]
    public async Task ResolveSecretAsync_BinarySecret_ThrowsInvalidOperationException()
    {
        _mockClient
            .Setup(c => c.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = null, // binary-only secret
                SecretBinary = new MemoryStream(new byte[] { 0x00, 0x01 }),
            });

        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var act = () => sut.ResolveSecretAsync(TestArn);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*did not return a SecretString value*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveSecretAsync_NullOrEmptyArn_ThrowsArgumentException(string? arn)
    {
        using var sut = new SecretsManagerSecretResolver(_mockClient.Object, DefaultCacheTtlSeconds);

        var act = () => sut.ResolveSecretAsync(arn!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Helpers

    private void SetupMockResponse(string arn, string secretValue)
    {
        _mockClient
            .Setup(c => c.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == arn),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = secretValue
            });
    }

    private void VerifyGetSecretValueCalled(Times times)
    {
        _mockClient.Verify(c => c.GetSecretValueAsync(
            It.IsAny<GetSecretValueRequest>(),
            It.IsAny<CancellationToken>()
        ), times);
    }
}