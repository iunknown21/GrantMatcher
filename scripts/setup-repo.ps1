# Setup Script for ScholarshipMatcher GitHub Repository
# This script initializes git, creates .gitignore, and prepares for first commit

param(
    [Parameter(Mandatory=$false)]
    [string]$GitHubUsername,

    [Parameter(Mandatory=$false)]
    [string]$RepositoryName = "ScholarshipMatcher"
)

Write-Host "🚀 ScholarshipMatcher Repository Setup" -ForegroundColor Cyan
Write-Host ""

# Check if git is installed
if (!(Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Git is not installed. Please install Git first." -ForegroundColor Red
    Write-Host "Download from: https://git-scm.com/downloads" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Git is installed" -ForegroundColor Green

# Navigate to project root
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

Write-Host "Project directory: $projectRoot" -ForegroundColor Yellow
Write-Host ""

# Check if .git already exists
if (Test-Path ".git") {
    Write-Host "⚠ Git repository already initialized" -ForegroundColor Yellow
    $continue = Read-Host "Do you want to continue? (y/n)"
    if ($continue -ne "y") {
        Write-Host "Exiting..." -ForegroundColor Yellow
        exit 0
    }
} else {
    # Initialize git repository
    Write-Host "Initializing git repository..." -ForegroundColor Cyan
    git init
    Write-Host "✓ Git repository initialized" -ForegroundColor Green
}

# Configure git user if not set
$gitUserName = git config user.name
if ([string]::IsNullOrEmpty($gitUserName)) {
    $userName = Read-Host "Enter your Git user name"
    git config user.name $userName
    Write-Host "✓ Git user name set" -ForegroundColor Green
}

$gitUserEmail = git config user.email
if ([string]::IsNullOrEmpty($gitUserEmail)) {
    $userEmail = Read-Host "Enter your Git email"
    git config user.email $userEmail
    Write-Host "✓ Git email set" -ForegroundColor Green
}

Write-Host ""

# Create initial commit
Write-Host "Preparing initial commit..." -ForegroundColor Cyan

# Stage all files
Write-Host "Staging files..." -ForegroundColor Yellow
git add .

# Check if there are changes to commit
$status = git status --porcelain
if ([string]::IsNullOrEmpty($status)) {
    Write-Host "⚠ No changes to commit" -ForegroundColor Yellow
} else {
    # Create commit
    Write-Host "Creating initial commit..." -ForegroundColor Yellow
    git commit -m "Initial commit: ScholarshipMatcher application with Azure deployment"
    Write-Host "✓ Initial commit created" -ForegroundColor Green
}

# Rename branch to main if needed
$currentBranch = git branch --show-current
if ($currentBranch -ne "main") {
    Write-Host "Renaming branch to 'main'..." -ForegroundColor Yellow
    git branch -M main
    Write-Host "✓ Branch renamed to main" -ForegroundColor Green
}

Write-Host ""

# GitHub repository setup
if ([string]::IsNullOrEmpty($GitHubUsername)) {
    Write-Host "📝 Next Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Create a new repository on GitHub:" -ForegroundColor White
    Write-Host "   https://github.com/new" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Add the remote origin:" -ForegroundColor White
    Write-Host "   git remote add origin https://github.com/YOUR_USERNAME/$RepositoryName.git" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Push to GitHub:" -ForegroundColor White
    Write-Host "   git push -u origin main" -ForegroundColor Gray
    Write-Host ""
} else {
    $remoteUrl = "https://github.com/$GitHubUsername/$RepositoryName.git"

    # Check if remote already exists
    $existingRemote = git remote get-url origin 2>$null
    if ($existingRemote) {
        Write-Host "⚠ Remote 'origin' already exists: $existingRemote" -ForegroundColor Yellow
        $updateRemote = Read-Host "Do you want to update it to $remoteUrl? (y/n)"
        if ($updateRemote -eq "y") {
            git remote set-url origin $remoteUrl
            Write-Host "✓ Remote updated" -ForegroundColor Green
        }
    } else {
        Write-Host "Adding remote origin..." -ForegroundColor Yellow
        git remote add origin $remoteUrl
        Write-Host "✓ Remote added: $remoteUrl" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "📝 Next Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Make sure you've created the repository on GitHub:" -ForegroundColor White
    Write-Host "   https://github.com/new" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Push to GitHub:" -ForegroundColor White
    Write-Host "   git push -u origin main" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "4. After pushing, follow the deployment guide:" -ForegroundColor White
Write-Host "   See docs/DEPLOYMENT.md" -ForegroundColor Gray
Write-Host ""

# Check if GitHub CLI is installed
if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host "💡 Tip: You have GitHub CLI installed!" -ForegroundColor Cyan
    Write-Host "You can create and push the repository with one command:" -ForegroundColor Yellow
    Write-Host "   gh repo create $RepositoryName --public --source=. --push" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "🎉 Repository setup complete!" -ForegroundColor Green
