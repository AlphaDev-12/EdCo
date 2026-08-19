# PowerShell Script: Execute EF Core Database Migrations
[CmdletBinding()]
param (
    [string]$ConnectionString = $env:ConnectionStrings__DefaultConnection,
    [string]$BundlePath = ".\efbundle.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       EdCo Platform - Database Migration Executor        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Host "[WARNING] No connection string passed or found in environment variables." -ForegroundColor Yellow
    Write-Host "[INFO] Attempting execution with default appsettings connection..." -ForegroundColor Yellow
}

# Option 1: Execute pre-compiled EF Migration Bundle (Production VPS)
if (Test-Path $BundlePath) {
    Write-Host "[INFO] Found pre-compiled EF Migration Bundle: $BundlePath" -ForegroundColor Green
    Write-Host "[INFO] Running migration bundle against target SQL Server..." -ForegroundColor Green
    
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        & $BundlePath --connection "$ConnectionString" --verbose
    } else {
        & $BundlePath --verbose
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] EF Core Database Migrations applied successfully via bundle!" -ForegroundColor Green
        exit 0
    } else {
        Write-Error "[ERROR] Migration bundle execution failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

# Option 2: Fallback to dotnet ef CLI command
Write-Host "[INFO] EF Bundle not found. Checking for dotnet-ef tool..." -ForegroundColor Yellow

try {
    $dotnetEfCheck = dotnet ef --version
    Write-Host "[INFO] Found dotnet-ef version: $dotnetEfCheck" -ForegroundColor Green
} catch {
    Write-Host "[INFO] Installing dotnet-ef CLI tool..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

Write-Host "[INFO] Running dotnet ef database update..." -ForegroundColor Green
dotnet ef database update --project "..\EdCo.Core\EdCo.Core.csproj" --startup-project "..\EdCo.API\EdCo.API.csproj"

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] EF Core Database Migrations applied successfully via dotnet ef CLI!" -ForegroundColor Green
} else {
    Write-Error "[ERROR] dotnet ef database update failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
