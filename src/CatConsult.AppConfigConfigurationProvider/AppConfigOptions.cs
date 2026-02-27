namespace CatConsult.AppConfigConfigurationProvider;


/// <summary>
/// Options that define which AWS AppConfig configuration sources to load at startup
/// Populated from the "AppConfig" section in appsettings.$ENV.json.
///
/// Each profile's AppConfig data can contain a mix of regular config values and Secrets Manager ARN references
/// When secret resolution is enabled in SecretsManagerOptions, the provider detects ARN values and resolves them to their actual secret strings
/// </summary>
public class AppConfigOptions
{
    // List of AWS AppConfig hosted configuration profile identifiers to load (format: "ApplicationId:EnvironmentId:ProfileId")
    public List<string> Profiles { get; set; } = new();

    // List of AppConfig feature flag profile identifiers to load (same format as Profiles)
    public List<string> FeatureFlags { get; set; } = new();

    public Defaults Defaults { get; set; } = new();

    public SecretsManagerOptions SecretsManager { get; set; } = new();
}

public class Defaults
{
    public int ReloadAfter { get; set; } = 60;
}

public class SecretsManagerOptions
{
    public bool Enabled { get; set; } = false;

    // How long in seconds resolved secrets remain cached before being re-fetched. Default: 300 (5 minutes)
    public int CacheTtlSeconds { get; set; } = 300;
}