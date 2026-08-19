# PowerShell Script for Automated EdCo Database Backup
# Target Container: edco-db (MSSQL 2022)

param(
    [string]$ContainerName = "edco-db",
    [string]$DatabaseName = "EdCoDb",
    [string]$BackupDir = "./backups",
    [int]$RetentionDays = 14
)

$ErrorActionPreference = "Stop"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupFileName = "$($DatabaseName)_backup_$Timestamp.bak"
$ContainerBackupPath = "/var/opt/mssql/data/$BackupFileName"

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Starting EdCo Database Backup..." -ForegroundColor Cyan
Write-Host "Timestamp: $Timestamp" -ForegroundColor Yellow
Write-Host "Database: $DatabaseName" -ForegroundColor Yellow
Write-Host "Container: $ContainerName" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Cyan

# Execute SQLCmd inside container to issue BACKUP DATABASE command
$SqlCommand = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$ContainerBackupPath' WITH NOFORMAT, NOINIT, NAME = '$DatabaseName-Full Backup', SKIP, NOUNLOAD, STATS = 10;"

try {
    Write-Host "Executing SQL Backup command in container..." -ForegroundColor Green
    docker exec $ContainerName /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $env:DB_PASSWORD -Q $SqlCommand

    # Copy backup file out of container to host backup directory
    $HostBackupFile = Join-Path $BackupDir $BackupFileName
    Write-Host "Copying backup from container to host: $HostBackupFile..." -ForegroundColor Green
    docker cp "$($ContainerName):$ContainerBackupPath" $HostBackupFile

    # Clean up backup file inside container
    docker exec $ContainerName rm -f $ContainerBackupPath

    # Verify backup file size
    $FileSize = (Get-Item $HostBackupFile).Length / 1MB
    Write-Host "Backup completed successfully! Size: $([math]::Round($FileSize, 2)) MB" -ForegroundColor Green

    # Cleanup old backups exceeding retention period
    Write-Host "Cleaning up backups older than $RetentionDays days..." -ForegroundColor Yellow
    Get-ChildItem -Path $BackupDir -Filter "$($DatabaseName)_backup_*.bak" | 
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } | 
        Remove-Item -Force -Verbose

    Write-Host "Backup operation finished successfully." -ForegroundColor Cyan
}
catch {
    Write-Host "Error during database backup: $_" -ForegroundColor Red
    exit 1
}
