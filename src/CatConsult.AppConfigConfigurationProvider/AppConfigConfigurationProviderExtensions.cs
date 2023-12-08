using Amazon.AppConfigData;

using CatConsult.AppConfigConfigurationProvider.Utilities;

using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
public static class AppConfigConfigurationProviderExtensions
{
    public const string DefaultSectionName = "AppConfig";

    public static IConfigurationBuilder AddAppConfig(
        this IConfigurationBuilder builder,
        string sectionName = DefaultSectionName
    )
    {
        foreach (var profile in LoadProfiles(builder, sectionName))
        {
            builder.Add(new AppConfigConfigurationSource(profile));
        }

        return builder;
    }

    public static IConfigurationBuilder AddAppConfig(
        this IConfigurationBuilder builder,
        IAmazonAppConfigData client,
        string sectionName = DefaultSectionName
    )
    {
        foreach (var profile in LoadProfiles(builder, sectionName))
        {
            builder.Add(new AppConfigConfigurationSource(client, profile));
        }

        return builder;
    }

    private static IEnumerable<AppConfigProfile> LoadProfiles(IConfigurationBuilder builder, string sectionName)
    {
        var options = builder.Build()
            .GetSection(sectionName)
            .Get<AppConfigOptions>() ?? new AppConfigOptions();

        return options.Profiles.Select(p => AppConfigProfileParser.Parse(p, options.Defaults.ReloadAfter));
    }
}