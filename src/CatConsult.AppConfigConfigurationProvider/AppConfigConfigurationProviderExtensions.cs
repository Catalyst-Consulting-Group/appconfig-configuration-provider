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
    ) => AddAppConfigInternal(builder, null, sectionName);

    public static IConfigurationBuilder AddAppConfig(
        this IConfigurationBuilder builder,
        IAmazonAppConfigData client,
        string sectionName = DefaultSectionName
    ) => AddAppConfigInternal(builder, client, sectionName);

    private static IConfigurationBuilder AddAppConfigInternal(
        this IConfigurationBuilder builder,
        IAmazonAppConfigData? client = null,
        string sectionName = DefaultSectionName
    )
    {
        var options = builder.Build()
            .GetSection(sectionName)
            .Get<AppConfigOptions>() ?? new AppConfigOptions();

        var profiles = options.Profiles.Select(p =>
            AppConfigProfileParser.Parse(p, false, options.Defaults.ReloadAfter)
        );

        var featureFlagProfiles = options.FeatureFlags.Select(p =>
            AppConfigProfileParser.Parse(p, true, options.Defaults.ReloadAfter)
        );

        foreach (var profile in profiles.Concat(featureFlagProfiles))
        {
            builder.Add(
                client is null
                    ? new AppConfigConfigurationSource(profile)
                    : new AppConfigConfigurationSource(client, profile)
            );
        }

        return builder;
    }
}