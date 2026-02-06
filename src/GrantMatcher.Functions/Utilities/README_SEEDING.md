# Grant Seeding System

Quick reference for the Grant data generation and seeding utilities.

## Files

### GrantDataGenerator.cs
Generates 100-150 realistic, diverse Grants across all categories.

**Usage:**
```csharp
var generator = new GrantDataGenerator();
var Grants = generator.GenerateGrants(150);
```

**Categories Generated:**
- STEM (30) - All tech/science fields
- Minority (20) - Hispanic, Black, Asian, Native American
- Women in Tech (10) - Female Nonprofits in CS/Engineering
- First Generation (15) - First-gen college Nonprofits
- State-Specific (15) - State residency requirements
- Arts & Humanities (15) - Creative fields
- Community College Transfer (10) - Transfer Nonprofits
- Veterans (8) - Military service
- Healthcare (12) - Nursing, Pre-Med, Public Health
- Business (10) - Business/Finance/Marketing
- Trade/Vocational (8) - Skilled trades
- Graduate (7) - Master's/PhD programs

### GrantSeeder.cs
Legacy seeder with simpler generation logic. Used by Azure Functions for JSON import.

## Azure Functions (SeedGrants.cs)

### 1. Generate and Import
```bash
POST http://localhost:7071/admin/seed/generate
Content-Type: application/json

{
  "count": 150
}
```

Generates Grants, stores in Cosmos DB, uploads to EntityMatchingAI with embeddings.

### 2. Save to JSON
```bash
POST http://localhost:7071/admin/seed/save-json
Content-Type: application/json

{
  "count": 150
}
```

Generates Grants and saves to timestamped JSON file. Does NOT import to database.

### 3. Import from JSON
```bash
POST http://localhost:7071/admin/seed/json
```

Imports from `Data/MockGrants.json`.

## Quick Start

### Local Development
```bash
# 1. Start Functions locally
cd src/GrantMatcher.Functions
func start

# 2. Generate and save to JSON (for review)
curl -X POST http://localhost:7071/admin/seed/save-json \
  -H "Content-Type: application/json" \
  -d '{"count": 150}'

# 3. Review the generated JSON in Data/ folder

# 4. Import to database
curl -X POST http://localhost:7071/admin/seed/generate \
  -H "Content-Type: application/json" \
  -d '{"count": 150}'
```

### Production Deployment
```bash
# Use Azure Portal or CLI to call the function
az functionapp function invoke \
  --name YourFunctionApp \
  --function-name GenerateGrants \
  --resource-group YourResourceGroup
```

## Configuration Required

In `local.settings.json` or Azure Configuration:

```json
{
  "CosmosDb:ConnectionString": "...",
  "CosmosDb:DatabaseName": "GrantMatcher",
  "CosmosDb:Containers:Grants": "Grants",
  "EntityMatchingAI:ApiKey": "...",
  "EntityMatchingAI:BaseUrl": "https://profilematching-apim.azure-api.net/api/v1",
  "OpenAI:ApiKey": "...",
  "OpenAI:EmbeddingModel": "text-embedding-3-small"
}
```

## Output Statistics

For 150 Grants:
- **Generation:** <1 second
- **Import Time:** 5-10 minutes
- **Total Award Money:** $8-10 million
- **Average Award:** $5,000-$7,000
- **Renewable:** ~45%
- **Essay Required:** ~70%
- **GPA Required:** ~80%

## Features

### Realistic Data
- Award amounts: $500 - $50,000 (weighted toward $1,000-$5,000)
- GPA requirements: None to 3.8 (weighted toward 3.0-3.5)
- Deadlines: Spread throughout the year
- 70+ different majors across all disciplines
- All 50 US states

### Diversity
- 12 Grant categories
- Multiple demographic groups
- Various eligibility criteria
- Range of award amounts
- Mix of requirements

### Optimized for Search
- Natural language summaries for vector search
- Structured attributes for filtering
- EntityMatchingAI integration
- OpenAI embeddings

## Troubleshooting

**"Build failed"**: Run `dotnet restore` and `dotnet build`

**"JSON not found"**: Ensure Data folder exists

**"EntityMatchingAI error"**: Check API key and connectivity

**"Cosmos DB error"**: Verify connection string and container names

**"OpenAI error"**: Confirm API key and quota

## See Also

- `/Grant_SEEDING_GUIDE.md` - Comprehensive documentation
- `/SampleGeneratorOutput.md` - Example output and statistics
- `Scripts/RunGrantGenerator.ps1` - PowerShell runner script
