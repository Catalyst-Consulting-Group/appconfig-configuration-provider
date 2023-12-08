namespace CatConsult.AppConfigConfigurationProvider;

public class AppConfigOptions
{
    public List<string> Profiles { get; set; } = new();

    public Defaults Defaults { get; set; } = new();
}

public class Defaults
{
    public int ReloadAfter { get; set; } = 60;
}