using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Core.Services;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using Xunit;

namespace GrantMatcher.Core.Tests.Services;

public class MatchingServiceTests
{
    private readonly Mock<IEntityMatchingService> _entityMatchingServiceMock;
    private readonly Mock<IOpenAIService> _openAIServiceMock;
    private readonly MatchingService _matchingService;

    public MatchingServiceTests()
    {
        _entityMatchingServiceMock = new Mock<IEntityMatchingService>();
        _openAIServiceMock = new Mock<IOpenAIService>();
        _matchingService = new MatchingService(_entityMatchingServiceMock.Object, _openAIServiceMock.Object);
    }

    [Fact]
    public void Constructor_WithNullEntityMatchingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MatchingService(null!, _openAIServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullOpenAIService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MatchingService(_entityMatchingServiceMock.Object, null!));
    }

    #region CheckEligibility Tests

    [Fact]
    public void CheckEligibility_WhenAllRequirementsMet_ReturnsTrueWithNoUnmet()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GPA = 3.8m,
            Major = "Computer Science",
            State = "California",
            Ethnicity = "Asian",
            Gender = "Female",
            FirstGeneration = true,
            GraduationYear = 2026
        };

        var Grant = new GrantEntity
        {
            MinGPA = 3.5m,
            MaxGPA = 4.0m,
            EligibleMajors = new List<string> { "Computer Science", "Engineering" },
            RequiredStates = new List<string> { "California", "Oregon" },
            RequiredEthnicities = new List<string> { "Asian", "Hispanic" },
            RequiredGenders = new List<string> { "Female" },
            FirstGenerationRequired = true,
            MinGraduationYear = 2025,
            MaxGraduationYear = 2027
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.True(meetsAll);
        Assert.Empty(unmetRequirements);
    }

    [Fact]
    public void CheckEligibility_WhenGPATooLow_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 3.0m, Major = "Computer Science" };
        var Grant = new GrantEntity { MinGPA = 3.5m };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Minimum GPA", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenGPATooHigh_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 4.0m, Major = "Computer Science" };
        var Grant = new GrantEntity { MaxGPA = 3.5m };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Maximum GPA", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenMajorNotEligible_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { Major = "Art History", GPA = 3.5m };
        var Grant = new GrantEntity
        {
            EligibleMajors = new List<string> { "Computer Science", "Engineering" }
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Major must be one of", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenStateNotRequired_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { State = "Texas", Major = "Computer Science", GPA = 3.5m };
        var Grant = new GrantEntity
        {
            RequiredStates = new List<string> { "California", "Oregon" }
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Must be resident of", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenEthnicityNotMatch_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            Ethnicity = "Caucasian",
            Major = "Computer Science",
            GPA = 3.5m
        };
        var Grant = new GrantEntity
        {
            RequiredEthnicities = new List<string> { "Hispanic", "African American" }
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Ethnicity must be", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenGenderNotMatch_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            Gender = "Male",
            Major = "Computer Science",
            GPA = 3.5m
        };
        var Grant = new GrantEntity
        {
            RequiredGenders = new List<string> { "Female", "Non-binary" }
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Gender must be", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenFirstGenerationRequired_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            FirstGeneration = false,
            Major = "Computer Science",
            GPA = 3.5m
        };
        var Grant = new GrantEntity
        {
            FirstGenerationRequired = true
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("first-generation", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenGraduationYearTooEarly_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GraduationYear = 2024,
            Major = "Computer Science",
            GPA = 3.5m
        };
        var Grant = new GrantEntity
        {
            MinGraduationYear = 2025
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Graduation year must be 2025 or later", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WhenGraduationYearTooLate_ReturnsUnmetRequirement()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GraduationYear = 2028,
            Major = "Computer Science",
            GPA = 3.5m
        };
        var Grant = new GrantEntity
        {
            MaxGraduationYear = 2027
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Single(unmetRequirements);
        Assert.Contains("Graduation year must be 2027 or earlier", unmetRequirements[0]);
    }

    [Fact]
    public void CheckEligibility_WithMultipleUnmetRequirements_ReturnsAllUnmet()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GPA = 2.5m,
            Major = "Art",
            State = "Texas",
            GraduationYear = 2024
        };
        var Grant = new GrantEntity
        {
            MinGPA = 3.5m,
            EligibleMajors = new List<string> { "Computer Science" },
            RequiredStates = new List<string> { "California" },
            MinGraduationYear = 2025
        };

        // Act
        var (meetsAll, unmetRequirements) = _matchingService.CheckEligibility(Nonprofit, Grant);

        // Assert
        Assert.False(meetsAll);
        Assert.Equal(4, unmetRequirements.Count);
    }

    #endregion

    #region CalculateMatchScore Tests

    [Fact]
    public void CalculateMatchScore_WithHighSimilarity_ReturnsHighScore()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GPA = 3.8m,
            Major = "Computer Science",
            State = "California"
        };

        var Grant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Tech Grant",
            AwardAmount = 10000,
            Deadline = DateTime.UtcNow.AddDays(60),
            RequiresEssay = false,
            RequiresRecommendation = false
        };

        var semanticSimilarity = 0.95;

        // Act
        var result = _matchingService.CalculateMatchScore(Nonprofit, Grant, semanticSimilarity);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Grant.Id, result.GrantId);
        Assert.Equal(semanticSimilarity, result.SemanticSimilarity);
        Assert.True(result.CompositeScore > 0);
        Assert.NotNull(result.Breakdown);
    }

    [Fact]
    public void CalculateMatchScore_WithHighAwardAmount_IncludesAwardScore()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 3.5m, Major = "Computer Science" };
        var highAwardGrant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "High Award",
            AwardAmount = 50000,
            Deadline = DateTime.UtcNow.AddDays(60)
        };
        var lowAwardGrant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Low Award",
            AwardAmount = 1000,
            Deadline = DateTime.UtcNow.AddDays(60)
        };

        // Act
        var highAwardResult = _matchingService.CalculateMatchScore(Nonprofit, highAwardGrant, 0.8);
        var lowAwardResult = _matchingService.CalculateMatchScore(Nonprofit, lowAwardGrant, 0.8);

        // Assert
        Assert.True(highAwardResult.Breakdown.AwardAmountScore > lowAwardResult.Breakdown.AwardAmountScore);
    }

    [Fact]
    public void CalculateMatchScore_WithEssayAndRecommendation_LowersComplexityScore()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 3.5m, Major = "Computer Science" };
        var simpleGrant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Simple Application",
            AwardAmount = 5000,
            Deadline = DateTime.UtcNow.AddDays(60),
            RequiresEssay = false,
            RequiresRecommendation = false
        };
        var complexGrant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Complex Application",
            AwardAmount = 5000,
            Deadline = DateTime.UtcNow.AddDays(60),
            RequiresEssay = true,
            RequiresRecommendation = true
        };

        // Act
        var simpleResult = _matchingService.CalculateMatchScore(Nonprofit, simpleGrant, 0.8);
        var complexResult = _matchingService.CalculateMatchScore(Nonprofit, complexGrant, 0.8);

        // Assert
        Assert.True(simpleResult.Breakdown.ComplexityScore > complexResult.Breakdown.ComplexityScore);
    }

    [Fact]
    public void CalculateMatchScore_WithSoonDeadline_LowersDeadlineScore()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 3.5m, Major = "Computer Science" };
        var soonDeadline = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Soon Deadline",
            AwardAmount = 5000,
            Deadline = DateTime.UtcNow.AddDays(15) // Less than 30 days
        };
        var laterDeadline = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Later Deadline",
            AwardAmount = 5000,
            Deadline = DateTime.UtcNow.AddDays(60)
        };

        // Act
        var soonResult = _matchingService.CalculateMatchScore(Nonprofit, soonDeadline, 0.8);
        var laterResult = _matchingService.CalculateMatchScore(Nonprofit, laterDeadline, 0.8);

        // Assert
        Assert.True(soonResult.Breakdown.DeadlineProximityScore < laterResult.Breakdown.DeadlineProximityScore);
    }

    [Fact]
    public void CalculateMatchScore_WhenRequirementsNotMet_SetsFlag()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile
        {
            GPA = 2.5m,
            Major = "Computer Science"
        };
        var Grant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "High GPA Required",
            MinGPA = 3.5m,
            AwardAmount = 5000,
            Deadline = DateTime.UtcNow.AddDays(60)
        };

        // Act
        var result = _matchingService.CalculateMatchScore(Nonprofit, Grant, 0.8);

        // Assert
        Assert.False(result.MeetsAllRequirements);
        Assert.NotEmpty(result.UnmetRequirements);
    }

    [Fact]
    public void CalculateMatchScore_IncludesAllBreakdownComponents()
    {
        // Arrange
        var Nonprofit = new NonprofitProfile { GPA = 3.5m, Major = "Computer Science" };
        var Grant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Grant",
            AwardAmount = 10000,
            Deadline = DateTime.UtcNow.AddDays(60),
            RequiresEssay = true,
            RequiresRecommendation = false
        };

        // Act
        var result = _matchingService.CalculateMatchScore(Nonprofit, Grant, 0.85);

        // Assert
        Assert.NotNull(result.Breakdown);
        Assert.True(result.Breakdown.SemanticScore > 0);
        Assert.True(result.Breakdown.AwardAmountScore >= 0);
        Assert.True(result.Breakdown.ComplexityScore >= 0);
        Assert.True(result.Breakdown.DeadlineProximityScore >= 0);

        // Composite score should be sum of all components
        var expectedComposite = result.Breakdown.SemanticScore +
                               result.Breakdown.AwardAmountScore +
                               result.Breakdown.ComplexityScore +
                               result.Breakdown.DeadlineProximityScore;
        Assert.Equal(expectedComposite, result.CompositeScore, 2);
    }

    #endregion

    #region FindGrantsAsync Tests

    [Fact]
    public async Task FindGrantsAsync_WithValidRequest_ReturnsResults()
    {
        // Arrange
        var searchRequest = new SearchRequest
        {
            NonprofitId = Guid.NewGuid(),
            Query = "Computer science Grants",
            MinSimilarity = 0.7,
            Limit = 10,
            Offset = 0
        };

        var mockSearchResponse = new VectorSearchResponse
        {
            Results = new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    ProfileId = Guid.NewGuid().ToString(),
                    Similarity = 0.85,
                    Profile = new EntityResponse
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Tech Grant",
                        Description = "A Grant for tech Nonprofits",
                        Attributes = new Dictionary<string, object>
                        {
                            { "GrantId", Guid.NewGuid().ToString() },
                            { "provider", "Tech Corp" },
                            { "awardAmount", 5000 },
                            { "deadline", DateTime.UtcNow.AddMonths(2).ToString("O") },
                            { "isRenewable", false },
                            { "requiresEssay", false },
                            { "requiresRecommendation", false }
                        }
                    }
                }
            },
            TotalResults = 1
        };

        _entityMatchingServiceMock
            .Setup(x => x.SearchGrantsAsync(
                It.IsAny<NonprofitProfile>(),
                It.IsAny<VectorSearchRequest>(),
                default))
            .ReturnsAsync(mockSearchResponse);

        // Act
        var result = await _matchingService.FindGrantsAsync(searchRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Matches);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ProcessingTime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task FindGrantsAsync_WithFilters_AppliesFilters()
    {
        // Arrange
        var searchRequest = new SearchRequest
        {
            NonprofitId = Guid.NewGuid(),
            Query = "Grants",
            MinAwardAmount = 1000,
            MaxAwardAmount = 10000,
            DeadlineAfter = DateTime.UtcNow,
            DeadlineBefore = DateTime.UtcNow.AddMonths(6),
            RequiresEssay = false,
            Limit = 10
        };

        VectorSearchRequest? capturedRequest = null;

        _entityMatchingServiceMock
            .Setup(x => x.SearchGrantsAsync(
                It.IsAny<NonprofitProfile>(),
                It.IsAny<VectorSearchRequest>(),
                default))
            .Callback<NonprofitProfile, VectorSearchRequest, System.Threading.CancellationToken>(
                (profile, request, ct) => capturedRequest = request)
            .ReturnsAsync(new VectorSearchResponse
            {
                Results = new List<SearchResultItem>(),
                TotalResults = 0
            });

        // Act
        await _matchingService.FindGrantsAsync(searchRequest);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.AttributeFilters);
        Assert.NotEmpty(capturedRequest.AttributeFilters.Filters);

        // Check for award amount filters
        Assert.Contains(capturedRequest.AttributeFilters.Filters,
            f => f.FieldPath == "attributes.awardAmount" && f.Operator == "GreaterThanOrEqual");
        Assert.Contains(capturedRequest.AttributeFilters.Filters,
            f => f.FieldPath == "attributes.awardAmount" && f.Operator == "LessThanOrEqual");

        // Check for deadline filters
        Assert.Contains(capturedRequest.AttributeFilters.Filters,
            f => f.FieldPath == "attributes.deadline");

        // Check for essay requirement filter
        Assert.Contains(capturedRequest.AttributeFilters.Filters,
            f => f.FieldPath == "attributes.requiresEssay" && f.Operator == "Equal");
    }

    [Fact]
    public async Task FindGrantsAsync_SortsResultsByCompositeScore()
    {
        // Arrange
        var searchRequest = new SearchRequest
        {
            NonprofitId = Guid.NewGuid(),
            Query = "Grants",
            Limit = 10
        };

        var mockSearchResponse = new VectorSearchResponse
        {
            Results = new List<SearchResultItem>
            {
                CreateMockSearchResult(Guid.NewGuid().ToString(), "Low Score", 5000, 0.6),
                CreateMockSearchResult(Guid.NewGuid().ToString(), "High Score", 20000, 0.95),
                CreateMockSearchResult(Guid.NewGuid().ToString(), "Medium Score", 10000, 0.8)
            },
            TotalResults = 3
        };

        _entityMatchingServiceMock
            .Setup(x => x.SearchGrantsAsync(
                It.IsAny<NonprofitProfile>(),
                It.IsAny<VectorSearchRequest>(),
                default))
            .ReturnsAsync(mockSearchResponse);

        // Act
        var result = await _matchingService.FindGrantsAsync(searchRequest);

        // Assert
        Assert.Equal(3, result.Matches.Count);
        // Results should be sorted by composite score (descending)
        for (int i = 0; i < result.Matches.Count - 1; i++)
        {
            Assert.True(result.Matches[i].CompositeScore >= result.Matches[i + 1].CompositeScore);
        }
    }

    #endregion

    #region Helper Methods

    private SearchResultItem CreateMockSearchResult(string id, string name, decimal awardAmount, double similarity)
    {
        return new SearchResultItem
        {
            ProfileId = id,
            Similarity = similarity,
            Profile = new EntityResponse
            {
                Id = id,
                Name = name,
                Description = $"Description for {name}",
                Attributes = new Dictionary<string, object>
                {
                    { "GrantId", Guid.NewGuid().ToString() },
                    { "provider", "Test Provider" },
                    { "awardAmount", awardAmount },
                    { "deadline", DateTime.UtcNow.AddMonths(2).ToString("O") },
                    { "isRenewable", false },
                    { "requiresEssay", false },
                    { "requiresRecommendation", false }
                }
            }
        };
    }

    #endregion
}
