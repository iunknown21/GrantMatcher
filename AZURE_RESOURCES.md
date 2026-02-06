# Azure Resources - GrantMatcher Development Environment

## Summary
All Azure resources have been successfully created on the **PayGo** subscription using free and low-cost tiers.

**Total Estimated Monthly Cost:** ~$5-10 (primarily from Cosmos DB containers at 400 RU/s each)

---

## Resource Group
- **Name:** `rg-grantmatcher-dev`
- **Location:** East US
- **Subscription:** PayGo (f6bd721d-0b4f-4fbd-94cf-4719f6e3888f)

---

## Storage Account
- **Name:** `stgrantmatcherdev`
- **Type:** StorageV2, Standard_LRS (Locally Redundant Storage)
- **Cost:** ~$0.02/GB/month
- **Purpose:** Backend storage for Azure Functions

**Connection:**
- Use Azure CLI or Portal to get connection string
- Command: `az storage account show-connection-string --name stgrantmatcherdev --resource-group rg-grantmatcher-dev`

---

## Cosmos DB
- **Account Name:** `cosmos-grantmatcher-dev`
- **Tier:** **FREE TIER ENABLED** (1000 RU/s free, 25GB storage free)
- **Consistency Level:** Session
- **Database:** `GrantMatcherDb`

### Connection String
```
AccountEndpoint=https://cosmos-grantmatcher-dev.documents.azure.com:443/;AccountKey=<CONFIGURED_IN_AZURE>;
```

**Note:** Connection string is configured in Azure Function App settings. Retrieve via:
```bash
az cosmosdb keys list --name cosmos-grantmatcher-dev --resource-group rg-grantmatcher-dev --type connection-strings
```

### Containers Created
1. **Grants** - Partition Key: `/Agency`, Throughput: 400 RU/s
2. **NonprofitProfiles** - Partition Key: `/id`, Throughput: 400 RU/s
3. **AnalyticsEvents** - Partition Key: `/SessionId`, Throughput: 400 RU/s
4. **GrantMetrics** - Partition Key: `/GrantId`, Throughput: 400 RU/s

**Total Allocated:** 1600 RU/s (600 RU/s will be charged, as 1000 RU/s is covered by free tier)

---

## Azure Functions
- **Name:** `func-grantmatcher-dev`
- **Runtime:** .NET 8 (Isolated)
- **Plan:** Consumption (Pay-per-execution)
- **OS:** Linux
- **Cost:** ~$0.20/month for light usage (1M executions/month free)

**Endpoints:**
- **Base URL:** https://func-grantmatcher-dev.azurewebsites.net
- **SCM URL:** https://func-grantmatcher-dev.scm.azurewebsites.net

**Application Insights:**
- Auto-created: `func-grantmatcher-dev`
- Portal: https://portal.azure.com/#resource/subscriptions/f6bd721d-0b4f-4fbd-94cf-4719f6e3888f/resourceGroups/rg-grantmatcher-dev/providers/microsoft.insights/components/func-grantmatcher-dev/overview

**Configured Settings:**
- `CosmosDb__ConnectionString` - Connected to Cosmos DB
- `CosmosDb__DatabaseName` - GrantMatcherDb
- `FUNCTIONS_WORKER_RUNTIME` - dotnet-isolated

**CORS:** Configured to allow Static Web App origin

---

## Static Web App
- **Name:** `swa-grantmatcher-dev`
- **Tier:** FREE
- **Location:** East US 2
- **Cost:** $0 (Free tier)

**URLs:**
- **Default Hostname:** https://salmon-ocean-0a660780f.1.azurestaticapps.net
- **Content Endpoint:** https://content-eus2.infrastructure.1.azurestaticapps.net

**Deployment Token (for GitHub Actions):**
```
<CONFIGURED_IN_GITHUB_SECRETS>
```

**Note:** Deployment token is already configured as GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN`. Retrieve via:
```bash
az staticwebapp secrets list --name swa-grantmatcher-dev --resource-group rg-grantmatcher-dev
```

**GitHub Actions Setup:**
1. Go to GitHub repository: https://github.com/iunknown21/GrantMatcher
2. Navigate to Settings → Secrets and variables → Actions
3. Add new secret:
   - Name: `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - Value: (token above)

---

## Required Configuration

### Function App - Settings Configured ✅

All necessary settings have been configured:

```bash
# Already configured in Azure Function App:
CosmosDb__ConnectionString=<CONFIGURED>
CosmosDb__DatabaseName=GrantMatcherDb
SimplerGrants__ApiKey=<CONFIGURED>
SimplerGrants__BaseUrl=https://api.simpler.grants.gov
EntityMatchingApi__BaseUrl=https://entityaiapi.azurewebsites.net
EntityMatchingApi__ApiKey=<CONFIGURED>
```

**Get values from Azure:**
```bash
az functionapp config appsettings list --name func-grantmatcher-dev --resource-group rg-grantmatcher-dev
```

**Note:** EntityMatching API handles all embedding generation - no OpenAI key needed!

### Local Development
Create `src/GrantMatcher.Functions/local.settings.json`:

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
# Option 1: Download all settings at once
az functionapp config appsettings list \
  --name func-grantmatcher-dev \
  --resource-group rg-grantmatcher-dev \
  --output json > local.settings.values.json

# Option 2: Get specific values
az cosmosdb keys list --name cosmos-grantmatcher-dev --resource-group rg-grantmatcher-dev --type connection-strings
```

**Note:** No OpenAI key needed - EntityMatching API handles embeddings!

### Blazor Client Configuration
Update `src/GrantMatcher.Client/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://func-grantmatcher-dev.azurewebsites.net"
}
```

---

## EntityMatching API Integration

GrantMatcher uses the **EntityMatching API** for AI-powered embedding generation:

- **API URL:** https://entityaiapi.azurewebsites.net
- **Purpose:** Generate semantic embeddings for grant descriptions and nonprofit profiles
- **Benefit:** No OpenAI key needed in GrantMatcher - EntityMatching API handles all AI operations
- **Documentation:** See `D:\Development\Main\EntityMatchingAPI\DEPLOYMENT_COMPLETE.md`

### How It Works:
1. GrantMatcher sends text (grant description, nonprofit mission) to EntityMatching API
2. EntityMatching API generates embeddings using its configured OpenAI account
3. Embeddings are returned to GrantMatcher for storage in Cosmos DB
4. GrantMatcher performs similarity matching using these embeddings

### Code Integration:
```csharp
// In GrantMatcher.Core - configure EntityMatching client
var client = new EntityMatchingClient(new EntityMatchingClientOptions
{
    ApiKey = configuration["EntityMatchingApi__ApiKey"],
    BaseUrl = configuration["EntityMatchingApi__BaseUrl"]
});
```

---

## Deployment Commands

### Deploy Azure Functions
```bash
cd src/GrantMatcher.Functions
func azure functionapp publish func-grantmatcher-dev
```

### Deploy Static Web App
```bash
# Via GitHub Actions (automatic on push to main)
# Or manually:
cd src/GrantMatcher.Client
az staticwebapp deploy \
  --name "swa-grantmatcher-dev" \
  --resource-group "rg-grantmatcher-dev" \
  --app-location "src/GrantMatcher.Client" \
  --api-location "" \
  --output-location "wwwroot"
```

---

## Cost Optimization Tips

1. **Cosmos DB Free Tier:** Currently using 1600 RU/s total, with 1000 RU/s free
   - **Tip:** Reduce container throughput to 400 RU/s each or use shared throughput at database level to stay within free tier

2. **Azure Functions:** Consumption plan charges only for execution time
   - **Free:** 1M executions/month, 400,000 GB-s compute time
   - **Tip:** Perfect for development with light usage

3. **Static Web App:** Free tier includes:
   - 100 GB bandwidth/month
   - Free custom domains
   - Free SSL certificates

4. **Storage Account:** LRS is cheapest option
   - **Cost:** ~$0.02/GB/month
   - **Tip:** Clean up old logs and unused files regularly

---

## Monitoring & Management

### Azure Portal Links
- **Resource Group:** https://portal.azure.com/#resource/subscriptions/f6bd721d-0b4f-4fbd-94cf-4719f6e3888f/resourceGroups/rg-grantmatcher-dev/overview
- **Cosmos DB:** https://portal.azure.com/#resource/subscriptions/f6bd721d-0b4f-4fbd-94cf-4719f6e3888f/resourceGroups/rg-grantmatcher-dev/providers/Microsoft.DocumentDB/databaseAccounts/cosmos-grantmatcher-dev/overview
- **Functions:** https://portal.azure.com/#resource/subscriptions/f6bd721d-0b4f-4fbd-94cf-4719f6e3888f/resourceGroups/rg-grantmatcher-dev/providers/Microsoft.Web/sites/func-grantmatcher-dev/appServices
- **Static Web App:** https://portal.azure.com/#resource/subscriptions/f6bd721d-0b4f-4fbd-94cf-4719f6e3888f/resourceGroups/rg-grantmatcher-dev/providers/Microsoft.Web/staticSites/swa-grantmatcher-dev/staticsite

### CLI Management
```bash
# View all resources
az resource list --resource-group rg-grantmatcher-dev --output table

# Check Cosmos DB metrics
az monitor metrics list \
  --resource cosmos-grantmatcher-dev \
  --resource-group rg-grantmatcher-dev \
  --resource-type Microsoft.DocumentDB/databaseAccounts \
  --metric "TotalRequests"

# View Function App logs
az functionapp log tail --name func-grantmatcher-dev --resource-group rg-grantmatcher-dev
```

---

## Next Steps

1. ✅ Azure resources created
2. ⏭️ Add OpenAI API key to Function App settings
3. ⏭️ Add Simpler.Grants.gov API key to Function App settings
4. ⏭️ Add Static Web App deployment token to GitHub secrets
5. ⏭️ Update Blazor client appsettings.json with Functions URL
6. ⏭️ Deploy Functions: `func azure functionapp publish func-grantmatcher-dev`
7. ⏭️ Test endpoints
8. ⏭️ Configure GitHub Actions for automated deployment

---

## Support & Troubleshooting

### Common Issues

**Functions not responding:**
- Check Application Insights for errors
- Verify CORS settings include Static Web App URL
- Ensure Cosmos DB connection string is correct

**Cosmos DB connection errors:**
- Verify firewall settings (currently allows all IPs)
- Check connection string in Function App settings
- Ensure containers are created in correct database

**Static Web App 404:**
- Ensure deployment completed successfully
- Check build output location is `wwwroot`
- Verify API base URL in client appsettings.json

### Delete All Resources
```bash
# WARNING: This will delete EVERYTHING in the resource group
az group delete --name rg-grantmatcher-dev --yes --no-wait
```

---

**Created:** 2026-02-06
**Account:** iunknown21@hotmail.com
**Subscription:** PayGo
