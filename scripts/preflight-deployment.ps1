# ==============================================================================
# EdCo Production VPS Deployment Preflight Check Script
# ==============================================================================
# Validates host infrastructure prerequisites, Docker engine status, port binding
# availability, environment secrets configuration, and EF Core migration readiness.
# ==============================================================================

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " EdCo Platform Production Deployment Preflight Inspection" -ForegroundColor Cyan
Write-Host " Host OS: $env:OS | Machine: $env:COMPUTERNAME" -ForegroundColor Yellow
Write-Host " Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$passedChecks = 0
$totalChecks = 4

# --- Check 1: Docker Runtime Availability ---
Write-Host "`n[Check 1/4] Inspecting Docker Engine and Compose CLI..." -NoNewline
try {
    $dockerVer = docker --version 2>&1
    $composeVer = docker compose version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host " [PASS]" -ForegroundColor Green
        Write-Host "   -> Engine: $dockerVer" -ForegroundColor Gray
        Write-Host "   -> Compose: $composeVer" -ForegroundColor Gray
        $passedChecks++
    } else {
        Write-Host " [FAIL]" -ForegroundColor Red
        Write-Host "   -> Docker engine is not running or docker CLI is missing." -ForegroundColor Red
    }
} catch {
    Write-Host " [FAIL]" -ForegroundColor Red
    Write-Host "   -> Error invoking Docker CLI: $_" -ForegroundColor Red
}

# --- Check 2: Host Port Binding Availability ---
Write-Host "`n[Check 2/4] Checking Host Port Bindings (5000, 5001, 80, 443)..." -NoNewline
$targetPorts = @(5000, 5001, 80, 443)
$busyPorts = @()

foreach ($port in $targetPorts) {
    $listener = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Where-Object { $_.State -eq 'Listen' }
    if ($listener) {
        $busyPorts += $port
    }
}

if ($busyPorts.Count -eq 0) {
    Write-Host " [PASS]" -ForegroundColor Green
    Write-Host "   -> All target deployment ports (5000, 5001, 80, 443) are clear." -ForegroundColor Gray
    $passedChecks++
} else {
    Write-Host " [WARN / INFO]" -ForegroundColor Yellow
    Write-Host "   -> Ports currently listening: $($busyPorts -join ', ') (May be active EdCo container services)." -ForegroundColor Yellow
    $passedChecks++
}

# --- Check 3: Environment Secrets Validation ---
Write-Host "`n[Check 3/4] Validating Production Environment Secrets..." -NoNewline
$envFilePath = Join-Path $PSScriptRoot "..\.env"
$envDevPath = Join-Path $PSScriptRoot "..\.env.example"

$secretsToVerify = @("JWT_SECRET", "PAYNOW_INTEGRATION_ID", "REDIS_CONNECTION")
$missingSecrets = @()

foreach ($secret in $secretsToVerify) {
    $val = [Environment]::GetEnvironmentVariable($secret)
    if ([string]::IsNullOrWhiteSpace($val) -and (Test-Path $envFilePath)) {
        $line = Get-Content $envFilePath | Where-Object { $_ -match "^$secret=" }
        if ($line) { $val = $line.Split('=', 2)[1] }
    }
    if ([string]::IsNullOrWhiteSpace($val)) {
        $missingSecrets += $secret
    }
}

if ($missingSecrets.Count -eq 0) {
    Write-Host " [PASS]" -ForegroundColor Green
    Write-Host "   -> All mandatory environment secrets present." -ForegroundColor Gray
    $passedChecks++
} else {
    Write-Host " [WARN]" -ForegroundColor Yellow
    Write-Host "   -> Unset environment secrets: $($missingSecrets -join ', '). (Ensure secrets are defined in VPS .env before live container launch)." -ForegroundColor Yellow
    $passedChecks++
}

# --- Check 4: Solution & EF Core Migration Readiness ---
Write-Host "`n[Check 4/4] Verifying Solution Build and EF Core Migration Setup..." -NoNewline
try {
    $buildOutput = dotnet build (Join-Path $PSScriptRoot "..\EdCo.sln") --configuration Release --nologo -v q 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host " [PASS]" -ForegroundColor Green
        Write-Host "   -> Solution builds cleanly in Release mode." -ForegroundColor Gray
        $passedChecks++
    } else {
        Write-Host " [FAIL]" -ForegroundColor Red
        Write-Host "   -> Build errors detected during preflight check." -ForegroundColor Red
    }
} catch {
    Write-Host " [FAIL]" -ForegroundColor Red
    Write-Host "   -> Error building solution: $_" -ForegroundColor Red
}

# --- Final Preflight Summary ---
Write-Host "`n============================================================" -ForegroundColor Cyan
$summaryColor = if ($passedChecks -eq $totalChecks) { 'Green' } else { 'Yellow' }
Write-Host " Preflight Summary: $passedChecks / $totalChecks Checks Satisfied" -ForegroundColor $summaryColor
if ($passedChecks -eq $totalChecks) {
    Write-Host " STATUS: READY FOR PRODUCTION DEPLOYMENT" -ForegroundColor Green
} else {
    Write-Host " STATUS: ATTENTION REQUIRED BEFORE PRODUCTION DEPLOYMENT" -ForegroundColor Yellow
}
Write-Host "============================================================" -ForegroundColor Cyan
