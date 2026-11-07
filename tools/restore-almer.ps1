param(
    [Parameter(Mandatory)][string]$BackupFile,
    [string]$ConnectionString,
    [string]$DataProtectionKeyPath,
    [Security.SecureString]$EncryptionPassword,
    [switch]$Apply,
    [string]$ConfirmDatabase
)
$ErrorActionPreference = 'Stop'; Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AlmerBackup.psm1') -Force
$root = Split-Path $PSScriptRoot -Parent
$settings = Get-Content -LiteralPath (Join-Path $root 'src/backend/Almer.Api/appsettings.json') -Raw | ConvertFrom-Json
if (-not $ConnectionString) { $ConnectionString = $env:ConnectionStrings__DefaultConnection }
if (-not $ConnectionString) { $ConnectionString = $settings.ConnectionStrings.DefaultConnection }
if (-not $DataProtectionKeyPath) { $DataProtectionKeyPath = $env:DataProtection__KeyPath }
if (-not $DataProtectionKeyPath) { $DataProtectionKeyPath = $settings.DataProtection.KeyPath }
if (-not [IO.Path]::IsPathRooted($DataProtectionKeyPath)) { $DataProtectionKeyPath = Join-Path $root $DataProtectionKeyPath }
if (-not $EncryptionPassword) { $EncryptionPassword = Read-Host 'Backup encryption password' -AsSecureString }
if (-not (Test-Path -LiteralPath $BackupFile -PathType Leaf)) { throw "Backup file was not found: $BackupFile" }
$connection = ConvertFrom-AlmerConnectionString $ConnectionString
$staging = Join-Path ([IO.Path]::GetTempPath()) ("almer-restore-" + [Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $zipPath = Join-Path $staging 'archive.zip'; $contentPath = Join-Path $staging 'content'
    Unprotect-AlmerBackup -InputFile $BackupFile -OutputFile $zipPath -Password $EncryptionPassword
    Expand-Archive -LiteralPath $zipPath -DestinationPath $contentPath
    $manifest = Test-AlmerBackupContents -Directory $contentPath
    if ($manifest.database -ne $connection.Database) { throw "Backup database '$($manifest.database)' does not match target '$($connection.Database)'." }
    Write-Output "Backup verified: $($manifest.createdAtUtc), database=$($manifest.database)"
    if (-not $Apply) { Write-Output 'Validation only. No database or key files were changed.'; return }
    if ($ConfirmDatabase -ne $connection.Database) { throw "Restore requires -ConfirmDatabase '$($connection.Database)'." }
    if (-not (Get-Command pg_restore -ErrorAction SilentlyContinue)) { throw 'pg_restore was not found. Install PostgreSQL client tools and add them to PATH.' }
    $previousPassword = $env:PGPASSWORD; $env:PGPASSWORD = $connection.Password
    try {
        & pg_restore --clean --if-exists --no-owner --no-privileges --host=$($connection.HostName) --port=$($connection.Port) --username=$($connection.UserName) --dbname=$($connection.Database) (Join-Path $contentPath $manifest.databaseFile)
        if ($LASTEXITCODE -ne 0) { throw "pg_restore failed with exit code $LASTEXITCODE. Data Protection keys were not changed." }
    } finally { $env:PGPASSWORD = $previousPassword }
    if (Test-Path -LiteralPath $DataProtectionKeyPath) {
        $safetyCopy = "$DataProtectionKeyPath.before-restore-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
        Copy-Item -LiteralPath $DataProtectionKeyPath -Destination $safetyCopy -Recurse
        Write-Output "Existing key ring safety copy: $safetyCopy"
    }
    New-Item -ItemType Directory -Path $DataProtectionKeyPath -Force | Out-Null
    Copy-Item -Path (Join-Path $contentPath $manifest.dataProtectionKeys '*') -Destination $DataProtectionKeyPath -Recurse -Force
    Write-Output 'Database and Data Protection key ring restored. Restart all API instances before accepting traffic.'
}
finally { if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force } }
