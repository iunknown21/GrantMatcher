# Performance Optimization Quick Start

## For Developers: Using the Performance Features

### 1. Using the Caching Service

```csharp
// Inject in your service
public class MyService
{
    private readonly ICachingService _cache;

    // Simple cache get/set
    var data = await _cache.GetAsync<MyData>("my-key");
    await _cache.SetAsync("my-key", data, TimeSpan.FromMinutes(30));

    // Cache with factory (recommended)
    var data = await _cache.GetOrCreateAsync(
        "my-key",
        async () => await ExpensiveOperation(),
        absoluteExpiration: TimeSpan.FromMinutes(30),
        slidingExpiration: TimeSpan.FromMinutes(10));

    // Invalidate cache
    await _cache.RemoveAsync("my-key");
    await _cache.RemoveByPatternAsync("search:*");

    // Get statistics
    var stats = _cache.GetStatistics();
    Console.WriteLine($"Hit Rate: {stats.HitRate}%");
}
```

### 2. Using Performance Monitoring

```csharp
// Inject IPerformanceMonitor
private readonly IPerformanceMonitor _perfMonitor;

// Track an async operation
var result = await _perfMonitor.TrackAsync(
    "OperationName",
    async () => await MyOperationAsync(),
    warningThreshold: TimeSpan.FromSeconds(2));

// Using a scope (auto-disposes)
using (var scope = _perfMonitor.BeginScope("LongOperation"))
{
    // ... do work ...
    scope.AddProperty("ItemsProcessed", count);
} // Automatically logs when disposed
```

### 3. Client-Side: Debouncing Search

```razor
@using GrantMatcher.Client.Utilities
@inject IApiClient ApiClient

<input @bind-value="searchQuery" @bind-value:event="oninput" @oninput="OnSearchInput" />

@code {
    private string searchQuery = "";
    private DebounceHelper _debouncer = new(500); // 500ms delay

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";

        await _debouncer.DebounceAsync(async () =>
        {
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                await PerformSearch();
            }
        });
    }

    private async Task PerformSearch()
    {
        var results = await ApiClient.SearchGrantsAsync(searchQuery);
        // ... update UI ...
    }
}
```

### 4. Client-Side: Request Deduplication

```csharp
public class ProfileService
{
    private readonly RequestDeduplicator _deduplicator = new();
    private readonly IApiClient _apiClient;

    public async Task<NonprofitProfile> GetProfileAsync(string id)
    {
        // Multiple calls with same ID will only execute once
        return await _deduplicator.ExecuteAsync(
            $"profile:{id}",
            async () => await _apiClient.GetNonprofitProfileAsync(id));
    }
}
```

### 5. Lazy Loading Images

```html
<!-- Instead of: -->
<img src="/images/large-image.jpg" alt="Image" />

<!-- Use: -->
<img data-src="/images/large-image.jpg" alt="Image" />
```

The `lazyload.js` module automatically loads images when they're about to become visible.

### 6. Cache Keys

Use the built-in cache key builder for consistency:

```csharp
using static GrantMatcher.Core.Services.CacheKeys;

// Search results
var key = GrantSearch(query, limit, minSimilarity);

// Nonprofit profile
var key = NonprofitProfile(NonprofitId);

// Conversation
var key = ConversationHistory(conversationId);

// Embeddings
var key = Embedding(text);
```

---

## For DevOps: Deployment Checklist

### 1. Enable Redis (Optional, for multi-instance)

```bash
# Create Azure Cache for Redis
az redis create \
  --name GrantMatcher-cache \
  --resource-group <your-rg> \
  --location <your-region> \
  --sku Basic \
  --vm-size c0

# Get connection string
az redis list-keys --name GrantMatcher-cache --resource-group <your-rg>
```

Update `local.settings.json` or App Settings:
```json
"Redis:ConnectionString": "<your-redis-connection-string>"
```

### 2. Initialize Cosmos DB

```powershell
# Run initialization script
.\scripts\initialize-cosmos-db.ps1 `
    -ResourceGroupName "<your-rg>" `
    -AccountName "<your-cosmos-account>" `
    -DatabaseName "GrantMatcher"
```

### 3. Configure App Settings

Add these to your Azure Function App Settings:

```
Cache:DefaultAbsoluteExpirationMinutes=30
Cache:SearchResultsExpirationMinutes=15
Performance:SlowOperationThresholdSeconds=3
RateLimit:MaxRequestsPerMinute=60
```

### 4. Enable Application Insights

Ensure Application Insights is connected for monitoring:
- Performance metrics
- Cache statistics
- Slow operations
- Error tracking

---

## Performance Targets

### API Endpoints

| Endpoint | Target (cached) | Target (uncached) |
|----------|----------------|-------------------|
| Search | < 20ms | < 1000ms |
| Get Profile | < 10ms | < 100ms |
| Save Grant | N/A | < 200ms |
| Process Conversation | N/A | < 3000ms |

### Cache Metrics

| Metric | Target |
|--------|--------|
| Hit Rate | > 80% |
| Average Retrieval Time | < 5ms |
| Memory Usage | < 500MB |

### Database

| Metric | Target |
|--------|--------|
| Average RU/Query | < 20 RU |
| Query Latency | < 100ms |
| Throttling Events | 0 |

---

## Troubleshooting

### High API Latency

1. Check if caching is enabled
2. Review Application Insights for slow dependencies
3. Check Cosmos DB RU consumption
4. Verify network latency

### Low Cache Hit Rate

1. Verify cache keys are consistent
2. Check TTL settings (might be too short)
3. Review eviction policy
4. Check memory constraints

### Rate Limiting Issues

1. Check client making excessive requests
2. Review rate limit configuration
3. Consider increasing limits for legitimate use
4. Implement better client-side caching

---

## Monitoring Dashboard

### Key Metrics to Watch

1. **Cache Performance**
   - Query: `customMetrics | where name == "Cache.HitRate"`
   - Alert: < 70%

2. **API Response Time**
   - Query: `requests | summarize avg(duration) by name`
   - Alert: > 1000ms average

3. **RU Consumption**
   - Query: `dependencies | where type == "Azure DocumentDB"`
   - Alert: > 50 RU average

4. **Error Rate**
   - Query: `requests | where success == false`
   - Alert: > 1%

---

## Quick Commands

```bash
# Check cache statistics (in application)
GET /api/diagnostics/cache-stats

# Clear cache (in application)
POST /api/diagnostics/clear-cache?pattern=search:*

# View performance statistics
GET /api/diagnostics/performance-stats

# Warm up cache (after deployment)
POST /api/diagnostics/warmup-cache
```

---

## Best Practices Summary

✅ **DO:**
- Use caching for frequently accessed data
- Implement debouncing for user input
- Monitor cache hit rates
- Use composite indexes for common queries
- Include partition key in all queries
- Lazy load images and components
- Deduplicate concurrent requests

❌ **DON'T:**
- Cache rapidly changing data
- Set TTL too long (stale data)
- Ignore slow operation warnings
- Use SELECT * in queries
- Forget to include partition key
- Load all data at once (use pagination)

---

**Need Help?** See [PERFORMANCE_OPTIMIZATION.md](./PERFORMANCE_OPTIMIZATION.md) for detailed documentation.
