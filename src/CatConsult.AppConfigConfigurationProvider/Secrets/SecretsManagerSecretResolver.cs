using System.Collections.Concurrent;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace CatConsult.AppConfigConfigurationProvider.Secrets;

/// <summary>
/// Resolves AWS Secrets Manager ARN references into their actual secret string values, with in-memory caching
///
/// Unlike AppConfig (which provides its own polling interval in API responses),
/// Secrets Manager has no built-in polling mechanism, so we have a TTL-based cache to avoid re-fetching secrets on every provider reload
/// Concurrency is handled with a SemaphoreSlim, consistent with the locking pattern used in AppConfigConfigurationProvider
/// </summary>
public class SecretsManagerSecretResolver : IDisposable
{
    // Time in milliseconds to wait for the lock before giving up
    private const int LockReleaseTimeout = 3000;
    // AWS Secrets Manager client used to fetch secret values
    private readonly IAmazonSecretsManager _client;
    // How long resolved secrets remain valid in the cache before being re-fetched
    private readonly TimeSpan _cacheTtl;
    // Cache keyed by the full ARN string. Each CacheEntry stores the resolved secret value and an expiration timestamp
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    // Prevents concurrent overlapping fetches to Secrets Manager
    private readonly SemaphoreSlim _lock;

    // Creates a resolver using a caller-provided Secrets Manager client (used for testing with mocked clients)
    public SecretsManagerSecretResolver(IAmazonSecretsManager client, int cacheTtlSeconds)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cacheTtl = TimeSpan.FromSeconds(cacheTtlSeconds);
        _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
        _lock = new SemaphoreSlim(1, 1);
    }

    // Creates a resolver that instantiates a default AmazonSecretsManagerClient using AWS credentials
    public SecretsManagerSecretResolver(int cacheTtlSeconds) : this(new AmazonSecretsManagerClient(), cacheTtlSeconds) { }

    // Resolves AWS Secrets Manager ARN reference into its actual secret string value
    // Returns a cached value if within time-to-live (TTL), otherwise fetch from AWS Secrets Manager and update the cache
    public async Task<string> ResolveSecretAsync(string arn)
    {
        if (string.IsNullOrWhiteSpace(arn))
        {
            throw new ArgumentException("ARN cannot be null or empty.", nameof(arn));
        }

        // Check cache dictionary, if the secret is already there and hasn't expired, return the resolved value
        if (_cache.TryGetValue(arn, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }

        // If lock can't be acquired (another thread is already fetching)
        if (!await _lock.WaitAsync(LockReleaseTimeout))
        {
            // Return the stale cached value if we have one, same approach as AppConfigConfigurationProvider
            if (cached != null)
            {
                return cached.Value;
            }

            // No cached value exists (first fetch for this secret) and we can't acquire the lock
            throw new TimeoutException($"Timed out waiting to resolve secret for ARN: {arn}");
        }

        try
        {
            // Check the cache before making AWS call to ensure no other thread already fetched the secret and updated the cache
            if (_cache.TryGetValue(arn, out cached) && !cached.IsExpired)
            {
                return cached.Value;
            }

            // Lock acquired, fetch the secret value from AWS Secrets Manager and cache it with the TTL
            var secretValue = await FetchSecretAsync(arn);
            _cache[arn] = new CacheEntry(secretValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return secretValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Given an ARN, fetches the secret string value from AWS Secrets Manager
    private async Task<string> FetchSecretAsync(string arn)
    {
        var request = new GetSecretValueRequest
        {
            SecretId = arn
        };

        var response = await _client.GetSecretValueAsync(request);

        if (response.SecretString != null)
        {
            return response.SecretString;
        }

        throw new InvalidOperationException($"Secret '{arn}' did not return a SecretString value");
    }

    // Clean up the SemaphoreSlim lock resources when the resolver is no longer needed
    public void Dispose()
    {
        _lock.Dispose();
    }

    // Represents a cached secret value with an expiration timestamp
    private class CacheEntry
    {
        // The resolved secret string from AWS Secrets Manager
        public string Value { get; }

        // When this cache entry expires and the secret should be re-fetched
        public DateTimeOffset ExpiresAtUtc { get; }

        public CacheEntry(string value, DateTimeOffset expiresAtUtc)
        {
            Value = value;
            ExpiresAtUtc = expiresAtUtc;
        }

        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
    }
}