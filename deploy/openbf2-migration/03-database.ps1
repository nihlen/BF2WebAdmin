# Production SQL Server (database bf2 in the cluster's mssql pod):
#   1. a fresh backup (a one-off Job from the CronJob mssql-backup) unless -SkipBackup
#   2. Schema.sql's admin-channel lines: Server.AdminKey nvarchar(128) NULL, Server.IsTestServer bit NOT NULL DEFAULT 0
#      (additive: WebAdmin 0.0.2 keeps working against the altered table)
#   3. Server.AdminKey from 1Password (02-secrets.ps1) and IsTestServer 0 for every server in servers.json
#
#   powershell -ExecutionPolicy Bypass -File .\03-database.ps1 -Check      # read only: shows the rows, changes nothing
#   powershell -ExecutionPolicy Bypass -File .\03-database.ps1             # backup, schema, keys (asks first)

param(
    [switch]$Check,
    [switch]$SkipBackup
)

. "$PSScriptRoot\lib.ps1"

function Get-ServerRows {
    $hasKey = "CASE WHEN COL_LENGTH(N'dbo.Server', N'AdminKey') IS NULL THEN 'no column' ELSE 'column exists' END"
    return Invoke-Sql @"
SET NOCOUNT ON;
SELECT $hasKey AS AdminKeyColumn;
SELECT ServerId, ServerGroup, IpAddress, GamePort, QueryPort, RconPort,
       CASE WHEN RconPassword IS NULL OR RconPassword = '' THEN 'missing' ELSE 'set' END AS RconPassword
FROM dbo.Server ORDER BY ServerGroup, ServerId;
SELECT 'listed in servers.json but missing in dbo.Server: ' + v.id
FROM (VALUES $(((Get-Servers) | ForEach-Object { "(N'$($_.serverId)')" }) -join ', ')) AS v(id)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Server s WHERE s.ServerId = v.id);
"@
}

Write-Step "dbo.Server (read only)"
Get-ServerRows | ForEach-Object { Write-Host "  $_" }
foreach ($s in Get-Servers) {
    if ($s.serverId.Length -gt 20) { throw "$($s.name): ServerId '$($s.serverId)' is longer than nvarchar(20)" }
}
if ($Check) { return }

Write-Step "Keys from 1Password"
$keys = @{}
foreach ($s in Get-Servers) {
    $k = Read-OpField $Config.onePassword.gameServersItem $s.name
    if ($k -notmatch '^[0-9a-f]{32,128}$') { throw "$($s.name): no valid key in 1Password (run 02-secrets.ps1)" }
    $keys[$s.name] = $k
    Write-Ok "$($s.name) -> $($s.serverId)"
}

Confirm-Action "Back up bf2, alter dbo.Server and set the admin keys in PRODUCTION?"

if (-not $SkipBackup) {
    Write-Step "Backup"
    $job = "mssql-backup-openbf2-" + (Get-Date -Format 'yyyyMMddHHmm')
    Invoke-Kubectl "-n $($Config.cluster.namespace) create job --from=cronjob/$($Config.cluster.backupCronJob) $job" | Out-Null
    Invoke-Kubectl "-n $($Config.cluster.namespace) wait --for=condition=complete --timeout=30m job/$job" | Out-Null
    Write-Ok "job/$job complete (storage box mssql/bf2/daily)"
}

Write-Step "Schema"
Invoke-Sql @"
IF COL_LENGTH(N'[dbo].[Server]', N'AdminKey') IS NULL
    ALTER TABLE [dbo].[Server] ADD [AdminKey] [nvarchar](128) NULL;
GO
IF COL_LENGTH(N'[dbo].[Server]', N'IsTestServer') IS NULL
    ALTER TABLE [dbo].[Server] ADD [IsTestServer] [bit] NOT NULL CONSTRAINT [DF_Server_IsTestServer] DEFAULT 0;
GO
"@ | Out-Null
Write-Ok "AdminKey and IsTestServer exist"

Write-Step "Keys"
$updates = foreach ($s in Get-Servers) {
    "UPDATE dbo.Server SET AdminKey = N'$($keys[$s.name])', IsTestServer = 0 WHERE ServerId = N'$($s.serverId)';" +
    " IF @@ROWCOUNT <> 1 THROW 50000, N'no row for $($s.serverId)', 1;"
}
Invoke-Sql ("SET XACT_ABORT ON; BEGIN TRAN;`n" + ($updates -join "`n") + "`nCOMMIT;") | Out-Null
$check = Invoke-Sql @"
SET NOCOUNT ON;
SELECT ServerId, CASE WHEN AdminKey IS NULL THEN 'no key' ELSE 'key set (' + CAST(LEN(AdminKey) AS nvarchar(5)) + ' hex)' END, IsTestServer
FROM dbo.Server ORDER BY ServerId;
"@
$check | ForEach-Object { Write-Host "  $_" }
Write-Ok "keys written"
