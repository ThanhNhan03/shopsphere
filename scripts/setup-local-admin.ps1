# Create local credentials once. Existing values and passwords are preserved.
$ErrorActionPreference = 'Stop'
$repoDirectory = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoDirectory '.env'
if (!(Test-Path -LiteralPath $envFile)) { Copy-Item -LiteralPath (Join-Path $repoDirectory '.env.example') -Destination $envFile }
$content = [IO.File]::ReadAllText($envFile)
function Set-MissingValue([string]$key, [string]$value) {
    $pattern = '(?m)^' + [regex]::Escape($key) + '=(.*)\r?$'
    $match = [regex]::Match($script:content, $pattern)
    if ($match.Success -and ![string]::IsNullOrWhiteSpace($match.Groups[1].Value)) { return $match.Groups[1].Value.Trim().Trim('"').Trim("'") }
    if ($match.Success) { $script:content = [regex]::Replace($script:content, $pattern, "$key=$value") }
    else { $script:content += "`n$key=$value`n" }
    return $value
}
$adminEmail = Set-MissingValue 'ADMIN_BOOTSTRAP_EMAIL' 'admin@shopsphere.local'
$adminPassword = Set-MissingValue 'ADMIN_BOOTSTRAP_PASSWORD' ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
[IO.File]::WriteAllText($envFile, $content)
$localDirectory = Join-Path $repoDirectory '.local'
New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $localDirectory 'admin-account.txt'), "ShopSphere local administrator`nEmail: $adminEmail`nPassword: $adminPassword`n`nCreated on first startup only. Changing the environment does not reset an existing account.`n")
Write-Output 'Local administrator configuration is ready. Credentials: .local/admin-account.txt'
