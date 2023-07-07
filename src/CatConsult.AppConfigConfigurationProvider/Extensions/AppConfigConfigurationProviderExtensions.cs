using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider.Extensions;

public static class AppConfigConfigurationProviderExtensions
{
    [Obsolete("Use CatConsult.AppConfigConfigurationProvider instead - this will be removed in a future version")]
    public static IConfigurationBuilder AddAppConfig(this IConfigurationBuilder builder, string sectionName = "AppConfig") =>
        AppConfigConfigurationProvider.AppConfigConfigurationProviderExtensions.AddAppConfig(builder, sectionName);
}
