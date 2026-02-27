using Amazon.AppConfigData;

using Microsoft.Extensions.Configuration;

namespace CatConsult.AppConfigConfigurationProvider;


/// <summary>
/// Implements IConfigurationSource, which is ASP.NET's abstraction for registering a configuration provider into the configuration pipeline
///
/// A configuration provider is a class that knows how to read configuration data from
/// a specific source and supply it as flattened key/value pairs to the application
/// 
/// AppConfigConfigurationProvider is a custom provider that does this with AWS AppConfig as its data source
/// It fetches JSON/YAML from AppConfig, flattens it into key/value pairs, and feeds them into the IConfiguration tree alongside everything else
///
/// This class acts as a lightweight factory that constructs a single AppConfigConfigurationProvider instance for a given AppConfigProfile
/// </summary>
public sealed class AppConfigConfigurationSource : IConfigurationSource
{
    private readonly AppConfigConfigurationProvider _provider;

    public AppConfigConfigurationSource(IAmazonAppConfigData client, AppConfigProfile profile) =>
        _provider = new AppConfigConfigurationProvider(client, profile);

    public AppConfigConfigurationSource(AppConfigProfile profile) =>
        _provider = new AppConfigConfigurationProvider(profile);

    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
}