using System.Text;

using Amazon.AppConfigData;
using Amazon.AppConfigData.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using CatConsult.AppConfigConfigurationProvider.Secrets;

using FluentAssertions;

using Moq;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class AppConfigConfigurationProviderSecretsIntegrationTests
{
    private const string TestArn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret";
    private const string TestSecretValue = "resolved-secret-value";
    private const int DefaultCacheTtlSeconds = 300;

    private readonly Mock<IAmazonAppConfigData> _mockAppConfigClient = new();
    private readonly Mock<IAmazonSecretsManager> _mockSecretsClient = new();

    // --- Enabled = true: ARN values should be resolved ---

    [Fact]
    public void Load_WhenEnabled_ResolvesArnValues()
    {
        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);

        var sut = CreateProvider(
            configJson: $@"{{""SecretKey"":""{TestArn}"",""NormalKey"":""plain-value""}}",
            secretResolver: resolver
        );

        sut.Load();

        // ARN value should be resolved to the actual secret
        sut.TryGet("SecretKey", out var secretValue).Should().BeTrue();
        secretValue.Should().Be(TestSecretValue);

        // Non-ARN value should be left unchanged
        sut.TryGet("NormalKey", out var normalValue).Should().BeTrue();
        normalValue.Should().Be("plain-value");
    }

    [Fact]
    public void Load_WhenEnabled_MultipleArnsResolved()
    {
        const string arn2 = "arn:aws:secretsmanager:us-east-1:123456789012:secret:other-secret";
        const string secret2 = "other-resolved-value";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);
        SetupSecretsManagerResponse(arn2, secret2);

        var sut = CreateProvider(
            configJson: $@"{{""Secret1"":""{TestArn}"",""Secret2"":""{arn2}""}}",
            secretResolver: resolver
        );

        sut.Load();

        sut.TryGet("Secret1", out var value1).Should().BeTrue();
        value1.Should().Be(TestSecretValue);

        sut.TryGet("Secret2", out var value2).Should().BeTrue();
        value2.Should().Be(secret2);
    }

    [Fact]
    public void Load_WhenEnabled_NullAndEmptyValuesSkipped()
    {
        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);

        var sut = CreateProvider(
            configJson: $@"{{""SecretKey"":""{TestArn}"",""EmptyKey"":"""",""NullKey"":null}}",
            secretResolver: resolver
        );

        sut.Load();

        sut.TryGet("SecretKey", out var secretValue).Should().BeTrue();
        secretValue.Should().Be(TestSecretValue);

        sut.TryGet("EmptyKey", out var emptyValue).Should().BeTrue();
        emptyValue.Should().Be("");
    }

    // --- Enabled = false: ARN values should NOT be resolved ---

    [Fact]
    public void Load_WhenDisabled_LeavesArnValuesAsIs()
    {
        // No resolver passed — simulates SecretsManager.Enabled = false
        var sut = CreateProvider(
            configJson: $@"{{""SecretKey"":""{TestArn}"",""NormalKey"":""plain-value""}}",
            secretResolver: null
        );

        sut.Load();

        // ARN value should be left as the raw ARN string
        sut.TryGet("SecretKey", out var secretValue).Should().BeTrue();
        secretValue.Should().Be(TestArn);

        // Non-ARN value should be unchanged
        sut.TryGet("NormalKey", out var normalValue).Should().BeTrue();
        normalValue.Should().Be("plain-value");
    }

    // --- Error handling ---

    [Fact]
    public void Load_WhenSecretResolutionFails_ThrowsWithConfigKey()
    {
        _mockSecretsClient
            .Setup(c => c.GetSecretValueAsync(
                It.IsAny<GetSecretValueRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new Amazon.SecretsManager.Model.ResourceNotFoundException("Secret not found"));

        var resolver = CreateResolver();

        var sut = CreateProvider(
            configJson: $@"{{""BadSecret"":""{TestArn}""}}",
            secretResolver: resolver
        );

        // Should throw with context about which config key failed
        var act = () => sut.Load();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BadSecret*")
            .WithMessage($"*{TestArn}*")
            .WithInnerException<Amazon.SecretsManager.Model.ResourceNotFoundException>();
    }

    // --- TTL caching across reloads ---

    [Fact]
    public void Load_CalledTwice_SecretsCachedWithinTtl()
    {
        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);

        // Return same AppConfig content on both loads (simulate reload with no config change)
        var configJson = $@"{{""SecretKey"":""{TestArn}""}}";

        var sut = CreateProviderWithMultipleLoads(configJson, resolver);

        // Load twice
        sut.Load();
        sut.Load();

        // Secrets Manager should only have been called once — second load used cache
        _mockSecretsClient.Verify(c => c.GetSecretValueAsync(
            It.IsAny<GetSecretValueRequest>(),
            It.IsAny<CancellationToken>()
        ), Times.Once());
    }

    // --- Helpers ---

    private SecretsManagerSecretResolver CreateResolver()
    {
        return new SecretsManagerSecretResolver(_mockSecretsClient.Object, DefaultCacheTtlSeconds);
    }

    private void SetupSecretsManagerResponse(string arn, string secretValue)
    {
        _mockSecretsClient
            .Setup(c => c.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == arn),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new GetSecretValueResponse
            {
                SecretString = secretValue
            });
    }

    private AppConfigConfigurationProvider CreateProvider(string configJson, SecretsManagerSecretResolver? secretResolver)
    {
        var profile = new AppConfigProfile("test", "foo", "bar")
        {
            ReloadAfter = null, // disable auto-reload for tests
        };

        SetupAppConfigMock(configJson);

        return new AppConfigConfigurationProvider(_mockAppConfigClient.Object, profile, secretResolver);
    }

    // Creates a provider that returns content on every GetLatestConfiguration call (for reload/TTL tests)
    private AppConfigConfigurationProvider CreateProviderWithMultipleLoads(string configJson, SecretsManagerSecretResolver? secretResolver)
    {
        var profile = new AppConfigProfile("test", "foo", "bar")
        {
            ReloadAfter = null,
        };

        _mockAppConfigClient
            .Setup(p => p.StartConfigurationSessionAsync(
                It.IsAny<StartConfigurationSessionRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new StartConfigurationSessionResponse());

        // Return content on every call so the provider re-parses and re-resolves
        _mockAppConfigClient
            .Setup(p => p.GetLatestConfigurationAsync(
                It.IsAny<GetLatestConfigurationRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(() => new GetLatestConfigurationResponse
            {
                ContentLength = 1,
                ContentType = "application/json",
                Configuration = new MemoryStream(Encoding.UTF8.GetBytes(configJson)),
                NextPollIntervalInSeconds = -1,
                NextPollConfigurationToken = "token",
            });

        return new AppConfigConfigurationProvider(_mockAppConfigClient.Object, profile, secretResolver);
    }

    private void SetupAppConfigMock(string configJson)
    {
        _mockAppConfigClient
            .Setup(p => p.StartConfigurationSessionAsync(
                It.IsAny<StartConfigurationSessionRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new StartConfigurationSessionResponse());

        _mockAppConfigClient
            .Setup(p => p.GetLatestConfigurationAsync(
                It.IsAny<GetLatestConfigurationRequest>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new GetLatestConfigurationResponse
            {
                ContentLength = 1,
                ContentType = "application/json",
                Configuration = new MemoryStream(Encoding.UTF8.GetBytes(configJson)),
                NextPollIntervalInSeconds = -1,
                NextPollConfigurationToken = "foobar",
            });
    }
}