using Microsoft.Extensions.Caching.Memory;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace CatConsult.AppConfigConfigurationProvider.Secrets;

/// <summary>
/// Resolves AWS Secrets Manager ARN references into their actual secret string values, with in-memory caching
///
/// Unlike AppConfig (which provides its own polling interval in API responses),
/// Secrets Manager has no built-in polling mechanism, so we use IMemoryCache with TTL-based expiration to avoid re-fetching secrets on every provider reload
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
    // Cache for resolved secret values - IMemoryCache handles expiration automatically
    private readonly IMemoryCache _cache;
    // Prevents concurrent overlapping fetches to Secrets Manager
    private readonly SemaphoreSlim _lock;

    // Creates a resolver using a caller-provided Secrets Manager client (used for testing with mocked clients)
    public SecretsManagerSecretResolver(IAmazonSecretsManager client, int cacheTtlSeconds)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cache = new MemoryCache(new MemoryCacheOptions());
        _cacheTtl = TimeSpan.FromSeconds(cacheTtlSeconds);
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

        // Check cache — if the secret is there and hasn't expired, return the resolved value
        if (_cache.TryGetValue(arn, out string? cached))
        {
            return cached!;
        }

        // If lock can't be acquired (another thread is already fetching)
        if (!await _lock.WaitAsync(LockReleaseTimeout))
        {
            // IMemoryCache automatically evicts expired entries, so no stale value to fall back on
            throw new TimeoutException($"Timed out waiting to resolve secret for ARN: {arn}");
        }

        try
        {
            // Check the cache before making AWS call to ensure no other thread already fetched the secret and updated the cache
            if (_cache.TryGetValue(arn, out cached))
            {
                return cached!;
            }

            // Lock acquired, fetch the secret value from AWS Secrets Manager and cache it with the TTL
            var secretValue = await FetchSecretAsync(arn);
            _cache.Set(arn, secretValue, _cacheTtl);
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

    // Clean up the cache and lock resources when the resolver is no longer needed
    public void Dispose()
    {
        _cache.Dispose();
        _lock.Dispose();
    }
}