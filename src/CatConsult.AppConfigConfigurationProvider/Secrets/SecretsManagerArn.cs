namespace CatConsult.AppConfigConfigurationProvider.Secrets;

/// <summary>
/// Detects whether a configuration value is a Secrets Manager ARN and parses it into its components
///
/// Used by the AppConfigConfigurationProvider (todo in SAFE-1875 ticket) to determine which AppConfig values are secret references
/// that need to be resolved, versus normal config values that should be left as-is
///
/// Supports both standard and GovCloud ARN formats:
///   arn:aws:secretsmanager:{region}:{accountId}:secret:{secretName}
///   arn:aws-us-gov:secretsmanager:{region}:{accountId}:secret:{secretName}
/// </summary>
public class SecretsManagerArn
{
    public string FullArn { get; } // The full original ARN string (ex: "arn:aws-us-gov:secretsmanager:us-gov-west-1:123456789012:secret:sit/examplesecret")
    public string Partition { get; } // AWS partition (ex: "aws" for standard, "aws-us-gov" for GovCloud)
    public string Region { get; } // AWS region (ex: "us-gov-west-1")
    public string AccountId { get; }
    public string SecretName { get; }

    private SecretsManagerArn(string fullArn, string partition, string region, string accountId, string secretName)
    {
        FullArn = fullArn;
        Partition = partition;
        Region = region;
        AccountId = accountId;
        SecretName = secretName;
    }

    // Determines whether a configuration value is an AWS Secrets Manager ARN 
    public static bool IsSecretsManagerArn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return TryParse(value, out _);
    }

    // Attempts to parse a string into a SecretsManagerArn using the AWS SDK's built-in Arn parser
    public static bool TryParse(string? value, out SecretsManagerArn? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value) || !Amazon.Arn.IsArn(value))
        {
            return false; // value is not a valid ARN string
        }

        Amazon.Arn arn;
        try
        {
            arn = Amazon.Arn.Parse(value);
        }
        catch
        {
            return false;
        }

        if (!string.Equals(arn.Service, "secretsmanager", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // arn.Resource is "secret:{secretName}"
        if (!arn.Resource.StartsWith("secret:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var secretName = arn.Resource.Substring(7); // after "secret:"

        // Reject ARNs with empty or whitespace-only secret names
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return false;
        }

        result = new SecretsManagerArn(value!, arn.Partition, arn.Region, arn.AccountId, secretName);
        return true;
    }
}