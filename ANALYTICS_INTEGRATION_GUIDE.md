# Analytics Integration Guide

This guide shows how to integrate analytics tracking into your GrantMatcher application components.

## Table of Contents
1. [Setup](#setup)
2. [Client-Side Integration](#client-side-integration)
3. [Server-Side Integration](#server-side-integration)
4. [Event Tracking Examples](#event-tracking-examples)
5. [Analytics Dashboard](#analytics-dashboard)
6. [Best Practices](#best-practices)

## Setup

### 1. Cosmos DB Containers

Create the following containers in your Cosmos DB database:

```bash
# AnalyticsEvents container
- Container Name: AnalyticsEvents
- Partition Key: /partitionKey
- Indexing Policy: Default

# UserSessions container
- Container Name: UserSessions
- Partition Key: /partitionKey
- Indexing Policy: Default

# AnalyticsMetrics container
- Container Name: AnalyticsMetrics
- Partition Key: /partitionKey
- Indexing Policy: Default
```

### 2. Register Services

Services are already registered in `Program.cs` files:

**Functions/Program.cs:**
```csharp
builder.Services.AddScoped<IAnalyticsService>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<AnalyticsService>>();
    return new AnalyticsService(cosmosClient, logger, databaseName);
});
```

**Client/Program.cs:**
```csharp
builder.Services.AddScoped<IAnalyticsClient, AnalyticsClient>();
```

### 3. Add Analytics Tracker to App.razor

Add the `AnalyticsTracker` component to your main `App.razor`:

```razor
@* In App.razor *@
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p role="alert">Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>

<!-- Add Analytics Tracker -->
<AnalyticsTracker />
```

## Client-Side Integration

### Inject Analytics Client

In any Razor component:

```razor
@inject IAnalyticsClient AnalyticsClient
```

### Track Events in Components

#### Example 1: Track Conversation Started

```razor
@* ConversationalInput.razor *@
@inject IAnalyticsClient AnalyticsClient

<div class="conversation-input">
    <textarea @bind="userMessage" placeholder="Tell me about yourself..."></textarea>
    <button @onclick="SendMessageAsync">Send</button>
</div>

@code {
    private string userMessage = "";
    private int messageCount = 0;

    private async Task SendMessageAsync()
    {
        if (messageCount == 0)
        {
            // Track conversation started
            await AnalyticsClient.TrackConversationStartedAsync();
        }

        messageCount++;

        // Track message sent
        await AnalyticsClient.TrackConversationMessageAsync(messageCount);

        // Process message...
        await ProcessMessage(userMessage);
    }
}
```

#### Example 2: Track Profile Creation

```razor
@* ProfileWizard.razor *@
@inject IAnalyticsClient AnalyticsClient
@inject IApiClient ApiClient

@code {
    private async Task SaveProfileAsync()
    {
        try
        {
            var profile = await ApiClient.CreateProfileAsync(currentProfile);

            // Track profile created
            await AnalyticsClient.TrackProfileCreatedAsync(profile.Id);

            // Track conversation completed
            await AnalyticsClient.TrackConversationCompletedAsync();

            NavigationManager.NavigateTo("/dashboard");
        }
        catch (Exception ex)
        {
            await AnalyticsClient.TrackErrorAsync(ex.Message, ex.StackTrace);
        }
    }
}
```

#### Example 3: Track Search

```razor
@* SearchPage.razor *@
@inject IAnalyticsClient AnalyticsClient
@inject IApiClient ApiClient

@code {
    private async Task PerformSearchAsync()
    {
        try
        {
            var searchRequest = new SearchRequest
            {
                Query = searchQuery,
                Limit = 50
            };

            var results = await ApiClient.SearchGrantsAsync(searchRequest);

            // Track search performed
            await AnalyticsClient.TrackSearchPerformedAsync(
                searchQuery,
                results.Matches.Count
            );

            Grants = results.Matches;
        }
        catch (Exception ex)
        {
            await AnalyticsClient.TrackErrorAsync(ex.Message);
        }
    }
}
```

#### Example 4: Track Grant Interactions

```razor
@* GrantCard.razor *@
@inject IAnalyticsClient AnalyticsClient

<div class="Grant-card" @onclick="ViewGrantAsync">
    <h3>@Grant.Name</h3>
    <p>@Grant.Description</p>

    <div class="actions">
        <button @onclick="SaveGrantAsync" @onclick:stopPropagation="true">
            Save
        </button>
        <a href="@Grant.ApplicationUrl"
           target="_blank"
           @onclick="TrackApplicationClickAsync"
           @onclick:stopPropagation="true">
            Apply Now
        </a>
    </div>
</div>

@code {
    [Parameter]
    public GrantEntity Grant { get; set; } = new();

    private async Task ViewGrantAsync()
    {
        // Track Grant viewed
        await AnalyticsClient.TrackGrantViewedAsync(
            Grant.Id,
            Grant.Name
        );

        // Navigate to details...
    }

    private async Task SaveGrantAsync()
    {
        // Save Grant logic...

        // Track save
        await AnalyticsClient.TrackGrantSavedAsync(
            Grant.Id,
            Grant.Name
        );
    }

    private async Task TrackApplicationClickAsync(MouseEventArgs e)
    {
        // Track application link clicked
        await AnalyticsClient.TrackApplicationLinkClickedAsync(
            Grant.Id,
            Grant.Name
        );
    }
}
```

#### Example 5: Custom Event Tracking

```razor
@inject IAnalyticsClient AnalyticsClient

@code {
    private async Task TrackCustomEventAsync()
    {
        await AnalyticsClient.TrackEventAsync(
            "filter_applied",
            EventCategories.Search,
            new Dictionary<string, object>
            {
                { "filterType", "major" },
                { "filterValue", "Computer Science" },
                { "resultCount", 25 }
            }
        );
    }
}
```

## Server-Side Integration

### Track Events in Azure Functions

```csharp
// In any Azure Function
public class GrantFunctions
{
    private readonly IAnalyticsService _analyticsService;

    public GrantFunctions(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [Function("CreateProfile")]
    public async Task<HttpResponseData> CreateProfile(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var profile = await JsonSerializer.DeserializeAsync<NonprofitProfile>(req.Body);

            // Save profile...

            // Track event
            await _analyticsService.TrackEventAsync(new AnalyticsEvent
            {
                UserId = profile.UserId,
                EventType = EventTypes.ProfileCreated,
                EventCategory = EventCategories.Profile,
                Properties = new Dictionary<string, object>
                {
                    { "profileId", profile.Id.ToString() },
                    { "major", profile.Major },
                    { "graduationYear", profile.GraduationYear }
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(profile);
            return response;
        }
        catch (Exception ex)
        {
            // Track error
            await _analyticsService.TrackEventAsync(new AnalyticsEvent
            {
                EventType = EventTypes.ErrorOccurred,
                EventCategory = EventCategories.Error,
                Properties = new Dictionary<string, object>
                {
                    { "error", ex.Message },
                    { "endpoint", "CreateProfile" }
                }
            });

            throw;
        }
    }
}
```

### Calculate Metrics (Background Jobs)

The analytics system includes timer-triggered functions that automatically calculate metrics:

```csharp
// These run automatically - no integration needed

[Function("CalculateDailyMetrics")]
public async Task CalculateDailyMetrics(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo)
{
    var yesterday = DateTime.UtcNow.Date.AddDays(-1);
    await _analyticsService.CalculateApplicationMetricsAsync(yesterday, "daily");
}

[Function("CalculateGrantMetrics")]
public async Task CalculateGrantMetrics(
    [TimerTrigger("0 30 * * * *")] TimerInfo timerInfo)
{
    // Calculate metrics for all Grants
}
```

## Analytics Dashboard

### Access the Dashboard

Navigate to `/admin/analytics` to view the analytics dashboard.

### Dashboard Features

1. **Real-Time Metrics**
   - Active users
   - Active sessions
   - Events per minute/hour

2. **Overview Metrics**
   - Total users
   - Conversations
   - Searches
   - Grant views/saves
   - Application clicks

3. **Session Metrics**
   - Average duration
   - Bounce rate
   - Conversion rates

4. **Top Grants**
   - Most viewed
   - Most saved
   - Highest CTR
   - Trending

5. **Funnel Analysis**
   - Conversion funnel from landing to application
   - Drop-off rates
   - Overall conversion rate

### Retrieve Analytics Programmatically

```csharp
// Get analytics data
var request = new GetAnalyticsRequest
{
    StartDate = DateTime.UtcNow.AddDays(-30),
    EndDate = DateTime.UtcNow,
    MetricType = "overview"
};

var analytics = await AnalyticsClient.GetAnalyticsAsync(request);

// Get real-time data
var realTime = await AnalyticsClient.GetRealTimeAnalyticsAsync();

// Get funnel analysis
var funnel = await AnalyticsClient.GetFunnelAnalysisAsync(daysBack: 30);

// Generate report
var reportRequest = new GenerateReportRequest
{
    ReportType = "monthly",
    StartDate = DateTime.UtcNow.AddMonths(-1),
    EndDate = DateTime.UtcNow,
    Format = "json"
};

var report = await AnalyticsClient.GenerateReportAsync(reportRequest);
```

## Best Practices

### 1. Fire-and-Forget Tracking

Analytics tracking is designed to be non-blocking:

```csharp
// Analytics calls are automatically fire-and-forget
await AnalyticsClient.TrackEventAsync(...); // Returns immediately
```

### 2. Error Handling

Always wrap analytics in try-catch if needed, but the service handles errors internally:

```csharp
try
{
    await AnalyticsClient.TrackEventAsync(...);
}
catch
{
    // Analytics should never break your app
    // Errors are logged but not thrown
}
```

### 3. Privacy Considerations

- Don't track personally identifiable information (PII) in event properties
- Use anonymized user IDs
- Respect user privacy preferences

```csharp
// Good - anonymized
await AnalyticsClient.TrackEventAsync(
    EventTypes.ProfileCreated,
    EventCategories.Profile,
    new Dictionary<string, object>
    {
        { "major", "Computer Science" },
        { "graduationYear", 2025 }
    }
);

// Bad - includes PII
await AnalyticsClient.TrackEventAsync(
    EventTypes.ProfileCreated,
    EventCategories.Profile,
    new Dictionary<string, object>
    {
        { "email", "Nonprofit@example.com" }, // DON'T DO THIS
        { "name", "John Doe" } // DON'T DO THIS
    }
);
```

### 4. Batch Similar Events

For high-frequency events, consider batching:

```csharp
// Instead of tracking every keystroke
private Timer? _debounceTimer;

private void OnSearchInputChanged(string value)
{
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(async _ =>
    {
        await AnalyticsClient.TrackEventAsync(
            "search_query_typed",
            EventCategories.Search,
            new Dictionary<string, object> { { "length", value.Length } }
        );
    }, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
}
```

### 5. Monitor Performance

Use the analytics dashboard to monitor:
- API response times
- Error rates
- User engagement
- Conversion rates

### 6. Regular Reporting

Set up scheduled reports:

```csharp
// Weekly report generation
var report = await AnalyticsClient.GenerateReportAsync(new GenerateReportRequest
{
    ReportType = "weekly",
    StartDate = DateTime.UtcNow.AddDays(-7),
    EndDate = DateTime.UtcNow,
    Metrics = new List<string>
    {
        "users",
        "conversions",
        "topGrants",
        "funnel"
    }
});
```

## Event Types Reference

### Predefined Event Types

```csharp
// Conversation
EventTypes.ConversationStarted
EventTypes.ConversationMessageSent
EventTypes.ConversationCompleted
EventTypes.ConversationAbandoned

// Profile
EventTypes.ProfileCreated
EventTypes.ProfileUpdated
EventTypes.ProfileViewed

// Search
EventTypes.SearchPerformed
EventTypes.SearchFilterApplied
EventTypes.SearchResultsViewed

// Grant
EventTypes.GrantViewed
EventTypes.GrantSaved
EventTypes.GrantUnsaved
EventTypes.GrantShared
EventTypes.ApplicationLinkClicked

// Page
EventTypes.PageViewed
EventTypes.PageTimeSpent

// Error
EventTypes.ErrorOccurred
EventTypes.ApiCallFailed
```

### Event Categories

```csharp
EventCategories.Conversation
EventCategories.Profile
EventCategories.Search
EventCategories.Grant
EventCategories.Page
EventCategories.Error
```

## API Endpoints

### Track Event
```
POST /api/analytics/track
Authorization: Anonymous (client-side tracking)
Body: TrackEventRequest
```

### Get Analytics
```
POST /api/analytics/query
Authorization: Function (admin only)
Body: GetAnalyticsRequest
```

### Generate Report
```
POST /api/analytics/report
Authorization: Function (admin only)
Body: GenerateReportRequest
```

### Real-Time Analytics
```
GET /api/analytics/realtime
Authorization: Function (admin only)
```

### Funnel Analysis
```
GET /api/analytics/funnel?daysBack=30
Authorization: Function (admin only)
```

### Top Grants
```
GET /api/analytics/Grants/top?limit=10
Authorization: Function (admin only)
```

## Troubleshooting

### Analytics Not Tracking

1. Check Cosmos DB containers exist
2. Verify connection string in configuration
3. Check browser console for JavaScript errors
4. Verify AnalyticsClient is injected correctly

### Dashboard Not Loading

1. Check API endpoint configuration
2. Verify authorization level (Function key required)
3. Check Cosmos DB connection
4. Review Function logs in Application Insights

### Performance Issues

1. Analytics is designed to be non-blocking
2. Increase Cosmos DB throughput if needed
3. Enable distributed caching for metrics
4. Optimize query patterns with proper indexing

## Support

For issues or questions:
1. Check Application Insights logs
2. Review Cosmos DB metrics
3. Enable detailed logging in AnalyticsService
