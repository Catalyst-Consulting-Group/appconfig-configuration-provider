using System.Threading;

using Amazon.AppConfigData;
using Amazon.AppConfigData.Model;

using CatConsult.AppConfigConfigurationProvider.Secrets;
using CatConsult.AppConfigConfigurationProvider.Utilities;
using CatConsult.ConfigurationParsers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CatConsult.AppConfigConfigurationProvider;

/// <summary>
/// Custom configuration provider that fetches configuration data from AWS AppConfig
/// and supplies it as flattened key/value pairs to the ASP.NET IConfiguration tree
///
/// Each instance handles a single AppConfigProfile. Multiple providers are registered
/// (one per profile) by AppConfigConfigurationProviderExtensions.AddAppConfig()
///
/// When secret resolution is enabled, the provider detects Secrets Manager ARN values
/// in the parsed AWS AppConfig data and resolves them to actual secret strings before setting
/// them on the IConfiguration tree. The application consuming IConfiguration never sees ARNs
///
/// Concurrency is handled with a SemaphoreSlim to prevent overlapping fetches
/// Auto-reload is controlled by the profile's ReloadAfter interval using ChangeToken
/// </summary>
public sealed class AppConfigConfigurationProvider : ConfigurationProvider, IDisposable
{
    private const int LockReleaseTimeout = 3_000; // Max milliseconds to wait for the lock before giving up
    private readonly IAmazonAppConfigData _client; // AWS AppConfig client used to fetch configuration data
    private readonly AppConfigProfile _profile; // The specific AppConfig profile this provider instance is responsible for fetching
    private readonly SemaphoreSlim _lock; // Prevents concurrent overlapping fetches to AppConfig
    private readonly SecretsManagerSecretResolver? _secretResolver; // Optional secret resolver - null when SecretsManager is disabled, non-null when enabled
    // Reference to the recurring timer that calls Load() on an interval (set by ReloadAfter) to re-fetch configuration from AppConfig
    // Null until the first Load() call sets it up
    private IDisposable? _reloadChangeToken;

    // Constructor for testing — accepts mocked AppConfig client and an optional secret resolver
    public AppConfigConfigurationProvider(IAmazonAppConfigData client, AppConfigProfile profile, SecretsManagerSecretResolver? secretResolver = null)
    {
        _profile = profile;
        _client = client;
        _lock = new SemaphoreSlim(1, 1);
        _secretResolver = secretResolver;
    }

    public AppConfigConfigurationProvider(AppConfigProfile profile, SecretsManagerSecretResolver? secretResolver = null)
        : this(new AmazonAppConfigDataClient(), profile, secretResolver) { }

    // Rolling session token from AWS AppConfig - each API response provides the next token to use for the subsequent request
    private string? ConfigurationToken { get; set; }

    // Earliest time the provider is allowed to poll AppConfig again, set by the NextPollIntervalInSeconds value returned by AWS
    private DateTimeOffset NextPollingTime { get; set; }

    // Called by ASP.NET when ConfigurationBuilder.Build() runs during app startup (ex: var app = builder.Build() in Program.cs)
    // Fetches configuration data from AppConfig via LoadAsync()
    // On the first call, sets up a recurring timer (based on ReloadAfter) that calls Load() again on an interval to re-fetch from AppConfig
    // The _reloadChangeToken null check prevents duplicate timers since Load() is called repeatedly by the timer itself
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

    // Fetches the latest configuration from AWS AppConfig for this provider's profile
    private async Task LoadAsync()
    {
        // Acquire lock (skip if another thread is already fetching)
        if (!await _lock.WaitAsync(LockReleaseTimeout))
        {
            return;
        }

        try
        {
            // Respect AWS polling interval - skip if it's too early to poll again
            if (DateTimeOffset.UtcNow < NextPollingTime)
            {
                return;
            }

            // Initialize session on first call to get the initial configuration token
            if (string.IsNullOrEmpty(ConfigurationToken))
            {
                await InitializeAppConfigSessionAsync();
            }

            var request = new GetLatestConfigurationRequest
            {
                ConfigurationToken = ConfigurationToken
            };


            // Call GetLatestConfiguration - AWS returns data only if config has changed since the last token
            var response = await _client.GetLatestConfigurationAsync(request);
            ConfigurationToken = response.NextPollConfigurationToken;
            NextPollingTime = DateTimeOffset.UtcNow.AddSeconds(response.NextPollIntervalInSeconds ?? 15);

            // If the remote configuration has changed, the API will send back data and we re-parse the response (JSON/YAML) into flattened key/value pairs
            if (response.ContentLength > 0)
            {
                var parsed = ParseConfig(response.Configuration, response.ContentType);
                // If secret resolution is enabled, detect and resolve any Secrets Manager ARN values from AWS AppConfig into actual secret values
                // Update the Data dictionary field (which ASP.NET reads from when the consumer app accesses IConfiguration)
                Data = _secretResolver != null ? await ResolveSecretsAsync(parsed) : parsed;
                // Call OnReload() to notify ASP.NET that config values have changed, allowing
                // consumers using IOptionsMonitor<T> to automatically pick up new values without a restart
                OnReload();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Iterates over parsed config key/value pairs and resolves any Secrets Manager ARN values
    /// to their actual secret strings. Non-ARN values are left unchanged.
    /// </summary>
    private async Task<IDictionary<string, string?>> ResolveSecretsAsync(IDictionary<string, string?> parsed)
    {
        var resolved = new Dictionary<string, string?>(parsed.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in parsed)
        {
            if (string.IsNullOrEmpty(entry.Value) || !SecretsManagerArn.IsSecretsManagerArn(entry.Value))
            {
                // Normal config value — leave as-is
                resolved[entry.Key] = entry.Value;
                continue;
            }

            // ARN detected — resolve to the actual secret string
            try
            {
                resolved[entry.Key] = await _secretResolver!.ResolveSecretAsync(entry.Value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve secret for config key '{entry.Key}' with ARN '{entry.Value}'", ex);
            }
        }

        return resolved;
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
            contentType = contentType?.Split(';')[0];
        }

        return contentType switch
        {
            "application/json" when _profile.IsFeatureFlag => FeatureFlagsProfileParser.Parse(stream),
            "application/json" => JsonConfigurationParser.Parse(stream),
            "application/x-yaml" => YamlConfigurationParser.Parse(stream),
            _ => throw new FormatException($"This configuration provider does not support: {contentType ?? "Unknown"}")
        };
    }

    public void Dispose()
    {
        _reloadChangeToken?.Dispose();
        _secretResolver?.Dispose();
    }

    public override string ToString()
    {
        var className = GetType().Name;
        var profile = $"{_profile.ApplicationId}:{_profile.EnvironmentId}:{_profile.ProfileId}:{_profile.ReloadAfter}";
        var isFeatureFlag = _profile.IsFeatureFlag ? " (Feature Flag)" : string.Empty;

        return $"{className} - {profile}{isFeatureFlag}";
    }
}