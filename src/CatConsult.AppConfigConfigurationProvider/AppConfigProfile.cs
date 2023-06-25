namespace CatConsult.AppConfigConfigurationProvider;

public record AppConfigProfile(string ApplicationId, string EnvironmentId, string ProfileId)
{
    public int? ReloadAfter { get; set; }
}
