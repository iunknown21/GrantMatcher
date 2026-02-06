# GrantMatcher

An AI-powered Grant matching application that helps Nonprofits discover Grants they're actually eligible for using semantic search and hybrid filtering.

## Project Status

**Current Progress: 12/13 Core Tasks Completed (92%)**

### ✅ Completed
1. ✅ Solution structure and projects
2. ✅ Shared models and DTOs
3. ✅ Azure Functions API endpoints
4. ✅ EntityMatchingAI integration service
5. ✅ Blazor WebAssembly with Tailwind CSS
6. ✅ Profile creation wizard UI (5-step wizard)
7. ✅ **Conversational profile building** (AI-powered chat interface)
8. ✅ Dashboard and Grant display (with filters & sorting)
9. ✅ Grant detail and saved pages
10. ✅ Mock Grant data generation (100 Grants)
11. ✅ Landing page and navigation
12. ✅ Documentation

### ⏳ Remaining
- Azure resources and deployment

## Architecture

### Tech Stack
- **Frontend**: Blazor WebAssembly (.NET 9) + Tailwind CSS
- **Backend**: Azure Functions (.NET 9, isolated worker model)
- **Database**: Azure Cosmos DB (serverless)
- **External APIs**:
  - EntityMatchingAI API (vector search)
  - OpenAI API (embeddings + conversational AI)

### Project Structure

```
GrantMatcher/
├── src/
│   ├── GrantMatcher.Shared/          # Shared models, DTOs, constants
│   │   ├── Models/                          # NonprofitProfile, GrantEntity, etc.
│   │   ├── DTOs/                            # API DTOs (Search, Entity, Conversation)
│   │   └── Constants/                       # AppConstants
│   │
│   ├── GrantMatcher.Core/             # Business logic layer
│   │   ├── Interfaces/                      # Service interfaces
│   │   │   ├── IEntityMatchingService.cs
│   │   │   ├── IOpenAIService.cs
│   │   │   └── IMatchingService.cs
│   │   └── Services/                        # Service implementations
│   │       ├── EntityMatchingService.cs     # Integrates with EntityMatchingAI API
│   │       ├── OpenAIService.cs             # Handles embeddings & conversations
│   │       └── MatchingService.cs           # Hybrid matching logic
│   │
│   ├── GrantMatcher.Functions/        # Azure Functions API
│   │   ├── Functions/
│   │   │   ├── ProfileFunctions.cs          # CRUD for Nonprofit profiles
│   │   │   ├── GrantFunctions.cs      # CRUD + import for Grants
│   │   │   ├── MatchingFunctions.cs         # Search/matching endpoints
│   │   │   └── ConversationFunctions.cs     # Conversational profile building
│   │   ├── Program.cs                       # DI configuration
│   │   └── local.settings.json              # Local configuration
│   │
│   └── GrantMatcher.Client/           # Blazor WebAssembly
│       ├── Components/
│       │   └── Layout/
│       │       └── MainLayout.razor         # Main app layout with nav & footer
│       ├── Pages/
│       │   └── Index.razor                  # Landing page
│       ├── Services/
│       │   ├── IApiClient.cs                # API client interface
│       │   └── ApiClient.cs                 # HTTP client for Functions API
│       ├── Styles/
│       │   └── app.css                      # Tailwind input file
│       ├── wwwroot/
│       │   ├── css/
│       │   │   └── app.css                  # Generated Tailwind CSS
│       │   └── index.html
│       ├── Program.cs                       # App startup & DI
│       ├── package.json                     # Tailwind dependencies
│       └── tailwind.config.js               # Tailwind configuration
│
├── tests/                                    # (Future: Unit & integration tests)
├── docs/                                     # (Future: API docs, architecture)
└── GrantMatcher.sln                    # Solution file
```

## Key Features Implemented

### 1. Hybrid Matching Engine
The core matching algorithm combines:
- **Boolean Filters**: Hard eligibility requirements (GPA, state, major, ethnicity, etc.)
- **Vector Similarity**: Semantic matching using OpenAI embeddings
- **Composite Scoring**: Weighted ranking (60% semantic, 20% award amount, 10% complexity, 10% deadline)

**Flow**:
```
Nonprofit Profile → Filter Phase (eligibility) → Vector Search → Ranking → Top 20 Matches
```

### 2. EntityMatchingAI Integration
- Stores Grants as entities in the ProfileMatchingAPI
- Generates embeddings using OpenAI text-embedding-3-small
- Performs hybrid search with attribute filters + vector similarity
- Supports complex filter logic (AND/OR operators)

### 3. Azure Functions API
**Endpoints**:
- `POST /api/profiles` - Create Nonprofit profile
- `GET /api/profiles/{id}` - Get profile
- `PUT /api/profiles/{id}` - Update profile
- `DELETE /api/profiles/{id}` - Delete profile
- `POST /api/Grants` - Create Grant (with auto-embedding)
- `GET /api/Grants/{id}` - Get Grant
- `GET /api/Grants` - List all Grants
- `POST /api/admin/Grants/import` - Bulk import Grants
- `POST /api/matches/search` - Find matching Grants
- `POST /api/conversation` - Conversational profile building
- `POST /api/embeddings/generate` - Generate text embedding

### 4. Blazor Client
- **Tailwind CSS** with custom theme (primary blue, accent orange)
- **Responsive design** (mobile-first)
- **Landing page** with hero, stats, how-it-works, testimonials
- **5-Step Profile Wizard** with progress tracking and validation
- **Dashboard** with filters, sorting, and Grant cards
- **Grant Detail Pages** with eligibility checker
- **Saved Grants** with application status tracking
- **Navigation** with profile, dashboard, saved Grants links
- **API client** for seamless backend communication

### 5. Conversational Profile Building ✨
**NEW!** AI-powered chat interface for natural profile creation:
- **Chat UI**: Modern message bubbles with typing indicators
- **Natural Language**: Nonprofits describe themselves conversationally
- **Smart Extraction**: GPT-4o-mini extracts structured data (activities, interests, career goals)
- **Dual Mode**: Toggle between traditional forms and AI chat
- **Real-time Preview**: See extracted information before applying
- **Auto-Application**: Data merges into profile when ready

**How it works**:
```
Nonprofit: "I'm on the debate team and volunteer at the food bank"
AI: "Great! What are you passionate about learning?"
Nonprofit: "Environmental science and climate change"
AI: "What do you want to do after college?"
Nonprofit: "I want to become an environmental lawyer"
→ [Extracted: Activities, Interests, Career Goals]
```

See [CONVERSATIONAL_PROFILE.md](docs/CONVERSATIONAL_PROFILE.md) for full documentation.

## Configuration

### Azure Functions (local.settings.json)
```json
{
  "CosmosDb:ConnectionString": "YOUR_COSMOS_CONNECTION_STRING_HERE",
  "CosmosDb:DatabaseName": "GrantMatcher",
  "EntityMatchingAI:BaseUrl": "https://profilematching-apim.azure-api.net/api/v1",
  "EntityMatchingAI:ApiKey": "YOUR_API_KEY_HERE",
  "OpenAI:ApiKey": "YOUR_OPENAI_API_KEY_HERE",
  "OpenAI:EmbeddingModel": "text-embedding-3-small",
  "OpenAI:ChatModel": "gpt-4o-mini"
}
```

### Blazor Client (appsettings.json)
```json
{
  "ApiBaseUrl": "http://localhost:7071/api"
}
```

## Getting Started

### Prerequisites
- .NET 9 SDK
- Node.js 18+ (for Tailwind CSS)
- Azure Functions Core Tools
- Azure Cosmos DB account
- OpenAI API key
- EntityMatchingAI API subscription key

### Setup

1. **Clone and restore packages**:
```bash
cd D:\Development\Main\Grants
dotnet restore
cd src/GrantMatcher.Client
npm install
```

2. **Configure Azure Functions**:
- Copy `src/GrantMatcher.Functions/local.settings.json.example` to `local.settings.json`
- Add your Cosmos DB connection string
- Add your EntityMatchingAI API key
- Add your OpenAI API key

3. **Build Tailwind CSS**:
```bash
cd src/GrantMatcher.Client
npm run css:build
# Or watch for changes:
npm run css:watch
```

4. **Run the Functions API**:
```bash
cd src/GrantMatcher.Functions
func start
```

5. **Run the Blazor Client**:
```bash
cd src/GrantMatcher.Client
dotnet run
```

6. **Access the app**:
- Client: http://localhost:5000
- API: http://localhost:7071

## Data Models

### NonprofitProfile
```csharp
public class NonprofitProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; }

    // Demographics: FirstName, LastName, Email, State, City, Ethnicity, Gender, FirstGeneration
    // Academic: GPA, Major, Minor, GraduationYear, CurrentSchool
    // Activities: ExtracurricularActivities, Interests, CareerGoals
    // Financial: FinancialNeedLevel, HouseholdIncome
    // Generated: ProfileSummary, EntityId
}
```

### GrantEntity
```csharp
public class GrantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Provider { get; set; }

    // Eligibility: MinGPA, MaxGPA, EligibleMajors, RequiredStates, RequiredEthnicities, etc.
    // Award: AwardAmount, IsRenewable, NumberOfAwards
    // Application: Deadline, RequiresEssay, RequiresRecommendation, ApplicationUrl
    // Vector: NaturalLanguageSummary, EntityId
}
```

## Next Steps

### Priority 1: Complete MVP UI (Tasks 6-9)
1. **Profile Creation Wizard** (`/profile/wizard`)
   - Multi-step form (Basic → Academic → Demographics → Activities → Review)
   - Conversational input for activities/interests
   - Profile summary generation

2. **Dashboard** (`/dashboard`)
   - Display matched Grants
   - Filter by award amount, deadline, complexity
   - Match score badges
   - Save Grants

3. **Grant Detail** (`/Grant/{id}`)
   - Full description
   - Eligibility checklist (✓ meets requirement)
   - Application requirements
   - Deadline countdown

4. **Saved Grants** (`/saved`)
   - List of saved Grants
   - Application status tracking
   - Deadline sorting

### Priority 2: Data & Testing (Tasks 11-12)
5. **Generate Mock Data**
   - 100 realistic Grants
   - Various GPA requirements (2.5-4.0)
   - Different majors, states, demographics
   - Award amounts ($500-$50,000)
   - Spread deadlines over 12 months

6. **Azure Deployment**
   - Create resource group: `GrantMatcher-rg`
   - Deploy Cosmos DB (serverless)
   - Deploy Azure Functions
   - Deploy Static Web App (for Blazor)
   - Configure Application Insights

### Priority 3: Polish & Production
7. **Authentication** (Azure AD B2C or Auth0)
8. **Error handling** and user feedback
9. **Performance optimization** (caching, lazy loading)
10. **Analytics** (track searches, saves, applications)

## Deployment

### Quick Start - Deploy to Azure

1. **Setup GitHub Repository**:
```bash
# Windows
.\scripts\setup-repo.ps1 -GitHubUsername YOUR_USERNAME

# Linux/Mac
chmod +x scripts/setup-repo.sh
./scripts/setup-repo.sh YOUR_USERNAME
```

2. **Deploy Azure Infrastructure**:
```bash
# Windows (PowerShell)
.\infrastructure\deploy.ps1 `
    -Environment dev `
    -ResourceGroupName GrantMatcher-dev-rg `
    -Location eastus `
    -OpenAIApiKey "sk-your-key" `
    -EntityMatchingApiKey "your-key"

# Linux/Mac (Bash)
chmod +x infrastructure/deploy.sh
./infrastructure/deploy.sh dev GrantMatcher-dev-rg eastus sk-your-key your-key
```

3. **Configure GitHub Secrets**:
   - `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` - From Azure Portal
   - `AZURE_STATIC_WEB_APPS_API_TOKEN` - From Azure Portal
   - `FUNCTION_APP_KEY` - Function host key

4. **Push to GitHub**:
```bash
git push -u origin main
```

The CI/CD pipeline will automatically build and deploy!

### Full Deployment Guide

See **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** for complete step-by-step instructions including:
- Azure resource setup
- GitHub Actions configuration
- Custom domain setup
- Monitoring and troubleshooting
- Production deployment checklist

### CI/CD Workflows

Three GitHub Actions workflows handle automated deployment:

1. **CI - Build and Test** (`.github/workflows/ci.yml`)
   - Runs on every push and PR
   - Builds all projects
   - Compiles Tailwind CSS
   - Runs tests
   - Creates artifacts

2. **Deploy Azure Functions** (`.github/workflows/deploy-functions.yml`)
   - Deploys API backend after successful CI
   - Supports manual seed trigger

3. **Deploy Static Web App** (`.github/workflows/deploy-static-web-app.yml`)
   - Deploys Blazor frontend
   - Automatic preview deployments for PRs

### Infrastructure as Code

All Azure resources are defined in **`infrastructure/main.bicep`**:
- Azure Cosmos DB (Serverless)
- Azure Functions (Consumption plan)
- Azure Static Web Apps
- Azure Key Vault (for secrets)
- Application Insights (monitoring)
- Managed identities and RBAC

### Cost Estimation

**Development**: ~$12-37/month
**Production**: ~$139-469/month

Plus API costs:
- OpenAI: ~$1-5 per 1000 requests
- EntityMatchingAI: Based on subscription

## Azure Account Information

**IMPORTANT**: All resources must be created under:
- **Azure Account**: iunknown21@hotmail.com
- **Resource Group**: `GrantMatcher-rg`
- **Location**: East US

## Matching Algorithm Details

### Phase 1: Filter (Eligibility)
Hard requirements that must be met:
- GPA range check
- State residency (if required by Grant)
- Major eligibility
- Ethnicity (if required)
- Gender (if required)
- First-generation status
- Graduation year range

### Phase 2: Vector Search
- Generate embedding for Nonprofit's ProfileSummary
- Search against Grant embeddings
- Minimum similarity threshold: 0.6
- Return top 20 results

### Phase 3: Composite Ranking
```
Final Score = (Semantic Similarity × 0.6) +
              (Normalized Award Amount × 0.2) +
              (Application Complexity × 0.1) +
              (Deadline Proximity × 0.1)
```

**Result**: Personalized, ranked list of Grants the Nonprofit can actually win.

## Development Notes

### Tailwind CSS
- Custom color scheme: Primary (blue), Accent (orange)
- Forms plugin installed (@tailwindcss/forms)
- Utility classes for common patterns (btn, card, input, label)
- Mobile-first responsive design

### API Integration
- EntityMatchingAI base URL: `https://profilematching-apim.azure-api.net/api/v1`
- Requires `Ocp-Apim-Subscription-Key` header
- Entity type for Grants: 3 (Product/Service)

### Cosmos DB Schema
- **Database**: GrantMatcher
- **Containers**:
  - `Nonprofits` (partition key: userId)
  - `Grants` (partition key: provider)
  - `matches` (partition key: NonprofitId)
  - `saved-Grants` (partition key: NonprofitId)

## Testing Checklist

- [ ] Create Nonprofit profile with various demographics
- [ ] Verify eligibility filters work correctly
- [ ] Test semantic matching returns relevant Grants
- [ ] Verify composite scoring ranks appropriately
- [ ] Test conversational profile building extracts data correctly
- [ ] Check mobile responsiveness
- [ ] Test error handling (API down, invalid data)
- [ ] Performance test (search < 2 seconds)

## License

Copyright © 2026 GrantMatcher. All rights reserved.

## Contact

For questions or support: support@GrantMatcher.com
