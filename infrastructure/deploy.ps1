# Azure Deployment Script for ScholarshipMatcher
# This script deploys all Azure resources using Bicep

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory=$true)]
    [string]$Location = 'eastus',

    [Parameter(Mandatory=$true)]
    [string]$OpenAIApiKey,

    [Parameter(Mandatory=$true)]
    [string]$EntityMatchingApiKey
)

Write-Host "🚀 Starting ScholarshipMatcher Azure Deployment" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "Location: $Location" -ForegroundColor Yellow
Write-Host ""

# Check if logged into Azure
Write-Host "Checking Azure login status..." -ForegroundColor Cyan
$azContext = Get-AzContext
if (!$azContext) {
    Write-Host "Not logged into Azure. Please login..." -ForegroundColor Yellow
    Connect-AzAccount
}

Write-Host "✓ Logged in as: $($azContext.Account.Id)" -ForegroundColor Green
Write-Host ""

# Create Resource Group if it doesn't exist
Write-Host "Checking for resource group..." -ForegroundColor Cyan
$rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (!$rg) {
    Write-Host "Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
    Write-Host "✓ Resource group created" -ForegroundColor Green
} else {
    Write-Host "✓ Resource group exists" -ForegroundColor Green
}
Write-Host ""

# Deploy Bicep template
Write-Host "Deploying Bicep template..." -ForegroundColor Cyan
Write-Host "This may take 5-10 minutes..." -ForegroundColor Yellow

$deploymentName = "scholarshipmatcher-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

try {
    $deployment = New-AzResourceGroupDeployment `
        -Name $deploymentName `
        -ResourceGroupName $ResourceGroupName `
        -TemplateFile "./main.bicep" `
        -environment $Environment `
        -location $Location `
        -baseName "scholarshipmatcher" `
        -openAIApiKey (ConvertTo-SecureString -String $OpenAIApiKey -AsPlainText -Force) `
        -entityMatchingApiKey (ConvertTo-SecureString -String $EntityMatchingApiKey -AsPlainText -Force) `
        -Verbose

    Write-Host ""
    Write-Host "✓ Deployment completed successfully!" -ForegroundColor Green
    Write-Host ""

    # Display outputs
    Write-Host "📋 Deployment Outputs:" -ForegroundColor Cyan
    Write-Host "===========================================" -ForegroundColor Gray
    Write-Host "Function App URL: " -NoNewline
    Write-Host $deployment.Outputs.functionAppUrl.Value -ForegroundColor Yellow
    Write-Host "Static Web App URL: " -NoNewline
    Write-Host $deployment.Outputs.staticWebAppUrl.Value -ForegroundColor Yellow
    Write-Host "Cosmos DB Account: " -NoNewline
    Write-Host $deployment.Outputs.cosmosAccountName.Value -ForegroundColor Yellow
    Write-Host "Key Vault: " -NoNewline
    Write-Host $deployment.Outputs.keyVaultName.Value -ForegroundColor Yellow
    Write-Host "===========================================" -ForegroundColor Gray
    Write-Host ""

    # Save outputs to file
    $outputsFile = "./deployment-outputs-$Environment.json"
    $deployment.Outputs | ConvertTo-Json | Out-File $outputsFile
    Write-Host "✓ Outputs saved to: $outputsFile" -ForegroundColor Green
    Write-Host ""

    # Next steps
    Write-Host "📝 Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Update GitHub repository settings with secrets:" -ForegroundColor White
    Write-Host "   - Get Function App publish profile from Azure Portal" -ForegroundColor Gray
    Write-Host "   - Get Static Web App deployment token from Azure Portal" -ForegroundColor Gray
    Write-Host "2. Configure GitHub Actions workflows" -ForegroundColor White
    Write-Host "3. Push code to trigger CI/CD pipeline" -ForegroundColor White
    Write-Host "4. Seed scholarship data using admin endpoint" -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "❌ Deployment failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "🎉 Deployment complete!" -ForegroundColor Green
