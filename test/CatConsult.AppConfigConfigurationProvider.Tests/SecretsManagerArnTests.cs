using CatConsult.AppConfigConfigurationProvider.Secrets;
using FluentAssertions;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class SecretsManagerArnTests
{
    // Valid ARNs

    [Theory]
    [InlineData("arn:aws:secretsmanager:us-east-1:123456789012:secret:my-secret")]
    [InlineData("arn:aws:secretsmanager:us-west-2:123456789012:secret:prod/api/key")]
    [InlineData("arn:aws:secretsmanager:eu-west-1:000000000000:secret:some-secret-AbCdEf")]
    [InlineData("arn:aws-us-gov:secretsmanager:us-gov-west-1:123456789012:secret:sit/test-secret")]
    [InlineData("arn:aws-cn:secretsmanager:cn-north-1:123456789012:secret:my-secret")]
    public void IsSecretsManagerArn_ValidArns_ReturnsTrue(string arn)
    {
        SecretsManagerArn.IsSecretsManagerArn(arn).Should().BeTrue();
    }

    [Fact]
    public void TryParse_ValidStandardArn_ExtractsCorrectComponents()
    {
        const string arn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:my-app/db-password";

        SecretsManagerArn.TryParse(arn, out var result).Should().BeTrue();

        result.Should().NotBeNull();
        result!.FullArn.Should().Be(arn);
        result.Partition.Should().Be("aws");
        result.Region.Should().Be("us-east-1");
        result.AccountId.Should().Be("123456789012");
        result.SecretName.Should().Be("my-app/db-password");
    }

    [Fact]
    public void TryParse_ValidGovCloudArn_ExtractsCorrectComponents()
    {
        const string arn = "arn:aws-us-gov:secretsmanager:us-gov-west-1:123456789012:secret:sit/test-secret";

        SecretsManagerArn.TryParse(arn, out var result).Should().BeTrue();

        result.Should().NotBeNull();
        result!.Partition.Should().Be("aws-us-gov");
        result.Region.Should().Be("us-gov-west-1");
        result.AccountId.Should().Be("123456789012");
        result.SecretName.Should().Be("sit/test-secret");
    }

    [Fact]
    public void TryParse_ArnWithColonsInSecretName_PreservesFullSecretName()
    {
        // Secret name portion can contain colons (e.g., version/stage suffixes)
        const string arn = "arn:aws:secretsmanager:us-east-1:123456789012:secret:my-secret:version-id:stage";

        SecretsManagerArn.TryParse(arn, out var result).Should().BeTrue();
        result!.SecretName.Should().Be("my-secret:version-id:stage");
    }


    // Invalid ARNs

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSecretsManagerArn_NullOrWhitespace_ReturnsFalse(string? value)
    {
        SecretsManagerArn.IsSecretsManagerArn(value).Should().BeFalse();
    }

    [Fact]
    public void IsSecretsManagerArn_PlainConfigValue_ReturnsFalse()
    {
        SecretsManagerArn.IsSecretsManagerArn("some-config-value").Should().BeFalse();
    }

    [Theory]
    [InlineData("arn:aws:s3:::my-bucket")]
    [InlineData("arn:aws:sqs:us-east-1:123456789012:my-queue")]
    [InlineData("arn:aws:iam::123456789012:role/my-role")]
    [InlineData("arn:aws:lambda:us-east-1:123456789012:function:my-function")]
    [InlineData("arn:aws:dynamodb:us-east-1:123456789012:table/my-table")]
    public void IsSecretsManagerArn_OtherAwsServiceArns_ReturnsFalse(string arn)
    {
        SecretsManagerArn.IsSecretsManagerArn(arn).Should().BeFalse();
    }

    [Theory]
    [InlineData("arn:aws:secretsmanager")]
    [InlineData("arn:aws:secretsmanager:us-east-1")]
    [InlineData("arn:aws:secretsmanager:us-east-1:123456789012")]
    [InlineData("arn:aws:secretsmanager:us-east-1:123456789012:secret")]
    public void IsSecretsManagerArn_TruncatedArns_ReturnsFalse(string arn)
    {
        SecretsManagerArn.IsSecretsManagerArn(arn).Should().BeFalse();
    }

    [Fact]
    public void IsSecretsManagerArn_WrongResourceType_ReturnsFalse()
    {
        // "key" instead of "secret"
        SecretsManagerArn.IsSecretsManagerArn("arn:aws:secretsmanager:us-east-1:123456789012:key:my-secret").Should().BeFalse();
    }

    [Fact]
    public void IsSecretsManagerArn_EmptySecretName_ReturnsFalse()
    {
        SecretsManagerArn.IsSecretsManagerArn("arn:aws:secretsmanager:us-east-1:123456789012:secret: ").Should().BeFalse();
    }

    [Fact]
    public void TryParse_InvalidArn_OutputsNull()
    {
        SecretsManagerArn.TryParse("not-an-arn", out var result).Should().BeFalse();
        result.Should().BeNull();
    }
}