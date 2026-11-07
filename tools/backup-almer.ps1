param(
    [string]$ConnectionString,
    [string]$DataProtectionKeyPath,
    [string]$OutputDirectory,
    [Security.SecureString]$EncryptionPassword
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
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'backups' }
if (-not $EncryptionPassword) { $EncryptionPassword = Read-Host 'Backup encryption password' -AsSecureString }
if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue)) { throw 'pg_dump was not found. Install PostgreSQL client tools and add them to PATH.' }
if (-not (Test-Path -LiteralPath $DataProtectionKeyPath -PathType Container)) { throw "Data Protection key directory was not found: $DataProtectionKeyPath" }

$connection = ConvertFrom-AlmerConnectionString $ConnectionString
$staging = Join-Path ([IO.Path]::GetTempPath()) ("almer-backup-" + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $staging 'archive.zip'; $dumpPath = Join-Path $staging 'database.dump'; $keysPath = Join-Path $staging 'data-protection-keys'
New-Item -ItemType Directory -Path $staging, $keysPath -Force | Out-Null
try {
    $previousPassword = $env:PGPASSWORD; $env:PGPASSWORD = $connection.Password
    try {
        & pg_dump --format=custom --no-owner --no-privileges --file=$dumpPath --host=$($connection.HostName) --port=$($connection.Port) --username=$($connection.UserName) $connection.Database
        if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit code $LASTEXITCODE." }
    } finally { $env:PGPASSWORD = $previousPassword }
    $keyFiles = @(Get-ChildItem -LiteralPath $DataProtectionKeyPath -File)
    if ($keyFiles.Count -eq 0) { throw 'Data Protection key directory is empty; refusing to create an incomplete MFA backup.' }
    $keyFiles | Copy-Item -Destination $keysPath -Force
    $files = Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object Name -ne 'archive.zip' | ForEach-Object { [pscustomobject]@{ path = [IO.Path]::GetRelativePath($staging, $_.FullName).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
    $manifest = [ordered]@{ formatVersion = 1; product = 'Almer Business Suite'; createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); database = $connection.Database; databaseFile = 'database.dump'; dataProtectionKeys = 'data-protection-keys'; files = @($files) }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging 'manifest.json') -Encoding UTF8
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $output = Join-Path $OutputDirectory ("almer-{0}-{1}.almerbak" -f $connection.Database, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    Protect-AlmerBackup -InputFile $zipPath -OutputFile $output -Password $EncryptionPassword
    Write-Output "Encrypted backup created: $output"
}
finally { if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force } }
