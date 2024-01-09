using Amazon.AppConfigData;
using Amazon.AppConfigData.Model;

using CatConsult.AppConfigConfigurationProvider.Utilities;
using CatConsult.ConfigurationParsers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CatConsult.AppConfigConfigurationProvider;

public sealed class AppConfigConfigurationProvider : ConfigurationProvider, IDisposable
{
    private const int LockReleaseTimeout = 3_000;

    private readonly IAmazonAppConfigData _client;
    private readonly AppConfigProfile _profile;
    private readonly SemaphoreSlim _lock;

    private IDisposable? _reloadChangeToken;

    public AppConfigConfigurationProvider(IAmazonAppConfigData client, AppConfigProfile profile)
    {
        _profile = profile;
        _client = client;
        _lock = new SemaphoreSlim(1, 1);
    }

    public AppConfigConfigurationProvider(AppConfigProfile profile) : this(new AmazonAppConfigDataClient(), profile) { }

    private string? ConfigurationToken { get; set; }

    private DateTimeOffset NextPollingTime { get; set; }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();

        if (_reloadChangeToken is null && _profile.ReloadAfter.HasValue)
        {
            var delay = TimeSpan.FromSeconds(_profile.ReloadAfter.Value);

            _reloadChangeToken = ChangeToken.OnChange(
                () => new CancellationChangeToken(
                    new CancellationTokenSource(delay).Token
                ),
                Load
            );
        }
    }

    private async Task LoadAsync()
    {
        if (!await _lock.WaitAsync(LockReleaseTimeout))
        {
            return;
        }

        try
        {
            if (DateTimeOffset.UtcNow < NextPollingTime)
            {
                return;
            }

            if (string.IsNullOrEmpty(ConfigurationToken))
            {
                await InitializeAppConfigSessionAsync();
            }

            var request = new GetLatestConfigurationRequest
            {
                ConfigurationToken = ConfigurationToken
            };

            var response = await _client.GetLatestConfigurationAsync(request);
            ConfigurationToken = response.NextPollConfigurationToken;
            NextPollingTime = DateTimeOffset.UtcNow.AddSeconds(response.NextPollIntervalInSeconds);

            // If the remote configuration has changed, the API will send back data and we re-parse
            if (response.ContentLength > 0)
            {
                Data = ParseConfig(response.Configuration, response.ContentType);
                OnReload();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InitializeAppConfigSessionAsync()
    {
        var session = await _client.StartConfigurationSessionAsync(new StartConfigurationSessionRequest
        {
            ApplicationIdentifier = _profile.ApplicationId,
            EnvironmentIdentifier = _profile.EnvironmentId,
            ConfigurationProfileIdentifier = _profile.ProfileId,
        });

        ConfigurationToken = session.InitialConfigurationToken;
    }

    private IDictionary<string, string?> ParseConfig(Stream stream, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType))
        {
            contentType = contentType.Split(";")[0];
        }

        return contentType switch
        {
            "application/json" when _profile.IsFeatureFlag => FeatureFlagsProfileParser.Parse(stream),
            "application/json" => JsonConfigurationParser.Parse(stream),
            "application/x-yaml" => YamlConfigurationParser.Parse(stream),
            _ => throw new FormatException($"This configuration provider does not support: {contentType ?? "Unknown"}")
        };
    }

    public void Dispose() => _reloadChangeToken?.Dispose();

    public override string ToString()
    {
        var className = GetType().Name;
        var profile = $"{_profile.ApplicationId}:{_profile.EnvironmentId}:{_profile.ProfileId}:{_profile.ReloadAfter}";
        var isFeatureFlag = _profile.IsFeatureFlag ? " (Feature Flag)" : string.Empty;

        return $"{className} - {profile}{isFeatureFlag}";
    }
}