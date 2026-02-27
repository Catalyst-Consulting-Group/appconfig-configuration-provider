namespace CatConsult.AppConfigConfigurationProvider;

/// <summary>
/// Represents a single AWS AppConfig configuration profile
///
/// AWS AppConfig organizes configuration data in a hierarchy:
///   Application -> Environment -> Configuration Profile(s)
///
/// Configuration Profile: A specific configuration document within that application/environment.
///
/// In appsettings.$ENV.json, profiles are declared as colon-delimited strings: "ApplicationId:EnvironmentId:ProfileId"
/// </summary>
public class AppConfigProfile
{
    public AppConfigProfile(string applicationId, string environmentId, string profileId)
    {
        ApplicationId = applicationId;
        EnvironmentId = environmentId;
        ProfileId = profileId;
    }

    public string ApplicationId { get; }
    public string EnvironmentId { get; }
    public string ProfileId { get; }

    // Indicates whether the profile is a feature flag configuration profile
    // Feature flag profiles are parsed differently, we use FeatureFlagsProfileParser instead of the standard JSON/YAML parser
    public bool IsFeatureFlag { get; set; }

    // polling interval in seconds that controls how often configuration refresh attempts are made
    public int? ReloadAfter { get; set; }
}