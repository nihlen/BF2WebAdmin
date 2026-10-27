# BF2WebAdmin agent instructions

A web admin for Battlefield 2 servers (ASP.NET Core with a React web UI, SQL Server or SQLite) with C# game modules
such as Heli2v2. [README.md](README.md) describes the product.

## Branches

- `admin-channel` (checked out here): the game-server backend is the native admin channel of the OpenBF2 module
  `libserverext.so` (openbf2-server-ext), not `mm_webadmin.py`. It is **not merged into `master`** and stays a separate
  branch for now. The backend is described in [docs/admin-channel.md](docs/admin-channel.md). The wire contract is
  openbf2-server-ext `docs/admin-channel-draft.md`; code follows it.
- `master`: the `mm_webadmin.py` text-socket version (port 4300), which production runs.
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
| `src/BF2WebAdmin.AppHost` | Aspire AppHost: runs the server and the Vite dev server; publishes one server image with the web UI in `wwwroot` |
| `src/BF2WebAdmin.Common`, `src/BF2WebAdmin.Shared` | Shared types and helpers |
| `src/BF2WebAdmin.Data` | Data access, EF migrations (SQLite), `Schema.sql` (SQL Server) |
| `test/BF2WebAdmin.Tests` | Unit tests (admin codec against the golden file `Fixtures/admin-v1-golden.txt`, handshake, TLS, session, Heli2v2) |
| `test/TestClient`, `test/TestCore`, `test/BF2WebAdmin.Benchmarks` | Old tools, not part of the test run |

## Build and test

The projects target .NET 9 (the AppHost .NET 10) and build with the installed .NET 10 SDK. Build and test projects,
not `BF2WebAdmin.sln`: the solution still lists the removed `BF2WebAdmin.Blazor` and not the AppHost or the web UI.

The web UI needs Node 24, pinned in its `package.json` for Volta (the machine default is Node 20, too old for Vite).
`dotnet publish` of the server runs `npm ci` and `npm run build` in `src/BF2WebAdmin.Web` and publishes `dist` as
`wwwroot` (`-p:SkipWebBuild=true` skips it).

```powershell
dotnet build src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj
dotnet test test\BF2WebAdmin.Tests\BF2WebAdmin.Tests.csproj        # 220 tests on 2026-10-26
dotnet publish src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj -c Release -o <dir>
BF2WebAdmin.Server.exe --admin-pin                                    # prints the admin-channel certificate pin

aspire run                                 # server + Vite dev server (aspire.config.json points at the AppHost)
cd src\BF2WebAdmin.Web
npm run build                              # type check and build the web UI
npm run api                                # after changing the web API: regenerate src/api/openapi.json and schema.d.ts
```

The server's admin-channel listener defaults to port 4310, which the localtest's play WebAdmin holds. To run a dev
server next to it, set `ServerSettings__AdminChannel__Port` (for example 4314) in the environment of `aspire run`.

End-to-end tests run in the localtest; see `system.md`. `setup.ps1` publishes this working tree, so uncommitted changes
are included; it records the commit and the changed paths in its `app\source.txt`.

## Rules

- The author has uncommitted work here (at the moment `src/BF2WebAdmin.Server/Constants/RconScript.cs`). Never stage, commit,
  revert or modify files you did not change; commit with explicit paths.
- Local commits are allowed. Don't push, don't merge `admin-channel` into `master`, and don't open PRs or issues.
- Port 16567 is reserved for the author's sessions. WebAdmin's admin-channel port must never be 16567.
- In the localtest, run `play-2v2.ps1 -Mode Status` before you stop, restart or republish anything. The author may be
  playing.
