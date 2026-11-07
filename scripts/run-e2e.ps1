[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$frontend = Join-Path $PSScriptRoot '..\src\frontend'
$node = (Get-Command node).Source
$server = $null

Push-Location $frontend
try {
  $server = Start-Process -FilePath $node -ArgumentList @('./node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '4174') -WorkingDirectory $frontend -WindowStyle Hidden -PassThru
  $ready = $false
  for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
      $response = Invoke-WebRequest -Uri 'http://127.0.0.1:4174' -TimeoutSec 2
      if ($response.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Milliseconds 500 }
  }
  if (-not $ready) { throw 'Vite E2E server did not become ready.' }
  $env:PLAYWRIGHT_EXTERNAL_SERVER = '1'
  & npx playwright test --reporter=list
  if ($LASTEXITCODE -ne 0) { throw "Playwright exited with code $LASTEXITCODE." }
} finally {
  Remove-Item Env:PLAYWRIGHT_EXTERNAL_SERVER -ErrorAction SilentlyContinue
  if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force }
  Pop-Location
}
