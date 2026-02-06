# Deployment Guide - GrantMatcher

This guide walks through deploying the GrantMatcher application to Azure with GitHub Actions CI/CD.

## Overview

The deployment architecture consists of:

- **Azure Cosmos DB** (Serverless) - Database for profiles and Grants
- **Azure Functions** (.NET 9) - API backend
- **Azure Static Web Apps** - Blazor WebAssembly frontend
- **Azure Key Vault** - Secure secret storage
- **Application Insights** - Monitoring and telemetry
- **GitHub Actions** - CI/CD pipelines

## Prerequisites

Before deploying, ensure you have:

1. **Azure Subscription** - Active Azure account
2. **Azure CLI** - [Install Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
3. **GitHub Account** - For repository and CI/CD
4. **OpenAI API Key** - From [OpenAI Platform](https://platform.openai.com/)
5. **EntityMatchingAI API Key** - From your EntityMatchingAI subscription
6. **PowerShell** (Windows) or **Bash** (Linux/Mac) - For running deployment scripts

### Tools Installation

```bash
# Install Azure CLI
# Windows (PowerShell):
winget install Microsoft.AzureCLI

# Mac:
brew install azure-cli

# Linux:
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Verify installation
az --version

# Install .NET 9 SDK
# Download from: https://dotnet.microsoft.com/download/dotnet/9.0
dotnet --version
```

## Step 1: Prepare Your Accounts

### 1.1 Azure Setup

```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account list --output table
az account set --subscription "<subscription-id>"

# Verify current subscription
az account show
```

### 1.2 Get API Keys

**OpenAI API Key:**
1. Go to [OpenAI Platform](https://platform.openai.com/)
2. Navigate to API Keys
3. Create new secret key
4. Copy and save securely

**EntityMatchingAI API Key:**
1. Access your EntityMatchingAI subscription
2. Copy the API key from your account settings

## Step 2: Deploy Azure Infrastructure

### Option A: Using PowerShell (Windows)

```powershell
# Navigate to infrastructure directory
cd D:\Development\Main\Grants\infrastructure

# Run deployment script
.\deploy.ps1 `
    -Environment "dev" `
    -ResourceGroupName "GrantMatcher-dev-rg" `
    -Location "eastus" `
    -OpenAIApiKey "sk-your-openai-key" `
    -EntityMatchingApiKey "your-entitymatching-key"
```

### Option B: Using Bash (Linux/Mac)

```bash
# Navigate to infrastructure directory
cd /path/to/Grants/infrastructure

# Make script executable
chmod +x deploy.sh

# Run deployment script
./deploy.sh dev GrantMatcher-dev-rg eastus sk-your-openai-key your-entitymatching-key
```

### Option C: Manual Azure CLI Deployment

```bash
# Create resource group
az group create \
    --name GrantMatcher-dev-rg \
    --location eastus

# Deploy Bicep template
az deployment group create \
    --name GrantMatcher-deployment \
    --resource-group GrantMatcher-dev-rg \
    --template-file ./main.bicep \
    --parameters environment=dev \
                 location=eastus \
                 baseName=GrantMatcher \
                 openAIApiKey="sk-your-key" \
                 entityMatchingApiKey="your-key"
```

### Deployment Output

After successful deployment, you'll see:

```
✓ Deployment completed successfully!

📋 Deployment Outputs:
===========================================
Function App URL: https://GrantMatcher-dev-func-xxxxx.azurewebsites.net
Static Web App URL: https://GrantMatcher-dev-web-xxxxx.azurestaticapps.net
Cosmos DB Account: GrantMatcher-dev-cosmos-xxxxx
Key Vault: GrantMatcher-dev-kv-xxxxx
===========================================
```

**Save these URLs!** You'll need them for the next steps.

## Step 3: Setup GitHub Repository

### 3.1 Create GitHub Repository

```bash
# Navigate to project root
cd D:\Development\Main\Grants

# Initialize git (if not already done)
git init

# Add all files
git add .

# Create initial commit
git commit -m "Initial commit: GrantMatcher application"

# Create GitHub repository
# Option 1: Via GitHub CLI
gh repo create GrantMatcher --public --source=. --push

# Option 2: Via GitHub website
# 1. Go to https://github.com/new
# 2. Create repository named "GrantMatcher"
# 3. Follow the instructions to push existing repository
```

### 3.2 Push Code to GitHub

```bash
# Add remote (replace YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/GrantMatcher.git

# Push to main branch
git branch -M main
git push -u origin main
```

## Step 4: Configure GitHub Secrets

GitHub Actions needs secrets to deploy to Azure. Add these in your repository settings.

### 4.1 Get Azure Function App Publish Profile

```bash
# Get the Function App name from deployment output
FUNCTION_APP_NAME="GrantMatcher-dev-func-xxxxx"

# Download publish profile
az functionapp deployment list-publishing-profiles \
    --name $FUNCTION_APP_NAME \
    --resource-group GrantMatcher-dev-rg \
    --xml
```

Or via Azure Portal:
1. Navigate to your Function App
2. Click **Get publish profile**
3. Save the downloaded XML file

### 4.2 Get Static Web App Deployment Token

```bash
# Get the Static Web App name from deployment output
STATIC_APP_NAME="GrantMatcher-dev-web-xxxxx"

# Get deployment token
az staticwebapp secrets list \
    --name $STATIC_APP_NAME \
    --resource-group GrantMatcher-dev-rg \
    --query properties.apiKey -o tsv
```

Or via Azure Portal:
1. Navigate to your Static Web App
2. Go to **Deployment tokens**
3. Copy the deployment token

### 4.3 Add Secrets to GitHub

1. Go to your GitHub repository
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**

Add these secrets:

| Secret Name | Value | Description |
|------------|-------|-------------|
| `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` | XML content from publish profile | Function App deployment credentials |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Deployment token | Static Web App deployment token |
| `FUNCTION_APP_KEY` | Function host key | For calling admin endpoints |

### 4.4 Get Function App Key

```bash
# Get function key for admin endpoints
az functionapp keys list \
    --name $FUNCTION_APP_NAME \
    --resource-group GrantMatcher-dev-rg \
    --query functionKeys.default -o tsv
```

## Step 5: Update GitHub Workflow Configuration

### 5.1 Update Function App Name

Edit `.github/workflows/deploy-functions.yml`:

```yaml
env:
  AZURE_FUNCTIONAPP_NAME: 'GrantMatcher-dev-func-xxxxx'  # Your actual name
```

### 5.2 Update Static Web App Configuration

Edit `infrastructure/main.bicep` - Update the repository URL:

```bicep
properties: {
  repositoryUrl: 'https://github.com/YOUR_USERNAME/GrantMatcher'
  branch: 'main'
  // ... rest of config
}
```

Redeploy the infrastructure to apply the change:

```bash
# Re-run deployment script with same parameters
./deploy.sh dev GrantMatcher-dev-rg eastus sk-your-key your-key
```

## Step 6: Test CI/CD Pipeline

### 6.1 Trigger Build

```bash
# Make a small change to trigger CI
echo "# GrantMatcher" >> README.md

# Commit and push
git add README.md
git commit -m "Test CI/CD pipeline"
git push origin main
```

### 6.2 Monitor Workflows

1. Go to your GitHub repository
2. Click **Actions** tab
3. Watch the workflows run:
   - ✅ CI - Build and Test
   - ✅ Deploy Azure Functions
   - ✅ Deploy Static Web App

### 6.3 Verify Deployment

**Functions API:**
```bash
# Test health endpoint
curl https://GrantMatcher-dev-func-xxxxx.azurewebsites.net/api/health

# Expected: 200 OK response
```

**Static Web App:**
```bash
# Open in browser
https://GrantMatcher-dev-web-xxxxx.azurestaticapps.net
```

## Step 7: Seed Grant Data

### 7.1 Trigger Seed Endpoint

```bash
# Use the function key from GitHub secrets
FUNCTION_KEY="your-function-key"
FUNCTION_URL="https://GrantMatcher-dev-func-xxxxx.azurewebsites.net"

# Seed Grants
curl -X POST \
    "$FUNCTION_URL/api/admin/Grants/seed" \
    -H "x-functions-key: $FUNCTION_KEY"
```

### 7.2 Verify Data

```bash
# List Grants
curl "$FUNCTION_URL/api/Grants" \
    -H "x-functions-key: $FUNCTION_KEY"

# Should return 100 Grants
```

## Step 8: Configure Custom Domain (Optional)

### 8.1 Add Custom Domain to Static Web App

1. Purchase a domain (e.g., GrantMatcher.com)
2. In Azure Portal, navigate to your Static Web App
3. Go to **Custom domains**
4. Click **Add**
5. Enter your domain
6. Add the required DNS records to your domain provider:
   - Type: `CNAME`
   - Name: `www` (or `@` for root)
   - Value: `<your-static-app>.azurestaticapps.net`

### 8.2 Add Custom Domain to Function App

1. In Azure Portal, navigate to your Function App
2. Go to **Custom domains**
3. Click **Add custom domain**
4. Follow the wizard to add `api.GrantMatcher.com`

## Step 9: Configure Environment Variables

### 9.1 Update Static Web App Configuration

Update the API URL in your Blazor app:

```bash
# In Azure Portal
# Static Web App → Configuration → Application settings
# Add:
# - Name: ApiBaseUrl
# - Value: https://GrantMatcher-dev-func-xxxxx.azurewebsites.net/api
```

### 9.2 Update CORS Settings

Ensure Function App allows requests from Static Web App:

```bash
# This is configured in Bicep, but you can verify:
az functionapp cors show \
    --name $FUNCTION_APP_NAME \
    --resource-group GrantMatcher-dev-rg
```

## Monitoring and Troubleshooting

### Application Insights

1. Navigate to Application Insights in Azure Portal
2. View:
   - **Live metrics** - Real-time performance
   - **Failures** - Exception tracking
   - **Performance** - Response times
   - **Usage** - User analytics

### Function App Logs

```bash
# Stream logs
az functionapp log tail \
    --name $FUNCTION_APP_NAME \
    --resource-group GrantMatcher-dev-rg

# Or view in Azure Portal:
# Function App → Functions → Monitor → Logs
```

### Common Issues

**Issue: Function App returns 500 errors**
- Check Application Insights for exceptions
- Verify Key Vault permissions for managed identity
- Ensure all configuration values are set

**Issue: Static Web App shows blank page**
- Check browser console for errors
- Verify API URL is correct in configuration
- Check CORS settings on Function App

**Issue: CI/CD fails**
- Verify GitHub secrets are correct
- Check workflow logs for specific error
- Ensure Azure credentials haven't expired

## Production Deployment

For production, create a separate environment:

```bash
# Create production resource group
az group create --name GrantMatcher-prod-rg --location eastus

# Deploy with prod environment
./deploy.sh prod GrantMatcher-prod-rg eastus sk-prod-key prod-key

# Configure production secrets in GitHub
# Add environment protection rules in GitHub
```

### Production Checklist

- [ ] Use production API keys (separate from dev)
- [ ] Enable Azure AD authentication
- [ ] Configure custom domain with SSL
- [ ] Set up Azure Front Door for global distribution
- [ ] Enable auto-scaling for Function App
- [ ] Configure backup for Cosmos DB
- [ ] Set up monitoring alerts
- [ ] Enable DDoS protection
- [ ] Review and optimize costs
- [ ] Set up staging environment for testing

## Cost Estimation

**Development Environment (per month):**
- Azure Cosmos DB (Serverless): ~$5-20
- Azure Functions (Consumption): ~$0-10
- Azure Static Web Apps (Free tier): $0
- Application Insights: ~$5
- Key Vault: ~$1
- Storage Account: ~$1
- **Total: ~$12-37/month**

**Production Environment (per month):**
- Azure Cosmos DB (Serverless): ~$50-200
- Azure Functions (Premium or App Service): ~$50-200
- Azure Static Web Apps (Standard): $9
- Application Insights: ~$20-50
- Other services: ~$10
- **Total: ~$139-469/month**

**API Costs:**
- OpenAI (per 1000 requests): ~$1-5
- EntityMatchingAI: Based on your subscription

## Maintenance

### Regular Tasks

**Weekly:**
- Review Application Insights for errors
- Monitor API costs (OpenAI usage)
- Check security alerts

**Monthly:**
- Review Azure costs
- Update dependencies
- Rotate API keys (if needed)

**Quarterly:**
- Update .NET and npm packages
- Review and optimize database queries
- Performance testing

## Rollback Procedure

If a deployment causes issues:

```bash
# Revert to previous deployment
az functionapp deployment source config-zip \
    --name $FUNCTION_APP_NAME \
    --resource-group GrantMatcher-dev-rg \
    --src ./previous-version.zip

# Or use GitHub Actions:
# 1. Go to Actions tab
# 2. Find successful previous deployment
# 3. Click "Re-run all jobs"
```

## Support and Resources

- **Azure Documentation**: https://docs.microsoft.com/azure
- **GitHub Actions**: https://docs.github.com/actions
- **Bicep**: https://docs.microsoft.com/azure/azure-resource-manager/bicep
- **Project Issues**: https://github.com/YOUR_USERNAME/GrantMatcher/issues

## Next Steps

After successful deployment:

1. **Test the application** - Create test profiles and searches
2. **Monitor usage** - Set up Azure Monitor alerts
3. **Optimize performance** - Review Application Insights
4. **Add custom branding** - Update logos and colors
5. **Enable authentication** - Add Azure AD B2C
6. **Add analytics** - Integrate Google Analytics or similar
7. **Create documentation** - User guides and FAQs
