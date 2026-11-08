# Switches the game servers on one VPS between the retail images (mm_webadmin) and the OpenBF2 images
# (libserverext.so admin channel, bf2-docker images/netsky-openbf2-*).
#
#   -Phase Canary    de2 only: the OpenBF2 test server's slot (16570) runs the new image on the internet, without the
#                    admin channel. From its own compose project (/data/openbf2-canary); the main compose file is not
#                    touched. For the BF2Hub listing, CD-key and human-client checks before migration day.
#   -Phase Migrate   the host's live servers: backs up /data/docker-compose.yml once (docker-compose.retail.yml),
#                    seeds a new state dir per server (/data/bf2/<name>/volume-openbf2: the ban lists and the uploaded
#                    demos from the old /volume, which stays untouched for a rollback), replaces the compose file and
#                    starts the new containers (same container names, so the 06:00 restart cron keeps working).
#                    On de2 it also stops the canary (both kinds) unless -KeepCanary.
#   -Phase Rollback  the host's retail compose file (docker-compose.retail.yml) back, retail containers started on their
#                    old server dirs. Bans made while on OpenBF2 stay in volume-openbf2.
#   -Phase Status    read only: containers, images, the module's admin-channel lines.
#
#   powershell -ExecutionPolicy Bypass -File .\05-gameservers.ps1 -HostAlias de2 -Phase Status
#
# RCON passwords, API keys, server names, player counts and demo URLs are taken from the running containers
# (docker inspect), so they don't live in this repo. Admin keys and the pin come from 1Password (02-secrets.ps1).
# Rendered env files (secrets) go to work\ here (git-ignored) and /data/openbf2/*.env (mode 600) on the host.

param(
    [Parameter(Mandatory)][ValidateSet('de', 'de2')][string]$HostAlias,
    [Parameter(Mandatory)][ValidateSet('Canary', 'Migrate', 'Rollback', 'Status')][string]$Phase,
    [switch]$KeepCanary,
    [switch]$Yes
)

. "$PSScriptRoot\lib.ps1"

$hostInfo = @($Config.hosts | Where-Object { $_.alias -eq $HostAlias })[0]
$servers = @($Config.servers | Where-Object { $_.host -eq $HostAlias })
$canary = $Config.canary
$hostWork = Join-Path $WorkDir $HostAlias
if (-not (Test-Path $hostWork)) { New-Item -ItemType Directory -Force $hostWork | Out-Null }

function Invoke-Remote([string]$Script) {
    $name = '_remote-' + [guid]::NewGuid().ToString('n').Substring(0, 8) + '.sh'
    $local = Join-Path $hostWork $name
    Write-LfFile $local ("set -euo pipefail`n" + $Script + "`n")
    try {
        Copy-ToHost $HostAlias $local "/tmp/$name"
        $out = ssh -o BatchMode=yes $HostAlias "bash /tmp/$name; rc=`$?; rm -f /tmp/$name; exit `$rc"
        if ($LASTEXITCODE -ne 0) { throw "remote script on $HostAlias failed ($LASTEXITCODE):`n$($out -join "`n")" }
        return $out
    } finally {
        Remove-Item $local -ErrorAction SilentlyContinue
    }
}

# ENV_* of a container (running or stopped), saved under work\ so a later run still has them after the container is gone
function Get-ContainerEnv([string]$Container) {
    $saved = Join-Path $hostWork "$Container.oldenv"
    $lines = @(Invoke-Remote "docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' $Container 2>/dev/null || true")
    $envLines = @($lines | Where-Object { $_ -match '^ENV_' })
    if ($envLines.Count -gt 0 -and -not ($envLines -match '^ENV_ADMIN_KEY=')) {
        # A retail container: keep its settings for later runs
        Write-LfFile $saved (($envLines -join "`n") + "`n")
    }
    if (-not (Test-Path $saved)) { throw "No settings for $Container`: the container is gone and work\$HostAlias\$Container.oldenv is missing" }
    $map = @{}
    foreach ($l in (Get-Content $saved)) {
        $i = $l.IndexOf('=')
        if ($i -gt 0) { $map[$l.Substring(0, $i)] = $l.Substring($i + 1) }
    }
    return $map
}

function Get-OldVolume([string]$Container) {
    $v = (Invoke-Remote "docker inspect -f '{{range .Mounts}}{{if eq .Destination `"/volume`"}}{{.Source}}{{end}}{{end}}' $Container 2>/dev/null || true") -join ''
    if (-not $v) { $v = "/data/bf2/$Container/volume" }
    return $v.Trim()
}

# Compose's env_file: single quotes keep a value literal (no interpolation, no # comments)
function Format-EnvLine([string]$Key, [string]$Value) {
    if ($Value.Contains("'") -or $Value.Contains("`n")) { throw "$Key has a quote or a line break; set it by hand" }
    return "$Key='$Value'"
}

function New-EnvFile($Server, [hashtable]$Old, [bool]$Admin, [string]$Key, [string]$Pin) {
    $lines = @()
    foreach ($k in 'ENV_SERVER_NAME', 'ENV_MAX_PLAYERS', 'ENV_DEMOS_URL', 'ENV_RCON_PASSWORD', 'ENV_API_KEY') {
        if (-not $Old.ContainsKey($k) -or -not $Old[$k]) { throw "$($Server.name): $k missing in the old container's settings" }
        $lines += Format-EnvLine $k $Old[$k]
    }
    $lines += Format-EnvLine 'ENV_SERVER_PORT' ([string]$Server.gamePort)
    $lines += Format-EnvLine 'ENV_GAMESPY_PORT' ([string]$Server.queryPort)
    $lines += Format-EnvLine 'ENV_INTERNET' '1'
    $lines += Format-EnvLine 'ENV_TV_AIM_FROM_MISSILE' ([string]$Config.gameplay.tvAimFromMissile)
    $lines += Format-EnvLine 'ENV_TV_TURN_BEHIND' ([string]$Config.gameplay.tvTurnBehind)
    if ($Admin) {
        $lines += Format-EnvLine 'ENV_ADMIN_CONNECT' $Config.webAdmin.adminConnect
        $lines += Format-EnvLine 'ENV_ADMIN_SERVER_ID' $Server.serverId
        $lines += Format-EnvLine 'ENV_ADMIN_KEY' $Key
        $lines += Format-EnvLine 'ENV_ADMIN_CERT_SHA256' $Pin
    }
    return ($lines -join "`n") + "`n"
}

function New-ComposeService($Server, [string]$EnvPath) {
    $ports = ($Server.ports | ForEach-Object { "      - `"$_`"" }) -join "`n"
    return @"
  $($Server.service):
    container_name: $($Server.name)
    image: "$($Server.image)"
    # .restartserver quits with exit code 0
    restart: unless-stopped
    env_file:
      - "$EnvPath"
    volumes:
      - "/data/bf2/$($Server.name)/volume-openbf2:/volume"
    ports:
$ports
"@
}

# Bash that creates /data/bf2/<name>/volume-openbf2 once from the old /volume: ban lists and uploaded demos
function Get-SeedScript([string]$Name, [string]$OldVolume) {
    return @"
new=/data/bf2/$Name/volume-openbf2
if [ ! -e "`$new/.seeded" ]; then
    mkdir -p "`$new/settings" "`$new/demos/uploaded"
    for f in banlist.con mm_bans.xml; do
        if [ -f "$OldVolume/settings/`$f" ]; then cp -p "$OldVolume/settings/`$f" "`$new/settings/`$f"; fi
    done
    if [ -d "$OldVolume/demos/uploaded" ]; then cp -a "$OldVolume/demos/uploaded/." "`$new/demos/uploaded/"; fi
    date -u +%FT%TZ > "`$new/.seeded"
    echo "seeded `$new from $OldVolume"
else
    echo "`$new already seeded"
fi
"@
}

function Wait-AdminChannel([string[]]$Containers) {
    $deadline = (Get-Date).AddMinutes(3)
    $pending = [System.Collections.ArrayList]@($Containers)
    while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
        foreach ($c in @($pending)) {
            $hit = Invoke-Remote "docker logs $c 2>&1 | grep -E 'admin: link session [0-9]+ to .* authenticated' | tail -1 || true"
            if ($hit) { Write-Ok "$c`: $($hit -join '')"; $pending.Remove($c) }
        }
        if ($pending.Count -gt 0) { Start-Sleep -Seconds 10 }
    }
    foreach ($c in $pending) {
        Write-Fail "$c`: no authenticated admin link after 3 minutes. Last module lines:"
        Invoke-Remote "docker logs $c 2>&1 | grep -E 'serverext|admin' | tail -15 || true" | ForEach-Object { Write-Host "    $_" }
    }
    return ($pending.Count -eq 0)
}

function Test-RconPorts($List) {
    foreach ($s in $List) {
        $ok = $false
        for ($i = 0; $i -lt 12 -and -not $ok; $i++) {
            $client = New-Object System.Net.Sockets.TcpClient
            try { $ok = $client.ConnectAsync($hostInfo.ip, [int]$s.rconPort).Wait(3000) } catch { $ok = $false } finally { $client.Dispose() }
            if (-not $ok) { Start-Sleep -Seconds 5 }
        }
        if ($ok) { Write-Ok "$($s.name): rcon $($hostInfo.ip):$($s.rconPort) answers" } else { Write-Fail "$($s.name): rcon $($hostInfo.ip):$($s.rconPort) does not answer" }
    }
}

switch ($Phase) {
    'Status' {
        Write-Step "$HostAlias containers"
        Invoke-Remote "docker ps -a --format '{{.Names}}\t{{.Image}}\t{{.Status}}' | grep -E 'netsky|bf2' || true" | ForEach-Object { Write-Host "  $_" }
        Write-Step "$HostAlias compose files"
        Invoke-Remote "ls -la /data/docker-compose*.yml /data/openbf2 /data/openbf2-canary 2>/dev/null || true" | ForEach-Object { Write-Host "  $_" }
        Write-Step "Admin channel lines"
        foreach ($s in $servers) {
            Invoke-Remote "docker logs $($s.name) 2>&1 | grep -E 'admin channel:|admin: ' | tail -3 || true" | ForEach-Object { Write-Host "  $($s.name): $_" }
        }
    }

    'Canary' {
        if ($HostAlias -ne $canary.host) { throw "The canary runs on $($canary.host)" }
        Write-Step "Canary $($canary.name) on $HostAlias"
        $old = Get-ContainerEnv $canary.name
        $oldVolume = Get-OldVolume $canary.name
        $envText = New-EnvFile $canary $old $false '' ''
        $compose = "services:`n" + (New-ComposeService $canary "/data/openbf2-canary/$($canary.name).env") + "`n"
        Write-LfFile (Join-Path $hostWork "canary.env") $envText
        Write-LfFile (Join-Path $hostWork "canary-compose.yml") $compose
        Write-Host $compose
        if (-not $Yes) { Confirm-Action "Replace the OpenBF2 test server $($canary.name) on $HostAlias with $($canary.image)?" }
        Invoke-Remote "mkdir -p /data/openbf2-canary && chmod 700 /data/openbf2-canary" | Out-Null
        Copy-ToHost $HostAlias (Join-Path $hostWork "canary.env") "/data/openbf2-canary/$($canary.name).env"
        Copy-ToHost $HostAlias (Join-Path $hostWork "canary-compose.yml") "/data/openbf2-canary/docker-compose.yml"
        Invoke-Remote @"
chmod 600 /data/openbf2-canary/*.env
docker pull -q "$($canary.image)"
$(Get-SeedScript $canary.name $oldVolume)
docker rm -f $($canary.name) >/dev/null 2>&1 || true
docker compose -p openbf2-canary -f /data/openbf2-canary/docker-compose.yml up -d
sleep 20
docker logs $($canary.name) 2>&1 | grep -E 'loaded|admin channel:|serverext' | tail -10 || true
"@ | ForEach-Object { Write-Host "  $_" }
        Test-RconPorts @($canary)
        Write-Host "`nNext: join $($hostInfo.ip):$($canary.gamePort) with a retail client from the internet; check the BF2Hub list, CD-key login, a round with two humans."
    }

    'Migrate' {
        Write-Step "Migrate $HostAlias`: $($servers.name -join ', ')"
        $pin = Read-OpField $Config.onePassword.gameServersItem 'adminCertSha256'
        if ($pin -notmatch '^[0-9a-f]{64}$') { throw "No pin in 1Password (02-secrets.ps1)" }
        $services = @()
        $seed = @()
        foreach ($s in $servers) {
            $key = Read-OpField $Config.onePassword.gameServersItem $s.name
            if ($key -notmatch '^[0-9a-f]{32,128}$') { throw "$($s.name): no key in 1Password (02-secrets.ps1)" }
            $old = Get-ContainerEnv $s.name
            if ($old.ContainsKey('ENV_SERVER_PORT') -and $old['ENV_SERVER_PORT'] -ne [string]$s.gamePort) {
                throw "$($s.name): running on port $($old['ENV_SERVER_PORT']), servers.json says $($s.gamePort)"
            }
            Write-LfFile (Join-Path $hostWork "$($s.name).env") (New-EnvFile $s $old $true $key $pin)
            $services += New-ComposeService $s "/data/openbf2/$($s.name).env"
            $seed += Get-SeedScript $s.name (Get-OldVolume $s.name)
            Write-Ok "$($s.name): env rendered (server id $($s.serverId))"
        }
        $compose = "# OpenBF2 game servers (BF2WebAdmin admin channel). Generated by BF2WebAdmin deploy/openbf2-migration/05-gameservers.ps1.`n" +
                   "# The retail file is docker-compose.retail.yml (05-gameservers.ps1 -Phase Rollback).`n" +
                   "services:`n" + ($services -join "`n") + "`n"
        Write-LfFile (Join-Path $hostWork 'docker-compose.yml') $compose
        Write-Host $compose
        if (-not $Yes) { Confirm-Action "Stop the retail servers on $HostAlias and start these OpenBF2 servers?" }

        Invoke-Remote "mkdir -p /data/openbf2 && chmod 700 /data/openbf2" | Out-Null
        foreach ($s in $servers) { Copy-ToHost $HostAlias (Join-Path $hostWork "$($s.name).env") "/data/openbf2/$($s.name).env" }
        Copy-ToHost $HostAlias (Join-Path $hostWork 'docker-compose.yml') '/data/openbf2/docker-compose.yml'
        $stopCanary = ''
        if ($HostAlias -eq $canary.host -and -not $KeepCanary) {
            $stopCanary = "docker compose -p openbf2-canary -f /data/openbf2-canary/docker-compose.yml down 2>/dev/null || true`ndocker rm -f $($canary.name) >/dev/null 2>&1 || true"
        }
        Invoke-Remote @"
chmod 600 /data/openbf2/*.env
$(($servers | ForEach-Object { "docker pull -q `"$($_.image)`"" }) -join "`n")
cd /data
if [ ! -f docker-compose.retail.yml ]; then cp -p docker-compose.yml docker-compose.retail.yml; echo 'saved docker-compose.retail.yml'; fi
$($seed -join "`n")
$stopCanary
docker compose -f docker-compose.retail.yml stop $($servers.service -join ' ') || true
docker compose -f docker-compose.retail.yml rm -f $($servers.service -join ' ') || true
cp /data/openbf2/docker-compose.yml /data/docker-compose.yml
docker compose -f /data/docker-compose.yml up -d
docker ps --format '{{.Names}}\t{{.Image}}\t{{.Status}}' | grep -E 'netsky|bf2' || true
"@ | ForEach-Object { Write-Host "  $_" }

        Write-Step "Checks"
        $ok = Wait-AdminChannel @($servers.name)
        Test-RconPorts $servers
        if (-not $ok) { Write-Warn "Not every server reached WebAdmin. Is 0.1.0 running with the certificate (04-webadmin.ps1)? Rollback: -Phase Rollback" }
    }

    'Rollback' {
        Write-Step "Rollback $HostAlias to the retail compose file"
        if (-not $Yes) { Confirm-Action "Stop the OpenBF2 servers on $HostAlias and start the retail ones again?" }
        Invoke-Remote @"
cd /data
[ -f docker-compose.retail.yml ] || { echo 'no docker-compose.retail.yml on this host' >&2; exit 1; }
docker compose -f docker-compose.yml down || true
cp -p docker-compose.retail.yml docker-compose.yml
docker compose -f docker-compose.yml up -d $($servers.service -join ' ')
docker ps --format '{{.Names}}\t{{.Image}}\t{{.Status}}' | grep -E 'netsky|bf2' || true
"@ | ForEach-Object { Write-Host "  $_" }
        Test-RconPorts $servers
        Write-Host "`nThe retail servers need WebAdmin 0.0.2 (04-webadmin.ps1 -Rollback)."
    }
}
