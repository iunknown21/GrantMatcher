using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;

namespace GrantMatcher.Functions.Functions;

/// <summary>
/// Azure Functions for analytics tracking and reporting
/// </summary>
public class AnalyticsFunctions
{
    private readonly ILogger<AnalyticsFunctions> _logger;
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsFunctions(
        ILogger<AnalyticsFunctions> logger,
        IAnalyticsService analyticsService)
    {
        _logger = logger;
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Track an analytics event (fire-and-forget endpoint)
    /// </summary>
    [Function("TrackEvent")]
    public async Task<HttpResponseData> TrackEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "analytics/track")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<TrackEventRequest>(req.Body);

            if (request == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request" });
                return badRequest;
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.EventType))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "EventType is required" });
                return badRequest;
            }

            // Extract user agent from headers
            if (req.Headers.TryGetValues("User-Agent", out var userAgentValues))
            {
                request.UserAgent = userAgentValues.FirstOrDefault();
            }

            // Track the event (non-blocking)
            var result = await _analyticsService.TrackEventAsync(request);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking event");

            // Don't fail the request - analytics should never break user experience
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new TrackEventResponse { Success = false });
            return response;
        }
    }

    /// <summary>
    /// Get analytics data
    /// </summary>
    [Function("GetAnalytics")]
    public async Task<HttpResponseData> GetAnalytics(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analytics/query")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<GetAnalyticsRequest>(req.Body);

            if (request == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request" });
                return badRequest;
            }

            var analyticsResponse = new GetAnalyticsResponse
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Get different types of analytics based on request
            if (string.IsNullOrEmpty(request.MetricType) || request.MetricType == "overview")
            {
                analyticsResponse.Overview = await _analyticsService.GetApplicationMetricsAsync(
                    request.StartDate,
                    request.EndDate);
            }

            if (string.IsNullOrEmpty(request.MetricType) || request.MetricType == "session")
            {
                analyticsResponse.SessionData = await _analyticsService.GetSessionMetricsAsync(
                    request.StartDate,
                    request.EndDate);
            }

            if (string.IsNullOrEmpty(request.MetricType) || request.MetricType == "Grant")
            {
                analyticsResponse.GrantData = await _analyticsService.GetGrantMetricsAsync(
                    request.StartDate,
                    request.EndDate,
                    request.Limit);

                analyticsResponse.TopPerformers = await _analyticsService.GetTopGrantsAsync();
            }

            // Get raw events if requested
            if (!string.IsNullOrEmpty(request.EventType) || !string.IsNullOrEmpty(request.EventCategory))
            {
                analyticsResponse.Events = await _analyticsService.GetEventsAsync(request);
                analyticsResponse.TotalCount = analyticsResponse.Events.Count;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(analyticsResponse);

            _logger.LogInformation("Retrieved analytics data for period {Start} to {End}",
                request.StartDate, request.EndDate);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve analytics" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Generate analytics report
    /// </summary>
    [Function("GenerateReport")]
    public async Task<HttpResponseData> GenerateReport(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analytics/report")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<GenerateReportRequest>(req.Body);

            if (request == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request" });
                return badRequest;
            }

            _logger.LogInformation("Generating {ReportType} report from {Start} to {End}",
                request.ReportType, request.StartDate, request.EndDate);

            // Get comprehensive analytics data
            var overview = await _analyticsService.GetApplicationMetricsAsync(
                request.StartDate,
                request.EndDate,
                request.ReportType);

            var sessionData = await _analyticsService.GetSessionMetricsAsync(
                request.StartDate,
                request.EndDate);

            var GrantData = await _analyticsService.GetGrantMetricsAsync(
                request.StartDate,
                request.EndDate,
                100);

            var topGrants = await _analyticsService.GetTopGrantsAsync(20);

            var funnelAnalysis = await _analyticsService.GetFunnelAnalysisAsync(
                request.StartDate,
                request.EndDate);

            // Build report data
            var reportData = new
            {
                ReportType = request.ReportType,
                Period = new
                {
                    Start = request.StartDate,
                    End = request.EndDate
                },
                Overview = overview,
                Sessions = sessionData,
                Grants = new
                {
                    Metrics = GrantData,
                    TopPerformers = topGrants
                },
                Funnel = funnelAnalysis,
                GeneratedAt = DateTime.UtcNow
            };

            var reportResponse = new GenerateReportResponse
            {
                Success = true,
                ReportId = Guid.NewGuid().ToString(),
                Format = request.Format,
                Data = reportData,
                GeneratedAt = DateTime.UtcNow
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(reportResponse);

            _logger.LogInformation("Successfully generated {ReportType} report", request.ReportType);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to generate report" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get real-time analytics dashboard data
    /// </summary>
    [Function("GetRealTimeAnalytics")]
    public async Task<HttpResponseData> GetRealTimeAnalytics(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/realtime")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            var realTimeData = await _analyticsService.GetRealTimeAnalyticsAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(realTimeData);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting real-time analytics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve real-time analytics" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get funnel analysis
    /// </summary>
    [Function("GetFunnelAnalysis")]
    public async Task<HttpResponseData> GetFunnelAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/funnel")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var daysBack = int.TryParse(query["daysBack"], out var days) ? days : 30;

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-daysBack);

            var funnelData = await _analyticsService.GetFunnelAnalysisAsync(startDate, endDate);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(funnelData);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting funnel analysis");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve funnel analysis" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get cohort analysis
    /// </summary>
    [Function("GetCohortAnalysis")]
    public async Task<HttpResponseData> GetCohortAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/cohorts")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var numberOfCohorts = int.TryParse(query["cohorts"], out var cohorts) ? cohorts : 6;

            var startDate = DateTime.UtcNow.AddDays(-numberOfCohorts * 7);
            var cohortData = await _analyticsService.GetCohortAnalysisAsync(startDate, numberOfCohorts);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(cohortData);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cohort analysis");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve cohort analysis" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Timer-triggered function to calculate daily metrics
    /// </summary>
    [Function("CalculateDailyMetrics")]
    public async Task CalculateDailyMetrics(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo, // Run at 2 AM every day
        FunctionContext executionContext)
    {
        _logger.LogInformation("Starting daily metrics calculation at {Time}", DateTime.UtcNow);

        try
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // Calculate application metrics
            await _analyticsService.CalculateApplicationMetricsAsync(yesterday, "daily");

            _logger.LogInformation("Successfully calculated daily metrics for {Date}", yesterday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily metrics");
        }
    }

    /// <summary>
    /// Timer-triggered function to calculate Grant metrics
    /// </summary>
    [Function("CalculateGrantMetrics")]
    public async Task CalculateGrantMetrics(
        [TimerTrigger("0 30 * * * *")] TimerInfo timerInfo, // Run every hour at 30 minutes past
        FunctionContext executionContext)
    {
        _logger.LogInformation("Starting Grant metrics calculation at {Time}", DateTime.UtcNow);

        try
        {
            // In a real implementation, you'd get all Grant IDs from the database
            // For now, this is a placeholder showing the pattern
            // TODO: Implement batch processing of all Grants

            _logger.LogInformation("Grant metrics calculation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Grant metrics");
        }
    }

    /// <summary>
    /// Get top performing Grants
    /// </summary>
    [Function("GetTopGrants")]
    public async Task<HttpResponseData> GetTopGrants(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/Grants/top")] HttpRequestData req,
        FunctionContext executionContext)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var limit = int.TryParse(query["limit"], out var l) ? l : 10;

            var topGrants = await _analyticsService.GetTopGrantsAsync(limit);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(topGrants);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top Grants");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve top Grants" });
            return errorResponse;
        }
    }
}
