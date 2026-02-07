# GrantMatcher Configuration Summary

## ✅ Completed Setup

### Azure Resources (All Created)
- ✅ Resource Group: `rg-grantmatcher-dev`
- ✅ Storage Account: `stgrantmatcherdev`
- ✅ Cosmos DB: `cosmos-grantmatcher-dev` (FREE TIER)
  - Database: `GrantMatcherDb`
  - Containers: Grants, NonprofitProfiles, AnalyticsEvents, GrantMetrics
- ✅ Azure Functions: `func-grantmatcher-dev`
- ✅ Static Web App: `swa-grantmatcher-dev` (FREE)

### Function App Configuration (All Set)
```bash
# All settings configured in Azure Function App:
✅ CosmosDb__ConnectionString = <CONFIGURED>
✅ CosmosDb__DatabaseName = GrantMatcherDb
✅ SimplerGrants__ApiKey = <CONFIGURED>
✅ SimplerGrants__BaseUrl = https://api.simpler.grants.gov
✅ EntityMatchingApi__BaseUrl = https://entityaiapi.azurewebsites.net
✅ EntityMatchingApi__ApiKey = <CONFIGURED>
```

**Note:** All sensitive keys are configured in Azure. View via:
```bash
az functionapp config appsettings list --name func-grantmatcher-dev --resource-group rg-grantmatcher-dev
```

**Important:** No OpenAI key needed! EntityMatching API (https://entityaiapi.azurewebsites.net) handles all embedding generation.

### Deployment Status ✅
**Last Deployed:** 2026-02-07
**Status:** All functions successfully deployed and operational

**Available Endpoints (25+ functions):**
- Grant Management: `/api/grants` (GET, POST), `/api/grants/{id}` (GET)
- Profile Management: `/api/profiles` (POST), `/api/profiles/{id}` (GET, PUT, DELETE)
- Matching: `/api/matches/search`
- Conversation: `/api/conversation`
- Analytics: `/api/analytics/*` (track, query, report, realtime, top grants, cohorts, funnel)
- Admin: `/api/admin/grants/import`
- Diagnostics: `/api/diagnostics/*` (health, cache-stats, clear-cache, warmup-cache, performance-stats)

**Health Check:** https://func-grantmatcher-dev.azurewebsites.net/api/diagnostics/health

---

## 🔧 Integration Architecture

```
┌─────────────────────┐
│  GrantMatcher App   │
│  (Blazor + Funcs)   │
└──────┬──────────────┘
       │
       ├──> Cosmos DB (grant data, profiles, matches)
       │    Connection: Configured in Function App
       │
       ├──> Simpler.Grants.gov API (federal grant data)
       │    Key: <CONFIGURED>
       │
       └──> EntityMatching API (embeddings)
            URL: https://entityaiapi.azurewebsites.net
            Key: <CONFIGURED>
            Purpose: Generate semantic embeddings for AI matching
```

---

## ⏭️ Next Steps

### 1. Add GitHub Secret for Static Web App
```bash
# Go to GitHub repository settings
# https://github.com/iunknown21/GrantMatcher/settings/secrets/actions

# Secret already configured:
Name: AZURE_STATIC_WEB_APPS_API_TOKEN
Status: ✅ Configured

# To get a new token if needed:
az staticwebapp secrets list --name swa-grantmatcher-dev --resource-group rg-grantmatcher-dev --query "properties.apiKey" -o tsv
```

### 2. Update Local Development Settings
Create/update `src/GrantMatcher.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__ConnectionString": "<GET_FROM_AZURE>",
    "CosmosDb__DatabaseName": "GrantMatcherDb",
    "SimplerGrants__ApiKey": "<GET_FROM_AZURE>",
    "SimplerGrants__BaseUrl": "https://api.simpler.grants.gov",
    "EntityMatchingApi__BaseUrl": "https://entityaiapi.azurewebsites.net",
    "EntityMatchingApi__ApiKey": "<GET_FROM_AZURE>"
  }
}
```

**Get actual values from Azure:**
```bash
# Get Cosmos DB connection string
az cosmosdb keys list --name cosmos-grantmatcher-dev --resource-group rg-grantmatcher-dev --type connection-strings --query "connectionStrings[0].connectionString" -o tsv

# Get all Function App settings (includes all API keys)
az functionapp config appsettings list --name func-grantmatcher-dev --resource-group rg-grantmatcher-dev
```

### 3. Update Blazor Client Configuration
Update `src/GrantMatcher.Client/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://func-grantmatcher-dev.azurewebsites.net"
}
```

### 4. Code Changes for EntityMatching Integration

#### Option A: Reference EntityMatching.SDK Project
Add project reference in `GrantMatcher.Core.csproj`:
```xml
<ProjectReference Include="..\..\EntityMatchingAPI\EntityMatching.SDK\EntityMatching.SDK.csproj" />
```

#### Option B: Install EntityMatching NuGet Package
```bash
cd src/GrantMatcher.Core
dotnet add package EntityMatching.SDK --version 1.0.0
```

#### Update OpenAIService to use EntityMatching API
In `src/GrantMatcher.Core/Services/OpenAIService.cs`, replace OpenAI calls with EntityMatching API:

```csharp
// OLD CODE (remove):
// var embeddingResponse = await _openAIClient.GetEmbeddingsAsync(...)

// NEW CODE:
private readonly EntityMatchingClient _entityMatchingClient;

public OpenAIService(IConfiguration configuration)
{
    _entityMatchingClient = new EntityMatchingClient(new EntityMatchingClientOptions
    {
        ApiKey = configuration["EntityMatchingApi__ApiKey"],
        BaseUrl = configuration["EntityMatchingApi__BaseUrl"],
        OpenAIKey = null // Not needed - server-side generation
    });
}

public async Task<float[]> GenerateEmbeddingAsync(string text)
{
    // Create temporary entity for embedding generation
    var entityId = Guid.NewGuid();

    // Generate embedding via EntityMatching API
    var embedding = await _entityMatchingClient.Embeddings.GenerateAsync(text);

    return embedding;
}
```

**Note:** You may need to add a simple "generate embedding from text" endpoint to EntityMatching API, or use the existing entity creation flow.

### 5. Test Local Development
```bash
# Start Azurite (Azure Storage Emulator)
azurite

# Start Functions
cd src/GrantMatcher.Functions
func start

# In another terminal, start Blazor client
cd src/GrantMatcher.Client
dotnet run
```

### 6. Deploy to Azure
```bash
# Deploy Functions
cd src/GrantMatcher.Functions
func azure functionapp publish func-grantmatcher-dev

# Static Web App deploys automatically via GitHub Actions
# (after you add the secret in step 1)
```

---

## 🔍 Testing Checklist

### Local Testing
- [ ] Functions start without errors
- [ ] Can connect to Cosmos DB
- [ ] Simpler.Grants.gov API calls work
- [ ] EntityMatching API integration works
- [ ] Blazor client runs and connects to Functions

### Azure Testing
- [ ] Functions deployed successfully
- [ ] Health endpoint responds: `curl https://func-grantmatcher-dev.azurewebsites.net/api/health`
- [ ] Cosmos DB containers accessible
- [ ] Static Web App loads
- [ ] End-to-end grant search works

---

## 💰 Cost Summary

| Resource | Tier | Monthly Cost |
|----------|------|--------------|
| Cosmos DB | Free tier (1000 RU/s free) | $3-5 (600 RU/s over free tier) |
| Azure Functions | Consumption | $0-2 (1M free executions) |
| Static Web App | Free | $0 |
| Storage Account | Standard LRS | $0.50 |
| **Total** | | **~$5-10/month** |

**Perfect for development!** 🎉

---

## 📚 Documentation Files

1. **AZURE_RESOURCES.md** - Complete Azure resource details
2. **CONFIGURATION_SUMMARY.md** (this file) - Quick configuration reference
3. **GRANT_MATCHER_SETUP.md** - Original setup guide
4. **EntityMatchingAPI/DEPLOYMENT_COMPLETE.md** - EntityMatching API documentation

---

## 🆘 Troubleshooting

### Functions won't start locally
- Check Azurite is running
- Verify local.settings.json has all values
- Check Cosmos DB connection string is correct

### EntityMatching API returns 401
- Verify function key is correct
- Check API base URL (https://entityaiapi.azurewebsites.net)
- Get key from Azure: `az functionapp keys list --name entityaiapi --resource-group entitymatchingai`
- Test with: `curl -H "x-functions-key: <YOUR_KEY>" https://entityaiapi.azurewebsites.net/api/version`

### Simpler.Grants.gov API errors
- Verify API key is configured in Azure Function App
- Check rate limits
- Get key: `az functionapp config appsettings list --name func-grantmatcher-dev --resource-group rg-grantmatcher-dev`
- Test with: `curl -H "X-API-Key: <YOUR_KEY>" https://api.simpler.grants.gov/opportunities`

### Cosmos DB connection issues
- Check firewall settings in Azure Portal
- Verify connection string
- Ensure containers exist in GrantMatcherDb database

---

**Status:** All Azure resources created and configured! Ready for code integration and deployment. 🚀
