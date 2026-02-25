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
    private const string ArnPrefix = "arn:";
    private const string SecretsManagerService = "secretsmanager";
    private const int ExpectedMinSegments = 7; // ARN format has 7 colon-delimited segments: arn:{partition}:{service}:{region}:{accountId}:secret:{secretName}
    public string FullArn { get; }  // The full original ARN string (ex: "arn:aws-us-gov:secretsmanager:us-gov-west-1:123456789012:secret:sit/examplesecret")
    public string Partition { get; }  // AWS partition (ex: "aws" for standard, "aws-us-gov" for GovCloud)
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

    // Attempts to parse a string into a SecretsManagerArn
    public static bool TryParse(string? value, out SecretsManagerArn? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(ArnPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false; // value is not a valid ARN string
        }

        // ARN format: arn:{partition}:{service}:{region}:{accountId}:secret:{secretName}
        // Split into max 7 segments (the secret name may contain colons)
        var segments = value.Split(new[] { ':' }, ExpectedMinSegments);

        if (segments.Length < ExpectedMinSegments)
        {
            return false;
        }

        var partition = segments[1];
        var service = segments[2]; // should be "secretsmanager"
        var region = segments[3];
        var accountId = segments[4];
        var resourceType = segments[5]; // should be "secret"
        var secretName = segments[6];

        if (!string.Equals(service, SecretsManagerService, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(resourceType, "secret", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If partition or region is invalid, the SecretsManagerSecretResolver Secrets Manager API call will fail with AWS error at app startup
        if (string.IsNullOrWhiteSpace(partition) ||
            string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(accountId) ||
            string.IsNullOrWhiteSpace(secretName))
        {
            return false;
        }

        result = new SecretsManagerArn(value, partition, region, accountId, secretName);
        return true;
    }
}