# PowerShell script to compile and run the scholarship generator
# Usage: .\RunScholarshipGenerator.ps1 [-Count 150] [-OutputPath "path/to/output.json"]

param(
    [int]$Count = 150,
    [string]$OutputPath = "",
    [switch]$NoSamples
)

Write-Host "=== Scholarship Data Generator Runner ===" -ForegroundColor Cyan
Write-Host ""

# Get the script directory and project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$functionsProject = Join-Path $projectRoot "ScholarshipMatcher.Functions.csproj"

Write-Host "Building project..." -ForegroundColor Yellow
dotnet build $functionsProject --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Build the dotnet run command
$binPath = Join-Path $projectRoot "bin\Release\net9.0\ScholarshipMatcher.Functions.dll"
$args = @("--count", $Count)

if ($OutputPath) {
    $args += "--output"
    $args += $OutputPath
}

if ($NoSamples) {
    $args += "--no-samples"
}

Write-Host "Running generator with arguments: $args" -ForegroundColor Yellow
Write-Host ""

# Note: This would need the script to be set as the entry point
# For now, we'll use it as reference for how to invoke
Write-Host "To run the generator, you can either:" -ForegroundColor Cyan
Write-Host "1. Call the Azure Function endpoint: POST /admin/seed/generate" -ForegroundColor White
Write-Host "2. Call the save-to-json endpoint: POST /admin/seed/save-json" -ForegroundColor White
Write-Host ""
Write-Host "Or use the Azure Functions Core Tools:" -ForegroundColor Cyan
Write-Host "  func start" -ForegroundColor White
Write-Host "  curl -X POST http://localhost:7071/admin/seed/save-json -d '{\"count\": $Count}'" -ForegroundColor White
