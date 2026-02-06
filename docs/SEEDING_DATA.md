# Seeding Mock Grant Data

## Overview

The GrantMatcher application includes utilities for seeding the database with mock Grant data for testing and development purposes.

## Available Data

### MockGrants.json
Located at: `src/GrantMatcher.Functions/Data/MockGrants.json`

Contains 20 hand-crafted, realistic Grants covering:
- **Demographic diversity**: Hispanic/Latino, African American, Asian American, etc.
- **Geographic diversity**: California, Texas, Florida, New York, Illinois, etc.
- **Academic fields**: STEM, Business, Nursing, Psychology, Education, etc.
- **GPA requirements**: 2.5 to 3.7
- **Award amounts**: $2,000 to $7,500
- **Various requirements**: First-generation, gender-specific, state-specific, etc.

## Seeding Methods

### Method 1: Seed from JSON File

Seeds the 20 hand-crafted Grants from `MockGrants.json`.

**Endpoint**: `POST /api/admin/seed/json`
**Authorization**: Admin level

**Example**:
```bash
curl -X POST http://localhost:7071/api/admin/seed/json
```

**Response**:
```json
{
  "imported": 20,
  "totalErrors": 0,
  "errors": [],
  "message": "Successfully imported 20 Grants"
}
```

**What it does**:
1. Reads `MockGrants.json`
2. For each Grant:
   - Creates record in Cosmos DB
   - Stores entity in EntityMatchingAI API
   - Generates embedding using OpenAI
   - Uploads embedding to EntityMatchingAI
3. Returns import statistics

### Method 2: Generate Additional Grants

Programmatically generates additional Grants with randomized criteria.

**Endpoint**: `POST /api/admin/seed/generate/{count}`
**Parameters**:
- `count`: Number of Grants to generate (1-200)
**Authorization**: Admin level

**Example**:
```bash
# Generate 80 additional Grants
curl -X POST http://localhost:7071/api/admin/seed/generate/80
```

**Response**:
```json
{
  "generated": 80,
  "imported": 80,
  "totalErrors": 0,
  "errors": []
}
```

**Generated Grant criteria** (randomized):
- **GPA**: Randomly selected from [2.5, 2.8, 3.0, 3.2, 3.3, 3.5, 3.7, or none]
- **Majors**: 0-3 random majors from a pool of 15 common majors
- **States**: 0-2 random states from top 10 most populous states
- **Ethnicities**: 25% chance of ethnicity requirement
- **Gender**: 25% chance of gender requirement
- **First-generation**: 30% chance of requiring first-gen status
- **Award amount**: $500 to $20,000
- **Renewable**: 50% chance
- **Deadline**: Random date 7-365 days from now
- **Essay required**: 50% chance
- **Recommendation required**: 50% chance

### Recommended Approach

For a complete test dataset of 100 Grants:

```bash
# Step 1: Seed the 20 curated Grants
curl -X POST http://localhost:7071/api/admin/seed/json

# Step 2: Generate 80 additional random Grants
curl -X POST http://localhost:7071/api/admin/seed/generate/80
```

## Grant Diversity Breakdown

### Hand-Crafted (20 Grants)

**By Category**:
- Demographic-specific: 5 (Hispanic, African American, Asian American, Women)
- Geographic-specific: 5 (California, Texas, Florida, New York, Illinois)
- Major-specific: 8 (CS, Engineering, Business, Nursing, Psychology, etc.)
- First-generation: 2
- Open to all: 5

**Award Amounts**:
- $2,000-$3,000: 8 Grants
- $3,500-$5,000: 9 Grants
- $5,500-$7,500: 3 Grants

**GPA Requirements**:
- 2.5: 2 Grants
- 2.8-3.0: 6 Grants
- 3.1-3.4: 7 Grants
- 3.5-3.7: 5 Grants

### Generated (80 Grants)

**Expected Distribution** (based on randomization):
- No major requirement: ~27 Grants
- 1-3 major requirements: ~53 Grants
- No state requirement: ~40 Grants
- State-specific: ~40 Grants
- Ethnicity-specific: ~20 Grants
- Gender-specific: ~20 Grants
- First-generation required: ~24 Grants

## Testing Scenarios

### Scenario 1: High-Achieving CS Nonprofit
**Profile**: GPA 3.8, CS major, California
**Expected matches**: 15-25 Grants including:
- Computer Science Excellence Grant
- California Resident Award
- High GPA general Grants

### Scenario 2: First-Gen Hispanic Woman in STEM
**Profile**: GPA 3.5, Engineering, Female, Hispanic, First-gen
**Expected matches**: 20-30 Grants including:
- Hispanic Women in STEM Excellence
- First-Generation College Nonprofit Grant
- Women in Engineering Grant

### Scenario 3: Nursing Nonprofit
**Profile**: GPA 3.3, Nursing major
**Expected matches**: 10-15 Grants including:
- Nursing Excellence Grant
- Healthcare-related Grants

### Scenario 4: Low GPA Nonprofit
**Profile**: GPA 2.6, Any major
**Expected matches**: 5-10 Grants (only those with GPA <= 2.6 or no GPA requirement)

## Verifying Data

### Check Cosmos DB
```bash
# Get Grant count
curl http://localhost:7071/api/Grants

# Get specific Grant
curl http://localhost:7071/api/Grants/{id}
```

### Check EntityMatchingAI
The seeder automatically:
1. Creates entities with `EntityType = 3` (Product/Service)
2. Generates embeddings using `text-embedding-3-small`
3. Uploads embeddings for vector search
4. Stores all Grant attributes for filtering

### Test Matching
```bash
# Create a test profile
curl -X POST http://localhost:7071/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Test","lastName":"Nonprofit","gpa":3.5,"major":"Computer Science",...}'

# Search for matches
curl -X POST http://localhost:7071/api/matches/search \
  -H "Content-Type: application/json" \
  -d '{"NonprofitId":"<profile-id>","limit":20}'
```

## Data Cleanup

To remove all seeded Grants and start over:

```bash
# TODO: Implement cleanup endpoint
# DELETE /api/admin/Grants/purge
```

Alternatively, manually delete from:
1. Cosmos DB `Grants` container
2. EntityMatchingAI entities (via API)

## Notes

- **Deadlines**: Generated deadlines are spread across the next year, so some may already be past depending on when you seed
- **Embeddings**: Generation can take 2-5 seconds per Grant due to OpenAI API calls
- **Rate Limits**: Be mindful of OpenAI API rate limits when generating 80+ Grants
- **Costs**: Each Grant generation costs ~$0.0001 for embedding (total ~$0.01 for 100 Grants)

## Customization

To create your own Grant data:

1. **Edit JSON**: Modify `MockGrants.json` with your custom Grants
2. **Extend Generator**: Update `GrantSeeder.GenerateAdditionalGrantsAsync()` with custom logic
3. **Create new JSON files**: Add more JSON files and create separate seeder endpoints

## Production Considerations

**DO NOT** use seeding endpoints in production:
- Remove or disable admin seeding endpoints
- Use real Grant data from FastWeb, Grants.com, or similar APIs
- Implement proper data validation and sanitization
- Add audit logging for Grant creation/updates
