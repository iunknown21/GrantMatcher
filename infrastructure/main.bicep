@description('The environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('The Azure region for all resources')
param location string = resourceGroup().location

@description('The base name for all resources')
param baseName string = 'scholarshipmatcher'

@description('OpenAI API Key')
@secure()
param openAIApiKey string

@description('EntityMatchingAI API Key')
@secure()
param entityMatchingApiKey string

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var appName = '${baseName}-${environment}'
var cosmosAccountName = '${appName}-cosmos-${uniqueSuffix}'
var functionAppName = '${appName}-func-${uniqueSuffix}'
var staticWebAppName = '${appName}-web-${uniqueSuffix}'
var storageAccountName = '${replace(appName, '-', '')}st${uniqueSuffix}'
var appInsightsName = '${appName}-insights'
var keyVaultName = '${appName}-kv-${uniqueSuffix}'
var logAnalyticsName = '${appName}-logs'

// Cosmos DB Account
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    enableFreeTier: environment == 'dev'
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'ScholarshipMatcher'
  properties: {
    resource: {
      id: 'ScholarshipMatcher'
    }
  }
}

// Cosmos DB Containers
resource profilesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'Profiles'
  properties: {
    resource: {
      id: 'Profiles'
      partitionKey: {
        paths: ['/userId']
        kind: 'Hash'
      }
    }
  }
}

resource scholarshipsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'Scholarships'
  properties: {
    resource: {
      id: 'Scholarships'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
  }
}

resource savedScholarshipsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'SavedScholarships'
  properties: {
    resource: {
      id: 'SavedScholarships'
      partitionKey: {
        paths: ['/studentId']
        kind: 'Hash'
      }
    }
  }
}

// Storage Account for Functions
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Store secrets in Key Vault
resource openAISecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'OpenAI-ApiKey'
  properties: {
    value: openAIApiKey
  }
}

resource entityMatchingSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'EntityMatchingAI-ApiKey'
  properties: {
    value: entityMatchingApiKey
  }
}

resource cosmosConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'CosmosDb-ConnectionString'
  properties: {
    value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

// App Service Plan (Consumption for Functions)
resource functionAppPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionAppPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'CosmosDb__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${cosmosConnectionStringSecret.properties.secretUri})'
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'ScholarshipMatcher'
        }
        {
          name: 'EntityMatchingAI__BaseUrl'
          value: 'https://profilematching-apim.azure-api.net/api/v1'
        }
        {
          name: 'EntityMatchingAI__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${entityMatchingSecret.properties.secretUri})'
        }
        {
          name: 'OpenAI__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${openAISecret.properties.secretUri})'
        }
        {
          name: 'OpenAI__EmbeddingModel'
          value: 'text-embedding-3-small'
        }
        {
          name: 'OpenAI__ChatModel'
          value: 'gpt-4o-mini'
        }
      ]
      cors: {
        allowedOrigins: [
          'https://${staticWebAppName}.azurestaticapps.net'
          environment == 'dev' ? 'http://localhost:5000' : ''
        ]
        supportCredentials: false
      }
    }
  }
}

// Grant Function App access to Key Vault
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Static Web App (Blazor)
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: 'https://github.com/YOUR_USERNAME/ScholarshipMatcher'
    branch: 'main'
    buildProperties: {
      appLocation: 'src/ScholarshipMatcher.Client'
      apiLocation: ''
      outputLocation: 'wwwroot'
    }
  }
}

// Static Web App Config
resource staticWebAppConfig 'Microsoft.Web/staticSites/config@2023-01-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    ApiBaseUrl: 'https://${functionApp.properties.defaultHostName}/api'
  }
}

// Outputs
output cosmosAccountName string = cosmosAccount.name
output cosmosConnectionString string = cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output keyVaultName string = keyVault.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
