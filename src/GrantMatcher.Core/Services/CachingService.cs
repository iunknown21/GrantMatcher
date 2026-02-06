using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GrantMatcher.Core.Services;

/// <summary>
/// Hybrid caching service that supports both in-memory and distributed (Redis) caching
/// </summary>
public class CachingService : ICachingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<CachingService> _logger;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private readonly CacheStatistics _statistics;
    private readonly bool _useDistributedCache;

    // Default cache durations
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(10);

    public CachingService(
        IMemoryCache memoryCache,
        ILogger<CachingService> logger,
        IDistributedCache? distributedCache = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _distributedCache = distributedCache;
        _cacheKeys = new ConcurrentDictionary<string, byte>();
        _statistics = new CacheStatistics();
        _useDistributedCache = distributedCache != null;

        _logger.LogInformation(
            "CachingService initialized with {CacheType} cache",
            _useDistributedCache ? "distributed (Redis)" : "in-memory");
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            Interlocked.Increment(ref _statistics.Hits);
            _logger.LogDebug("Cache hit (memory): {Key}", key);
            return cachedValue;
        }

        // Try distributed cache if available
        if (_useDistributedCache && _distributedCache != null)
        {
            try
            {
                var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (!string.IsNullOrEmpty(distributedValue))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);
                    if (deserializedValue != null)
                    {
                        Interlocked.Increment(ref _statistics.Hits);
                        _logger.LogDebug("Cache hit (distributed): {Key}", key);

                        // Populate memory cache
                        SetMemoryCache(key, deserializedValue, absoluteExpiration, slidingExpiration);
                        return deserializedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from distributed cache for key: {Key}", key);
            }
        }

        // Cache miss - compute value
        Interlocked.Increment(ref _statistics.Misses);
        _logger.LogDebug("Cache miss: {Key}", key);

        var value = await factory();
        if (value != null)
        {
            await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);
        }

        return value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            Interlocked.Increment(ref _statistics.Hits);
            _logger.LogDebug("Cache hit (memory): {Key}", key);
            return cachedValue;
        }

        // Try distributed cache if available
        if (_useDistributedCache && _distributedCache != null)
        {
            try
            {
                var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (!string.IsNullOrEmpty(distributedValue))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);
                    if (deserializedValue != null)
                    {
                        Interlocked.Increment(ref _statistics.Hits);
                        _logger.LogDebug("Cache hit (distributed): {Key}", key);

                        // Populate memory cache
                        SetMemoryCache(key, deserializedValue);
                        return deserializedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from distributed cache for key: {Key}", key);
            }
        }

        Interlocked.Increment(ref _statistics.Misses);
        return null;
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        // Track cache key
        _cacheKeys.TryAdd(key, 0);

        // Set in memory cache
        SetMemoryCache(key, value, absoluteExpiration, slidingExpiration);

        // Set in distributed cache if available
        if (_useDistributedCache && _distributedCache != null)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = absoluteExpiration ?? DefaultAbsoluteExpiration,
                    SlidingExpiration = slidingExpiration ?? DefaultSlidingExpiration
                };

                await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
                _logger.LogDebug("Set cache (distributed): {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error writing to distributed cache for key: {Key}", key);
            }
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        _memoryCache.Remove(key);
        _cacheKeys.TryRemove(key, out _);
        Interlocked.Increment(ref _statistics.Evictions);

        if (_useDistributedCache && _distributedCache != null)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                _logger.LogDebug("Removed from cache: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing from distributed cache for key: {Key}", key);
            }
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

        var keysToRemove = _cacheKeys.Keys
            .Where(k => IsMatch(k, pattern))
            .ToList();

        _logger.LogInformation("Removing {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);

        foreach (var key in keysToRemove)
        {
            await RemoveAsync(key, cancellationToken);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Clearing all cache entries");

        var allKeys = _cacheKeys.Keys.ToList();
        foreach (var key in allKeys)
        {
            await RemoveAsync(key, cancellationToken);
        }

        _cacheKeys.Clear();
    }

    public CacheStatistics GetStatistics()
    {
        _statistics.CurrentEntries = _cacheKeys.Count;
        return _statistics;
    }

    private void SetMemoryCache<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null) where T : class
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? DefaultAbsoluteExpiration,
            SlidingExpiration = slidingExpiration ?? DefaultSlidingExpiration
        };

        options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
        {
            if (evictedKey is string keyString)
            {
                _cacheKeys.TryRemove(keyString, out _);
                Interlocked.Increment(ref _statistics.Evictions);
            }
        });

        _memoryCache.Set(key, value, options);
        _logger.LogDebug("Set cache (memory): {Key}", key);
    }

    private static bool IsMatch(string key, string pattern)
    {
        // Simple pattern matching - supports * as wildcard
        if (pattern == "*")
            return true;

        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(key, regexPattern);
        }

        return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Cache key builder for consistent key generation
/// </summary>
public static class CacheKeys
{
    public const string GrantSearchPrefix = "search:Grants";
    public const string NonprofitProfilePrefix = "profile:Nonprofit";
    public const string ConversationHistoryPrefix = "conversation:history";
    public const string GrantEntityPrefix = "entity:Grant";
    public const string EmbeddingPrefix = "embedding";

    public static string GrantSearch(string query, int limit, decimal? minSimilarity) =>
        $"{GrantSearchPrefix}:{ComputeHash(query)}:{limit}:{minSimilarity ?? 0}";

    public static string NonprofitProfile(string NonprofitId) =>
        $"{NonprofitProfilePrefix}:{NonprofitId}";

    public static string ConversationHistory(string conversationId) =>
        $"{ConversationHistoryPrefix}:{conversationId}";

    public static string GrantEntity(string GrantId) =>
        $"{GrantEntityPrefix}:{GrantId}";

    public static string Embedding(string text) =>
        $"{EmbeddingPrefix}:{ComputeHash(text)}";

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16);
    }
}
