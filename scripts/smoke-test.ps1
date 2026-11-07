[CmdletBinding()]
param(
  [string]$ApiBaseUrl = 'http://localhost:8080',
  [string]$Email,
  [string]$Password,
  [Guid]$TenantId
)

$ErrorActionPreference = 'Stop'
$base = $ApiBaseUrl.TrimEnd('/')

function Invoke-Checked([string]$Name, [string]$Uri, [hashtable]$Arguments = @{}) {
  try {
    $response = Invoke-WebRequest -Uri $Uri -SkipHttpErrorCheck @Arguments
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) { throw "$Name returned HTTP $($response.StatusCode)." }
    Write-Host "[ok] $Name (HTTP $($response.StatusCode))"
    return $response
  } catch { throw "$Name failed: $($_.Exception.Message)" }
}

$live = Invoke-Checked 'Liveness' "$base/health/live"
$ready = Invoke-Checked 'Readiness' "$base/health/ready"
if (-not $live.Headers['X-Trace-Id']) { throw 'Liveness response is missing X-Trace-Id.' }

if ($Email -and $Password) {
  $payload = @{ email = $Email; password = $Password }
  if ($TenantId -ne [Guid]::Empty) { $payload.tenantId = $TenantId }
  $login = Invoke-Checked 'Login' "$base/api/auth/login" @{ Method = 'Post'; ContentType = 'application/json'; Body = ($payload | ConvertTo-Json) }
  $session = $login.Content | ConvertFrom-Json
  if (-not $session.accessToken) { throw 'Login response does not contain an access token.' }
  $headers = @{ Authorization = "Bearer $($session.accessToken)" }
  $null = Invoke-Checked 'Dashboard' "$base/api/dashboard" @{ Headers = $headers }
  Write-Host "[ok] Authenticated smoke flow passed for tenant $($session.tenant.id)."
} else {
  Write-Host '[skip] Authenticated smoke flow: provide -Email and -Password.'
}

Write-Host '[ok] Smoke test completed.'
