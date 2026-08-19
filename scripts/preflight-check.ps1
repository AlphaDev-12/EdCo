# PowerShell Script for EdCo Pre-Flight Production Deployment Audit

param(
    [string]$EnvFile = ".env.production",
    [bool]$SkipTests = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " EdCo Production Pre-Flight Audit Tool   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$Passed = $true

# 1. Verify .NET SDK & Runtime
try {
    $DotnetVersion = & dotnet --version
    Write-Host "[PASS] .NET SDK installed: v$DotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] .NET SDK is not available in PATH." -ForegroundColor Red
    $Passed = $false
}

# 2. Verify Docker Engine
try {
    $DockerVersion = & docker --version
    Write-Host "[PASS] Docker Engine available: $DockerVersion" -ForegroundColor Green
} catch {
    Write-Host "[WARN] Docker Engine not detected locally (required on VPS target)." -ForegroundColor Yellow
}

# 3. Environment Secrets Audit
if (Test-Path $EnvFile) {
    Write-Host "[PASS] Environment file '$EnvFile' found." -ForegroundColor Green
} else {
    Write-Host "[WARN] Environment file '$EnvFile' not found locally (will rely on OS/Container env vars)." -ForegroundColor Yellow
}

# Check JWT Secret length requirement (Min 32 chars)
$JwtSecret = $env:JWT__Secret
if (-not [string]::IsNullOrEmpty($JwtSecret)) {
    if ($JwtSecret.Length -ge 32) {
        Write-Host "[PASS] JWT Secret is configured with strong length ($($JwtSecret.Length) chars)." -ForegroundColor Green
    } else {
        Write-Host "[FAIL] JWT Secret must be at least 32 characters long! Current length: $($JwtSecret.Length)" -ForegroundColor Red
        $Passed = $false
    }
} else {
    Write-Host "[INFO] JWT Secret environment variable unset (will be injected via docker-compose / VPS secrets)." -ForegroundColor Cyan
}

# 4. Automated Test Suite Audit
if (-not $SkipTests) {
    Write-Host "Executing xUnit Test Suite..." -ForegroundColor Yellow
    try {
        $TestResult = & dotnet test EdCo.Tests/EdCo.Tests.csproj --no-restore --logger "console;verbosity=quiet"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[PASS] All solution unit & integration tests passed." -ForegroundColor Green
        } else {
            Write-Host "[FAIL] One or more xUnit tests failed." -ForegroundColor Red
            $Passed = $false
        }
    } catch {
        Write-Host "[FAIL] Failed to execute dotnet test suite." -ForegroundColor Red
        $Passed = $false
    }
}

Write-Host "=========================================" -ForegroundColor Cyan
if ($Passed) {
    Write-Host " PRE-FLIGHT AUDIT PASSED! READY FOR VPS DEPLOYMENT." -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host " PRE-FLIGHT AUDIT FAILED! RESOLVE WARNINGS/ERRORS." -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Cyan
    exit 1
}
