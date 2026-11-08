# Shared helpers for the OpenBF2 migration scripts. Windows PowerShell 5.1; dot-source it:  . "$PSScriptRoot\lib.ps1"
# Run every script with: powershell -ExecutionPolicy Bypass -File .\<script>.ps1 (this PC's policy is AllSigned).

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$Kit = $PSScriptRoot
$Config = Get-Content (Join-Path $Kit 'servers.json') -Raw | ConvertFrom-Json
# Per-run output (backups, rendered env files). Git-ignored: it holds secrets
$WorkDir = Join-Path $Kit 'work'
if (-not (Test-Path $WorkDir)) { New-Item -ItemType Directory -Force $WorkDir | Out-Null }

function Write-Step([string]$Text) { Write-Host "`n=== $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text) { Write-Host "  OK   $Text" -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host "  WARN $Text" -ForegroundColor Yellow }
function Write-Fail([string]$Text) { Write-Host "  FAIL $Text" -ForegroundColor Red }

function Confirm-Action([string]$Question) {
    $answer = Read-Host "$Question [y/N]"
    if ($answer -ne 'y') { throw "Stopped: '$Question' was not confirmed" }
}

function ConvertTo-WslPath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    return '/mnt/' + $full.Substring(0, 1).ToLowerInvariant() + ($full.Substring(2) -replace '\\', '/')
}

function Write-LfFile([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, ($Text -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding $false))
}

# kubectl, talosctl and flux live in WSL (pve-cluster CLAUDE.md "Tooling"). Windows PowerShell 5.1 mangles quotes in
# native arguments, so each call is written to a bash script file and run from there.
function Invoke-WslScript {
    param([Parameter(Mandatory)][string]$Script)
    $file = Join-Path $WorkDir '_wsl.sh'
    Write-LfFile $file ("set -euo pipefail`nexport KUBECONFIG='$($Config.cluster.kubeconfig)'`n" + $Script + "`n")
    $out = wsl -d Ubuntu -- bash -ic "bash '$(ConvertTo-WslPath $file)'"
    $code = $LASTEXITCODE
    Remove-Item $file -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw "WSL script failed ($code):`n$Script`n$($out -join "`n")" }
    return $out
}

function Invoke-Kubectl([Parameter(Mandatory)][string]$Arguments) {
    return Invoke-WslScript "kubectl $Arguments"
}

# Runs T-SQL in the SQL Server pod as sa. The password never leaves the pod: sqlcmd reads it from the pod's env
# (MSSQL_SA_PASSWORD, from the 1Password item mssql-env). The script goes in on stdin from a temporary file.
function Invoke-Sql {
    param([Parameter(Mandatory)][string]$Sql)
    $sqlFile = Join-Path $WorkDir '_query.sql'
    Write-LfFile $sqlFile $Sql
    try {
        $c = $Config.cluster
        $script = "kubectl -n $($c.namespace) exec -i $($c.mssqlPod) -- bash -c " +
            "'SQLCMDPASSWORD=`"`$MSSQL_SA_PASSWORD`" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -s `"|`" -d $($c.database) -i /dev/stdin' " +
            "< '$(ConvertTo-WslPath $sqlFile)'"
        return Invoke-WslScript $script
    } finally {
        Remove-Item $sqlFile -ErrorAction SilentlyContinue
    }
}

# Git's OpenSSH: Windows' own ssh.exe refuses ~/.ssh/config when another local user has permissions on it
$GitSsh = 'C:\Program Files\Git\usr\bin\ssh.exe'
$GitScp = 'C:\Program Files\Git\usr\bin\scp.exe'
$Ssh = if (Test-Path $GitSsh) { $GitSsh } else { 'ssh' }
$Scp = if (Test-Path $GitScp) { $GitScp } else { 'scp' }

function Invoke-Ssh {
    param([Parameter(Mandatory)][string]$HostAlias, [Parameter(Mandatory)][string]$Command, [string]$InputText)
    if ($PSBoundParameters.ContainsKey('InputText')) {
        $out = $InputText | & $Ssh -o BatchMode=yes $HostAlias $Command
    } else {
        $out = & $Ssh -o BatchMode=yes $HostAlias $Command
    }
    if ($LASTEXITCODE -ne 0) { throw "ssh $HostAlias failed ($LASTEXITCODE): $Command`n$out" }
    return $out
}

function Copy-ToHost {
    param([Parameter(Mandatory)][string]$HostAlias, [Parameter(Mandatory)][string]$LocalPath, [Parameter(Mandatory)][string]$RemotePath)
    & $Scp -q -o BatchMode=yes $LocalPath "${HostAlias}:$RemotePath"
    if ($LASTEXITCODE -ne 0) { throw "scp to $HostAlias`:$RemotePath failed" }
}

# Secrets: from work\secrets.json when it exists (filled by hand from 1Password for migration day, deleted after;
# the author keeps the 1Password CLI out of these scripts), else from the 1Password CLI. Never printed.
$SecretsFile = Join-Path $WorkDir 'secrets.json'
function Read-OpField([string]$Item, [string]$Field) {
    if (Test-Path $SecretsFile) {
        $secrets = Get-Content $SecretsFile -Raw | ConvertFrom-Json
        $p = $secrets.PSObject.Properties[$Field]
        if ($p -and $p.Value -and -not ([string]$p.Value).StartsWith('<')) { return ([string]$p.Value).Trim() }
        return $null
    }
    $value = op read "op://$($Config.onePassword.vault)/$Item/$Field" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($value -join "`n").Trim()
}

# The admin keys live in dbo.Server.AdminKey (03-database.ps1 has SQL Server generate them). Returns ServerId -> key.
function Get-AdminKeys {
    $ids = (@($Config.servers) | ForEach-Object { "N'$($_.serverId)'" }) -join ', '
    $rows = Invoke-Sql "SET NOCOUNT ON; SELECT ServerId + '|' + ISNULL(AdminKey, '') FROM dbo.Server WHERE ServerId IN ($ids);"
    $keys = @{}
    foreach ($r in @($rows)) {
        if ($r -match '^([^|\s]+)\|([0-9a-f]*)\s*$') { $keys[$Matches[1]] = $Matches[2] }
    }
    return $keys
}

# The pin (adminCertSha256) of the certificate WebAdmin actually uses: it logs it at startup ("Admin channel: TLS
# certificate ... adminCertSha256 <pin>"). work\secrets.json's adminCertSha256 wins when set.
function Get-WebAdminPin {
    $fromFile = Read-OpField $Config.onePassword.gameServersItem 'adminCertSha256'
    if ((Test-Path $SecretsFile) -and $fromFile -match '^[0-9a-f]{64}$') { return $fromFile }
    $log = @(Invoke-Kubectl "-n $($Config.cluster.namespace) logs deploy/$($Config.cluster.deployment) --tail=5000")
    $line = @($log | Where-Object { $_ -match 'adminCertSha256 ([0-9a-f]{64})' } | Select-Object -Last 1)
    if ($line.Count -and $line[0] -match 'adminCertSha256 ([0-9a-f]{64})') { return $Matches[1] }
    return $null
}

function Get-Servers([string[]]$Names) {
    $all = @($Config.servers)
    if (-not $Names -or $Names.Count -eq 0) { return $all }
    $picked = @($all | Where-Object { $Names -contains $_.name })
    $unknown = @($Names | Where-Object { -not ($all.name -contains $_) })
    if ($unknown.Count -gt 0) { throw "Unknown server(s): $($unknown -join ', ')" }
    return $picked
}

function New-AdminKey {
    # 32 random bytes as 64 hex digits (Server.AdminKey takes 32-128 hex digits)
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}
