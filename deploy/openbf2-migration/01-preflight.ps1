# Read only. Checks that everything the migration needs is reachable and in place. Run it the day before and again
# right before migrating.
#
#   powershell -ExecutionPolicy Bypass -File .\01-preflight.ps1

. "$PSScriptRoot\lib.ps1"
$failed = 0
function Check([string]$Name, [scriptblock]$Test) {
    try {
        $r = & $Test
        if ($r -eq $false) { Write-Fail $Name; $script:failed++ } else { Write-Ok "$Name $(if ($r -is [string]) { "- $r" })" }
    } catch { Write-Fail "$Name - $($_.Exception.Message.Split("`n")[0])"; $script:failed++ }
}

Write-Step "Tools"
Check "WSL kubectl reaches the cluster" { (Invoke-Kubectl "get ns $($Config.cluster.namespace) -o name") -join '' }
Check "WSL flux" { (Invoke-WslScript "flux version --client") -join ' ' }
Check "pve-cluster checkout on main" { ((git -C $Config.cluster.repo rev-parse --abbrev-ref HEAD) -join '') -eq 'main' }

Write-Step "Hosts"
foreach ($h in $Config.hosts) {
    Check "ssh $($h.alias)" { (Invoke-Ssh $h.alias "docker compose version --short") -join '' }
    Check "$($h.alias) $($h.composeFile)" { (Invoke-Ssh $h.alias "test -f $($h.composeFile) && echo present") -join '' }
    foreach ($s in @($Config.servers | Where-Object { $_.host -eq $h.alias })) {
        Check "$($h.alias) pulls $($s.image)" { (Invoke-Ssh $h.alias "docker manifest inspect $($s.image) >/dev/null && echo ok") -join '' }
        Check "$($h.alias) runs $($s.name)" { (Invoke-Ssh $h.alias "docker inspect -f '{{.Config.Image}} {{.State.Status}}' $($s.name)") -join '' }
    }
}

Write-Step "Secrets"
# The certificate: in the cluster secret (from 1Password). The value is only measured, never printed.
Check "certificate in the cluster secret bf2-webadmin-env" {
    ((Invoke-Kubectl "-n $($Config.cluster.namespace) get secret bf2-webadmin-env -o jsonpath='{.data.ServerSettings__AdminChannel__Certificate}'") -join '').Length -gt 100
}
# The admin keys are made by 03-database.ps1 in dbo.Server and the pin is read from WebAdmin's log on migration day

Write-Step "Database (read only)"
Check "dbo.Server rows" { & "$Kit\03-database.ps1" -Check; $true }

Write-Step "WebAdmin"
Check "04-webadmin.ps1 -Phase Check" { & "$Kit\04-webadmin.ps1" -Phase Check; $true }

Write-Host ""
if ($failed -gt 0) { Write-Fail "$failed check(s) failed"; exit 1 } else { Write-Ok "all checks passed" }
