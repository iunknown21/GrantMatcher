using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GrantMatcher.Functions.Middleware;

/// <summary>
/// Middleware to track API performance and add headers
/// </summary>
public class PerformanceMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<PerformanceMiddleware> _logger;

    public PerformanceMiddleware(ILogger<PerformanceMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var functionName = context.FunctionDefinition.Name;

        try
        {
            _logger.LogInformation("Function {FunctionName} started", functionName);

            await next(context);

            stopwatch.Stop();

            // Add performance headers to HTTP response
            var httpReqData = await context.GetHttpRequestDataAsync();
            if (httpReqData != null)
            {
                var httpResponseData = context.GetHttpResponseData();
                if (httpResponseData != null)
                {
                    httpResponseData.Headers.Add("X-Processing-Time-Ms", stopwatch.ElapsedMilliseconds.ToString());
                    httpResponseData.Headers.Add("X-Server-Timing", $"total;dur={stopwatch.ElapsedMilliseconds}");
                }
            }

            _logger.LogInformation(
                "Function {FunctionName} completed in {ElapsedMs}ms",
                functionName,
                stopwatch.ElapsedMilliseconds);

            // Log slow functions
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(3))
            {
                _logger.LogWarning(
                    "Slow function detected: {FunctionName} took {ElapsedMs}ms",
                    functionName,
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Function {FunctionName} failed after {ElapsedMs}ms",
                functionName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Extension methods for HttpResponseData
/// </summary>
public static class HttpResponseDataExtensions
{
    public static HttpResponseData? GetHttpResponseData(this FunctionContext context)
    {
        var httpResponseData = context.GetInvocationResult().Value as HttpResponseData;
        return httpResponseData;
    }
}
