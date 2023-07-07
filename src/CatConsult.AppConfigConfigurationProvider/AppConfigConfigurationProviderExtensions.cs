using CatConsult.AppConfigConfigurationProvider.Utilities;

using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider;

public static class AppConfigConfigurationProviderExtensions
{
    public static IConfigurationBuilder AddAppConfig(
        this IConfigurationBuilder builder,
        string sectionName = "AppConfig"
    )
    {
        var options = builder.Build()
            .GetSection(sectionName)
            .Get<AppConfigOptions>();

        if (options is null)
        {
            return builder;
        }

        var profiles = options.Profiles.Select(AppConfigProfileParser.Parse);

        foreach (var profile in profiles)
        {
            builder.AddAppConfig(
                profile.ApplicationId,
                profile.EnvironmentId,
                profile.ProfileId,
                TimeSpan.FromSeconds(profile.ReloadAfter ?? options.Defaults.ReloadAfter)
            );
        }

        return builder;
    }
}
