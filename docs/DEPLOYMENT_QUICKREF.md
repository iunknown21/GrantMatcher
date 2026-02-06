# Deployment Quick Reference

## Prerequisites Checklist

- [ ] Azure CLI installed (`az --version`)
- [ ] .NET 9 SDK installed (`dotnet --version`)
- [ ] Node.js 20+ installed (`node --version`)
- [ ] Git installed (`git --version`)
- [ ] Azure subscription active
- [ ] OpenAI API key obtained
- [ ] EntityMatchingAI API key obtained
- [ ] GitHub account created

## One-Time Setup Commands

### 1. Login to Azure
```bash
az login
az account set --subscription "<subscription-id>"
```

### 2. Initialize Git Repository
```bash
# Windows
.\scripts\setup-repo.ps1 -GitHubUsername YOUR_USERNAME

# Linux/Mac
./scripts/setup-repo.sh YOUR_USERNAME
```

### 3. Deploy Azure Resources
```bash
# Windows
.\infrastructure\deploy.ps1 `
    -Environment dev `
    -ResourceGroupName GrantMatcher-dev-rg `
    -Location eastus `
    -OpenAIApiKey "sk-xxxxx" `
    -EntityMatchingApiKey "xxxxx"

# Linux/Mac
./infrastructure/deploy.sh dev GrantMatcher-dev-rg eastus sk-xxxxx xxxxx
```

### 4. Get Deployment Credentials

**Function App Publish Profile:**
```bash
az functionapp deployment list-publishing-profiles \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg \
    --xml > publish-profile.xml
```

**Static Web App Token:**
```bash
az staticwebapp secrets list \
    --name <static-app-name> \
    --resource-group GrantMatcher-dev-rg \
    --query properties.apiKey -o tsv
```

**Function App Key:**
```bash
az functionapp keys list \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg \
    --query functionKeys.default -o tsv
```

### 5. Configure GitHub Secrets

In GitHub: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**

Add:
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` = (paste XML content)
- `AZURE_STATIC_WEB_APPS_API_TOKEN` = (paste token)
- `FUNCTION_APP_KEY` = (paste key)

### 6. Update Workflow Files

Edit `.github/workflows/deploy-functions.yml`:
```yaml
env:
  AZURE_FUNCTIONAPP_NAME: 'your-actual-function-app-name'
```

### 7. Push to GitHub
```bash
git push -u origin main
```

## Daily Development Commands

### Local Development
```bash
# Terminal 1 - Functions API
cd src/GrantMatcher.Functions
func start

# Terminal 2 - Blazor Client
cd src/GrantMatcher.Client
npm run css:watch  # In background
dotnet run

# Terminal 3 - Tailwind CSS (or use watch above)
cd src/GrantMatcher.Client
npm run css:build
```

### Build and Test
```bash
# Build solution
dotnet build

# Build Tailwind CSS
cd src/GrantMatcher.Client && npm run css:build

# Run tests (when added)
dotnet test
```

### Git Workflow
```bash
# Create feature branch
git checkout -b feature/your-feature

# Stage and commit changes
git add .
git commit -m "Description of changes"

# Push to GitHub
git push origin feature/your-feature

# Create pull request on GitHub
# After merge, CI/CD automatically deploys
```

## Common Management Commands

### View Logs
```bash
# Function App logs
az functionapp log tail \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg

# Or stream in real-time
az webapp log tail \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg
```

### Seed Grant Data
```bash
curl -X POST \
    "https://<function-app-name>.azurewebsites.net/api/admin/Grants/seed" \
    -H "x-functions-key: <function-key>"
```

### List Grants
```bash
curl "https://<function-app-name>.azurewebsites.net/api/Grants" \
    -H "x-functions-key: <function-key>"
```

### Check Cosmos DB
```bash
# List containers
az cosmosdb sql container list \
    --account-name <cosmos-account-name> \
    --resource-group GrantMatcher-dev-rg \
    --database-name GrantMatcher

# Query data (example)
az cosmosdb sql query \
    --account-name <cosmos-account-name> \
    --database-name GrantMatcher \
    --container-name Grants \
    --query-text "SELECT * FROM c"
```

### Restart Function App
```bash
az functionapp restart \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg
```

### View Application Insights
```bash
# Get instrumentation key
az monitor app-insights component show \
    --app <app-insights-name> \
    --resource-group GrantMatcher-dev-rg \
    --query instrumentationKey -o tsv

# Query logs (last hour)
az monitor app-insights query \
    --app <app-insights-name> \
    --resource-group GrantMatcher-dev-rg \
    --analytics-query "requests | where timestamp > ago(1h)"
```

## Troubleshooting Commands

### Check Resource Group
```bash
az resource list \
    --resource-group GrantMatcher-dev-rg \
    --output table
```

### Test Function Endpoint
```bash
# Health check
curl https://<function-app-name>.azurewebsites.net/api/health

# Create profile
curl -X POST \
    https://<function-app-name>.azurewebsites.net/api/profiles \
    -H "Content-Type: application/json" \
    -H "x-functions-key: <function-key>" \
    -d '{
        "userId": "test-user",
        "firstName": "Test",
        "lastName": "User",
        "email": "test@example.com",
        "gpa": 3.5,
        "major": "Computer Science"
    }'
```

### Check Static Web App
```bash
# Get default hostname
az staticwebapp show \
    --name <static-app-name> \
    --resource-group GrantMatcher-dev-rg \
    --query defaultHostname -o tsv

# List environments
az staticwebapp environment list \
    --name <static-app-name> \
    --resource-group GrantMatcher-dev-rg
```

### View Deployment History
```bash
az deployment group list \
    --resource-group GrantMatcher-dev-rg \
    --output table
```

### Check Function App Configuration
```bash
az functionapp config appsettings list \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg
```

### Validate Bicep Template
```bash
az deployment group validate \
    --resource-group GrantMatcher-dev-rg \
    --template-file infrastructure/main.bicep \
    --parameters <your-parameters>
```

## Cost Management

### View Current Costs
```bash
# This month's costs
az consumption usage list \
    --start-date $(date -u -d "-30 days" '+%Y-%m-%d') \
    --end-date $(date -u '+%Y-%m-%d')

# Cost by resource
az consumption usage list \
    --query "[?resourceGroup=='GrantMatcher-dev-rg']" \
    --output table
```

### Set Budget Alert
```bash
az consumption budget create \
    --budget-name monthly-budget \
    --amount 50 \
    --category cost \
    --time-grain monthly \
    --resource-group GrantMatcher-dev-rg
```

## Cleanup Commands

### Delete Everything
```bash
# ⚠️ WARNING: This deletes all resources!
az group delete \
    --name GrantMatcher-dev-rg \
    --yes --no-wait
```

### Delete Specific Resource
```bash
# Delete Function App
az functionapp delete \
    --name <function-app-name> \
    --resource-group GrantMatcher-dev-rg

# Delete Static Web App
az staticwebapp delete \
    --name <static-app-name> \
    --resource-group GrantMatcher-dev-rg

# Delete Cosmos DB
az cosmosdb delete \
    --name <cosmos-account-name> \
    --resource-group GrantMatcher-dev-rg
```

## Environment Variables Reference

### Required for Local Development (local.settings.json)
```json
{
  "CosmosDb:ConnectionString": "AccountEndpoint=https://...",
  "CosmosDb:DatabaseName": "GrantMatcher",
  "EntityMatchingAI:BaseUrl": "https://profilematching-apim.azure-api.net/api/v1",
  "EntityMatchingAI:ApiKey": "xxxxx",
  "OpenAI:ApiKey": "sk-xxxxx",
  "OpenAI:EmbeddingModel": "text-embedding-3-small",
  "OpenAI:ChatModel": "gpt-4o-mini"
}
```

### Required GitHub Secrets
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
- `AZURE_STATIC_WEB_APPS_API_TOKEN`
- `FUNCTION_APP_KEY`

## Useful URLs

### Azure Portal
- **Resource Group**: https://portal.azure.com/#@/resource/subscriptions/{sub-id}/resourceGroups/GrantMatcher-dev-rg
- **Function App**: https://portal.azure.com/#@/resource/subscriptions/{sub-id}/resourceGroups/GrantMatcher-dev-rg/providers/Microsoft.Web/sites/{function-app-name}
- **Static Web App**: https://portal.azure.com/#@/resource/subscriptions/{sub-id}/resourceGroups/GrantMatcher-dev-rg/providers/Microsoft.Web/staticSites/{static-app-name}
- **Cosmos DB**: https://portal.azure.com/#@/resource/subscriptions/{sub-id}/resourceGroups/GrantMatcher-dev-rg/providers/Microsoft.DocumentDB/databaseAccounts/{cosmos-name}

### Application URLs
- **API**: https://{function-app-name}.azurewebsites.net/api
- **Frontend**: https://{static-app-name}.azurestaticapps.net
- **Application Insights**: https://portal.azure.com/#@/resource/.../microsoft.insights/components/{insights-name}

## Quick Deploy (After Initial Setup)

```bash
# Just push to GitHub - CI/CD handles the rest!
git add .
git commit -m "Your changes"
git push

# Or manually trigger deployment
# Go to GitHub Actions and click "Run workflow"
```

## Emergency Rollback

```bash
# If deployment breaks production:

# 1. Find last working deployment
az deployment group list \
    --resource-group GrantMatcher-dev-rg \
    --query "[?properties.provisioningState=='Succeeded']" \
    --output table

# 2. Redeploy from working commit
git checkout <working-commit-hash>
git push --force origin main

# 3. Or use Azure Portal:
# Function App → Deployment Center → Sync
# Select previous deployment slot
```

## Health Check Checklist

- [ ] Function App responding: `curl https://{func-app}/api/health`
- [ ] Static Web App loading: Open in browser
- [ ] Cosmos DB accessible: Check portal
- [ ] Application Insights receiving data: Check Live Metrics
- [ ] No errors in logs: `az functionapp log tail`
- [ ] GitHub Actions passing: Check Actions tab
- [ ] Costs within budget: Check Cost Management

## Support

- **Documentation**: docs/DEPLOYMENT.md
- **GitHub Issues**: https://github.com/YOUR_USERNAME/GrantMatcher/issues
- **Azure Support**: https://portal.azure.com/#blade/Microsoft_Azure_Support/HelpAndSupportBlade
