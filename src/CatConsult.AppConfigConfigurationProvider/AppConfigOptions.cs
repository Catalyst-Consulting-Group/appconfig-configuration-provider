namespace CatConsult.AppConfigConfigurationProvider;

public class AppConfigOptions
{
    public List<string> Profiles { get; set; } = new();

    public List<string> FeatureFlags { get; set; } = new();

    public Defaults Defaults { get; set; } = new();
}

public class Defaults
{
    public int ReloadAfter { get; set; } = 60;

    public FeatureFlagDefaults FeatureFlags { get; set; } = new();
}

public class FeatureFlagDefaults
{
    public string SectionName { get; set; } = "FeatureManagement";
}