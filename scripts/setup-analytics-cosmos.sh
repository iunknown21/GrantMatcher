#!/bin/bash

# Setup script for Cosmos DB Analytics containers
# This script creates the necessary containers for the analytics system

# Configuration
RESOURCE_GROUP="ScholarshipMatcher-RG"
ACCOUNT_NAME="scholarshipmatcher-cosmos"
DATABASE_NAME="ScholarshipMatcher"

echo "Setting up Analytics Cosmos DB containers..."
echo "Resource Group: $RESOURCE_GROUP"
echo "Cosmos Account: $ACCOUNT_NAME"
echo "Database: $DATABASE_NAME"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI is not installed. Please install it first."
    echo "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in
echo "Checking Azure login status..."
if ! az account show &> /dev/null; then
    echo "Not logged in to Azure. Please login..."
    az login
fi

echo "Creating AnalyticsEvents container..."
az cosmosdb sql container create \
    --resource-group $RESOURCE_GROUP \
    --account-name $ACCOUNT_NAME \
    --database-name $DATABASE_NAME \
    --name AnalyticsEvents \
    --partition-key-path "/partitionKey" \
    --throughput 400 \
    --idx @analytics-events-indexing.json

echo ""
echo "Creating UserSessions container..."
az cosmosdb sql container create \
    --resource-group $RESOURCE_GROUP \
    --account-name $ACCOUNT_NAME \
    --database-name $DATABASE_NAME \
    --name UserSessions \
    --partition-key-path "/partitionKey" \
    --throughput 400

echo ""
echo "Creating AnalyticsMetrics container..."
az cosmosdb sql container create \
    --resource-group $RESOURCE_GROUP \
    --account-name $ACCOUNT_NAME \
    --database-name $DATABASE_NAME \
    --name AnalyticsMetrics \
    --partition-key-path "/partitionKey" \
    --throughput 400

echo ""
echo "Analytics containers created successfully!"
echo ""
echo "Container Details:"
echo "- AnalyticsEvents: Stores all user events and interactions"
echo "- UserSessions: Tracks user session data"
echo "- AnalyticsMetrics: Stores calculated metrics and aggregations"
echo ""
echo "Next steps:"
echo "1. Update your local.settings.json with Cosmos DB connection string"
echo "2. Deploy Azure Functions to enable analytics endpoints"
echo "3. Navigate to /admin/analytics to view the dashboard"
