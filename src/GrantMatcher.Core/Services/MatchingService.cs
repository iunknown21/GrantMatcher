using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.Constants;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using System.Diagnostics;

namespace GrantMatcher.Core.Services;

public class MatchingService : IMatchingService
{
    private readonly IEntityMatchingService _entityMatchingService;
    private readonly IOpenAIService _openAIService;
    private readonly ICachingService? _cachingService;
    private readonly IPerformanceMonitor? _performanceMonitor;

    public MatchingService(
        IEntityMatchingService entityMatchingService,
        IOpenAIService openAIService,
        ICachingService? cachingService = null,
        IPerformanceMonitor? performanceMonitor = null)
    {
        _entityMatchingService = entityMatchingService ?? throw new ArgumentNullException(nameof(entityMatchingService));
        _openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
        _cachingService = cachingService;
        _performanceMonitor = performanceMonitor;
    }

    public async Task<SearchResponse> FindGrantsAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        // Use performance monitor if available
        if (_performanceMonitor != null)
        {
            return await _performanceMonitor.TrackAsync(
                "FindGrants",
                async () => await FindGrantsInternalAsync(request, cancellationToken),
                TimeSpan.FromSeconds(3),
                new Dictionary<string, object>
                {
                    ["Query"] = request.Query ?? "null",
                    ["Limit"] = request.Limit,
                    ["MinSimilarity"] = request.MinSimilarity
                },
                cancellationToken);
        }

        return await FindGrantsInternalAsync(request, cancellationToken);
    }

    private async Task<SearchResponse> FindGrantsInternalAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Try to get from cache first
        if (_cachingService != null && !string.IsNullOrEmpty(request.Query))
        {
            var cacheKey = CacheKeys.GrantSearch(request.Query, request.Limit, (decimal)request.MinSimilarity);
            var cachedResult = await _cachingService.GetAsync<SearchResponse>(cacheKey, cancellationToken);
            if (cachedResult != null)
            {
                cachedResult.Metadata.FromCache = true;
                return cachedResult;
            }
        }

        // Get Nonprofit profile (assumed to be passed or retrieved)
        // For now, we'll assume the Nonprofit profile is available in context
        // In real implementation, retrieve from database using request.NonprofitId

        // Build vector search request
        var vectorSearchRequest = new VectorSearchRequest
        {
            Query = request.Query,  // Will use Nonprofit profile summary if null
            MinSimilarity = request.MinSimilarity,
            Limit = request.Limit
        };

        // Add additional filters from search request
        if (vectorSearchRequest.AttributeFilters == null)
        {
            vectorSearchRequest.AttributeFilters = new FilterGroup
            {
                LogicalOperator = "And",
                Filters = new List<AttributeFilter>()
            };
        }

        // Add award amount filters
        if (request.MinAwardAmount.HasValue)
        {
            vectorSearchRequest.AttributeFilters.Filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.awardAmount",
                Operator = "GreaterThanOrEqual",
                Value = request.MinAwardAmount.Value
            });
        }

        if (request.MaxAwardAmount.HasValue)
        {
            vectorSearchRequest.AttributeFilters.Filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.awardAmount",
                Operator = "LessThanOrEqual",
                Value = request.MaxAwardAmount.Value
            });
        }

        // Add deadline filters
        if (request.DeadlineAfter.HasValue)
        {
            vectorSearchRequest.AttributeFilters.Filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.deadline",
                Operator = "GreaterThanOrEqual",
                Value = request.DeadlineAfter.Value
            });
        }

        if (request.DeadlineBefore.HasValue)
        {
            vectorSearchRequest.AttributeFilters.Filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.deadline",
                Operator = "LessThanOrEqual",
                Value = request.DeadlineBefore.Value
            });
        }

        // Essay requirement filter
        if (request.RequiresEssay.HasValue)
        {
            vectorSearchRequest.AttributeFilters.Filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.requiresEssay",
                Operator = "Equal",
                Value = request.RequiresEssay.Value
            });
        }

        // NOTE: In real implementation, retrieve Nonprofit profile from database
        // For now, this is a placeholder showing the pattern
        var NonprofitProfile = new NonprofitProfile(); // TODO: Retrieve from DB

        // Perform vector search
        var searchResults = await _entityMatchingService.SearchGrantsAsync(
            NonprofitProfile,
            vectorSearchRequest,
            cancellationToken);

        // Convert results to MatchResults and calculate composite scores
        var matches = new List<MatchResult>();
        foreach (var result in searchResults.Results)
        {
            // Convert entity attributes back to GrantEntity
            var Grant = ConvertToGrant(result.Profile);

            var matchResult = CalculateMatchScore(NonprofitProfile, Grant, result.Similarity);
            matches.Add(matchResult);
        }

        // Sort by composite score (descending)
        matches = matches.OrderByDescending(m => m.CompositeScore).ToList();

        stopwatch.Stop();

        var response = new SearchResponse
        {
            Matches = matches.Skip(request.Offset).Take(request.Limit).ToList(),
            TotalCount = matches.Count,
            Metadata = new SearchMetadata
            {
                ProcessingTime = stopwatch.Elapsed,
                FilteredGrants = searchResults.TotalResults,
                EligibleGrants = matches.Count(m => m.MeetsAllRequirements),
                SearchStrategy = "Hybrid (Filters + Vector Similarity)",
                FromCache = false
            }
        };

        // Cache the result
        if (_cachingService != null && !string.IsNullOrEmpty(request.Query))
        {
            var cacheKey = CacheKeys.GrantSearch(request.Query, request.Limit, (decimal)request.MinSimilarity);
            await _cachingService.SetAsync(
                cacheKey,
                response,
                absoluteExpiration: TimeSpan.FromMinutes(15), // Search results cached for 15 minutes
                slidingExpiration: TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        return response;
    }

    public MatchResult CalculateMatchScore(NonprofitProfile Nonprofit, GrantEntity Grant, double semanticSimilarity)
    {
        var (meetsAll, unmetRequirements) = CheckEligibility(Nonprofit, Grant);

        // Calculate component scores
        var semanticScore = semanticSimilarity * AppConstants.MatchingWeights.SemanticWeight;

        // Mission alignment score (based on funding category overlap)
        var missionAlignmentScore = CalculateMissionAlignment(Nonprofit, Grant) * AppConstants.MatchingWeights.MissionAlignmentWeight;

        // Award amount score (normalized to 0-1 based on $500k max for grants)
        var awardAmount = Grant.AwardCeiling ?? Grant.AwardFloor ?? 0;
        var normalizedAward = Math.Min((double)awardAmount / 500000.0, 1.0);
        var awardScore = normalizedAward * AppConstants.MatchingWeights.AwardAmountWeight;

        // Deadline proximity score (sooner = lower score, to avoid overwhelming with urgent deadlines)
        var daysUntilDeadline = Grant.CloseDate.HasValue
            ? (Grant.CloseDate.Value - DateTime.UtcNow).TotalDays
            : 365;
        var deadlineFactor = daysUntilDeadline < 30 ? 0.5 : 1.0;
        var deadlineScore = deadlineFactor * AppConstants.MatchingWeights.DeadlineWeight;

        // Composite score
        var compositeScore = semanticScore + missionAlignmentScore + awardScore + deadlineScore;

        return new MatchResult
        {
            GrantId = Grant.id,
            Grant = Grant,
            SemanticSimilarity = semanticSimilarity,
            CompositeScore = compositeScore,
            Breakdown = new ScoreBreakdown
            {
                SemanticScore = semanticScore,
                MissionAlignmentScore = missionAlignmentScore,
                AwardAmountScore = awardScore,
                DeadlineProximityScore = deadlineScore
            },
            MeetsAllRequirements = meetsAll,
            UnmetRequirements = unmetRequirements,
            MatchedAt = DateTime.UtcNow
        };
    }

    private double CalculateMissionAlignment(NonprofitProfile nonprofit, GrantEntity grant)
    {
        if (!nonprofit.FundingCategories.Any() || !grant.FundingCategories.Any())
        {
            return 0.5; // Neutral score if no categories specified
        }

        // Calculate overlap between nonprofit's focus and grant's funding categories
        var overlap = nonprofit.FundingCategories
            .Intersect(grant.FundingCategories, StringComparer.OrdinalIgnoreCase)
            .Count();

        var maxCategories = Math.Max(nonprofit.FundingCategories.Count, grant.FundingCategories.Count);
        return (double)overlap / maxCategories;
    }

    public (bool meetsAll, List<string> unmetRequirements) CheckEligibility(NonprofitProfile Nonprofit, GrantEntity Grant)
    {
        var unmet = new List<string>();

        // Check applicant type eligibility
        if (Grant.ApplicantTypes.Any() && Nonprofit.ApplicantTypes.Any())
        {
            var hasMatchingType = Nonprofit.ApplicantTypes
                .Any(nonprofitType => Grant.ApplicantTypes.Contains(nonprofitType, StringComparer.OrdinalIgnoreCase));

            if (!hasMatchingType)
            {
                unmet.Add($"Organization type must be one of: {string.Join(", ", Grant.ApplicantTypes)}");
            }
        }

        // Check geographic eligibility (state)
        if (Grant.EligibleStates.Any() && !Grant.EligibleStates.Contains(Nonprofit.State, StringComparer.OrdinalIgnoreCase))
        {
            unmet.Add($"Organization must be located in: {string.Join(", ", Grant.EligibleStates)}");
        }

        // Check funding category alignment
        if (Grant.FundingCategories.Any() && Nonprofit.FundingCategories.Any())
        {
            var hasMatchingCategory = Nonprofit.FundingCategories
                .Any(category => Grant.FundingCategories.Contains(category, StringComparer.OrdinalIgnoreCase));

            if (!hasMatchingCategory)
            {
                unmet.Add($"Funding focus must align with: {string.Join(", ", Grant.FundingCategories)}");
            }
        }

        // Check budget capacity (organization can handle award size)
        if (Grant.AwardFloor.HasValue && Nonprofit.TypicalProjectBudget.HasValue)
        {
            if (Nonprofit.TypicalProjectBudget.Value < Grant.AwardFloor.Value * 0.5m)
            {
                unmet.Add($"Typical project budget may be too small for this grant (minimum award: ${Grant.AwardFloor.Value:N0})");
            }
        }

        if (Grant.AwardCeiling.HasValue && Nonprofit.AnnualBudget > 0)
        {
            // Flag if award is more than 3x annual budget (capacity concern)
            if (Grant.AwardCeiling.Value > Nonprofit.AnnualBudget * 3)
            {
                unmet.Add($"Grant size may exceed organizational capacity (award up to ${Grant.AwardCeiling.Value:N0})");
            }
        }

        // Check deadline hasn't passed
        if (Grant.CloseDate.HasValue && Grant.CloseDate.Value < DateTime.UtcNow)
        {
            unmet.Add($"Deadline has passed ({Grant.CloseDate.Value:MMM dd, yyyy})");
        }

        return (unmet.Count == 0, unmet);
    }

    private GrantEntity ConvertToGrant(EntityResponse? entity)
    {
        if (entity == null)
            return new GrantEntity();

        var Grant = new GrantEntity
        {
            id = entity.Attributes.GetValueOrDefault("grantId")?.ToString() ?? Guid.NewGuid().ToString(),
            Name = entity.Name,
            Description = entity.Description ?? string.Empty,
            NaturalLanguageSummary = entity.Description ?? string.Empty,
            EntityId = entity.Id
        };

        // Extract grant-specific attributes
        if (entity.Attributes.TryGetValue("opportunityNumber", out var oppNum))
            Grant.OpportunityNumber = oppNum.ToString() ?? string.Empty;

        if (entity.Attributes.TryGetValue("agency", out var agency))
            Grant.Agency = agency.ToString() ?? string.Empty;

        if (entity.Attributes.TryGetValue("agencyCode", out var agencyCode))
            Grant.AgencyCode = agencyCode.ToString() ?? string.Empty;

        if (entity.Attributes.TryGetValue("awardCeiling", out var ceiling))
            Grant.AwardCeiling = Convert.ToDecimal(ceiling);

        if (entity.Attributes.TryGetValue("awardFloor", out var floor))
            Grant.AwardFloor = Convert.ToDecimal(floor);

        if (entity.Attributes.TryGetValue("closeDate", out var closeDate))
            Grant.CloseDate = DateTime.Parse(closeDate.ToString() ?? DateTime.UtcNow.ToString());

        if (entity.Attributes.TryGetValue("postDate", out var postDate))
            Grant.PostDate = DateTime.Parse(postDate.ToString() ?? DateTime.UtcNow.ToString());

        if (entity.Attributes.TryGetValue("fundingInstrument", out var instrument))
            Grant.FundingInstrument = instrument.ToString() ?? string.Empty;

        if (entity.Attributes.TryGetValue("cfdaNumber", out var cfda))
            Grant.CFDANumber = cfda.ToString() ?? string.Empty;

        if (entity.Attributes.TryGetValue("applicationUrl", out var url))
            Grant.ApplicationUrl = url.ToString() ?? string.Empty;

        // TODO: Extract list attributes (applicant types, funding categories, eligible states, etc.)

        return Grant;
    }
}
