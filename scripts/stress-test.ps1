# ==============================================================================
# EdCo Local Concurrency & Load Stress Test Script
# ==============================================================================
# Simulates 100 concurrent requests against local EdCo API endpoints to measure
# API throughput, latency distribution, rate-limiter enforcement, and resilience.
# ==============================================================================

param(
    [string]$TargetUrl = "http://localhost:5075/healthz",
    [int]$TotalRequests = 100,
    [int]$Concurrency = 10
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " EdCo Platform API Stress & Concurrency Benchmark" -ForegroundColor Cyan
Write-Host " Target URL: $TargetUrl" -ForegroundColor Yellow
Write-Host " Total Requests: $TotalRequests | Concurrency Threads: $Concurrency" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$startTime = [System.Diagnostics.Stopwatch]::StartNew()
$results = [System.Collections.Concurrent.ConcurrentBag[PSCustomObject]]::new()

$batchSize = [Math]::Ceiling($TotalRequests / $Concurrency)
$jobs = 1..$Concurrency | ForEach-Object {
    Start-ThreadJob -ScriptBlock {
        param($Url, $Count)
        $threadResults = @()
        for ($i = 0; $i -lt $Count; $i++) {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $statusCode = 0
            try {
                $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
                $statusCode = [int]$response.StatusCode
            } catch {
                if ($_.Exception.Response) {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                } else {
                    $statusCode = 500
                }
            }
            $sw.Stop()
            $threadResults += [PSCustomObject]@{
                StatusCode = $statusCode
                LatencyMs  = $sw.ElapsedMilliseconds
            }
        }
        return $threadResults
    } -ArgumentList $TargetUrl, $batchSize
}

$jobs | Wait-Job | ForEach-Object {
    $res = Receive-Job -Job $_
    foreach ($r in $res) {
        $results.Add($r)
    }
}
$jobs | Remove-Job

$startTime.Stop()

$items = $results.ToArray()
$successCount = ($items | Where-Object { $_.StatusCode -eq 200 }).Count
$rateLimitedCount = ($items | Where-Object { $_.StatusCode -eq 429 }).Count
$errorCount = ($items | Where-Object { $_.StatusCode -ne 200 -and $_.StatusCode -ne 429 }).Count
$avgLatency = ($items | Measure-Object -Property LatencyMs -Average).Average
$maxLatency = ($items | Measure-Object -Property LatencyMs -Maximum).Maximum
$minLatency = ($items | Measure-Object -Property LatencyMs -Minimum).Minimum

Write-Host ""
Write-Host "--- Benchmark Summary ---" -ForegroundColor Green
Write-Host " Total Duration: $([Math]::Round($startTime.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor White
Write-Host " Total Requests Delivered: $($items.Count)" -ForegroundColor White
Write-Host " Successful (200 OK): $successCount" -ForegroundColor Green
Write-Host " Rate Limited (429 Too Many Requests): $rateLimitedCount" -ForegroundColor Yellow
Write-Host " Server Errors (5xx / Connection Error): $errorCount" -ForegroundColor Red
Write-Host " Average Response Time: $([Math]::Round($avgLatency, 2)) ms" -ForegroundColor Cyan
Write-Host " Min / Max Response Time: $minLatency ms / $maxLatency ms" -ForegroundColor Cyan
Write-Host " Throughput: $([Math]::Round($items.Count / $startTime.Elapsed.TotalSeconds, 2)) req/sec" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Cyan
