using Amazon.AppConfigData;

using CatConsult.AppConfigConfigurationProvider.Utilities;

using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

/// <summary>
/// Defines the configuration.AddAppConfig() extension method that consumer applications
/// call to register AppConfig configuration providers into IConfigurationBuilder builder
///
/// The IConfigurationBuilder is ASP.NET's pipeline for assembling configuration sources
/// during application startup. Each source added via builder.Add(...) contributes
/// key/value pairs that are merged into the single IConfiguration tree the application consumes
/// 
/// For each profile defined in AppConfigOptions.Profiles and AppConfigOptions.FeatureFlags,
/// we register a separate AppConfigConfigurationProvider instance that independently fetches and refreshes configuration data from AWS AppConfig for that profile
/// </summary>
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
        // Read the "AppConfig" section from the current configuration
        // (populated from appsettings.$ENV.json) to determine
        // which AWS AppConfig profiles and feature flags to load
        var options = builder.Build()
            .GetSection(sectionName)
            .Get<AppConfigOptions>() ?? new AppConfigOptions();

        // Convert each profile string entry (Application:Environment:Profile format) into an AppConfigProfile object
        var profiles = options.Profiles.Select(p =>
            AppConfigProfileParser.Parse(p, false, options.Defaults.ReloadAfter)
        );

        var featureFlagProfiles = options.FeatureFlags.Select(p =>
            AppConfigProfileParser.Parse(p, true, options.Defaults.ReloadAfter)
        );

        // Register one AppConfigConfigurationProvider per profile by creating an AppConfigConfigurationSource and adding it to the builder
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