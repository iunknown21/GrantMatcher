#!/bin/bash

# Azure Deployment Script for ScholarshipMatcher
# This script deploys all Azure resources using Bicep

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${CYAN}$1${NC}"
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_warning() {
    echo -e "${YELLOW}$1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
}

# Check arguments
if [ "$#" -lt 5 ]; then
    print_error "Usage: $0 <environment> <resource-group> <location> <openai-api-key> <entitymatching-api-key>"
    print_warning "Example: $0 dev scholarshipmatcher-dev-rg eastus sk-xxxxx xxxxx"
    exit 1
fi

ENVIRONMENT=$1
RESOURCE_GROUP=$2
LOCATION=$3
OPENAI_API_KEY=$4
ENTITYMATCHING_API_KEY=$5

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|prod)$ ]]; then
    print_error "Environment must be: dev, staging, or prod"
    exit 1
fi

print_info "🚀 Starting ScholarshipMatcher Azure Deployment"
print_warning "Environment: $ENVIRONMENT"
print_warning "Resource Group: $RESOURCE_GROUP"
print_warning "Location: $LOCATION"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Check if logged into Azure
print_info "Checking Azure login status..."
if ! az account show &> /dev/null; then
    print_warning "Not logged into Azure. Please login..."
    az login
fi

ACCOUNT=$(az account show --query user.name -o tsv)
print_success "✓ Logged in as: $ACCOUNT"
echo ""

# Create Resource Group if it doesn't exist
print_info "Checking for resource group..."
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    print_warning "Creating resource group: $RESOURCE_GROUP"
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
    print_success "✓ Resource group created"
else
    print_success "✓ Resource group exists"
fi
echo ""

# Deploy Bicep template
print_info "Deploying Bicep template..."
print_warning "This may take 5-10 minutes..."
echo ""

DEPLOYMENT_NAME="scholarshipmatcher-$ENVIRONMENT-$(date +%Y%m%d-%H%M%S)"

if az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "./main.bicep" \
    --parameters environment="$ENVIRONMENT" \
                 location="$LOCATION" \
                 baseName="scholarshipmatcher" \
                 openAIApiKey="$OPENAI_API_KEY" \
                 entityMatchingApiKey="$ENTITYMATCHING_API_KEY"; then

    echo ""
    print_success "✓ Deployment completed successfully!"
    echo ""

    # Display outputs
    print_info "📋 Deployment Outputs:"
    echo "==========================================="

    FUNCTION_URL=$(az deployment group show \
        --name "$DEPLOYMENT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query properties.outputs.functionAppUrl.value -o tsv)

    STATIC_URL=$(az deployment group show \
        --name "$DEPLOYMENT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query properties.outputs.staticWebAppUrl.value -o tsv)

    COSMOS_NAME=$(az deployment group show \
        --name "$DEPLOYMENT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query properties.outputs.cosmosAccountName.value -o tsv)

    KEYVAULT_NAME=$(az deployment group show \
        --name "$DEPLOYMENT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query properties.outputs.keyVaultName.value -o tsv)

    echo -e "Function App URL: ${YELLOW}$FUNCTION_URL${NC}"
    echo -e "Static Web App URL: ${YELLOW}$STATIC_URL${NC}"
    echo -e "Cosmos DB Account: ${YELLOW}$COSMOS_NAME${NC}"
    echo -e "Key Vault: ${YELLOW}$KEYVAULT_NAME${NC}"
    echo "==========================================="
    echo ""

    # Save outputs to file
    OUTPUTS_FILE="./deployment-outputs-$ENVIRONMENT.json"
    az deployment group show \
        --name "$DEPLOYMENT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query properties.outputs > "$OUTPUTS_FILE"

    print_success "✓ Outputs saved to: $OUTPUTS_FILE"
    echo ""

    # Next steps
    print_info "📝 Next Steps:"
    echo "1. Update GitHub repository settings with secrets:"
    echo "   - Get Function App publish profile from Azure Portal"
    echo "   - Get Static Web App deployment token from Azure Portal"
    echo "2. Configure GitHub Actions workflows"
    echo "3. Push code to trigger CI/CD pipeline"
    echo "4. Seed scholarship data using admin endpoint"
    echo ""

else
    echo ""
    print_error "❌ Deployment failed!"
    exit 1
fi

print_success "🎉 Deployment complete!"
