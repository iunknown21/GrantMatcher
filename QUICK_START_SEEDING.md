# Quick Start: Grant Seeding

Get up and running with Grant data generation in 5 minutes.

## Prerequisites

- Azure Functions Core Tools installed
- .NET 9.0 SDK
- Valid API keys (Cosmos DB, EntityMatchingAI, OpenAI)

## Configuration

Edit `src/GrantMatcher.Functions/local.settings.json`:

```json
{
  "Values": {
    "CosmosDb:ConnectionString": "YOUR_COSMOS_CONNECTION_STRING",
    "CosmosDb:DatabaseName": "GrantMatcher",
    "CosmosDb:Containers:Grants": "Grants",
    "EntityMatchingAI:ApiKey": "YOUR_ENTITY_MATCHING_API_KEY",
    "EntityMatchingAI:BaseUrl": "https://profilematching-apim.azure-api.net/api/v1",
    "OpenAI:ApiKey": "YOUR_OPENAI_API_KEY",
    "OpenAI:EmbeddingModel": "text-embedding-3-small"
  }
}
```

## Usage

### Method 1: Generate and Review (Recommended First Time)

```bash
# 1. Start Functions
cd src/GrantMatcher.Functions
func start

# 2. Generate and save to JSON (for review)
curl -X POST http://localhost:7071/admin/seed/save-json \
  -H "Content-Type: application/json" \
  -d "{\"count\": 150}"

# 3. Check output
cat Data/GeneratedGrants_*.json | jq '.[:3]'

# 4. If satisfied, import to database
curl -X POST http://localhost:7071/admin/seed/generate \
  -H "Content-Type: application/json" \
  -d "{\"count\": 150}"
```

### Method 2: Direct Import (Skip Review)

```bash
# Start and import in one go
cd src/GrantMatcher.Functions
func start

curl -X POST http://localhost:7071/admin/seed/generate \
  -H "Content-Type: application/json" \
  -d "{\"count\": 150}"
```

### Method 3: Programmatic

```csharp
using GrantMatcher.Functions.Utilities;

// Generate Grants
var generator = new GrantDataGenerator();
var Grants = generator.GenerateGrants(150);

// Use them
foreach (var Grant in Grants)
{
    Console.WriteLine($"{Grant.Name}: ${Grant.AwardAmount}");
}
```

## What Gets Generated

150 Grants across:
- 🔬 STEM (30)
- 🌍 Minority-Focused (20)
- 💻 Women in Tech (10)
- 🎓 First Generation (15)
- 📍 State-Specific (15)
- 🎨 Arts & Humanities (15)
- 🏫 Community College Transfer (10)
- 🎖️ Veterans (8)
- 🏥 Healthcare (12)
- 💼 Business (10)
- 🔧 Trade/Vocational (8)
- 🎓 Graduate School (7)

**Total Award Money:** $8-10 million
**Import Time:** ~5-7 minutes

## Verify Import

```bash
# Check Cosmos DB
az cosmosdb sql query \
  --account-name YOUR_ACCOUNT \
  --database-name GrantMatcher \
  --container-name Grants \
  --query "SELECT COUNT(1) as count FROM c"

# Should return: {"count": 150}
```

## Troubleshooting

**"Connection refused"**: Make sure Functions are running with `func start`

**"401 Unauthorized"**: Check your API keys in local.settings.json

**"429 Too Many Requests"**: OpenAI rate limit - wait and retry

**"JSON not found"**: Ensure Data folder exists in Functions directory

## Next Steps

1. ✅ Generate Grants
2. ✅ Import to database
3. ➡️ Test Nonprofit profile matching
4. ➡️ Run vector search queries
5. ➡️ Deploy to Azure

## Full Documentation

- **Comprehensive Guide:** `/Grant_SEEDING_GUIDE.md`
- **Sample Output:** `/SampleGeneratorOutput.md`
- **Implementation Summary:** `/SEEDING_SYSTEM_SUMMARY.md`
- **Quick Reference:** `/src/GrantMatcher.Functions/Utilities/README_SEEDING.md`

## Support

Issues? Check:
1. Azure Functions logs: `func start --verbose`
2. Cosmos DB metrics in Azure Portal
3. OpenAI usage dashboard
4. Error logs in Functions output

---

**Time to First Grant:** < 5 minutes
**Ready to Match:** < 10 minutes
