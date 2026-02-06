using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Core.Services;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

/// <summary>
/// Diagnostic endpoints for monitoring and troubleshooting
/// </summary>
public class DiagnosticsFunctions
{
    private readonly ILogger<DiagnosticsFunctions> _logger;
    private readonly ICachingService? _cachingService;
    private readonly IPerformanceMonitor? _performanceMonitor;

    public DiagnosticsFunctions(
        ILogger<DiagnosticsFunctions> logger,
        ICachingService? cachingService = null,
        IPerformanceMonitor? performanceMonitor = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachingService = cachingService;
        _performanceMonitor = performanceMonitor;
    }

    /// <summary>
    /// Gets cache statistics
    /// GET /api/diagnostics/cache-stats
    /// </summary>
    [Function("GetCacheStats")]
    public async Task<HttpResponseData> GetCacheStats(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "diagnostics/cache-stats")] HttpRequestData req)
    {
        _logger.LogInformation("Getting cache statistics");

        if (_cachingService == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("{\"error\":\"Caching service not configured\"}");
            return notFoundResponse;
        }

        var stats = _cachingService.GetStatistics();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var result = new
        {
            hits = stats.Hits,
            misses = stats.Misses,
            evictions = stats.Evictions,
            currentEntries = stats.CurrentEntries,
            hitRate = stats.HitRate,
            timestamp = DateTime.UtcNow
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

        return response;
    }

    /// <summary>
    /// Gets performance statistics
    /// GET /api/diagnostics/performance-stats
    /// </summary>
    [Function("GetPerformanceStats")]
    public async Task<HttpResponseData> GetPerformanceStats(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "diagnostics/performance-stats")] HttpRequestData req)
    {
        _logger.LogInformation("Getting performance statistics");

        if (_performanceMonitor == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("{\"error\":\"Performance monitor not configured\"}");
            return notFoundResponse;
        }

        var stats = _performanceMonitor.GetStatistics();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var result = new
        {
            totalOperations = stats.TotalOperations,
            slowOperations = stats.SlowOperations,
            averageDuration = stats.AverageDuration.TotalMilliseconds,
            maxDuration = stats.MaxDuration.TotalMilliseconds,
            minDuration = stats.MinDuration.TotalMilliseconds,
            operationBreakdown = stats.OperationBreakdown.Select(kvp => new
            {
                operation = kvp.Key,
                count = kvp.Value.Count,
                averageDuration = kvp.Value.AverageDuration.TotalMilliseconds,
                maxDuration = kvp.Value.MaxDuration.TotalMilliseconds,
                minDuration = kvp.Value.MinDuration.TotalMilliseconds
            }),
            timestamp = DateTime.UtcNow
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

        return response;
    }

    /// <summary>
    /// Clears cache by pattern
    /// POST /api/diagnostics/clear-cache?pattern=search:*
    /// </summary>
    [Function("ClearCache")]
    public async Task<HttpResponseData> ClearCache(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "diagnostics/clear-cache")] HttpRequestData req)
    {
        var pattern = req.Query["pattern"] ?? "*";

        _logger.LogWarning("Clearing cache with pattern: {Pattern}", pattern);

        if (_cachingService == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("{\"error\":\"Caching service not configured\"}");
            return notFoundResponse;
        }

        if (pattern == "*")
        {
            await _cachingService.ClearAsync();
        }
        else
        {
            await _cachingService.RemoveByPatternAsync(pattern);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            message = $"Cache cleared for pattern: {pattern}",
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        return response;
    }

    /// <summary>
    /// Resets performance statistics
    /// POST /api/diagnostics/reset-performance-stats
    /// </summary>
    [Function("ResetPerformanceStats")]
    public async Task<HttpResponseData> ResetPerformanceStats(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "diagnostics/reset-performance-stats")] HttpRequestData req)
    {
        _logger.LogInformation("Resetting performance statistics");

        if (_performanceMonitor == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("{\"error\":\"Performance monitor not configured\"}");
            return notFoundResponse;
        }

        _performanceMonitor.ResetStatistics();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            message = "Performance statistics reset",
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        return response;
    }

    /// <summary>
    /// Health check endpoint
    /// GET /api/diagnostics/health
    /// </summary>
    [Function("HealthCheck")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "diagnostics/health")] HttpRequestData req)
    {
        _logger.LogDebug("Health check requested");

        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            features = new
            {
                caching = _cachingService != null,
                performanceMonitoring = _performanceMonitor != null
            }
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        await response.WriteStringAsync(JsonSerializer.Serialize(health, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

        return response;
    }

    /// <summary>
    /// Warms up cache with common queries
    /// POST /api/diagnostics/warmup-cache
    /// </summary>
    [Function("WarmupCache")]
    public async Task<HttpResponseData> WarmupCache(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "diagnostics/warmup-cache")] HttpRequestData req)
    {
        _logger.LogInformation("Starting cache warmup");

        if (_cachingService == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("{\"error\":\"Caching service not configured\"}");
            return notFoundResponse;
        }

        // This is a placeholder - implement actual warmup logic
        // based on your application's common queries
        var itemsWarmed = 0;

        // Example warmup tasks:
        // 1. Popular Grants
        // 2. Common search queries
        // 3. Active Nonprofit profiles
        // etc.

        _logger.LogInformation("Cache warmup completed. Items warmed: {ItemsWarmed}", itemsWarmed);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            message = "Cache warmup completed",
            itemsWarmed = itemsWarmed,
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        return response;
    }
}
