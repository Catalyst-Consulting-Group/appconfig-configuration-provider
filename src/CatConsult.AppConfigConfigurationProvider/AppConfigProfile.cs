namespace CatConsult.AppConfigConfigurationProvider;

public record AppConfigProfile(string ApplicationId, string EnvironmentId, string ProfileId)
{
    public bool IsFeatureFlag { get; set; }

    public int? ReloadAfter { get; set; }
}