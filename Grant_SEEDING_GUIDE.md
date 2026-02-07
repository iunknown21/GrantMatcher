# Grant Seeding & Daily Sync System

## Overview

This system automatically fetches federal grant opportunities from Simpler.Grants.gov, generates AI summaries, creates vector embeddings, and stores them in Cosmos DB with automatic expiration.

## Features

- **Initial Seed**: One-time bulk import of ~500-1,000 active federal grants
- **Daily Sync**: Automatic updates for new and modified grants (runs at 2 AM UTC)
- **AI Summaries**: Groq AI generates natural language summaries optimized for nonprofits
- **Vector Search**: OpenAI embeddings enable semantic matching
- **Auto-Expiration**: Grants automatically deleted after 90 days via Cosmos DB TTL

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Grant Sync Functions                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  SeedGrants (HTTP)          DailySyncGrants (Timer)             │
│  └─> Manual trigger         └─> Runs daily at 2 AM UTC         │
│                                                                   │
└────────┬────────────────────────────────────────────────────────┘
         │
         ├──> Simpler.Grants.gov API (fetch grants)
         │
         ├──> Groq AI (generate summaries)
         │
         ├──> OpenAI (generate embeddings)
         │
         ├──> EntityMatching API (store for vector search)
         │
         └──> Cosmos DB (store with 90-day TTL)
```

## Configuration

### Required Environment Variables

Add these to your Azure Function App settings:

```bash
# Groq AI (for grant summaries)
Groq__ApiKey=<your_groq_api_key>

# OpenAI (for embeddings) - Optional if EntityMatching handles it
OpenAI__ApiKey=<your_openai_api_key>
OpenAI__EmbeddingModel=text-embedding-3-small

# Simpler.Grants.gov (already configured)
SimplerGrants__ApiKey=<your_grants_gov_key>
SimplerGrants__BaseUrl=https://api.simpler.grants.gov/v1

# EntityMatching API (already configured)
EntityMatchingApi__BaseUrl=https://entityaiapi.azurewebsites.net
EntityMatchingApi__ApiKey=<your_entity_matching_key>

# Cosmos DB (already configured)
CosmosDb__ConnectionString=<your_cosmos_connection>
CosmosDb__DatabaseName=GrantMatcherDb
```

### Getting Groq API Key (Required)

1. Go to https://console.groq.com/
2. Sign up/login with GitHub or email
3. Navigate to "API Keys" section
4. Click "Create API Key"
5. Copy the key (starts with `gsk_...`)
6. Add to Azure:

```bash
az functionapp config appsettings set \
  --name func-grantmatcher-dev \
  --resource-group rg-grantmatcher-dev \
  --settings Groq__ApiKey="gsk_..."
```

## Usage

### Initial Seed (Run Once)

**Via HTTP Request:**
```bash
curl -X POST \
  https://func-grantmatcher-dev.azurewebsites.net/api/management/grants/seed \
  -H "x-functions-key: <YOUR_FUNCTION_KEY>"
```

**Note:** Route uses `/management/` not `/admin/` (admin is reserved by Azure Kudu).

**Expected Response:**
```json
{
  "totalFetched": 1000,
  "totalFiltered": 650,
  "successful": 645,
  "failed": 5,
  "durationSeconds": 320.5,
  "failureRate": 0.007,
  "errors": [
    "GRANT-123: Timeout generating embedding"
  ]
}
```

**What It Does:**
1. Fetches up to 1,000 active grants
2. Filters for nonprofit eligibility
3. Generates AI summaries (Groq)
4. Creates embeddings (OpenAI)
5. Stores in Cosmos DB with 90-day TTL
6. Takes ~5-10 minutes

### Daily Sync (Automatic)

- **Schedule**: 2 AM UTC daily
- **Fetches**: Grants modified in last 48 hours
- **New grants**: Full processing
- **Updated grants**: Regenerate if description changed
- **Metadata only**: Quick update

## Files Created

1. **IGroqService.cs** - Groq AI interface
2. **GroqService.cs** - Groq AI implementation
3. **GrantSyncFunctions.cs** - Seed + Daily Sync
4. **SimplerGrantsService.cs** - Enhanced with bulk methods
5. **IOpportunityDataService.cs** - Updated interface

## Next Steps

1. Add Groq API key to Azure settings
2. Deploy the updated Functions app
3. Run initial seed
4. Monitor daily sync logs

---

**Status**: ✅ Implementation complete! Ready to deploy and seed.
