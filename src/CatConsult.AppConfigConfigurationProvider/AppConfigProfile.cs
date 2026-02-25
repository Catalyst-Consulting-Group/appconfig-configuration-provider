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
public record AppConfigProfile(string ApplicationId, string EnvironmentId, string ProfileId)
{
    // Indicates whether the profile is a feature flag configuration profile
    // Feature flag profiles are parsed differently, we use FeatureFlagsProfileParser instead of the standard JSON/YAML parser
    public bool IsFeatureFlag { get; set; }

    // polling interval in seconds that controls how often configuration refresh attempts are made
    public int? ReloadAfter { get; set; }
}