# Creates the migration's secrets in 1Password, once:
#   - the admin-channel certificate: field ServerSettings__AdminChannel__Certificate in the item bf2-webadmin-env, which
#     the 1Password operator turns into WebAdmin's env (envFrom secretRef bf2-webadmin-env)
#   - its pin (adminCertSha256) and one admin key per game server: item bf2-gameservers (fields adminCertSha256 and
#     <server name>)
# Existing values are kept; -RotateCertificate / -RotateKeys replace them (then every server needs the new values).
# Values pass through `op` arguments briefly (visible in this PC's process list); nothing is printed or written to disk.
#
#   powershell -ExecutionPolicy Bypass -File .\02-secrets.ps1 [-WebAdminImage docker.io/nihlen/bf2-webadmin:0.1.0]
#
# Without -WebAdminImage the certificate is made with this checkout (dotnet run of BF2WebAdmin.Server).

param(
    [string]$WebAdminImage,
    [switch]$RotateCertificate,
    [switch]$RotateKeys
)

. "$PSScriptRoot\lib.ps1"

$vault = $Config.onePassword.vault
$webItem = $Config.onePassword.webAdminItem
$gsItem = $Config.onePassword.gameServersItem
$certField = 'ServerSettings__AdminChannel__Certificate'

function Invoke-WebAdmin([string[]]$Arguments, [hashtable]$Environment) {
    if ($WebAdminImage) {
        $envArgs = @()
        if ($Environment) { foreach ($k in $Environment.Keys) { $envArgs += @('-e', $k) } }
        foreach ($k in @($Environment.Keys)) { Set-Item "env:$k" $Environment[$k] }
        try { $out = docker run --rm @envArgs $WebAdminImage @Arguments }
        finally { foreach ($k in @($Environment.Keys)) { Remove-Item "env:$k" -ErrorAction SilentlyContinue } }
    } else {
        $project = Join-Path $Kit '..\..\src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj'
        foreach ($k in @($Environment.Keys)) { Set-Item "env:$k" $Environment[$k] }
        try { $out = dotnet run --project $project -c Release --no-launch-profile -- @Arguments }
        finally { foreach ($k in @($Environment.Keys)) { Remove-Item "env:$k" -ErrorAction SilentlyContinue } }
    }
    if ($LASTEXITCODE -ne 0) { throw "WebAdmin $($Arguments -join ' ') failed ($LASTEXITCODE)" }
    return @($out)
}

Write-Step "1Password"
op whoami | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Sign in first: op signin" }
op item get $webItem --vault $vault --format json | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Item $vault/$webItem not found" }
Write-Ok "signed in, $vault/$webItem exists"

$gsExists = $true
op item get $gsItem --vault $vault --format json 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { $gsExists = $false }

Write-Step "Admin-channel certificate"
$cert = Read-OpField $webItem $certField
$pin = if ($gsExists) { Read-OpField $gsItem 'adminCertSha256' } else { $null }
if ($cert -and -not $RotateCertificate) {
    Write-Ok "$webItem/$certField exists, kept"
} else {
    if ($cert) { Confirm-Action "Replace the certificate? Every game server then needs the new adminCertSha256" }
    $lines = Invoke-WebAdmin @('--admin-cert-create') @{}
    $cert = ($lines | Where-Object { $_ -and -not $_.StartsWith('#') -and -not $_.StartsWith('adminCertSha256 ') }) -join ''
    $pin = (($lines | Where-Object { $_.StartsWith('adminCertSha256 ') }) -replace '^adminCertSha256 ', '').Trim()
    if (-not $cert -or $pin -notmatch '^[0-9a-f]{64}$') { throw "Unexpected --admin-cert-create output" }
    op item edit $webItem --vault $vault "$certField[password]=$cert" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "op item edit $webItem failed" }
    Write-Ok "stored in $webItem/$certField"
}

# The pin is always computed again from the stored certificate, so it cannot drift from it
$pinLines = Invoke-WebAdmin @('--admin-pin') @{ 'ServerSettings__AdminChannel__Certificate' = $cert }
$computedPin = ($pinLines | Where-Object { $_ -match '^[0-9a-f]{64}$' } | Select-Object -Last 1)
if (-not $computedPin) { throw "--admin-pin printed no pin" }
if ($pin -and $pin -ne $computedPin) { Write-Warn "stored pin differs from the certificate's, replacing it" }
$pin = $computedPin
Write-Ok "pin $pin"

Write-Step "Game server admin keys"
$assignments = @("adminCertSha256[text]=$pin")
foreach ($s in Get-Servers) {
    $existing = if ($gsExists) { Read-OpField $gsItem $s.name } else { $null }
    if ($existing -and -not $RotateKeys) {
        Write-Ok "$($s.name): key exists, kept"
    } else {
        $assignments += "$($s.name)[password]=$(New-AdminKey)"
        Write-Ok "$($s.name): new key"
    }
}
if ($gsExists) {
    op item edit $gsItem --vault $vault @assignments | Out-Null
} else {
    op item create --vault $vault --category 'Secure Note' --title $gsItem @assignments | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw "op failed to store $gsItem" }
Write-Ok "stored in $vault/$gsItem"

Write-Host "`nNext: 03-database.ps1 (writes the keys into dbo.Server.AdminKey)"
