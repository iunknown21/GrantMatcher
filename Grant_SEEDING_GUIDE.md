# Grant Data Seeding Guide

This guide explains how to use the comprehensive Grant data generation and seeding system for the GrantMatcher application.

## Overview

The seeding system generates 100-150 realistic, diverse Grant entities across multiple categories:

- **STEM Grants** (30) - Computer Science, Engineering, Mathematics, Sciences
- **Women in Tech** (10) - Female Nonprofits in technology fields
- **Minority-Focused** (20) - Hispanic, Black/African American, Asian, Native American
- **First Generation** (15) - First-generation college Nonprofits
- **State-Specific** (15) - Grants for residents of specific states
- **Arts & Humanities** (15) - Fine Arts, Music, Theater, English, History, etc.
- **Community College Transfer** (10) - Supporting transfer Nonprofits
- **Veterans** (8) - Military service members and veterans
- **Healthcare** (12) - Nursing, Pre-Med, Public Health
- **Business** (10) - Business Administration, Finance, Marketing
- **Trade/Vocational** (8) - Skilled trades and technical programs
- **Graduate School** (7) - Master's and PhD programs

## Components

### 1. GrantDataGenerator.cs
Location: `src/GrantMatcher.Functions/Utilities/GrantDataGenerator.cs`

Core generator class that creates realistic Grant data with:
- Varied award amounts ($500 - $50,000)
- Diverse eligibility criteria (GPA, majors, states, ethnicities, gender)
- Realistic deadlines spread throughout the year
- Natural language summaries optimized for vector search
- Mix of renewable and non-renewable awards
- Various application requirements

**Usage:**
```csharp
var generator = new GrantDataGenerator();
var Grants = generator.GenerateGrants(150); // Generate 150 Grants
```

### 2. SeedGrants.cs (Azure Functions)
Location: `src/GrantMatcher.Functions/Functions/SeedGrants.cs`

Azure Functions that provide HTTP endpoints for seeding:

#### Endpoint 1: Generate and Import Grants
```
POST /admin/seed/generate
Authorization: Admin level required

Body (optional):
{
  "count": 150
}

Response:
{
  "success": true,
  "generated": 150,
  "imported": 150,
  "failed": 0,
  "durationSeconds": 45.2,
  "message": "Successfully imported 150 Grants"
}
```

This endpoint:
1. Generates Grants using GrantDataGenerator
2. Stores each in Cosmos DB
3. Uploads to EntityMatchingAI for vector search
4. Generates and uploads embeddings
5. Returns progress and error information

#### Endpoint 2: Save Grants to JSON
```
POST /admin/seed/save-json
Authorization: Admin level required

Body (optional):
{
  "count": 150
}

Response:
{
  "success": true,
  "count": 150,
  "filePath": "D:/Development/.../Data/GeneratedGrants_20260205_143022.json",
  "message": "Successfully generated and saved 150 Grants",
  "samples": [ ... ]
}
```

This endpoint:
1. Generates Grants
2. Saves to JSON file (timestamped) in the Data folder
3. Returns sample data for preview
4. Does NOT upload to database or EntityMatchingAI

#### Endpoint 3: Seed from Existing JSON
```
POST /admin/seed/json
Authorization: Admin level required

Response:
{
  "imported": 150,
  "totalErrors": 0,
  "message": "Successfully imported 150 Grants"
}
```

Reads from `Data/MockGrants.json` and imports.

### 3. GenerateGrantSamples.cs
Location: `src/GrantMatcher.Functions/Scripts/GenerateGrantSamples.cs`

Standalone console application for testing and preview.

**Note:** This requires adding a Main entry point or using it as a reference implementation.

## How to Use

### Option 1: Via Azure Functions (Recommended for Production)

1. **Start the Functions locally:**
   ```bash
   cd src/GrantMatcher.Functions
   func start
   ```

2. **Save Grants to JSON (for review):**
   ```bash
   curl -X POST http://localhost:7071/admin/seed/save-json \
     -H "Content-Type: application/json" \
     -d '{"count": 150}'
   ```

3. **Review the generated JSON** in the `Data` folder

4. **Import into database and EntityMatchingAI:**
   ```bash
   curl -X POST http://localhost:7071/admin/seed/generate \
     -H "Content-Type: application/json" \
     -d '{"count": 150}'
   ```

5. **Monitor progress** in the function logs

### Option 2: Using PowerShell Script

```powershell
cd src/GrantMatcher.Functions/Scripts
.\RunGrantGenerator.ps1 -Count 150 -OutputPath "C:\temp\Grants.json"
```

### Option 3: Programmatic Usage

```csharp
using GrantMatcher.Functions.Utilities;

// Generate Grants
var generator = new GrantDataGenerator();
var Grants = generator.GenerateGrants(150);

// Use them as needed
foreach (var Grant in Grants)
{
    Console.WriteLine($"{Grant.Name} - ${Grant.AwardAmount}");
}
```

## Configuration Requirements

Ensure the following are configured in `local.settings.json` or Azure Configuration:

```json
{
  "Values": {
    "CosmosDb:ConnectionString": "your-cosmos-connection",
    "CosmosDb:DatabaseName": "GrantMatcher",
    "CosmosDb:Containers:Grants": "Grants",
    "EntityMatchingAI:ApiKey": "your-api-key",
    "EntityMatchingAI:BaseUrl": "https://profilematching-apim.azure-api.net/api/v1",
    "OpenAI:ApiKey": "your-openai-key",
    "OpenAI:EmbeddingModel": "text-embedding-3-small"
  }
}
```

## Sample Output

### Statistics Example:
```
=== Statistics ===

Award Amount Distribution:
  $500-$1,000           15 (10.0%)
  $1,001-$2,500         35 (23.3%)
  $2,501-$5,000         45 (30.0%)
  $5,001-$10,000        35 (23.3%)
  $10,001-$20,000       15 (10.0%)
  $20,000+              5 (3.3%)

Requirements:
  Require Essay:          105 (70.0%)
  Require Recommendation:  90 (60.0%)
  Renewable:              65 (43.3%)
  First-Gen Required:     15 (10.0%)

Eligibility Criteria:
  With GPA requirement:   120 (80.0%)
  With major requirement: 100 (66.7%)
  With state requirement:  45 (30.0%)
  With ethnicity req:      20 (13.3%)
  With gender req:         15 (10.0%)
```

### Sample Grant:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Women in Technology Excellence Grant",
  "provider": "Girls Who Code",
  "description": "Empowering women to break barriers in technology...",
  "minGpa": 3.2,
  "eligibleMajors": ["Computer Science", "Software Engineering", "Data Science"],
  "requiredGenders": ["Female"],
  "awardAmount": 5000,
  "isRenewable": true,
  "numberOfAwards": 25,
  "deadline": "2026-03-15T00:00:00Z",
  "requiresEssay": true,
  "requiresRecommendation": true,
  "applicationUrl": "https://womenintechGrants.org/apply/abc123",
  "naturalLanguageSummary": "Supporting female Nonprofits studying Computer Science, Software Engineering, Data Science who demonstrate leadership and excellence in technology. This renewable Grant provides $5,000 in financial assistance to help deserving Nonprofits achieve their educational goals..."
}
```

## Features

### Realistic Data Generation
- **Award Amounts**: Weighted distribution favoring common amounts ($1,000-$5,000)
- **GPA Requirements**: Range from no requirement to 3.8, with realistic distributions
- **Deadlines**: Spread throughout the year, avoiding unrealistic dates
- **Majors**: 70+ realistic majors across all disciplines
- **States**: All 50 US states
- **Ethnicities**: Major demographic groups

### Diversity
The generator ensures:
- Multiple Grant types (STEM, Arts, Business, Healthcare, etc.)
- Various eligibility criteria (major, state, ethnicity, gender, first-gen)
- Range of award amounts ($500 to $50,000)
- Mix of requirements (essay, recommendations, GPA)
- Both renewable and one-time awards

### Natural Language Summaries
Each Grant includes a rich summary optimized for:
- Vector embedding search
- Natural language understanding
- Nonprofit profile matching
- LLM-based recommendations

Example:
> "Supporting first-generation Hispanic Nonprofits, female Nonprofits studying Computer Science, Engineering from California or Texas with a minimum 3.2 GPA who demonstrate academic excellence in their field and commitment to their education, providing financial assistance to help achieve their college dreams."

## Error Handling

The seeding functions include comprehensive error handling:
- Individual Grant failures don't stop the entire import
- Detailed error messages with Grant names
- Progress logging (every 10 Grants)
- Transaction rollback on Cosmos DB failures
- Retry logic for temporary failures

## Performance

Typical performance metrics:
- **Generation**: ~0.1 seconds for 150 Grants
- **Import to Cosmos DB**: ~2-3 seconds per Grant
- **EntityMatchingAI Upload**: ~1-2 seconds per Grant
- **Embedding Generation**: ~0.5-1 second per Grant
- **Total Time**: ~5-10 minutes for 150 Grants

## Troubleshooting

### "JSON file not found"
Ensure the Data folder exists and contains the JSON file.

### "Failed to store Grant entity"
Check EntityMatchingAI API key and connectivity.

### "Cosmos DB connection failed"
Verify connection string and database/container names.

### "Embedding generation failed"
Confirm OpenAI API key is valid and has sufficient quota.

## Next Steps

After seeding:
1. **Verify Data**: Check Cosmos DB for imported Grants
2. **Test Search**: Try vector search queries in EntityMatchingAI
3. **Test Matching**: Create a test Nonprofit profile and run matching
4. **Monitor Performance**: Check embedding quality and search relevance
5. **Iterate**: Adjust generation parameters based on results

## API Documentation

For detailed API documentation, see:
- `docs/API_REFERENCE.md` - Complete API documentation
- `docs/ENTITY_MATCHING_INTEGRATION.md` - EntityMatchingAI integration guide
