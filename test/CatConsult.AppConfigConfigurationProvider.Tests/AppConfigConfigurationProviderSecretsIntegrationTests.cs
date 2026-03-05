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

    // Enabled = true: ARN values should be resolved 

    // Verifies that when secret resolution is enabled, ARN values are resolved to actual secrets while normal config values are left unchanged
    [Fact]
    public void Load_WhenEnabled_ResolvesArnValues()
    {
        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);

        // Config with a mix of an ARN value and a normal value
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

    // Verifies that when multiple config values are ARNs, each one is independently resolved to its actual secret
    [Fact]
    public void Load_WhenEnabled_MultipleArnsResolved()
    {
        const string arn2 = "arn:aws:secretsmanager:us-east-1:123456789012:secret:other-secret";
        const string secret2 = "other-resolved-value";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);
        SetupSecretsManagerResponse(arn2, secret2);

        // Config with two different ARN values
        var sut = CreateProvider(
            configJson: $@"{{""Secret1"":""{TestArn}"",""Secret2"":""{arn2}""}}",
            secretResolver: resolver
        );

        sut.Load();

        // Both ARNs should be resolved to their respective secret values
        sut.TryGet("Secret1", out var value1).Should().BeTrue();
        value1.Should().Be(TestSecretValue);

        sut.TryGet("Secret2", out var value2).Should().BeTrue();
        value2.Should().Be(secret2);
    }

    // Verifies that null and empty config values are skipped during secret resolution
    [Fact]
    public void Load_WhenEnabled_NullAndEmptyValuesSkipped()
    {
        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, TestSecretValue);

        // Config with an ARN, an empty string, and a null value
        var sut = CreateProvider(
            configJson: $@"{{""SecretKey"":""{TestArn}"",""EmptyKey"":"""",""NullKey"":null}}",
            secretResolver: resolver
        );

        sut.Load();

        // ARN should be resolved as normal
        sut.TryGet("SecretKey", out var secretValue).Should().BeTrue();
        secretValue.Should().Be(TestSecretValue);

        // Empty value should be left as-is, not treated as an ARN
        sut.TryGet("EmptyKey", out var emptyValue).Should().BeTrue();
        emptyValue.Should().Be("");
    }

    // Verifies that when a resolved secret is a JSON object, it's parsed and flattened with the original config key as prefix
    [Fact]
    public void Load_WhenEnabled_JsonSecretFlattenedWithPrefix()
    {
        const string jsonSecret = @"{""Username"":""admin"",""Password"":""secret""}";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, jsonSecret);

        var sut = CreateProvider(
            configJson: $@"{{""Database"":""{TestArn}""}}",
            secretResolver: resolver
        );

        sut.Load();

        // JSON secret should be flattened with "Database" as prefix
        sut.TryGet("Database:Username", out var username).Should().BeTrue();
        username.Should().Be("admin");

        sut.TryGet("Database:Password", out var password).Should().BeTrue();
        password.Should().Be("secret");

        // The original key should not exist as a single value
        sut.TryGet("Database", out _).Should().BeFalse();
    }

    // Verifies that nested JSON secrets are fully flattened with colon-delimited keys
    [Fact]
    public void Load_WhenEnabled_NestedJsonSecretFullyFlattened()
    {
        const string nestedJsonSecret = @"{""Db"":{""Host"":""localhost"",""Port"":""5432""}}";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, nestedJsonSecret);

        var sut = CreateProvider(
            configJson: $@"{{""Connection"":""{TestArn}""}}",
            secretResolver: resolver
        );

        sut.Load();

        // Nested JSON should be fully flattened: Connection:Db:Host, Connection:Db:Port
        sut.TryGet("Connection:Db:Host", out var host).Should().BeTrue();
        host.Should().Be("localhost");

        sut.TryGet("Connection:Db:Port", out var port).Should().BeTrue();
        port.Should().Be("5432");
    }

    // Verifies that plain string secrets still resolve as single string values
    [Fact]
    public void Load_WhenEnabled_PlainStringSecretUnchanged()
    {
        const string plainSecret = "my-api-key-12345";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(TestArn, plainSecret);

        var sut = CreateProvider(
            configJson: $@"{{""ApiKey"":""{TestArn}""}}",
            secretResolver: resolver
        );

        sut.Load();

        // Plain string should be set as a single value, not parsed as JSON
        sut.TryGet("ApiKey", out var value).Should().BeTrue();
        value.Should().Be(plainSecret);
    }

    // Verifies that a mix of JSON and plain string secrets are handled correctly in the same config
    [Fact]
    public void Load_WhenEnabled_MixedJsonAndStringSecrets()
    {
        const string jsonArn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:db-creds";
        const string jsonSecret = @"{""User"":""admin"",""Pass"":""s3cr3t""}";
        const string stringSecret = "plain-api-key";

        var resolver = CreateResolver();
        SetupSecretsManagerResponse(jsonArn, jsonSecret);
        SetupSecretsManagerResponse(TestArn, stringSecret);

        var sut = CreateProvider(
            configJson: $@"{{""Database"":""{jsonArn}"",""ApiKey"":""{TestArn}""}}",
            secretResolver: resolver
        );

        sut.Load();

        // JSON secret should be flattened
        sut.TryGet("Database:User", out var user).Should().BeTrue();
        user.Should().Be("admin");

        sut.TryGet("Database:Pass", out var pass).Should().BeTrue();
        pass.Should().Be("s3cr3t");

        // Plain string secret should be a single value
        sut.TryGet("ApiKey", out var apiKey).Should().BeTrue();
        apiKey.Should().Be(stringSecret);
    }

    // Enabled = false: ARN values should NOT be resolved

    // Verifies that when secret resolution is disabled , ARN values are left as raw ARN strings in the config - no resolution attempt is made
    [Fact]
    public void Load_WhenDisabled_LeavesArnValuesAsIs()
    {
        // No resolver passed simulates SecretsManager.Enabled = false
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

    // Error handling

    // Verifies that when a secret can't be resolved (ex: secret doesn't exist in AWS),
    // the error is wrapped in an InvalidOperationException that includes the config key name
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

    // TTL caching across reloads

    // Verifies that resolved secrets are cached across provider reloads
    // When Load() is called twice with the same ARN, Secrets Manager should only be called once 
    // the second load should use the cached value from the shared resolver
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

        // Secrets Manager should only have been called once, second load used cache
        _mockSecretsClient.Verify(c => c.GetSecretValueAsync(
            It.IsAny<GetSecretValueRequest>(),
            It.IsAny<CancellationToken>()
        ), Times.Once());
    }

    // Helpers

    private SecretsManagerSecretResolver CreateResolver()
    {
        return new SecretsManagerSecretResolver(_mockSecretsClient.Object, DefaultCacheTtlSeconds);
    }

    // Creates a resolver with the mocked Secrets Manager client for testing
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

    // Creates a provider with a mocked AppConfig client that returns the given JSON config
    // Auto-reload is disabled so tests only fetch when Load() is explicitly called
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

    // Sets up the mocked AppConfig client to return a session and the given JSON config content
    // NextPollIntervalInSeconds is set to -1 so the polling interval doesn't block subsequent Load() calls in tests
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