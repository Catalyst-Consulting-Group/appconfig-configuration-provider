namespace CatConsult.AppConfigConfigurationProvider;

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

    public bool IsFeatureFlag { get; set; }

    public int? ReloadAfter { get; set; }
}