using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;

namespace GrantMatcher.Functions.Middleware;

/// <summary>
/// Simple in-memory rate limiting middleware
/// For production, consider using Azure API Management or Redis-based rate limiting
/// </summary>
public class RateLimitingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, ClientRequestTracker> _requestTrackers;

    // Configuration
    private const int MaxRequestsPerMinute = 60;
    private const int MaxRequestsPer5Minutes = 200;

    public RateLimitingMiddleware(ILogger<RateLimitingMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestTrackers = new ConcurrentDictionary<string, ClientRequestTracker>();

        // Cleanup old entries periodically
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(10));
                CleanupOldEntries();
            }
        });
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpReqData = await context.GetHttpRequestDataAsync();
        if (httpReqData != null)
        {
            var clientId = GetClientIdentifier(httpReqData);
            var tracker = _requestTrackers.GetOrAdd(clientId, _ => new ClientRequestTracker());

            if (tracker.IsRateLimited())
            {
                _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);

                var response = httpReqData.CreateResponse(HttpStatusCode.TooManyRequests);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Retry-After", "60");

                await response.WriteStringAsync(
                    "{\"error\":\"Rate limit exceeded\",\"message\":\"Too many requests. Please try again later.\"}");

                context.GetInvocationResult().Value = response;
                return;
            }

            tracker.RecordRequest();
        }

        await next(context);
    }

    private string GetClientIdentifier(HttpRequestData request)
    {
        // Try to get user ID from claims
        if (request.Identities?.FirstOrDefault()?.Claims != null)
        {
            var userIdClaim = request.Identities.FirstOrDefault()?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type == "oid");
            if (userIdClaim != null)
                return $"user:{userIdClaim.Value}";
        }

        // Fall back to IP address (not ideal for production behind load balancers)
        var ipAddress = request.Headers.TryGetValues("X-Forwarded-For", out var forwardedFor)
            ? forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
            : request.Headers.TryGetValues("X-Real-IP", out var realIp)
                ? realIp.FirstOrDefault()
                : "unknown";

        return $"ip:{ipAddress}";
    }

    private void CleanupOldEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var keysToRemove = _requestTrackers
            .Where(kvp => kvp.Value.LastRequest < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _requestTrackers.TryRemove(key, out _);
        }

        _logger.LogDebug("Cleaned up {Count} rate limit tracking entries", keysToRemove.Count);
    }

    private class ClientRequestTracker
    {
        private readonly Queue<DateTime> _requests = new();
        private readonly object _lock = new();

        public DateTime LastRequest { get; private set; } = DateTime.UtcNow;

        public void RecordRequest()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                _requests.Enqueue(now);
                LastRequest = now;

                // Remove requests older than 5 minutes
                var cutoff = now.AddMinutes(-5);
                while (_requests.Count > 0 && _requests.Peek() < cutoff)
                {
                    _requests.Dequeue();
                }
            }
        }

        public bool IsRateLimited()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                // Check last minute
                var lastMinute = now.AddMinutes(-1);
                var requestsInLastMinute = _requests.Count(r => r >= lastMinute);
                if (requestsInLastMinute >= MaxRequestsPerMinute)
                    return true;

                // Check last 5 minutes
                var last5Minutes = now.AddMinutes(-5);
                var requestsInLast5Minutes = _requests.Count(r => r >= last5Minutes);
                if (requestsInLast5Minutes >= MaxRequestsPer5Minutes)
                    return true;

                return false;
            }
        }
    }
}
