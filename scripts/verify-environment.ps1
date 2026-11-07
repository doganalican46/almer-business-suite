[CmdletBinding()]
param([switch]$Production)

$ErrorActionPreference = 'Stop'
$minimumNodeMajor = 22
$failures = [System.Collections.Generic.List[string]]::new()

function Test-Command([string]$Name, [string]$VersionArgument = '--version') {
  $command = Get-Command $Name -ErrorAction SilentlyContinue
  if (-not $command) { $failures.Add("Missing command: $Name"); return }
  $version = & $command.Source $VersionArgument 2>&1 | Select-Object -First 1
  Write-Host "[ok] $Name $version"
}

Test-Command 'dotnet'
Test-Command 'node'
Test-Command 'npm'

$nodeMajor = [int]((& node --version).TrimStart('v').Split('.')[0])
if ($nodeMajor -lt $minimumNodeMajor) { $failures.Add("Node.js $minimumNodeMajor+ is required.") }

if ($Production) {
  $required = @('ConnectionStrings__DefaultConnection', 'Jwt__Key', 'DataProtection__KeyPath', 'App__PublicUrl')
  foreach ($name in $required) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) { $failures.Add("Missing production setting: $name") }
  }
  $jwt = [Environment]::GetEnvironmentVariable('Jwt__Key')
  if ($jwt -and [Text.Encoding]::UTF8.GetByteCount($jwt) -lt 32) { $failures.Add('Jwt__Key must be at least 32 bytes.') }
  if ([Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT') -eq 'Development') { $failures.Add('Production verification cannot use ASPNETCORE_ENVIRONMENT=Development.') }
}

if ($failures.Count) {
  $failures | ForEach-Object { Write-Error $_ }
  exit 1
}

Write-Host '[ok] Environment preflight passed.'
