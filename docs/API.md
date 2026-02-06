# GrantMatcher API Documentation

Base URL: `http://localhost:7071/api` (development)

## Authentication

Currently using Function-level authentication. In production, implement Azure AD B2C or similar.

## Endpoints

### Profile Management

#### Create Profile
```http
POST /profiles
Content-Type: application/json

{
  "userId": "string",
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "state": "string",
  "city": "string",
  "ethnicity": "string",
  "gender": "string",
  "firstGeneration": boolean,
  "gpa": number,
  "major": "string",
  "minor": "string",
  "graduationYear": number,
  "currentSchool": "string",
  "extracurricularActivities": ["string"],
  "interests": ["string"],
  "careerGoals": ["string"],
  "financialNeedLevel": "High|Medium|Low",
  "householdIncome": number
}

Response: 201 Created
{
  "id": "guid",
  "userId": "string",
  ...
  "createdAt": "datetime",
  "lastModified": "datetime"
}
```

#### Get Profile
```http
GET /profiles/{id}

Response: 200 OK
{
  "id": "guid",
  "userId": "string",
  ...
}
```

#### Update Profile
```http
PUT /profiles/{id}
Content-Type: application/json

{
  "id": "guid",
  "userId": "string",
  ...
}

Response: 200 OK
```

#### Delete Profile
```http
DELETE /profiles/{id}

Response: 204 No Content
```

### Grant Management

#### Create Grant
```http
POST /Grants
Content-Type: application/json

{
  "name": "string",
  "description": "string",
  "provider": "string",
  "minGPA": number,
  "maxGPA": number,
  "eligibleMajors": ["string"],
  "requiredStates": ["string"],
  "requiredEthnicities": ["string"],
  "requiredGenders": ["string"],
  "firstGenerationRequired": boolean,
  "minGraduationYear": number,
  "maxGraduationYear": number,
  "awardAmount": number,
  "isRenewable": boolean,
  "numberOfAwards": number,
  "deadline": "datetime",
  "requiresEssay": boolean,
  "requiresRecommendation": boolean,
  "applicationUrl": "string",
  "naturalLanguageSummary": "string"
}

Response: 201 Created
{
  "id": "guid",
  "entityId": "string",
  ...
}
```

#### Get Grant
```http
GET /Grants/{id}

Response: 200 OK
```

#### List Grants
```http
GET /Grants

Response: 200 OK
[
  {
    "id": "guid",
    "name": "string",
    ...
  }
]
```

#### Import Grants (Admin)
```http
POST /admin/Grants/import
Content-Type: application/json

[
  {
    "name": "string",
    ...
  }
]

Response: 200 OK
{
  "imported": number,
  "total": number,
  "errors": ["string"]
}
```

### Matching

#### Search Grants
```http
POST /matches/search
Content-Type: application/json

{
  "NonprofitId": "guid",
  "query": "string (optional)",
  "minAwardAmount": number,
  "maxAwardAmount": number,
  "deadlineAfter": "datetime",
  "deadlineBefore": "datetime",
  "requiresEssay": boolean,
  "limit": number,
  "offset": number,
  "minSimilarity": number
}

Response: 200 OK
{
  "matches": [
    {
      "GrantId": "guid",
      "Grant": { ... },
      "semanticSimilarity": number,
      "compositeScore": number,
      "breakdown": {
        "semanticScore": number,
        "awardAmountScore": number,
        "complexityScore": number,
        "deadlineProximityScore": number
      },
      "meetsAllRequirements": boolean,
      "unmetRequirements": ["string"],
      "matchedAt": "datetime"
    }
  ],
  "totalCount": number,
  "metadata": {
    "processingTime": "timespan",
    "filteredGrants": number,
    "eligibleGrants": number,
    "searchStrategy": "string"
  }
}
```

### Conversation

#### Process Conversation
```http
POST /conversation
Content-Type: application/json

{
  "NonprofitId": "guid",
  "message": "string",
  "history": [
    {
      "role": "user|assistant",
      "content": "string",
      "timestamp": "datetime"
    }
  ]
}

Response: 200 OK
{
  "reply": "string",
  "updatedHistory": [ ... ],
  "extractedData": {
    "gpa": number,
    "major": "string",
    "extracurricularActivities": ["string"],
    "interests": ["string"],
    "careerGoals": ["string"],
    "profileSummary": "string"
  },
  "profileComplete": boolean
}
```

### Embeddings

#### Generate Embedding
```http
POST /embeddings/generate
Content-Type: application/json

{
  "text": "string"
}

Response: 200 OK
{
  "embedding": [number]
}
```

## Error Responses

```http
400 Bad Request
{
  "error": "Invalid request data"
}

404 Not Found
{
  "error": "Resource not found"
}

500 Internal Server Error
{
  "error": "An error occurred: ..."
}
```

## Rate Limiting

- No rate limiting in development
- Production: TBD (Azure API Management)

## Pagination

Use `limit` and `offset` parameters:
```http
POST /matches/search
{
  "limit": 20,
  "offset": 0
}
```

## Filtering

Search endpoint supports multiple filters:
- Award amount range
- Deadline range
- Essay requirement
- Minimum similarity threshold

Filters are combined with AND logic.

## Matching Algorithm

### Phase 1: Eligibility Filtering
Boolean filters applied at EntityMatchingAI layer:
- `minGpa <= Nonprofit.GPA`
- `requiredStates` contains `Nonprofit.State` (if specified)
- `eligibleMajors` contains `Nonprofit.Major` (if specified)
- Graduation year within range

### Phase 2: Vector Search
- Nonprofit profile summary is embedded
- Cosine similarity search against Grant embeddings
- Minimum threshold: 0.6
- Top 20 results

### Phase 3: Composite Ranking
```
score = (semantic × 0.6) + (award × 0.2) + (complexity × 0.1) + (deadline × 0.1)
```

Where:
- `semantic`: Cosine similarity (0-1)
- `award`: Normalized award amount (0-1, max $50k)
- `complexity`: 1.0 - (0.3 if essay) - (0.3 if recommendation)
- `deadline`: 0.5 if < 30 days, 1.0 otherwise

## Examples

### Create Profile
```bash
curl -X POST http://localhost:7071/api/profiles \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user123",
    "firstName": "Maria",
    "lastName": "Garcia",
    "email": "maria@example.com",
    "state": "California",
    "city": "Los Angeles",
    "ethnicity": "Hispanic",
    "gender": "Female",
    "firstGeneration": true,
    "gpa": 3.8,
    "major": "Computer Science",
    "graduationYear": 2027,
    "currentSchool": "UCLA",
    "extracurricularActivities": ["Coding Club", "Volunteer Tutor"],
    "interests": ["AI", "Web Development"],
    "careerGoals": ["Software Engineer"],
    "financialNeedLevel": "High"
  }'
```

### Search for Grants
```bash
curl -X POST http://localhost:7071/api/matches/search \
  -H "Content-Type: application/json" \
  -d '{
    "NonprofitId": "guid",
    "minAwardAmount": 1000,
    "deadlineAfter": "2026-02-01",
    "limit": 20,
    "minSimilarity": 0.7
  }'
```

### Conversational Profile Building
```bash
curl -X POST http://localhost:7071/api/conversation \
  -H "Content-Type: application/json" \
  -d '{
    "NonprofitId": "guid",
    "message": "I study computer science and I love building websites",
    "history": []
  }'
```

## Integration with EntityMatchingAI

### Store Grant
```
POST https://profilematching-apim.azure-api.net/api/v1/profiles
Headers:
  Ocp-Apim-Subscription-Key: YOUR_KEY

{
  "entityType": 3,
  "name": "Grant Name",
  "description": "Natural language summary",
  "attributes": {
    "minGpa": 3.5,
    "awardAmount": 5000,
    ...
  }
}
```

### Upload Embedding
```
POST https://profilematching-apim.azure-api.net/api/v1/profiles/{id}/embeddings/upload
Headers:
  Ocp-Apim-Subscription-Key: YOUR_KEY

{
  "embedding": [0.123, -0.456, ...],
  "embeddingModel": "text-embedding-3-small"
}
```

### Search
```
POST https://profilematching-apim.azure-api.net/api/v1/profiles/search
Headers:
  Ocp-Apim-Subscription-Key: YOUR_KEY

{
  "query": "Nonprofit profile summary",
  "attributeFilters": {
    "logicalOperator": "And",
    "filters": [
      {
        "fieldPath": "attributes.minGpa",
        "operator": "LessThanOrEqual",
        "value": 3.8
      }
    ]
  },
  "minSimilarity": 0.6,
  "limit": 20
}
```
