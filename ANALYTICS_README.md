# GrantMatcher Analytics System

A comprehensive analytics tracking and reporting system for the GrantMatcher application.

## Overview

The analytics system provides real-time and historical insights into user behavior, application performance, and Grant engagement. It's designed to be non-blocking, privacy-conscious, and highly scalable.

## Features

### Real-Time Analytics
- Active users and sessions
- Events per minute/hour
- Live activity feed
- Auto-refreshing dashboard

### User Behavior Tracking
- Page views and time spent
- Conversation interactions
- Profile creation and updates
- Search patterns
- Grant views and saves
- Application link clicks

### Performance Metrics
- API response times
- Error rates
- Conversion rates
- Bounce rates
- Session duration

### Grant Analytics
- View counts and trends
- Save rates
- Click-through rates
- Trending Grants
- Top performers

### Advanced Analytics
- Conversion funnel analysis
- Cohort retention analysis
- Session metrics
- Geographic data
- Device and browser analytics

## Architecture

### Components

```
GrantMatcher.Shared/
├── Models/
│   ├── AnalyticsEvent.cs          # Base event model
│   ├── UserSession.cs             # Session tracking
│   └── GrantMetrics.cs      # Per-Grant metrics
└── DTOs/
    └── AnalyticsDTOs.cs           # API request/response models

GrantMatcher.Core/
├── Interfaces/
│   └── IAnalyticsService.cs       # Service interface
└── Services/
    └── AnalyticsService.cs        # Core analytics logic

GrantMatcher.Functions/
└── Functions/
    └── AnalyticsFunctions.cs      # HTTP endpoints + timers

GrantMatcher.Client/
├── Services/
│   ├── IAnalyticsClient.cs        # Client interface
│   └── AnalyticsClient.cs         # Client-side tracking
├── Components/
│   ├── AnalyticsTracker.razor     # Auto page tracking
│   └── Pages/Admin/
│       ├── AnalyticsDashboard.razor      # Main dashboard
│       └── AnalyticsDashboard.razor.css  # Dashboard styles
```

### Data Flow

```
User Action → Client Event → Fire-and-Forget API Call → Cosmos DB
                                                              ↓
Timer Functions → Calculate Metrics → Store Aggregations
                                              ↓
Dashboard Query → Retrieve Metrics → Visualize Data
```

### Storage

**Cosmos DB Containers:**
1. **AnalyticsEvents** - Raw event data
   - Partition Key: `eventCategory_date`
   - TTL: Optional (e.g., 90 days for raw data)

2. **UserSessions** - Session tracking
   - Partition Key: `session_date`
   - Tracks user journeys and engagement

3. **AnalyticsMetrics** - Aggregated metrics
   - Partition Key: `period_date`
   - Pre-calculated daily/weekly/monthly metrics

## Quick Start

### 1. Setup Cosmos DB

Run the setup script:

```bash
cd scripts
chmod +x setup-analytics-cosmos.sh
./setup-analytics-cosmos.sh
```

Or create containers manually in Azure Portal:
- AnalyticsEvents (partition: /partitionKey)
- UserSessions (partition: /partitionKey)
- AnalyticsMetrics (partition: /partitionKey)

### 2. Configure Connection String

Update your `local.settings.json`:

```json
{
  "Values": {
    "CosmosDb:ConnectionString": "your-cosmos-connection-string",
    "CosmosDb:DatabaseName": "GrantMatcher"
  }
}
```

### 3. Deploy Functions

Deploy the Azure Functions project to enable analytics endpoints.

### 4. Access Dashboard

Navigate to `/admin/analytics` in your application.

## Usage

### Client-Side Tracking

The analytics system automatically tracks page views via the `AnalyticsTracker` component. For custom tracking:

```razor
@inject IAnalyticsClient AnalyticsClient

@code {
    // Track custom event
    await AnalyticsClient.TrackEventAsync(
        "button_clicked",
        EventCategories.Page,
        new Dictionary<string, object>
        {
            { "buttonName", "SaveProfile" },
            { "page", "ProfilePage" }
        }
    );

    // Track Grant interaction
    await AnalyticsClient.TrackGrantViewedAsync(
        GrantId,
        GrantName
    );
}
```

### Server-Side Tracking

```csharp
public class MyFunction
{
    private readonly IAnalyticsService _analyticsService;

    public MyFunction(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [Function("MyEndpoint")]
    public async Task<HttpResponseData> Run(HttpRequestData req)
    {
        // Track event
        await _analyticsService.TrackEventAsync(new AnalyticsEvent
        {
            UserId = userId,
            EventType = EventTypes.ProfileCreated,
            EventCategory = EventCategories.Profile,
            Properties = new Dictionary<string, object>
            {
                { "profileId", profileId.ToString() }
            }
        });

        // Your logic...
    }
}
```

### Query Analytics

```csharp
// Get application metrics
var metrics = await _analyticsService.GetApplicationMetricsAsync(
    startDate: DateTime.UtcNow.AddDays(-30),
    endDate: DateTime.UtcNow,
    period: "daily"
);

// Get top Grants
var topGrants = await _analyticsService.GetTopGrantsAsync(limit: 10);

// Get funnel analysis
var funnel = await _analyticsService.GetFunnelAnalysisAsync(
    DateTime.UtcNow.AddDays(-30),
    DateTime.UtcNow
);
```

## API Endpoints

### Track Event (Anonymous)
```
POST /api/analytics/track
Body: TrackEventRequest
```

Tracks a single analytics event. Fire-and-forget, returns immediately.

### Get Analytics (Authenticated)
```
POST /api/analytics/query
Body: GetAnalyticsRequest
```

Retrieves analytics data based on filters and date range.

### Generate Report (Authenticated)
```
POST /api/analytics/report
Body: GenerateReportRequest
```

Generates a comprehensive analytics report.

### Real-Time Analytics (Authenticated)
```
GET /api/analytics/realtime
```

Returns current real-time metrics (active users, sessions, recent events).

### Funnel Analysis (Authenticated)
```
GET /api/analytics/funnel?daysBack=30
```

Returns conversion funnel data.

### Cohort Analysis (Authenticated)
```
GET /api/analytics/cohorts?cohorts=6
```

Returns cohort retention analysis.

### Top Grants (Authenticated)
```
GET /api/analytics/Grants/top?limit=10
```

Returns top performing Grants.

## Event Types

### Conversation Events
- `conversation_started` - User starts conversation
- `conversation_message_sent` - User sends message
- `conversation_completed` - Profile creation completed
- `conversation_abandoned` - User exits without completing

### Profile Events
- `profile_created` - New profile created
- `profile_updated` - Profile modified
- `profile_viewed` - Profile page viewed

### Search Events
- `search_performed` - User performs search
- `search_filter_applied` - Filter applied to search
- `search_results_viewed` - Results page viewed

### Grant Events
- `Grant_viewed` - Grant details viewed
- `Grant_saved` - Grant saved to favorites
- `Grant_unsaved` - Grant removed from favorites
- `Grant_shared` - Grant shared
- `application_link_clicked` - Application URL clicked

### Page Events
- `page_viewed` - Page navigation
- `page_time_spent` - Time spent on page

### Error Events
- `error_occurred` - Application error
- `api_call_failed` - API request failed

## Event Categories

- `conversation` - Conversation-related events
- `profile` - Profile-related events
- `search` - Search-related events
- `Grant` - Grant-related events
- `page` - Page navigation events
- `error` - Error events

## Dashboard Metrics

### Overview
- Total users (new vs returning)
- Conversations started and completed
- Searches performed
- Grants viewed, saved, and clicked
- API performance (response time, error rate)

### Sessions
- Total sessions
- Average duration
- Average page views
- Bounce rate
- Conversion rates

### Grants
- Most viewed
- Most saved
- Highest click-through rate
- Trending (based on recent growth)

### Funnel
- 7-step conversion funnel
- Drop-off rates at each step
- Overall conversion rate

## Background Jobs

### Daily Metrics Calculation
**Schedule:** 2:00 AM daily
**Function:** `CalculateDailyMetrics`

Aggregates previous day's events into daily metrics.

### Grant Metrics Calculation
**Schedule:** Every hour at :30
**Function:** `CalculateGrantMetrics`

Updates per-Grant performance metrics.

## Best Practices

### 1. Fire-and-Forget
Analytics tracking is non-blocking and should never affect user experience:

```csharp
// This returns immediately
await AnalyticsClient.TrackEventAsync(...);
```

### 2. Privacy First
Never track PII (personally identifiable information):

```csharp
// Good
await AnalyticsClient.TrackEventAsync(
    EventTypes.ProfileCreated,
    EventCategories.Profile,
    new Dictionary<string, object>
    {
        { "major", "Computer Science" }
    }
);

// Bad - includes PII
await AnalyticsClient.TrackEventAsync(
    EventTypes.ProfileCreated,
    EventCategories.Profile,
    new Dictionary<string, object>
    {
        { "email", "user@example.com" } // DON'T
    }
);
```

### 3. Meaningful Properties
Include context that helps with analysis:

```csharp
await AnalyticsClient.TrackEventAsync(
    EventTypes.SearchPerformed,
    EventCategories.Search,
    new Dictionary<string, object>
    {
        { "query", searchQuery },
        { "resultCount", results.Count },
        { "hasFilters", filters.Any() },
        { "filterTypes", string.Join(",", filters.Select(f => f.Type)) }
    }
);
```

### 4. Error Handling
Always wrap analytics in error handling, but the service handles errors internally:

```csharp
try
{
    await AnalyticsClient.TrackEventAsync(...);
}
catch
{
    // Silently fail - analytics should never break the app
}
```

### 5. Performance
Use composite indexes in Cosmos DB for common query patterns:

```json
{
  "compositeIndexes": [
    [
      { "path": "/eventType", "order": "ascending" },
      { "path": "/timestamp", "order": "descending" }
    ]
  ]
}
```

## Monitoring

### Application Insights
All Functions are integrated with Application Insights for:
- Function execution metrics
- Error tracking
- Performance monitoring

### Cosmos DB Metrics
Monitor in Azure Portal:
- Request units (RU) consumption
- Storage usage
- Query performance

### Dashboard Health
The analytics dashboard auto-refreshes real-time data every 30 seconds.

## Troubleshooting

### Events Not Appearing

1. **Check Cosmos DB connection**
   - Verify connection string in configuration
   - Ensure containers exist

2. **Check Function deployment**
   - Verify Functions are deployed and running
   - Check Function logs in Application Insights

3. **Check browser console**
   - Look for JavaScript errors
   - Verify network requests to `/api/analytics/track`

### Dashboard Not Loading

1. **Authentication issues**
   - Dashboard endpoints require Function-level auth
   - Ensure proper Function keys

2. **CORS configuration**
   - Verify CORS settings in Function App

3. **Query timeouts**
   - Large date ranges may timeout
   - Use smaller date ranges or pagination

### High Costs

1. **Reduce RU allocation**
   - Start with 400 RU/s per container
   - Enable auto-scale if needed

2. **Implement TTL**
   - Set time-to-live on raw events (e.g., 90 days)
   - Keep aggregated metrics indefinitely

3. **Optimize queries**
   - Use composite indexes
   - Limit result sets
   - Cache frequently accessed data

## Roadmap

### Phase 1 (Current)
- ✅ Core event tracking
- ✅ Real-time analytics
- ✅ Analytics dashboard
- ✅ Funnel analysis

### Phase 2 (Future)
- [ ] A/B testing framework
- [ ] Predictive analytics (ML)
- [ ] Custom report builder
- [ ] Email report scheduling
- [ ] Export to CSV/PDF

### Phase 3 (Future)
- [ ] User segmentation
- [ ] Behavioral cohorts
- [ ] Conversion optimization suggestions
- [ ] Integration with external tools (Google Analytics, Segment)

## Support

For questions or issues:
1. Check the [Integration Guide](ANALYTICS_INTEGRATION_GUIDE.md)
2. Review Application Insights logs
3. Check Cosmos DB metrics in Azure Portal

## License

Part of the GrantMatcher application.
