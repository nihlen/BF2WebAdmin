# BF2WebAdmin agent instructions

A web admin for Battlefield 2 servers (ASP.NET Core with a React web UI, SQL Server or SQLite) with C# game modules
such as Heli2v2. [README.md](README.md) describes the product.

## Branches

- `admin-channel` (checked out here): the game-server backend is the native admin channel of the OpenBF2 module
  `libserverext.so` (openbf2-server-ext), not `mm_webadmin.py`. **Production runs it since 2026-11-08** (release 0.1.0;
  the migration runbook and scripts are in the author's private vps-setup repo, `C:\Data\Projects\VPS\openbf2-migration`).
  It is **not merged into `master`** and stays a separate branch for now. The backend is described in [docs/admin-channel.md](docs/admin-channel.md). The wire contract is
  openbf2-server-ext `docs/admin-channel-draft.md`; code follows it.
- `master`: the `mm_webadmin.py` text-socket version (port 4300, release 0.0.2), which production ran until 2026-11-08
  and is the rollback.
  `src/BF2WebAdmin.Server/README.md` documents that old protocol.

## The whole system

`C:\Data\Projects\openbf2-server-ext\docs\system.md` explains how WebAdmin fits with the module, the OpenBF2 engine,
the bf2-bots sidecar and the BF2WebAdmin localtest (`C:\Data\Projects\DotNet\BF2WebAdmin-localtest`). It covers how
to publish this repo into the localtest (`setup.ps1 -OpenBF2`), how to test on a separate instance with its own
database (`test-instance.ps1`, database `bf2test`), the ports, the logs and the working rules. Read it before you
deploy or test against a game server.

## Layout

| Path | Contents |
|---|---|
| `src/BF2WebAdmin.Server` | The host (`Program.cs`): web API for the web UI (`Api/ApiEndpoints.cs`: queries and commands), SignalR hub (`Hubs/ServerHub.cs`: live updates only), `AdminChannel/` (listener, handshake, session, protocol codec, state applier), game modules (`Modules/BF2/`: `Heli2v2Module`, `MapModule`, `ChopperMayhemModule`, ...), `RconClient` (ModManager rcon), configuration in `Configuration/` (`appsettings.json`, `appsettings.<profile>.json`; a published build runs profile `release`, log level Information) plus an `appsecrets.json` |
| `src/BF2WebAdmin.Web` | The web UI: Vite, React, TypeScript, TanStack Query and Router, shadcn/ui on Base UI, Tailwind CSS. Talks to the web API (types generated from its OpenAPI document into `src/api/`) and to the hub (event types in `src/api/events.ts`, by hand). See its README |
| `src/BF2WebAdmin.AppHost` | Aspire AppHost: runs the server, the Vite dev server and an optional OpenBF2 game server (`OpenBF2.cs`, started from the dashboard); publishes one server image with the web UI in `wwwroot` |
| `src/BF2WebAdmin.Common`, `src/BF2WebAdmin.Shared` | Shared types and helpers |
| `src/BF2WebAdmin.Data` | Data access, EF migrations (SQLite), `Schema.sql` (SQL Server). Includes `dbo.JoinHistory`, the BF2v2 site's player join table in the shared `bf2` database: WebAdmin inserts a row per human player connect (`AdminChannel/JoinHistoryWriter.cs`; see docs/admin-channel.md) |
| `test/BF2WebAdmin.Tests` | Unit tests (admin codec against the golden file `Fixtures/admin-v1-golden.txt`, handshake, TLS, session, Heli2v2, JoinHistory). `BF2WA_TEST_SQLSERVER` (a connection string, e.g. `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`) runs the SQL Server test, which creates and drops a scratch database |
| `test/TestClient`, `test/TestCore`, `test/BF2WebAdmin.Benchmarks` | Old tools, not part of the test run |

## Build and test

All projects target .NET 10 and build with the installed .NET 10 SDK. `BF2WebAdmin.sln` holds
the .NET projects (the AppHost too); the web UI is an npm project, not in the solution.

The web UI needs Node 24, pinned in its `package.json` for Volta (the machine default is Node 20, too old for Vite).
`dotnet publish` of the server runs `npm ci` and `npm run build` in `src/BF2WebAdmin.Web` and publishes `dist` as
`wwwroot` (`-p:SkipWebBuild=true` skips it).

```powershell
dotnet build src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj
dotnet test test\BF2WebAdmin.Tests\BF2WebAdmin.Tests.csproj        # 293 tests on 2026-11-08, 1 skipped without BF2WA_TEST_SQLSERVER
dotnet publish src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj -c Release -o <dir>
BF2WebAdmin.Server.exe --admin-pin                                    # prints the admin-channel certificate pin
BF2WebAdmin.Server.exe --admin-cert-create [--pem]                     # prints a new certificate for the Certificate secret, and its pin

aspire run                                 # server + Vite dev server (aspire.config.json points at the AppHost)
cd src\BF2WebAdmin.Web
npm run build                              # type check and build the web UI
npm run api                                # after changing the web API: regenerate src/api/openapi.json and schema.d.ts
```

The server's admin-channel listener defaults to port 4310, which the localtest's play WebAdmin holds; the server stops
when it cannot bind its port. Under `aspire run` the AppHost gives it 4314 instead (`AdminChannelPort` in the AppHost's
configuration). Running the server without Aspire next to the localtest needs `ServerSettings__AdminChannel__Port`.

The `openbf2` resource in the Aspire dashboard is a game server container (OpenBF2 with the server extension) that
starts only when you press Start; nothing is pulled or created before. Its image is local and never pulled:
`bf2wa-localtest-openbf2:latest`, built by the localtest's `openbf2/build.sh`; other machines set `OpenBF2:Image`.
The AppHost connects both ends: the container's `adminConnect` (the dev admin port), `adminServerId 127.0.0.1:16575`, a
new admin key per run and the pin of the dev certificate (`%LOCALAPPDATA%\BF2WebAdmindmin-channel.pfx`, created by
the AppHost if missing), and the matching `ServerSettings:GameServers` entry for the server, so the game server is in
the server list without adding it. Ports: game 16575/udp, query 29907/udp, rcon 127.0.0.1:4727, bot channel
127.0.0.1:16708. Configuration section `OpenBF2` in the AppHost (`Image`, `GamePort`, `QueryPort`, `RconPort`,
`BotPort`, `AdminKey`, `RconPassword`, `ServerGroup`, `ServerName`, `CertificatePath`). Do not save that server in the
edit form: a database row wins over the configured entry and keeps a key from an earlier run.

End-to-end tests run in the localtest; see `system.md`. `setup.ps1` publishes this working tree, so uncommitted changes
are included; it records the commit and the changed paths in its `app\source.txt`.

## Rules

- The author may have uncommitted work here. Never stage, commit, revert or modify files you did not change; commit
  with explicit paths.
- Local commits are allowed. Don't push, don't merge `admin-channel` into `master`, and don't open PRs or issues.
- Port 16567 is reserved for the author's sessions. WebAdmin's admin-channel port must never be 16567.
- In the localtest, run `play-2v2.ps1 -Mode Status` before you stop, restart or republish anything. The author may be
  playing.
