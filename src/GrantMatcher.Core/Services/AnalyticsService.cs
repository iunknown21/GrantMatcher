using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Services;

/// <summary>
/// Service for tracking and analyzing user behavior and application metrics
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly string _databaseName;
    private readonly Container _eventsContainer;
    private readonly Container _sessionsContainer;
    private readonly Container _metricsContainer;

    public AnalyticsService(
        CosmosClient cosmosClient,
        ILogger<AnalyticsService> logger,
        string databaseName = "GrantMatcher")
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = databaseName;

        // Initialize containers (will be created if they don't exist)
        var database = _cosmosClient.GetDatabase(_databaseName);
        _eventsContainer = database.GetContainer("AnalyticsEvents");
        _sessionsContainer = database.GetContainer("UserSessions");
        _metricsContainer = database.GetContainer("AnalyticsMetrics");
    }

    #region Event Tracking

    public async Task<TrackEventResponse> TrackEventAsync(TrackEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var analyticsEvent = new AnalyticsEvent
            {
                UserId = request.UserId,
                SessionId = request.SessionId,
                EventType = request.EventType,
                EventCategory = request.EventCategory,
                Properties = request.Properties,
                PageUrl = request.PageUrl,
                PageTitle = request.PageTitle,
                Referrer = request.Referrer,
                UserAgent = request.UserAgent,
                DurationMs = request.DurationMs,
                Timestamp = DateTime.UtcNow
            };

            // Parse user agent for device info
            ParseUserAgent(request.UserAgent, analyticsEvent);

            await _eventsContainer.CreateItemAsync(
                analyticsEvent,
                new PartitionKey(analyticsEvent.PartitionKey),
                cancellationToken: cancellationToken);

            // Update session if session ID is provided
            if (!string.IsNullOrEmpty(request.SessionId))
            {
                await UpdateSessionEventCountAsync(request.SessionId, cancellationToken);
            }

            _logger.LogInformation("Tracked event: {EventType} for user {UserId}", request.EventType, request.UserId);

            return new TrackEventResponse
            {
                Success = true,
                EventId = analyticsEvent.Id,
                Timestamp = analyticsEvent.Timestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking event: {EventType}", request.EventType);
            return new TrackEventResponse { Success = false };
        }
    }

    public async Task TrackEventAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _eventsContainer.CreateItemAsync(
                analyticsEvent,
                new PartitionKey(analyticsEvent.PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Tracked event: {EventType} for user {UserId}", analyticsEvent.EventType, analyticsEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking analytics event");
        }
    }

    #endregion

    #region Session Management

    public async Task<UserSession> CreateSessionAsync(string userId, string? referrer = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = new UserSession
            {
                UserId = userId,
                StartTime = DateTime.UtcNow,
                Referrer = referrer
            };

            await _sessionsContainer.CreateItemAsync(
                session,
                new PartitionKey(session.PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created session {SessionId} for user {UserId}", session.Id, userId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sessionsContainer.ReplaceItemAsync(
                session,
                session.Id,
                new PartitionKey(session.PartitionKey),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session {SessionId}", session.Id);
        }
    }

    public async Task<UserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @sessionId")
                .WithParameter("@sessionId", sessionId);

            var iterator = _sessionsContainer.GetItemQueryIterator<UserSession>(query);
            var results = await iterator.ReadNextAsync(cancellationToken);

            return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session != null && !session.EndTime.HasValue)
            {
                session.EndTime = DateTime.UtcNow;
                await UpdateSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
        }
    }

    private async Task UpdateSessionEventCountAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session != null)
            {
                session.EventCount++;
                await UpdateSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session event count");
        }
    }

    #endregion

    #region Metrics Retrieval

    public async Task<ApplicationMetrics> GetApplicationMetricsAsync(DateTime startDate, DateTime endDate, string period = "daily", CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.date >= @startDate AND c.date <= @endDate AND c.period = @period")
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate)
                .WithParameter("@period", period);

            var iterator = _metricsContainer.GetItemQueryIterator<ApplicationMetrics>(query);
            var results = new List<ApplicationMetrics>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            // If no cached metrics, calculate on-the-fly
            if (!results.Any())
            {
                return await CalculateApplicationMetricsInternalAsync(startDate, endDate, period, cancellationToken);
            }

            // Aggregate multiple metrics if necessary
            return AggregateApplicationMetrics(results, period);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application metrics");
            return new ApplicationMetrics { Date = startDate, Period = period };
        }
    }

    public async Task<SessionMetrics> GetSessionMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.startTime >= @startDate AND c.startTime <= @endDate")
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate);

            var iterator = _sessionsContainer.GetItemQueryIterator<UserSession>(query);
            var sessions = new List<UserSession>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                sessions.AddRange(response);
            }

            return CalculateSessionMetrics(sessions, startDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session metrics");
            return new SessionMetrics { Date = startDate };
        }
    }

    public async Task<List<GrantMetrics>> GetGrantMetricsAsync(DateTime startDate, DateTime endDate, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT TOP @limit * FROM c WHERE c.calculatedAt >= @startDate AND c.calculatedAt <= @endDate ORDER BY c.totalViews DESC")
                .WithParameter("@limit", limit)
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate);

            var iterator = _metricsContainer.GetItemQueryIterator<GrantMetrics>(query);
            var results = new List<GrantMetrics>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Grant metrics");
            return new List<GrantMetrics>();
        }
    }

    public async Task<GrantMetrics?> GetGrantMetricsByIdAsync(Guid GrantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM c WHERE c.GrantId = @GrantId ORDER BY c.calculatedAt DESC")
                .WithParameter("@GrantId", GrantId);

            var iterator = _metricsContainer.GetItemQueryIterator<GrantMetrics>(query);
            var results = await iterator.ReadNextAsync(cancellationToken);

            return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Grant metrics for {GrantId}", GrantId);
            return null;
        }
    }

    public async Task<TopGrants> GetTopGrantsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);

            var allMetrics = await GetGrantMetricsAsync(startDate, endDate, 1000, cancellationToken);

            return new TopGrants
            {
                GeneratedAt = DateTime.UtcNow,
                MostViewed = allMetrics
                    .OrderByDescending(m => m.TotalViews)
                    .Take(limit)
                    .Select((m, i) => new GrantRanking
                    {
                        GrantId = m.GrantId,
                        Name = m.GrantName,
                        Rank = i + 1,
                        Count = m.TotalViews,
                        Score = m.TotalViews
                    }).ToList(),
                MostSaved = allMetrics
                    .OrderByDescending(m => m.TotalSaves)
                    .Take(limit)
                    .Select((m, i) => new GrantRanking
                    {
                        GrantId = m.GrantId,
                        Name = m.GrantName,
                        Rank = i + 1,
                        Count = m.TotalSaves,
                        Score = m.TotalSaves
                    }).ToList(),
                HighestClickThrough = allMetrics
                    .Where(m => m.TotalViews > 0)
                    .OrderByDescending(m => m.ViewToClickRate)
                    .Take(limit)
                    .Select((m, i) => new GrantRanking
                    {
                        GrantId = m.GrantId,
                        Name = m.GrantName,
                        Rank = i + 1,
                        Count = m.ApplicationLinkClicks,
                        Score = m.ViewToClickRate
                    }).ToList(),
                Trending = allMetrics
                    .Where(m => m.IsTrending)
                    .OrderByDescending(m => m.TrendingScore)
                    .Take(limit)
                    .Select((m, i) => new GrantRanking
                    {
                        GrantId = m.GrantId,
                        Name = m.GrantName,
                        Rank = i + 1,
                        Count = m.ViewsLast7Days,
                        Score = m.TrendingScore
                    }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top Grants");
            return new TopGrants();
        }
    }

    #endregion

    #region Event Querying

    public async Task<List<AnalyticsEvent>> GetEventsAsync(GetAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.timestamp >= @startDate AND c.timestamp <= @endDate";
            var query = new QueryDefinition(queryText)
                .WithParameter("@startDate", request.StartDate)
                .WithParameter("@endDate", request.EndDate);

            if (!string.IsNullOrEmpty(request.UserId))
            {
                queryText += " AND c.userId = @userId";
                query = new QueryDefinition(queryText)
                    .WithParameter("@startDate", request.StartDate)
                    .WithParameter("@endDate", request.EndDate)
                    .WithParameter("@userId", request.UserId);
            }

            if (!string.IsNullOrEmpty(request.EventCategory))
            {
                queryText += " AND c.eventCategory = @eventCategory";
                query = new QueryDefinition(queryText)
                    .WithParameter("@startDate", request.StartDate)
                    .WithParameter("@endDate", request.EndDate)
                    .WithParameter("@eventCategory", request.EventCategory);
            }

            if (!string.IsNullOrEmpty(request.EventType))
            {
                queryText += " AND c.eventType = @eventType";
                query = new QueryDefinition(queryText)
                    .WithParameter("@startDate", request.StartDate)
                    .WithParameter("@endDate", request.EndDate)
                    .WithParameter("@eventType", request.EventType);
            }

            queryText += $" ORDER BY c.timestamp DESC OFFSET {request.Offset} LIMIT {request.Limit}";
            query = new QueryDefinition(queryText);

            var iterator = _eventsContainer.GetItemQueryIterator<AnalyticsEvent>(query);
            var results = new List<AnalyticsEvent>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events");
            return new List<AnalyticsEvent>();
        }
    }

    public async Task<int> GetEventCountAsync(string eventType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT VALUE COUNT(1) FROM c WHERE c.eventType = @eventType AND c.timestamp >= @startDate AND c.timestamp <= @endDate")
                .WithParameter("@eventType", eventType)
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate);

            var iterator = _eventsContainer.GetItemQueryIterator<int>(query);
            var results = await iterator.ReadNextAsync(cancellationToken);

            return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event count for {EventType}", eventType);
            return 0;
        }
    }

    #endregion

    #region Analytics Calculations

    public async Task CalculateGrantMetricsAsync(Guid GrantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);
            var last7Days = endDate.AddDays(-7);

            // Get all events related to this Grant
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.properties.GrantId = @GrantId AND c.timestamp >= @startDate")
                .WithParameter("@GrantId", GrantId.ToString())
                .WithParameter("@startDate", startDate);

            var iterator = _eventsContainer.GetItemQueryIterator<AnalyticsEvent>(query);
            var events = new List<AnalyticsEvent>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                events.AddRange(response);
            }

            // Calculate metrics
            var metrics = new GrantMetrics
            {
                GrantId = GrantId,
                CalculatedAt = DateTime.UtcNow,
                TotalViews = events.Count(e => e.EventType == EventTypes.GrantViewed),
                UniqueViews = events.Where(e => e.EventType == EventTypes.GrantViewed)
                    .Select(e => e.UserId).Distinct().Count(),
                TotalSaves = events.Count(e => e.EventType == EventTypes.GrantSaved),
                TotalShares = events.Count(e => e.EventType == EventTypes.GrantShared),
                ApplicationLinkClicks = events.Count(e => e.EventType == EventTypes.ApplicationLinkClicked),
                ViewsLast7Days = events.Count(e => e.EventType == EventTypes.GrantViewed && e.Timestamp >= last7Days),
                SavesLast7Days = events.Count(e => e.EventType == EventTypes.GrantSaved && e.Timestamp >= last7Days),
                ClicksLast7Days = events.Count(e => e.EventType == EventTypes.ApplicationLinkClicked && e.Timestamp >= last7Days)
            };

            // Calculate average view duration
            var viewEvents = events.Where(e => e.EventType == EventTypes.GrantViewed && e.DurationMs.HasValue);
            if (viewEvents.Any())
            {
                metrics.AverageViewDurationSeconds = viewEvents.Average(e => e.DurationMs!.Value) / 1000;
            }

            // Calculate trending score (views in last 7 days vs previous 7 days)
            var previous7Days = last7Days.AddDays(-7);
            var previousViews = events.Count(e => e.EventType == EventTypes.GrantViewed &&
                e.Timestamp >= previous7Days && e.Timestamp < last7Days);

            if (previousViews > 0)
            {
                metrics.TrendingScore = ((double)metrics.ViewsLast7Days - previousViews) / previousViews * 100;
                metrics.IsTrending = metrics.TrendingScore > 20; // 20% increase threshold
            }

            await _metricsContainer.UpsertItemAsync(
                metrics,
                new PartitionKey(metrics.PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Calculated metrics for Grant {GrantId}", GrantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Grant metrics for {GrantId}", GrantId);
        }
    }

    public async Task CalculateApplicationMetricsAsync(DateTime date, string period, CancellationToken cancellationToken = default)
    {
        try
        {
            var endDate = date.Date.AddDays(1);
            var startDate = period switch
            {
                "weekly" => date.Date.AddDays(-7),
                "monthly" => date.Date.AddMonths(-1),
                _ => date.Date
            };

            var metrics = await CalculateApplicationMetricsInternalAsync(startDate, endDate, period, cancellationToken);

            await _metricsContainer.UpsertItemAsync(
                metrics,
                new PartitionKey(metrics.PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Calculated application metrics for {Date} ({Period})", date, period);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating application metrics");
        }
    }

    #endregion

    #region Funnel and Cohort Analysis

    public async Task<FunnelAnalysis> GetFunnelAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Define funnel steps
            var steps = new[]
            {
                (EventTypes.PageViewed, "Landing Page"),
                (EventTypes.ConversationStarted, "Started Conversation"),
                (EventTypes.ProfileCreated, "Created Profile"),
                (EventTypes.SearchPerformed, "Searched Grants"),
                (EventTypes.GrantViewed, "Viewed Grant"),
                (EventTypes.GrantSaved, "Saved Grant"),
                (EventTypes.ApplicationLinkClicked, "Clicked Application")
            };

            var funnelSteps = new List<FunnelStep>();
            var previousCount = 0;

            for (int i = 0; i < steps.Length; i++)
            {
                var (eventType, stepName) = steps[i];
                var count = await GetEventCountAsync(eventType, startDate, endDate, cancellationToken);

                var dropoffRate = i > 0 && previousCount > 0
                    ? (double)(previousCount - count) / previousCount * 100
                    : 0;

                var conversionRate = i == 0 ? 100 : previousCount > 0
                    ? (double)count / previousCount * 100
                    : 0;

                funnelSteps.Add(new FunnelStep
                {
                    StepName = stepName,
                    StepNumber = i + 1,
                    UserCount = count,
                    DropoffRate = dropoffRate,
                    ConversionRate = conversionRate
                });

                previousCount = count;
            }

            var initialCount = funnelSteps.FirstOrDefault()?.UserCount ?? 0;
            var finalCount = funnelSteps.LastOrDefault()?.UserCount ?? 0;
            var overallConversion = initialCount > 0 ? (double)finalCount / initialCount * 100 : 0;

            return new FunnelAnalysis
            {
                StartDate = startDate,
                EndDate = endDate,
                Steps = funnelSteps,
                OverallConversionRate = overallConversion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting funnel analysis");
            return new FunnelAnalysis { StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<List<CohortAnalysis>> GetCohortAnalysisAsync(DateTime startDate, int numberOfCohorts = 6, CancellationToken cancellationToken = default)
    {
        // Simplified cohort analysis - track users by week they joined
        var cohorts = new List<CohortAnalysis>();

        for (int i = 0; i < numberOfCohorts; i++)
        {
            var cohortStart = startDate.AddDays(-i * 7);
            var cohortEnd = cohortStart.AddDays(7);

            // Get users who joined in this cohort week
            var cohortUsers = await GetEventCountAsync(EventTypes.ProfileCreated, cohortStart, cohortEnd, cancellationToken);

            var cohort = new CohortAnalysis
            {
                CohortName = $"Week of {cohortStart:MMM dd}",
                StartDate = cohortStart,
                InitialUsers = cohortUsers,
                RetentionByWeek = new List<RetentionData>()
            };

            // Calculate retention for each subsequent week
            for (int week = 0; week < 8; week++)
            {
                var weekStart = cohortStart.AddDays(week * 7);
                var weekEnd = weekStart.AddDays(7);

                // Count active users in this week
                var query = new QueryDefinition(
                    "SELECT VALUE COUNT(DISTINCT c.userId) FROM c WHERE c.timestamp >= @start AND c.timestamp < @end")
                    .WithParameter("@start", weekStart)
                    .WithParameter("@end", weekEnd);

                var iterator = _eventsContainer.GetItemQueryIterator<int>(query);
                var results = await iterator.ReadNextAsync(cancellationToken);
                var activeUsers = results.FirstOrDefault();

                var retentionRate = cohortUsers > 0 ? (double)activeUsers / cohortUsers * 100 : 0;

                cohort.RetentionByWeek.Add(new RetentionData
                {
                    WeekNumber = week,
                    WeekStart = weekStart,
                    ActiveUsers = activeUsers,
                    RetentionRate = retentionRate
                });
            }

            cohorts.Add(cohort);
        }

        return cohorts;
    }

    #endregion

    #region Real-time Analytics

    public async Task<RealTimeAnalytics> GetRealTimeAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            var oneHourAgo = now.AddHours(-1);

            // Get events from last minute and hour
            var eventsLastMinute = await GetEventCountAsync("*", oneMinuteAgo, now, cancellationToken);
            var eventsLastHour = await GetEventCountAsync("*", oneHourAgo, now, cancellationToken);

            // Get active sessions
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.endTime = null AND c.startTime >= @fiveMinutesAgo")
                .WithParameter("@fiveMinutesAgo", now.AddMinutes(-5));

            var iterator = _sessionsContainer.GetItemQueryIterator<UserSession>(query);
            var activeSessions = new List<UserSession>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                activeSessions.AddRange(response);
            }

            // Get recent events
            var recentEventsQuery = new QueryDefinition(
                "SELECT TOP 10 * FROM c WHERE c.timestamp >= @fiveMinutesAgo ORDER BY c.timestamp DESC")
                .WithParameter("@fiveMinutesAgo", now.AddMinutes(-5));

            var recentIterator = _eventsContainer.GetItemQueryIterator<AnalyticsEvent>(recentEventsQuery);
            var recentEvents = new List<AnalyticsEvent>();

            while (recentIterator.HasMoreResults)
            {
                var response = await recentIterator.ReadNextAsync(cancellationToken);
                recentEvents.AddRange(response);
            }

            return new RealTimeAnalytics
            {
                Timestamp = now,
                ActiveUsers = activeSessions.Select(s => s.UserId).Distinct().Count(),
                ActiveSessions = activeSessions.Count,
                EventsLastMinute = eventsLastMinute,
                EventsLastHour = eventsLastHour,
                RecentEvents = recentEvents.Select(e => new RecentEvent
                {
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    UserId = e.UserId,
                    Description = GetEventDescription(e)
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting real-time analytics");
            return new RealTimeAnalytics();
        }
    }

    #endregion

    #region Helper Methods

    private async Task<ApplicationMetrics> CalculateApplicationMetricsInternalAsync(
        DateTime startDate,
        DateTime endDate,
        string period,
        CancellationToken cancellationToken)
    {
        var metrics = new ApplicationMetrics
        {
            Date = startDate,
            Period = period
        };

        // Get all events in the period
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.timestamp >= @startDate AND c.timestamp <= @endDate")
            .WithParameter("@startDate", startDate)
            .WithParameter("@endDate", endDate);

        var iterator = _eventsContainer.GetItemQueryIterator<AnalyticsEvent>(query);
        var events = new List<AnalyticsEvent>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            events.AddRange(response);
        }

        // Calculate metrics
        metrics.TotalUsers = events.Select(e => e.UserId).Distinct().Count();
        metrics.ProfilesCreated = events.Count(e => e.EventType == EventTypes.ProfileCreated);
        metrics.ProfilesUpdated = events.Count(e => e.EventType == EventTypes.ProfileUpdated);
        metrics.ConversationsStarted = events.Count(e => e.EventType == EventTypes.ConversationStarted);
        metrics.ConversationsCompleted = events.Count(e => e.EventType == EventTypes.ConversationCompleted);
        metrics.SearchesPerformed = events.Count(e => e.EventType == EventTypes.SearchPerformed);
        metrics.GrantsViewed = events.Count(e => e.EventType == EventTypes.GrantViewed);
        metrics.GrantsSaved = events.Count(e => e.EventType == EventTypes.GrantSaved);
        metrics.ApplicationLinksClicked = events.Count(e => e.EventType == EventTypes.ApplicationLinkClicked);

        // Calculate API metrics
        var apiEvents = events.Where(e => e.Properties.ContainsKey("apiCall"));
        metrics.TotalApiCalls = apiEvents.Count();
        metrics.FailedApiCalls = events.Count(e => e.EventType == EventTypes.ApiCallFailed);

        if (apiEvents.Any())
        {
            var apiDurations = apiEvents.Where(e => e.DurationMs.HasValue).Select(e => e.DurationMs!.Value);
            if (apiDurations.Any())
            {
                metrics.AverageApiResponseTimeMs = apiDurations.Average();
            }
        }

        return metrics;
    }

    private SessionMetrics CalculateSessionMetrics(List<UserSession> sessions, DateTime date)
    {
        var metrics = new SessionMetrics
        {
            Date = date,
            TotalSessions = sessions.Count,
            UniqueSessions = sessions.Select(s => s.UserId).Distinct().Count(),
            BounceCount = sessions.Count(s => s.PageViews <= 1),
            SessionsWithProfileCreation = sessions.Count(s => s.ProfileCreated),
            SessionsWithSearch = sessions.Count(s => s.SearchesPerformed > 0),
            SessionsWithGrantView = sessions.Count(s => s.GrantsViewed > 0),
            SessionsWithApplicationClick = sessions.Count(s => s.ApplicationLinksClicked > 0)
        };

        if (sessions.Any())
        {
            metrics.AverageDurationMinutes = sessions.Average(s => s.DurationMinutes);
            metrics.AveragePageViews = sessions.Average(s => s.PageViews);
        }

        return metrics;
    }

    private ApplicationMetrics AggregateApplicationMetrics(List<ApplicationMetrics> metrics, string period)
    {
        if (!metrics.Any())
            return new ApplicationMetrics { Period = period };

        return new ApplicationMetrics
        {
            Date = metrics.Min(m => m.Date),
            Period = period,
            TotalUsers = metrics.Sum(m => m.TotalUsers),
            NewUsers = metrics.Sum(m => m.NewUsers),
            ActiveUsers = metrics.Sum(m => m.ActiveUsers),
            ProfilesCreated = metrics.Sum(m => m.ProfilesCreated),
            ProfilesUpdated = metrics.Sum(m => m.ProfilesUpdated),
            ConversationsStarted = metrics.Sum(m => m.ConversationsStarted),
            ConversationsCompleted = metrics.Sum(m => m.ConversationsCompleted),
            SearchesPerformed = metrics.Sum(m => m.SearchesPerformed),
            GrantsViewed = metrics.Sum(m => m.GrantsViewed),
            GrantsSaved = metrics.Sum(m => m.GrantsSaved),
            ApplicationLinksClicked = metrics.Sum(m => m.ApplicationLinksClicked),
            AverageApiResponseTimeMs = metrics.Average(m => m.AverageApiResponseTimeMs),
            TotalApiCalls = metrics.Sum(m => m.TotalApiCalls),
            FailedApiCalls = metrics.Sum(m => m.FailedApiCalls)
        };
    }

    private void ParseUserAgent(string? userAgent, AnalyticsEvent analyticsEvent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return;

        // Simple user agent parsing (in production, use a library like UAParser)
        analyticsEvent.UserAgent = userAgent;

        if (userAgent.Contains("Mobile") || userAgent.Contains("Android") || userAgent.Contains("iPhone"))
        {
            analyticsEvent.DeviceType = "Mobile";
        }
        else if (userAgent.Contains("Tablet") || userAgent.Contains("iPad"))
        {
            analyticsEvent.DeviceType = "Tablet";
        }
        else
        {
            analyticsEvent.DeviceType = "Desktop";
        }

        if (userAgent.Contains("Chrome"))
            analyticsEvent.Browser = "Chrome";
        else if (userAgent.Contains("Firefox"))
            analyticsEvent.Browser = "Firefox";
        else if (userAgent.Contains("Safari"))
            analyticsEvent.Browser = "Safari";
        else if (userAgent.Contains("Edge"))
            analyticsEvent.Browser = "Edge";

        if (userAgent.Contains("Windows"))
            analyticsEvent.OS = "Windows";
        else if (userAgent.Contains("Mac"))
            analyticsEvent.OS = "macOS";
        else if (userAgent.Contains("Linux"))
            analyticsEvent.OS = "Linux";
        else if (userAgent.Contains("Android"))
            analyticsEvent.OS = "Android";
        else if (userAgent.Contains("iOS"))
            analyticsEvent.OS = "iOS";
    }

    private string GetEventDescription(AnalyticsEvent e)
    {
        return e.EventType switch
        {
            EventTypes.ProfileCreated => "User created profile",
            EventTypes.SearchPerformed => "User performed search",
            EventTypes.GrantViewed => $"Viewed Grant",
            EventTypes.GrantSaved => "Saved Grant",
            EventTypes.ApplicationLinkClicked => "Clicked application link",
            _ => e.EventType
        };
    }

    #endregion
}
