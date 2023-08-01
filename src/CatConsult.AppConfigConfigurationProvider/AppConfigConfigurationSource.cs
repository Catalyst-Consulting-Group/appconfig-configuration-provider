using Amazon.AppConfigData;

using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider;

public class AppConfigConfigurationSource : IConfigurationSource
{
    private readonly AppConfigConfigurationProvider _provider;

    public AppConfigConfigurationSource(IAmazonAppConfigData client, AppConfigProfile profile) =>
        _provider = new AppConfigConfigurationProvider(client, profile);

    public AppConfigConfigurationSource(AppConfigProfile profile) =>
        _provider = new AppConfigConfigurationProvider(profile);

    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
}
