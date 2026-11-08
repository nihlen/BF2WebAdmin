# Read only. After the migration: did every game server authenticate to WebAdmin, are the containers on the OpenBF2
# images, do rcon ports answer.
#
#   powershell -ExecutionPolicy Bypass -File .\06-verify.ps1

. "$PSScriptRoot\lib.ps1"

Write-Step "WebAdmin"
$ns = $Config.cluster.namespace
Write-Host "  image: $((Invoke-Kubectl "-n $ns get deploy $($Config.cluster.deployment) -o jsonpath='{.spec.template.spec.containers[0].image}'") -join '')"
$log = @(Invoke-Kubectl "-n $ns logs deploy/$($Config.cluster.deployment) --tail=2000")
foreach ($s in $Config.servers) {
    $hit = @($log | Where-Object { $_ -match "Admin channel: $([regex]::Escape($s.serverId)) authenticated" } | Select-Object -Last 1)
    if ($hit) { Write-Ok "$($s.name) ($($s.serverId)) authenticated" } else { Write-Fail "$($s.name) ($($s.serverId)) has not authenticated (WebAdmin log)" }
}
$refused = @($log | Where-Object { $_ -match 'Admin channel: refused' } | Select-Object -Last 5)
foreach ($r in $refused) { Write-Warn $r }

Write-Step "Game servers"
foreach ($s in $Config.servers) {
    $state = (Invoke-Ssh $s.host "docker inspect -f '{{.Config.Image}} {{.State.Status}} restarts={{.RestartCount}}' $($s.name)") -join ''
    if ($state -like "$($s.image) running*") { Write-Ok "$($s.name): $state" } else { Write-Fail "$($s.name): $state" }
    $ip = (@($Config.hosts | Where-Object { $_.alias -eq $s.host })[0]).ip
    $client = New-Object System.Net.Sockets.TcpClient
    try { $ok = $client.ConnectAsync($ip, [int]$s.rconPort).Wait(3000) } catch { $ok = $false } finally { $client.Dispose() }
    if ($ok) { Write-Ok "$($s.name): rcon $ip`:$($s.rconPort)" } else { Write-Fail "$($s.name): rcon $ip`:$($s.rconPort) does not answer" }
}

Write-Host @"

By hand (README "Verify"): the web UI lists every server as connected with players; chat and a . command in game;
a kick and a ban (iga); a map change from the web UI; the servers on the BF2Hub list; a player's CD-key hash in the
player sheet (not name:<name>); Discord notifications.
"@
