# PowerShell script to initialize Cosmos DB with optimized settings
# Requires: Az.CosmosDB PowerShell module

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory=$true)]
    [string]$AccountName,

    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

# Import configuration
$configPath = Join-Path $PSScriptRoot "cosmos-db-optimization.json"
$config = Get-Content $configPath | ConvertFrom-Json

Write-Host "Initializing Cosmos DB: $DatabaseName in $AccountName" -ForegroundColor Cyan

# Create database if it doesn't exist
Write-Host "`nCreating database..." -ForegroundColor Yellow
if (-not $DryRun) {
    try {
        $database = Get-AzCosmosDBSqlDatabase -ResourceGroupName $ResourceGroupName `
            -AccountName $AccountName -Name $DatabaseName -ErrorAction SilentlyContinue

        if (-not $database) {
            New-AzCosmosDBSqlDatabase -ResourceGroupName $ResourceGroupName `
                -AccountName $AccountName -Name $DatabaseName
            Write-Host "Database created successfully" -ForegroundColor Green
        } else {
            Write-Host "Database already exists" -ForegroundColor Green
        }
    } catch {
        Write-Error "Failed to create database: $_"
        exit 1
    }
} else {
    Write-Host "[DRY RUN] Would create database: $DatabaseName" -ForegroundColor Gray
}

# Create containers with optimized settings
foreach ($containerName in $config.containers.PSObject.Properties.Name) {
    $containerConfig = $config.containers.$containerName

    Write-Host "`nConfiguring container: $containerName" -ForegroundColor Yellow
    Write-Host "  Partition Key: $($containerConfig.partitionKey.path)" -ForegroundColor Gray
    Write-Host "  Throughput: $($containerConfig.throughput.mode)" -ForegroundColor Gray

    if (-not $DryRun) {
        try {
            # Check if container exists
            $container = Get-AzCosmosDBSqlContainer -ResourceGroupName $ResourceGroupName `
                -AccountName $AccountName -DatabaseName $DatabaseName `
                -Name $containerName -ErrorAction SilentlyContinue

            if (-not $container) {
                # Create indexing policy
                $indexingPolicy = New-AzCosmosDBSqlIndexingPolicy `
                    -IncludedPath $containerConfig.indexingPolicy.includedPaths `
                    -ExcludedPath $containerConfig.indexingPolicy.excludedPaths

                # Add composite indexes if defined
                if ($containerConfig.indexingPolicy.compositeIndexes) {
                    foreach ($compositeIndex in $containerConfig.indexingPolicy.compositeIndexes) {
                        $paths = @()
                        foreach ($pathConfig in $compositeIndex) {
                            $paths += New-AzCosmosDBSqlCompositePath `
                                -Path $pathConfig.path -Order $pathConfig.order
                        }
                        $indexingPolicy = Add-AzCosmosDBSqlCompositePathToIndexingPolicy `
                            -IndexingPolicy $indexingPolicy -CompositePath $paths
                    }
                }

                # Create container
                $params = @{
                    ResourceGroupName = $ResourceGroupName
                    AccountName = $AccountName
                    DatabaseName = $DatabaseName
                    Name = $containerName
                    PartitionKeyPath = $containerConfig.partitionKey.path
                    PartitionKeyKind = $containerConfig.partitionKey.kind
                    IndexingPolicy = $indexingPolicy
                }

                # Add throughput settings
                if ($containerConfig.throughput.mode -eq "autoscale") {
                    $params.AutoscaleMaxThroughput = $containerConfig.throughput.maxRU
                } else {
                    $params.Throughput = $containerConfig.throughput.RU
                }

                # Add TTL if configured
                if ($containerConfig.ttl.enabled) {
                    $params.TtlInSeconds = $containerConfig.ttl.defaultTTL
                }

                New-AzCosmosDBSqlContainer @params
                Write-Host "  Container created successfully" -ForegroundColor Green
            } else {
                Write-Host "  Container already exists" -ForegroundColor Green
                Write-Host "  To update indexing, use Update-AzCosmosDBSqlContainer" -ForegroundColor Yellow
            }
        } catch {
            Write-Error "Failed to create container $containerName: $_"
        }
    } else {
        Write-Host "[DRY RUN] Would create container: $containerName" -ForegroundColor Gray
    }
}

Write-Host "`nInitialization complete!" -ForegroundColor Green
Write-Host "`nRecommendations:" -ForegroundColor Cyan
foreach ($rec in $config.queryOptimizations.recommendations) {
    Write-Host "  - $rec" -ForegroundColor Gray
}

if ($DryRun) {
    Write-Host "`nThis was a dry run. Use without -DryRun to apply changes." -ForegroundColor Yellow
}
