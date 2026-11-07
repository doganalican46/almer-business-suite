Set-StrictMode -Version Latest

function ConvertFrom-AlmerSecureString {
    param([Parameter(Mandatory)][Security.SecureString]$Value)
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function ConvertFrom-AlmerConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)
    $values = @{}
    foreach ($part in $ConnectionString.Split(';', [StringSplitOptions]::RemoveEmptyEntries)) {
        $pair = $part.Split('=', 2)
        if ($pair.Count -eq 2) { $values[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim() }
    }
    $hostName = $values['host']; $database = $values['database']; $userName = $values['username']
    if (-not $userName) { $userName = $values['user id'] }
    if (-not $hostName -or -not $database -or -not $userName) { throw 'Connection string must contain Host, Database and Username.' }
    [pscustomobject]@{ HostName = $hostName; Port = $(if ($values['port']) { $values['port'] } else { '5432' }); Database = $database; UserName = $userName; Password = $values['password'] }
}

function Protect-AlmerBackup {
    param([Parameter(Mandatory)][string]$InputFile, [Parameter(Mandatory)][string]$OutputFile, [Parameter(Mandatory)][Security.SecureString]$Password)
    $passwordText = ConvertFrom-AlmerSecureString $Password
    $passwordBytes = [Text.Encoding]::UTF8.GetBytes($passwordText)
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $nonce = [Security.Cryptography.RandomNumberGenerator]::GetBytes(12)
    $key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($passwordBytes, $salt, 210000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    try {
        $plain = [IO.File]::ReadAllBytes($InputFile); $cipher = [byte[]]::new($plain.Length); $tag = [byte[]]::new(16)
        $aes = [Security.Cryptography.AesGcm]::new($key, 16)
        try { $aes.Encrypt($nonce, $plain, $cipher, $tag) } finally { $aes.Dispose() }
        $header = [Text.Encoding]::ASCII.GetBytes('ALMERBAK1')
        $output = [byte[]]::new($header.Length + $salt.Length + $nonce.Length + $tag.Length + $cipher.Length)
        $offset = 0; foreach ($part in @($header, $salt, $nonce, $tag, $cipher)) { [Array]::Copy($part, 0, $output, $offset, $part.Length); $offset += $part.Length }
        [IO.File]::WriteAllBytes($OutputFile, $output)
    }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($key); [Security.Cryptography.CryptographicOperations]::ZeroMemory($passwordBytes) }
}

function Unprotect-AlmerBackup {
    param([Parameter(Mandatory)][string]$InputFile, [Parameter(Mandatory)][string]$OutputFile, [Parameter(Mandatory)][Security.SecureString]$Password)
    $input = [IO.File]::ReadAllBytes($InputFile)
    if ($input.Length -lt 54 -or [Text.Encoding]::ASCII.GetString($input, 0, 9) -ne 'ALMERBAK1') { throw 'This is not a supported Almer backup file.' }
    $salt = $input[9..24]; $nonce = $input[25..36]; $tag = $input[37..52]; $cipher = $input[53..($input.Length - 1)]
    $passwordText = ConvertFrom-AlmerSecureString $Password; $passwordBytes = [Text.Encoding]::UTF8.GetBytes($passwordText)
    $key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($passwordBytes, $salt, 210000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    try {
        $plain = [byte[]]::new($cipher.Length); $aes = [Security.Cryptography.AesGcm]::new($key, 16)
        try { $aes.Decrypt($nonce, $cipher, $tag, $plain) } finally { $aes.Dispose() }
        [IO.File]::WriteAllBytes($OutputFile, $plain)
    }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($key); [Security.Cryptography.CryptographicOperations]::ZeroMemory($passwordBytes) }
}

function Test-AlmerBackupContents {
    param([Parameter(Mandatory)][string]$Directory)
    $manifestPath = Join-Path $Directory 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Backup manifest is missing.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.formatVersion -ne 1 -or $manifest.databaseFile -ne 'database.dump' -or $manifest.dataProtectionKeys -ne 'data-protection-keys') { throw 'Backup manifest version or layout is unsupported.' }
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($file in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $Directory $file.path))
        if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe backup path: $($file.path)" }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Backup file is missing: $($file.path)" }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actual -ne $file.sha256) { throw "Backup hash check failed: $($file.path)" }
    }
    if (-not ($manifest.files.path -contains 'database.dump')) { throw 'Database dump is not listed in the manifest.' }
    $actualFiles = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Where-Object Name -ne 'manifest.json' | ForEach-Object { [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\','/') })
    if ($actualFiles.Count -ne $manifest.files.Count -or $actualFiles.Where({ $manifest.files.path -notcontains $_ }).Count -gt 0) { throw 'Backup contains files that are not declared in the manifest.' }
    $manifest
}

Export-ModuleMember -Function ConvertFrom-AlmerConnectionString, Protect-AlmerBackup, Unprotect-AlmerBackup, Test-AlmerBackupContents
