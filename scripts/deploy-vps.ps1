# PowerShell Script: Windows VPS Production Deployment & Health Verification
[CmdletBinding()]
param (
    [string]$AppDir = "C:\EdCo\Deployments",
    [string]$Registry = "ghcr.io",
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Header
Write-Host "    EdCo Platform - Managed Windows VPS Deployer           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Header

# 1. Verify Docker Service is running on Windows Host
Write-Host "[1/5] Validating Docker Daemon status on Windows Host..." -ForegroundColor Yellow
try {
    $dockerInfo = docker info 2>&1
    Write-Host "[OK] Docker Engine is running and responsive." -ForegroundColor Green
} catch {
    Write-Error "[FATAL] Docker Engine is not running on this host. Please start Docker Desktop / Docker Service."
    exit 1
}

# 2. Set Working Directory
if (-not (Test-Path $AppDir)) {
    Write-Host "[INFO] Creating deployment directory: $AppDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $AppDir -Force | Out-Null
}
Set-Location $AppDir

# 3. Apply Automated EF Core Database Migrations
Write-Host "[2/5] Running Automated EF Core Database Migrations..." -ForegroundColor Yellow
$migrationScript = Join-Path $AppDir "scripts\run-migrations.ps1"
if (Test-Path $migrationScript) {
    & $migrationScript
} else {
    Write-Host "[NOTICE] Migration script not found in deployment folder; skipping inline EF migration step." -ForegroundColor Yellow
}

# 4. Pull Latest Container Images & Restart Compose Services
Write-Host "[3/5] Pulling latest production container images from registry..." -ForegroundColor Yellow
docker compose pull

Write-Host "[4/5] Starting production container stack..." -ForegroundColor Yellow
docker compose up -d --remove-orphans

# 5. Perform Post-Deployment Health Check Verification
Write-Host "[5/5] Performing post-deployment container health verification..." -ForegroundColor Yellow

$endpoints = @(
    @{ Name = "EdCo API Direct"; URL = "http://localhost:5000/healthz" },
    @{ Name = "EdCo AdminPortal Direct"; URL = "http://localhost:5001/healthz" }
)

$allHealthy = $true

foreach ($endpoint in $endpoints) {
    Write-Host "Checking health of $($endpoint.Name) at $($endpoint.URL)..." -NoNewline
    $startTime = Get-Date
    $healthy = $false

    while (((Get-Date) - $startTime).TotalSeconds -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint.URL -UseBasicParsing -TimeoutSec 3 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch {
            # Retry until timeout
        }
        Start-Sleep -Seconds 3
    }

    if ($healthy) {
        Write-Host " [HEALTHY 200 OK]" -ForegroundColor Green
    } else {
        Write-Host " [FAILED / TIMEOUT]" -ForegroundColor Red
        $allHealthy = $false
    }
}

if ($allHealthy) {
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " [SUCCESS] EdCo Platform deployed and healthy on VPS!     " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    exit 0
} else {
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host " [WARNING] One or more services failed health validation! " -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
    exit 1
}
