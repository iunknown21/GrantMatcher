# GrantMatcher Setup Guide

## Phase 1: Clone & Initial Setup

### 1. Create New Repository
```bash
# In D:\Development\Main\
cd ..
git clone D:\Development\Main\Grants GrantMatcher
cd GrantMatcher

# Initialize new git repo
rm -rf .git
git init
git remote add origin <your-grant-matcher-repo-url>
```

### 2. Global Rename: GrantMatcher → GrantMatcher
```bash
# PowerShell script to rename all references
Get-ChildItem -Recurse -File | Where-Object {
    $_.Extension -in '.cs','.razor','.csproj','.json','.md'
} | ForEach-Object {
    (Get-Content $_.FullName) -replace 'GrantMatcher','GrantMatcher' |
    Set-Content $_.FullName
}

# Rename folders
Get-ChildItem -Recurse -Directory -Filter "*GrantMatcher*" |
    ForEach-Object { Rename-Item $_.FullName $_.Name.Replace('GrantMatcher','GrantMatcher') }

# Rename files
Get-ChildItem -Recurse -File -Filter "*Grant*" |
    ForEach-Object { Rename-Item $_.FullName $_.Name.Replace('Grant','Grant') }
```

### 3. Update Project Structure
**Before:**
```
GrantMatcher/
├── GrantMatcher.Client/
├── GrantMatcher.Core/
├── GrantMatcher.Functions/
└── GrantMatcher.Shared/
```

**After:**
```
GrantMatcher/
├── GrantMatcher.Client/
├── GrantMatcher.Core/
├── GrantMatcher.Functions/
└── GrantMatcher.Shared/
```

### 4. Delete CareerOneStop Integration
Remove these files (they're Grant-specific and API doesn't exist):
```bash
rm src/GrantMatcher.Core/Services/CareerOneStopService.cs
rm src/GrantMatcher.Core/Interfaces/ICareerOneStopService.cs
rm src/GrantMatcher.Functions/Functions/CareerOneStopFunctions.cs
rm src/GrantMatcher.Shared/DTOs/CareerOneStopDTOs.cs
rm src/GrantMatcher.Client/Pages/Browse.razor
rm src/GrantMatcher.Client/Components/Shared/PublicGrantCard.razor
rm CAREERONESTOP_INTEGRATION.md
```

---

## Phase 2: Data Model Changes (3 hours)

### 1. Replace NonprofitProfile with NonprofitProfile
**File:** `src/GrantMatcher.Shared/Models/NonprofitProfile.cs`

```csharp
namespace GrantMatcher.Shared.Models;

public class NonprofitProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Organization Info
    public string OrganizationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EIN { get; set; } = string.Empty; // Tax ID
    public string OrganizationType { get; set; } = string.Empty; // 501c3, University, State/Local Gov, etc.

    // Location
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public List<string> ServiceAreas { get; set; } = new(); // States/regions served

    // Financial
    public decimal? AnnualBudget { get; set; }
    public string BudgetRange { get; set; } = string.Empty; // <$100k, $100k-$500k, etc.

    // Mission & Focus
    public List<string> FundingCategories { get; set; } = new(); // Education, Health, Environment, etc.
    public List<string> PopulationsServed { get; set; } = new(); // Youth, Seniors, Veterans, etc.
    public string MissionStatement { get; set; } = string.Empty;

    // Grant History
    public bool HasReceivedFederalGrants { get; set; }
    public List<string> PastGrantAgencies { get; set; } = new(); // DOE, NSF, NEA, etc.

    // Project Info
    public List<string> ProjectTypes { get; set; } = new(); // Research, Program, Capital, Operating
    public decimal? TypicalProjectBudget { get; set; }

    // Generated fields
    public string? ProfileSummary { get; set; }  // Natural language summary for embedding
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }

    // Entity matching
    public string? EntityId { get; set; }
}
```

### 2. Replace GrantEntity with GrantEntity
**File:** `src/GrantMatcher.Shared/Models/GrantEntity.cs`

```csharp
namespace GrantMatcher.Shared.Models;

public class GrantEntity
{
    public Guid Id { get; set; }
    public string OpportunityNumber { get; set; } = string.Empty; // e.g., "ED-GRANTS-012025-001"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty; // Department of Education, NSF, etc.

    // Eligibility Criteria
    public List<string> ApplicantTypes { get; set; } = new(); // nonprofits, universities, state/local gov, etc.
    public List<string> FundingCategories { get; set; } = new(); // education, health, environment, etc.
    public List<string> EligibleStates { get; set; } = new(); // Geographic restrictions

    // Award Details
    public decimal? AwardCeiling { get; set; } // Maximum award amount
    public decimal? AwardFloor { get; set; }   // Minimum award amount
    public decimal? EstimatedTotalFunding { get; set; }
    public int? ExpectedAwards { get; set; }
    public string FundingInstrument { get; set; } = string.Empty; // grant, cooperative agreement, etc.

    // Dates
    public DateTime CloseDate { get; set; }
    public DateTime? PostDate { get; set; }
    public DateTime? ArchiveDate { get; set; }

    // Additional Info
    public string AgencyContactEmail { get; set; } = string.Empty;
    public string GrantsGovUrl { get; set; } = string.Empty;
    public string OpportunityStatus { get; set; } = string.Empty; // posted, forecasted, closed, archived

    // Applicant eligibility details
    public string ApplicantEligibilitySummary { get; set; } = string.Empty;

    // For vector search
    public string NaturalLanguageSummary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Entity matching
    public string? EntityId { get; set; }
}
```

### 3. Update MatchResult
**File:** `src/GrantMatcher.Shared/Models/MatchResult.cs`

```csharp
namespace GrantMatcher.Shared.Models;

public class MatchResult
{
    public Guid GrantId { get; set; }
    public GrantEntity Grant { get; set; } = null!;

    // Matching scores
    public double SemanticSimilarity { get; set; }
    public double CompositeScore { get; set; }

    // Score breakdown
    public ScoreBreakdown Breakdown { get; set; } = new();

    // Eligibility
    public bool MeetsAllRequirements { get; set; }
    public List<string> UnmetRequirements { get; set; } = new();

    public DateTime MatchedAt { get; set; }
}

public class ScoreBreakdown
{
    public double SemanticScore { get; set; }           // 60%
    public double FundingAmountScore { get; set; }      // 20%
    public double MissionAlignmentScore { get; set; }   // 10%
    public double DeadlineProximityScore { get; set; }  // 10%
}
```

---

## Phase 3: Service Layer Updates (4 hours)

### 1. Create SimplerGrantsService
**File:** `src/GrantMatcher.Core/Services/SimplerGrantsService.cs`

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Services;

public class SimplerGrantsService : IOpportunityDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SimplerGrantsService>? _logger;
    private const string BaseUrl = "https://api.simpler.grants.gov";

    public SimplerGrantsService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SimplerGrantsService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);

        // API key from configuration
        var apiKey = configuration["SimplerGrants:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    public async Task<List<GrantEntity>> SearchGrantsAsync(
        string? keyword = null,
        List<string>? fundingCategories = null,
        List<string>? agencies = null,
        List<string>? applicantTypes = null,
        int limit = 50,
        int offset = 0)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(keyword))
            queryParams.Add($"keyword={Uri.EscapeDataString(keyword)}");

        if (fundingCategories?.Any() == true)
            queryParams.Add($"funding_categories={string.Join(",", fundingCategories.Select(Uri.EscapeDataString))}");

        if (agencies?.Any() == true)
            queryParams.Add($"agencies={string.Join(",", agencies.Select(Uri.EscapeDataString))}");

        if (applicantTypes?.Any() == true)
            queryParams.Add($"applicant_types={string.Join(",", applicantTypes.Select(Uri.EscapeDataString))}");

        queryParams.Add($"limit={limit}");
        queryParams.Add($"offset={offset}");

        var query = string.Join("&", queryParams);
        var endpoint = $"/opportunities?{query}";

        _logger?.LogInformation("Searching Simpler.Grants.gov: {Endpoint}", endpoint);

        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>();

        return apiResponse?.Data?.Select(MapToGrantEntity).ToList() ?? new List<GrantEntity>();
    }

    public async Task<GrantEntity?> GetGrantByIdAsync(int opportunityId)
    {
        var endpoint = $"/opportunities/{opportunityId}";

        var response = await _httpClient.GetAsync(endpoint);

        if (!response.IsSuccessStatusCode)
            return null;

        var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsOpportunity>();

        return apiResponse != null ? MapToGrantEntity(apiResponse) : null;
    }

    private GrantEntity MapToGrantEntity(SimplerGrantsOpportunity source)
    {
        return new GrantEntity
        {
            Id = Guid.NewGuid(),
            OpportunityNumber = source.OpportunityNumber ?? "",
            Title = source.OpportunityTitle ?? "",
            Description = source.Description ?? "",
            Agency = source.Agency ?? "",
            ApplicantTypes = source.ApplicantTypes ?? new List<string>(),
            FundingCategories = source.FundingCategories ?? new List<string>(),
            AwardCeiling = source.AwardCeiling,
            AwardFloor = source.AwardFloor,
            EstimatedTotalFunding = source.EstimatedTotalFunding,
            ExpectedAwards = source.ExpectedAwards,
            FundingInstrument = source.FundingInstrument ?? "",
            CloseDate = DateTime.TryParse(source.CloseDate, out var closeDate)
                ? closeDate
                : DateTime.UtcNow.AddMonths(3),
            PostDate = DateTime.TryParse(source.PostDate, out var postDate) ? postDate : null,
            AgencyContactEmail = source.AgencyContactEmail ?? "",
            GrantsGovUrl = $"https://www.grants.gov/search-grants?oppNum={source.OpportunityNumber}",
            OpportunityStatus = source.OpportunityStatus ?? "posted",
            ApplicantEligibilitySummary = source.Summary?.ApplicantEligibility ?? "",
            NaturalLanguageSummary = BuildNaturalLanguageSummary(source),
            CreatedAt = DateTime.UtcNow
        };
    }

    private string BuildNaturalLanguageSummary(SimplerGrantsOpportunity source)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(source.OpportunityTitle))
            parts.Add(source.OpportunityTitle);

        if (!string.IsNullOrEmpty(source.Description))
            parts.Add(source.Description);

        if (source.FundingCategories?.Any() == true)
            parts.Add($"Funding categories: {string.Join(", ", source.FundingCategories)}");

        if (source.ApplicantTypes?.Any() == true)
            parts.Add($"Eligible applicants: {string.Join(", ", source.ApplicantTypes)}");

        if (source.AwardCeiling.HasValue || source.AwardFloor.HasValue)
        {
            var range = source.AwardFloor.HasValue && source.AwardCeiling.HasValue
                ? $"${source.AwardFloor:N0} - ${source.AwardCeiling:N0}"
                : source.AwardCeiling.HasValue
                    ? $"Up to ${source.AwardCeiling:N0}"
                    : $"Minimum ${source.AwardFloor:N0}";
            parts.Add($"Award range: {range}");
        }

        return string.Join(". ", parts);
    }
}

// DTOs for Simpler.Grants.gov API
public class SimplerGrantsResponse
{
    public List<SimplerGrantsOpportunity>? Data { get; set; }
    public int TotalResults { get; set; }
}

public class SimplerGrantsOpportunity
{
    public int OpportunityId { get; set; }
    public string? OpportunityNumber { get; set; }
    public string? OpportunityTitle { get; set; }
    public string? Agency { get; set; }
    public string? OpportunityStatus { get; set; }
    public string? FundingInstrument { get; set; }
    public decimal? AwardCeiling { get; set; }
    public decimal? AwardFloor { get; set; }
    public decimal? EstimatedTotalFunding { get; set; }
    public int? ExpectedAwards { get; set; }
    public List<string>? ApplicantTypes { get; set; }
    public List<string>? FundingCategories { get; set; }
    public string? CloseDate { get; set; }
    public string? PostDate { get; set; }
    public string? Description { get; set; }
    public string? AgencyContactEmail { get; set; }
    public SimplerGrantsSummary? Summary { get; set; }
}

public class SimplerGrantsSummary
{
    public string? ApplicantEligibility { get; set; }
}
```

### 2. Update MatchingService
Replace eligibility checking logic to match nonprofit profiles against grant requirements:

```csharp
public (bool meetsAll, List<string> unmetRequirements) CheckEligibility(
    NonprofitProfile nonprofit,
    GrantEntity grant)
{
    var unmet = new List<string>();

    // Check applicant type
    if (grant.ApplicantTypes.Any() &&
        !grant.ApplicantTypes.Contains(nonprofit.OrganizationType, StringComparer.OrdinalIgnoreCase))
    {
        unmet.Add($"Applicant type must be: {string.Join(", ", grant.ApplicantTypes)}");
    }

    // Check geographic eligibility
    if (grant.EligibleStates.Any() &&
        !grant.EligibleStates.Contains(nonprofit.State, StringComparer.OrdinalIgnoreCase))
    {
        unmet.Add($"Must be located in: {string.Join(", ", grant.EligibleStates)}");
    }

    // Check funding category alignment
    if (grant.FundingCategories.Any())
    {
        var hasMatch = grant.FundingCategories.Any(gc =>
            nonprofit.FundingCategories.Contains(gc, StringComparer.OrdinalIgnoreCase));

        if (!hasMatch)
        {
            unmet.Add($"Organization must focus on: {string.Join(", ", grant.FundingCategories)}");
        }
    }

    // Check budget capacity (if grant has minimum award)
    if (grant.AwardFloor.HasValue && nonprofit.AnnualBudget.HasValue)
    {
        // Rough heuristic: org budget should be at least 3x the grant floor
        if (nonprofit.AnnualBudget.Value < grant.AwardFloor.Value * 3)
        {
            unmet.Add($"Minimum award of ${grant.AwardFloor:N0} may require larger organizational capacity");
        }
    }

    return (unmet.Count == 0, unmet);
}
```

---

## Phase 4: UI Updates (6 hours)

### 1. Update Component Names
- `GrantCard.razor` → `GrantCard.razor`
- `GrantDetail.razor` → `GrantDetail.razor`
- `SavedGrants.razor` → `SavedGrants.razor`

### 2. Redesign GrantCard Component
**File:** `src/GrantMatcher.Client/Components/Shared/GrantCard.razor`

```razor
@using GrantMatcher.Shared.Models

<div class="card hover:shadow-xl transition-all duration-300 hover:scale-[1.02]">
    <div class="flex items-start justify-between mb-2">
        <div class="flex-1">
            <h3 class="text-lg font-semibold text-gray-900 hover:text-primary-600">
                <a href="@($"/grant/{Match.GrantId}")">
                    @Match.Grant.Title
                </a>
            </h3>
            <p class="text-sm text-gray-600">@Match.Grant.Agency</p>
            <span class="inline-block px-2 py-1 text-xs font-medium rounded-full bg-blue-100 text-blue-800">
                @Match.Grant.OpportunityNumber
            </span>
        </div>
        <MatchScoreBadge Score="@((int)(Match.SemanticSimilarity * 100))" />
    </div>

    <p class="text-sm text-gray-700 mb-4 line-clamp-2">
        @Match.Grant.Description
    </p>

    <div class="flex flex-wrap gap-4 mb-4 text-sm">
        <!-- Award Range -->
        <div class="flex items-center text-green-700">
            <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>
            <span class="font-semibold">
                @if (Match.Grant.AwardFloor.HasValue && Match.Grant.AwardCeiling.HasValue)
                {
                    <text>@Match.Grant.AwardFloor.Value.ToString("C0") - @Match.Grant.AwardCeiling.Value.ToString("C0")</text>
                }
                else if (Match.Grant.AwardCeiling.HasValue)
                {
                    <text>Up to @Match.Grant.AwardCeiling.Value.ToString("C0")</text>
                }
                else
                {
                    <text>Amount varies</text>
                }
            </span>
        </div>

        <!-- Close Date -->
        <div class="flex items-center @GetDeadlineClass()">
            <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"></path>
            </svg>
            <span>@GetDeadlineText()</span>
        </div>

        <!-- Expected Awards -->
        @if (Match.Grant.ExpectedAwards.HasValue)
        {
            <div class="flex items-center text-blue-700">
                <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"></path>
                </svg>
                <span>~@Match.Grant.ExpectedAwards awards</span>
            </div>
        }
    </div>

    <!-- Funding Categories -->
    <div class="flex flex-wrap gap-2 mb-4">
        @foreach (var category in Match.Grant.FundingCategories.Take(3))
        {
            <span class="text-xs px-2 py-1 bg-purple-100 text-purple-800 rounded-full">@category</span>
        }
        @if (Match.Grant.FundingCategories.Count > 3)
        {
            <span class="text-xs px-2 py-1 bg-gray-100 text-gray-600 rounded-full">+@(Match.Grant.FundingCategories.Count - 3) more</span>
        }
    </div>

    <!-- Applicant Types -->
    <div class="flex flex-wrap gap-2 mb-4">
        @foreach (var applicantType in Match.Grant.ApplicantTypes.Take(3))
        {
            <span class="text-xs px-2 py-1 bg-blue-100 text-blue-800 rounded-full">@applicantType</span>
        }
    </div>

    <!-- Eligibility Status -->
    @if (Match.MeetsAllRequirements)
    {
        <div class="flex items-center text-green-700 text-sm mb-3">
            <svg class="w-5 h-5 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>
            <span class="font-medium">Your organization is eligible</span>
        </div>
    }
    else if (Match.UnmetRequirements.Any())
    {
        <div class="mb-3">
            <button @onclick="ToggleRequirements" class="flex items-center text-sm text-amber-700 hover:text-amber-800">
                <svg class="w-5 h-5 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
                </svg>
                <span>@Match.UnmetRequirements.Count eligibility concern@(Match.UnmetRequirements.Count > 1 ? "s" : "")</span>
            </button>

            @if (showRequirements)
            {
                <ul class="mt-2 text-sm text-amber-700 space-y-1 ml-6 list-disc">
                    @foreach (var requirement in Match.UnmetRequirements)
                    {
                        <li>@requirement</li>
                    }
                </ul>
            }
        </div>
    }

    <!-- Actions -->
    <div class="flex gap-3">
        <a href="@($"/grant/{Match.GrantId}")" class="btn-primary flex-1 text-center">
            View Details
        </a>
        <a href="@Match.Grant.GrantsGovUrl" target="_blank" class="btn-secondary flex-1 text-center">
            Apply on Grants.gov
        </a>
    </div>
</div>

@code {
    [Parameter]
    public MatchResult Match { get; set; } = null!;

    private bool showRequirements = false;

    private void ToggleRequirements() => showRequirements = !showRequirements;

    private string GetDeadlineClass()
    {
        var daysUntil = (Match.Grant.CloseDate - DateTime.UtcNow).TotalDays;
        return daysUntil < 7 ? "text-red-700" : daysUntil < 30 ? "text-orange-700" : "text-gray-700";
    }

    private string GetDeadlineText()
    {
        var daysUntil = (int)(Match.Grant.CloseDate - DateTime.UtcNow).TotalDays;
        return daysUntil < 0 ? "Closed" :
               daysUntil == 0 ? "Closes today!" :
               daysUntil < 7 ? $"Closes in {daysUntil} days" :
               daysUntil < 30 ? $"Closes in {daysUntil / 7} weeks" :
               Match.Grant.CloseDate.ToString("MMM dd, yyyy");
    }
}
```

### 3. Update Profile Wizard
Replace Nonprofit profile steps with nonprofit profile steps:

- `StepBasicInfo.razor` → Organization name, EIN, type, location
- `StepMission.razor` → Mission statement, funding categories, populations served
- `StepFinancial.razor` → Annual budget, typical project budget
- `StepHistory.razor` → Past grants, project types
- `StepReview.razor` → Summary and confirmation

---

## Phase 5: Configuration (30 minutes)

### Update local.settings.json
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "SimplerGrants": {
      "BaseUrl": "https://api.simpler.grants.gov",
      "ApiKey": "YOUR_API_KEY_HERE"
    },

    "EntityMatchingAI": {
      "BaseUrl": "https://your-entitymatching-instance.azurewebsites.net",
      "ApiKey": "your-key-here"
    },

    "OpenAI": {
      "ApiKey": "your-openai-key",
      "EmbeddingModel": "text-embedding-3-small"
    }
  }
}
```

### Update AppConstants.cs
```csharp
public static class AppConstants
{
    public static class EntityTypes
    {
        public const int Nonprofit = 1;  // Person/Organization
        public const int Grant = 3;      // Product/Service
    }

    public static class MatchingWeights
    {
        public const double SemanticWeight = 0.6;
        public const double FundingAmountWeight = 0.2;
        public const double MissionAlignmentWeight = 0.1;
        public const double DeadlineWeight = 0.1;
    }

    public static class SimplerGrants
    {
        public const string BaseUrl = "https://api.simpler.grants.gov";
        public const string AttributionText = "Grant data provided by U.S. Department of Health & Human Services via Simpler.Grants.gov";
    }
}
```

---

## Phase 6: Testing Checklist

### 1. API Integration Test
```bash
# Get Simpler.Grants.gov API key
# 1. Go to: https://simpler.grants.gov/developer
# 2. Sign in with Login.gov
# 3. Generate API key
# 4. Add to local.settings.json

# Test API directly
curl -H "X-API-Key: YOUR_KEY" \
  "https://api.simpler.grants.gov/opportunities?keyword=education&limit=10"
```

### 2. Service Layer Tests
- Test `SimplerGrantsService.SearchGrantsAsync()`
- Test `MatchingService.CheckEligibility()` with nonprofit profile
- Test Entity Matching API integration

### 3. UI Tests
- Browse grants page loads
- Filters work (funding category, agency, applicant type)
- Grant cards display correctly
- Match scores calculate properly
- Profile wizard collects nonprofit data

---

## Phase 7: Deployment Differences

### Pricing Update
**Grants:** $15/month (Nonprofits)
**Grants:** $99/month (organizations)

### Marketing Copy
**Grants:** "Find Grants that match YOUR profile"
**Grants:** "Discover federal grants aligned with YOUR mission"

### User Onboarding
**Grants:** Nonprofit email (.edu verification optional)
**Grants:** Require EIN verification for trust/compliance

---

## Timeline Summary
- **Phase 1:** Repository setup - 1 hour
- **Phase 2:** Data models - 3 hours
- **Phase 3:** Services - 4 hours
- **Phase 4:** UI - 6 hours
- **Phase 5:** Configuration - 30 minutes
- **Phase 6:** Testing - 2 hours

**Total: ~17 hours (~2 days)**

---

## Long-Term: Shared Library Extraction (Future)

If you later want to add Fellowships, Residencies, etc., extract shared code:

```
OpportunityMatcher.Shared/
├── EntityMatchingService.cs
├── OpenAIService.cs
├── CachingService.cs
├── PerformanceMonitor.cs
└── AnalyticsService.cs

Then:
- GrantMatcher references OpportunityMatcher.Shared
- GrantMatcher references OpportunityMatcher.Shared
- FellowshipMatcher references OpportunityMatcher.Shared
```

This hybrid approach gives you both benefits:
- Fast initial development (clone & modify)
- Future scalability (extract shared library when needed)
