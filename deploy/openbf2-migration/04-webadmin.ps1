# WebAdmin in the cluster (pve-cluster repo, Flux deploys its main branch). Every phase that pushes asks first;
# pve-cluster's own rule: never change the live cluster without the author's approval.
#
#   -Phase Hold      BEFORE the GitHub release of 0.1.0: Flux's ImagePolicy bf2-webadmin is held below 0.1.0
#                    (pve-cluster\pve-cluster-1-hold.patch). Without it Flux deploys any new release tag within 5 min,
#                    and 0.1.0 cannot talk to the retail servers.
#   -Phase Check     read only: the 0.1.0 image exists, 1Password and the cluster secret have the certificate, the
#                    deployment's image and env, the hold is in place.
#   -Phase Migrate   migration day: image 0.1.0, ServerSettings__AdminChannel__Port=4300, ImagePolicy back to >=0.0.0
#                    (pve-cluster\pve-cluster-2-migrate.patch); commit, push, flux reconcile, wait for the rollout,
#                    compare the pin in the log with 1Password.
#   -Phase Rollback  reverts the migrate commit (0.0.2 and the old env), keeps the hold (0.1.0 must not come back by
#                    itself), push, reconcile, rollout.
#
#   powershell -ExecutionPolicy Bypass -File .\04-webadmin.ps1 -Phase Check

param(
    [Parameter(Mandatory)][ValidateSet('Hold', 'Check', 'Migrate', 'Rollback')][string]$Phase,
    [switch]$NoPush,
    [switch]$Yes
)

. "$PSScriptRoot\lib.ps1"

$repo = $Config.cluster.repo
$ns = $Config.cluster.namespace
$deploy = $Config.cluster.deployment
$image = "$($Config.webAdmin.image):$($Config.webAdmin.version)"
$holdPatch = Join-Path $Kit 'pve-cluster\pve-cluster-1-hold.patch'
$migratePatch = Join-Path $Kit 'pve-cluster\pve-cluster-2-migrate.patch'
$manifest = 'kubernetes/apps/bf2/bf2-webadmin.yaml'
$policy = 'kubernetes/apps/image-automation/image-automation.yaml'

function Invoke-Git([string[]]$Arguments) {
    $out = git -C $repo @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed:`n$out" }
    return $out
}

function Assert-CleanRepo {
    Invoke-Git @('fetch', '-q', 'origin') | Out-Null
    $branch = (Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')) -join ''
    if ($branch -ne 'main') { throw "pve-cluster is on '$branch', not main" }
    $dirty = Invoke-Git @('status', '--porcelain', '--', $manifest, $policy)
    if ($dirty) { throw "pve-cluster has local changes in $manifest or $policy" }
    Invoke-Git @('merge', '--ff-only', '-q', 'origin/main') | Out-Null
}

function Publish-Commit([string]$Message) {
    Invoke-Git @('diff', '--', $manifest, $policy) | ForEach-Object { Write-Host "  $_" }
    if ($NoPush) { Write-Warn "-NoPush: committed locally only"; Invoke-Git @('commit', '-q', '-m', $Message, '--', $manifest, $policy) | Out-Null; return }
    if (-not $Yes) { Confirm-Action "Commit and push this to pve-cluster main (Flux deploys it)?" }
    Invoke-Git @('commit', '-q', '-m', $Message, '--', $manifest, $policy) | Out-Null
    Invoke-Git @('push', '-q', 'origin', 'main') | Out-Null
    Write-Ok "pushed $((Invoke-Git @('rev-parse', '--short', 'HEAD')) -join '')"
    Invoke-WslScript "flux reconcile source git flux-system -n flux-system && flux reconcile kustomization apps -n flux-system --timeout=10m" | Out-Null
    Write-Ok "Flux reconciled"
}

function Get-DeploymentImage { return ((Invoke-Kubectl "-n $ns get deploy $deploy -o jsonpath='{.spec.template.spec.containers[0].image}'") -join '').Trim() }

function Wait-Rollout { Invoke-Kubectl "-n $ns rollout status deploy/$deploy --timeout=10m" | ForEach-Object { Write-Host "  $_" } }

function Test-SecretHasCertificate {
    $t = "{{if index .data `"ServerSettings__AdminChannel__Certificate`"}}present{{end}}"
    $tFile = Join-Path $WorkDir '_tmpl.txt'
    Write-LfFile $tFile $t
    try { $r = (Invoke-Kubectl "-n $ns get secret bf2-webadmin-env -o go-template-file='$(ConvertTo-WslPath $tFile)'") -join '' }
    finally { Remove-Item $tFile -ErrorAction SilentlyContinue }
    return ($r -match 'present')
}

switch ($Phase) {
    'Hold' {
        Write-Step "Hold Flux below 0.1.0"
        Assert-CleanRepo
        if ((Get-Content (Join-Path $repo $policy) -Raw) -match 'range: "<0\.1\.0"') { Write-Ok "already held"; return }
        Invoke-Git @('apply', '--check', $holdPatch) | Out-Null
        Invoke-Git @('apply', $holdPatch) | Out-Null
        Publish-Commit "bf2-webadmin: hold the image policy below 0.1.0 until the OpenBF2 migration"
        Invoke-WslScript "flux get image policy bf2-webadmin -n flux-system" | ForEach-Object { Write-Host "  $_" }
    }

    'Check' {
        Write-Step "Image $image"
        docker manifest inspect $image *> $null
        if ($LASTEXITCODE -eq 0) { Write-Ok "exists on Docker Hub" } else { Write-Fail "not found (create the GitHub release $($Config.webAdmin.version) after -Phase Hold)" }
        Write-Step "Certificate"
        if (Test-SecretHasCertificate) { Write-Ok "in the cluster secret bf2-webadmin-env" } else { Write-Fail "not yet in the cluster secret (the 1Password operator syncs it; wait or restart the operator)" }
        Write-Step "Cluster"
        Write-Host "  deployment image: $(Get-DeploymentImage)"
        Invoke-WslScript "flux get image policy bf2-webadmin -n flux-system" | ForEach-Object { Write-Host "  $_" }
        $held = (Get-Content (Join-Path $repo $policy) -Raw) -match 'range: "<0\.1\.0"'
        if ($held) { Write-Ok "hold in place (pve-cluster checkout)" } else { Write-Warn "no hold in the pve-cluster checkout: a 0.1.0 release would deploy by itself" }
    }

    'Migrate' {
        Write-Step "WebAdmin $image"
        docker manifest inspect $image *> $null
        if ($LASTEXITCODE -ne 0) { throw "$image does not exist" }
        if (-not (Test-SecretHasCertificate)) { throw "The cluster secret bf2-webadmin-env has no ServerSettings__AdminChannel__Certificate yet" }
        Assert-CleanRepo
        Invoke-Git @('apply', '--check', $migratePatch) | Out-Null
        Invoke-Git @('apply', $migratePatch) | Out-Null
        Publish-Commit "bf2-webadmin $($Config.webAdmin.version): the OpenBF2 admin channel on 4300 (certificate from bf2-webadmin-env), image policy released"
        if ($NoPush) { return }
        Wait-Rollout
        $log = Invoke-Kubectl "-n $ns logs deploy/$deploy --tail=400"
        $pinLine = @($log | Where-Object { $_ -match 'adminCertSha256 ([0-9a-f]{64})' } | Select-Object -Last 1)
        $listen = @($log | Where-Object { $_ -match 'Admin channel: listening on' } | Select-Object -Last 1)
        if ($listen) { Write-Ok ($listen -join '') } else { Write-Fail "no 'Admin channel: listening' line" }
        if ($pinLine -and ($pinLine[0] -match 'adminCertSha256 ([0-9a-f]{64})')) {
            Write-Ok "pin $($Matches[1]) (05-gameservers.ps1 reads it from this log; it must equal the adminCertSha256 line of --admin-cert-create)"
        } else { Write-Fail "no adminCertSha256 line in the log" }
        Write-Host "  image: $(Get-DeploymentImage)"
    }

    'Rollback' {
        Write-Step "Back to 0.0.2"
        Assert-CleanRepo
        $commit = (Invoke-Git @('log', '-1', '--format=%h', '--grep=the OpenBF2 admin channel on 4300', '--', $manifest)) -join ''
        if (-not $commit) { throw "No migrate commit found in pve-cluster" }
        if ($NoPush) { Invoke-Git @('revert', '--no-edit', $commit) | Out-Null; Write-Warn "-NoPush: reverted locally only"; return }
        if (-not $Yes) { Confirm-Action "Revert $commit in pve-cluster main and push (WebAdmin 0.0.2 again)?" }
        Invoke-Git @('revert', '--no-edit', $commit) | Out-Null
        Invoke-Git @('push', '-q', 'origin', 'main') | Out-Null
        Invoke-WslScript "flux reconcile source git flux-system -n flux-system && flux reconcile kustomization apps -n flux-system --timeout=10m" | Out-Null
        Wait-Rollout
        Write-Host "  image: $(Get-DeploymentImage)"
    }
}
