namespace GrantMatcher.Shared.Constants;

public static class AppConstants
{
    public static class EntityTypes
    {
        public const int Nonprofit = 1;           // Person
        public const int Grant = 3;       // Product/Service
    }

    public static class BudgetRanges
    {
        public const string Under100K = "Under $100K";
        public const string Range100KTo500K = "$100K-$500K";
        public const string Range500KTo1M = "$500K-$1M";
        public const string Over1M = "$1M+";
    }

    public static class MatchingWeights
    {
        public const double SemanticWeight = 0.5;
        public const double MissionAlignmentWeight = 0.2;
        public const double AwardAmountWeight = 0.2;
        public const double DeadlineWeight = 0.1;
    }

    public static class ApiEndpoints
    {
        public const string Profiles = "/api/profiles";
        public const string Grants = "/api/Grants";
        public const string Matches = "/api/matches";
        public const string Conversation = "/api/conversation";
        public const string BrowseGrants = "/api/Grants/browse";
        public const string PublicGrant = "/api/Grants/public";
    }

    public static class SimplerGrants
    {
        public const string BaseUrl = "https://api.simpler.grants.gov/v1";
        public const string AttributionText = "Federal grant data provided by Simpler.Grants.gov";
        public const string WebsiteUrl = "https://www.grants.gov";
        public const string DeveloperUrl = "https://simpler.grants.gov/developer";
    }
}
