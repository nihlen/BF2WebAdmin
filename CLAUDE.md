# BF2WebAdmin agent instructions

A web admin for Battlefield 2 servers (ASP.NET Core + Blazor, SQL Server or SQLite) with C# game modules such as
Heli2v2. [README.md](README.md) describes the product.

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
| `src/BF2WebAdmin.Server` | The host (`Program.cs`): web API, SignalR hubs (`Hubs/`), `AdminChannel/` (listener, handshake, session, protocol codec, state applier), game modules (`Modules/BF2/`: `Heli2v2Module`, `MapModule`, `ChopperMayhemModule`, ...), `RconClient` (ModManager rcon), configuration in `Configuration/` (`appsettings.json`, `appsettings.<profile>.json`; a published build runs profile `release`, log level Information) plus an `appsecrets.json` |
| `src/BF2WebAdmin.Blazor` | The web UI |
| `src/BF2WebAdmin.Common`, `src/BF2WebAdmin.Shared` | Shared types and helpers |
| `src/BF2WebAdmin.Data` | Data access, EF migrations (SQLite), `Schema.sql` (SQL Server) |
| `test/BF2WebAdmin.Tests` | Unit tests (admin codec against the golden file `Fixtures/admin-v1-golden.txt`, handshake, TLS, session, Heli2v2) |
| `test/TestClient`, `test/TestCore`, `test/BF2WebAdmin.Benchmarks` | Old tools, not part of the test run |

## Build and test

The projects target .NET 9 and build with the installed .NET 10 SDK. Build and test projects, not
`BF2WebAdmin.sln`: the author's uncommitted copy of the solution lists an untracked `BF2WebAdmin.AppHost`.

```powershell
dotnet build src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj
dotnet test test\BF2WebAdmin.Tests\BF2WebAdmin.Tests.csproj        # 220 tests on 2026-10-26
dotnet publish src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj -c Release -o <dir>
BF2WebAdmin.Server.exe --admin-pin                                    # prints the admin-channel certificate pin
```

End-to-end tests run in the localtest; see `system.md`. `setup.ps1` publishes this working tree, so uncommitted changes
are included; it records the commit and the changed paths in its `app\source.txt`.

## Rules

- The author has uncommitted work here (at the moment `BF2WebAdmin.sln`,
  `src/BF2WebAdmin.Server/Constants/RconScript.cs` and the untracked `BF2WebAdmin.AppHost/`). Never stage, commit,
  revert or modify files you did not change; commit with explicit paths.
- Local commits are allowed. Don't push, don't merge `admin-channel` into `master`, and don't open PRs or issues.
- Port 16567 is reserved for the author's sessions. WebAdmin's admin-channel port must never be 16567.
- In the localtest, run `play-2v2.ps1 -Mode Status` before you stop, restart or republish anything. The author may be
  playing.
