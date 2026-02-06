using Microsoft.JSInterop;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using System.Net.Http.Json;

namespace GrantMatcher.Client.Services;

/// <summary>
/// Client-side analytics tracking service with fire-and-forget tracking
/// </summary>
public class AnalyticsClient : IAnalyticsClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private string _sessionId;
    private string _userId;
    private readonly Dictionary<string, DateTime> _pageStartTimes = new();

    public string SessionId => _sessionId;

    public AnalyticsClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _sessionId = Guid.NewGuid().ToString();
        _userId = "anonymous"; // Will be updated after user identification
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Try to get existing session ID from local storage
            var storedSessionId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "analytics_session_id");

            if (!string.IsNullOrEmpty(storedSessionId))
            {
                _sessionId = storedSessionId;
            }
            else
            {
                // Store new session ID
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "analytics_session_id", _sessionId);
            }

            // Get or create user ID
            var storedUserId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "analytics_user_id");
            if (!string.IsNullOrEmpty(storedUserId))
            {
                _userId = storedUserId;
            }
            else
            {
                _userId = Guid.NewGuid().ToString();
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "analytics_user_id", _userId);
            }
        }
        catch
        {
            // Silently fail - analytics should never break the app
        }
    }

    #region Event Tracking (Fire-and-Forget)

    public async Task TrackPageViewAsync(string pageUrl, string pageTitle)
    {
        await TrackEventAsync(
            EventTypes.PageViewed,
            EventCategories.Page,
            new Dictionary<string, object>
            {
                { "pageUrl", pageUrl },
                { "pageTitle", pageTitle }
            });

        StartTimeTracking(pageUrl);
    }

    public async Task TrackEventAsync(string eventType, string eventCategory, Dictionary<string, object>? properties = null)
    {
        // Fire-and-forget - don't block the UI
        _ = Task.Run(async () =>
        {
            try
            {
                var request = new TrackEventRequest
                {
                    UserId = _userId,
                    SessionId = _sessionId,
                    EventType = eventType,
                    EventCategory = eventCategory,
                    Properties = properties ?? new Dictionary<string, object>()
                };

                // Get current page info
                try
                {
                    request.PageUrl = await _jsRuntime.InvokeAsync<string>("eval", "window.location.href");
                    request.PageTitle = await _jsRuntime.InvokeAsync<string>("eval", "document.title");
                    request.Referrer = await _jsRuntime.InvokeAsync<string>("eval", "document.referrer");
                    request.UserAgent = await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
                }
                catch
                {
                    // Continue without this info if JS interop fails
                }

                // Send to API (with short timeout)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _httpClient.PostAsJsonAsync("analytics/track", request, cts.Token);
            }
            catch
            {
                // Silently fail - analytics should never break the app
            }
        });

        await Task.CompletedTask;
    }

    public Task TrackConversationStartedAsync()
    {
        return TrackEventAsync(EventTypes.ConversationStarted, EventCategories.Conversation);
    }

    public Task TrackConversationMessageAsync(int messageNumber)
    {
        return TrackEventAsync(
            EventTypes.ConversationMessageSent,
            EventCategories.Conversation,
            new Dictionary<string, object> { { "messageNumber", messageNumber } });
    }

    public Task TrackConversationCompletedAsync()
    {
        return TrackEventAsync(EventTypes.ConversationCompleted, EventCategories.Conversation);
    }

    public Task TrackProfileCreatedAsync(Guid profileId)
    {
        return TrackEventAsync(
            EventTypes.ProfileCreated,
            EventCategories.Profile,
            new Dictionary<string, object> { { "profileId", profileId.ToString() } });
    }

    public Task TrackProfileUpdatedAsync(Guid profileId)
    {
        return TrackEventAsync(
            EventTypes.ProfileUpdated,
            EventCategories.Profile,
            new Dictionary<string, object> { { "profileId", profileId.ToString() } });
    }

    public Task TrackSearchPerformedAsync(string query, int resultCount)
    {
        return TrackEventAsync(
            EventTypes.SearchPerformed,
            EventCategories.Search,
            new Dictionary<string, object>
            {
                { "query", query },
                { "resultCount", resultCount }
            });
    }

    public Task TrackGrantViewedAsync(Guid GrantId, string GrantName)
    {
        return TrackEventAsync(
            EventTypes.GrantViewed,
            EventCategories.Grant,
            new Dictionary<string, object>
            {
                { "GrantId", GrantId.ToString() },
                { "GrantName", GrantName }
            });
    }

    public Task TrackGrantSavedAsync(Guid GrantId, string GrantName)
    {
        return TrackEventAsync(
            EventTypes.GrantSaved,
            EventCategories.Grant,
            new Dictionary<string, object>
            {
                { "GrantId", GrantId.ToString() },
                { "GrantName", GrantName }
            });
    }

    public Task TrackApplicationLinkClickedAsync(Guid GrantId, string GrantName)
    {
        return TrackEventAsync(
            EventTypes.ApplicationLinkClicked,
            EventCategories.Grant,
            new Dictionary<string, object>
            {
                { "GrantId", GrantId.ToString() },
                { "GrantName", GrantName }
            });
    }

    public Task TrackErrorAsync(string errorMessage, string? stackTrace = null)
    {
        var properties = new Dictionary<string, object>
        {
            { "errorMessage", errorMessage }
        };

        if (!string.IsNullOrEmpty(stackTrace))
        {
            properties.Add("stackTrace", stackTrace);
        }

        return TrackEventAsync(EventTypes.ErrorOccurred, EventCategories.Error, properties);
    }

    #endregion

    #region Time Tracking

    public void StartTimeTracking(string pageUrl)
    {
        _pageStartTimes[pageUrl] = DateTime.UtcNow;
    }

    public async Task EndTimeTrackingAsync(string pageUrl)
    {
        if (_pageStartTimes.TryGetValue(pageUrl, out var startTime))
        {
            var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            await TrackEventAsync(
                EventTypes.PageTimeSpent,
                EventCategories.Page,
                new Dictionary<string, object>
                {
                    { "pageUrl", pageUrl },
                    { "durationMs", durationMs }
                });

            _pageStartTimes.Remove(pageUrl);
        }
    }

    #endregion

    #region Analytics Retrieval

    public async Task<GetAnalyticsResponse?> GetAnalyticsAsync(GetAnalyticsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("analytics/query", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GetAnalyticsResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<GenerateReportResponse?> GenerateReportAsync(GenerateReportRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("analytics/report", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GenerateReportResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<RealTimeAnalytics?> GetRealTimeAnalyticsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("analytics/realtime");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RealTimeAnalytics>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<FunnelAnalysis?> GetFunnelAnalysisAsync(int daysBack = 30)
    {
        try
        {
            var response = await _httpClient.GetAsync($"analytics/funnel?daysBack={daysBack}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FunnelAnalysis>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<TopGrants?> GetTopGrantsAsync(int limit = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"analytics/Grants/top?limit={limit}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TopGrants>();
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
