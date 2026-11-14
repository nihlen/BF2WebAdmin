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
  `src/BF2WebAdmin.Server/README.md` on that branch documents that old protocol.

## The whole system

`C:\Data\Projects\openbf2-server-ext\docs\system.md` explains how WebAdmin fits with the module, the OpenBF2 engine,
the bf2-bots sidecar and the BF2WebAdmin localtest (`C:\Data\Projects\DotNet\BF2WebAdmin-localtest`). It covers how
to publish this repo into the localtest (`setup.ps1 -OpenBF2`), how to test on a separate instance with its own
database (`test-instance.ps1`, database `bf2test`), the ports, the logs and the working rules. Read it before you
deploy or test against a game server.

## Layout

| Path | Contents |
|---|---|
| `src/BF2WebAdmin.Server` | The host (`Program.cs`): web API for the web UI (`Api/ApiEndpoints.cs`: queries and commands), SignalR hub (`Hubs/ServerHub.cs`: live updates only; the DTOs and hub events it and the web API send are in `Communication/`), the export API for other programs (`Api/ExportEndpoints.cs`, contract in [docs/export-api.md](docs/export-api.md): matches by change cursor, player joins, servers), `Controllers/ServerController.cs` (`/api/v1/servers` with the API key; BF2v2's API adds and removes its temporary VPS servers with its `POST` and `DELETE`), `Services/StaleMatchCloser.cs` (ends matches left open), `AdminChannel/` (listener, handshake, session, protocol codec, state applier, `ScriptRunner.cs` for game scripts), `GameScripts/` (the game scripts: TypeScript that WebAdmin sends to a game server's module, which runs it every tick; an npm project with its own tests, built and embedded by the server's build; `nasa` for `.nasa` and `dance` for `.dance`, each with a fallback for servers whose module runs no scripts; [docs/scripts.md](docs/scripts.md)), game modules (`Modules/`: `BF2Module` with `AdminModule`, `ServerControlModule` and `FunModule` on every server, `MapModule`, `ChopperMayhemModule`, ...; `Modules/DiscordBot/`: a server's Discord bot (`DiscordModule` for the game's messages, `DiscordConnection` for the client and its send queue, `DiscordCommandHandler` for what people write to it); `Modules/Heli2v2/`: `Heli2v2Module` with `MatchTracker` (rounds and matches as they are played), `MatchStore` (saves), `MatchStatistics` (summaries) and the practice modules that load with it; their services are registered once in `ModuleServices.cs` and each server gets a scope), `StreamBot/` (the stream bot's hub `/hubs/stream-bot`, its own key `Authentication:StreamBotKey`; [docs/stream-bot.md](docs/stream-bot.md)), `Services/ServerAdmin.cs` (kick, warn, ban, unban and the `!` admin commands of the web chat box and Discord, all over the admin channel; the bans are the engine's own list, WebAdmin records who and why in `ServerBan`), configuration in `Configuration/` (`appsettings.json`, `appsettings.<profile>.json`; a published build runs profile `release`, log level Information) plus an `appsecrets.json` |
| `src/BF2WebAdmin.Web` | The web UI: Vite, React, TypeScript, TanStack Query and Router, shadcn/ui on Base UI, Tailwind CSS. Talks to the web API (types generated from its OpenAPI document into `src/api/`) and to the hub (event types in `src/api/events.ts`, by hand). See its README |
| `src/BF2WebAdmin.AppHost` | Aspire AppHost: runs the server, the Vite dev server and an optional OpenBF2 game server (`OpenBF2.cs`, started from the dashboard); publishes one server image with the web UI in `wwwroot` |
| `src/BF2WebAdmin.Common` | Game entities (`Entities/Game`) and helpers (country lookup, telemetry, caching) |
| `src/BF2WebAdmin.Data` | Data access for three providers, chosen by `Database:Provider` (`sqlite`, `sqlserver`, `postgres`; without it the connection string decides) in `DatabaseProvider.cs`, wired by the server's `DatabaseSetup.cs`; see [docs/database.md](docs/database.md). SQLite: `BF2Context` with the EF migrations in `Migrations/`. Postgres (database `bf2webadmin`): `PostgresBF2Context` (snake_case tables, paths in `match_round_player_path`, sequence `match_change_seq`) with its own migrations in `Migrations/Postgres/`. SQL Server: `SqlServerBF2Context` (the dbo tables of the old Dapper `Schema.sql`, sequence `MatchChangeSeq`) with its migrations in `Migrations/SqlServer/`; the first one is idempotent and takes over a database from before EF Core. The EF repositories serve all three, one short context per call. Player joins: `dbo.JoinHistory` (`player_join` on Postgres), a row per human player connect (`AdminChannel/JoinHistoryWriter.cs`; see docs/admin-channel.md). Ban records: `ServerBan` (`server_ban`), keyed by server and banned hash or address |
| `test/BF2WebAdmin.Tests` | Unit tests (admin codec against the golden file `Fixtures/admin-v1-golden.txt`, handshake, TLS, session, Heli2v2, JoinHistory). `BF2WA_TEST_SQLSERVER` (a connection string, e.g. `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`) runs the SQL Server tests (`SqlServerTests.cs`: migrations, taking over a database from the old `Schema.sql` in `Fixtures/`, repositories), which create and drop scratch databases. `BF2WA_TEST_POSTGRES` (a connection string of a server where it may create databases, e.g. `Host=127.0.0.1;Port=5432;Username=postgres;Password=dev`) runs the Postgres tests (`PostgresTests.cs`: migrations and repositories) |

## Build and test

All projects target .NET 10 and build with the installed .NET 10 SDK. `BF2WebAdmin.sln` holds
the .NET projects (the AppHost too); the web UI is an npm project, not in the solution.

The web UI needs Node 24, pinned in its `package.json` for Volta (the machine default is Node 20, too old for Vite).
`dotnet publish` of the server runs `npm ci` and `npm run build` in `src/BF2WebAdmin.Web` and publishes `dist` as
`wwwroot` (`-p:SkipWebBuild=true` skips it). `dotnet build` of the server also needs Node when a game script changed:
it runs `npm run build` in `src/BF2WebAdmin.Server/GameScripts` (`npm ci` first without `node_modules`) and embeds the
result (`-p:SkipGameScripts=true` embeds what `GameScripts/dist` already holds).

```powershell
dotnet build src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj
dotnet test test\BF2WebAdmin.Tests\BF2WebAdmin.Tests.csproj        # 466 tests on 2026-11-14, some skipped without BF2WA_TEST_SQLSERVER and BF2WA_TEST_POSTGRES
dotnet publish src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj -c Release -o <dir>
BF2WebAdmin.Server.exe --admin-pin                                    # prints the admin-channel certificate pin
BF2WebAdmin.Server.exe --admin-cert-create [--pem]                     # prints a new certificate for the Certificate secret, and its pin

cd src\BF2WebAdmin.Data                    # after a model change, a migration per EF provider
dotnet ef migrations add <Name> --context BF2Context --output-dir Migrations
dotnet ef migrations add <Name> --context PostgresBF2Context --output-dir Migrations/Postgres
dotnet ef migrations add <Name> --context SqlServerBF2Context --output-dir Migrations/SqlServer

aspire run                                 # server + Vite dev server (aspire.config.json points at the AppHost)
cd src\BF2WebAdmin.Web
npm run build                              # type check and build the web UI
npm run api                                # after changing the web API: regenerate src/api/openapi.json and schema.d.ts

cd src\BF2WebAdmin.Server\GameScripts
npm test                                   # type check and build the game scripts, then their tests (15 on 2026-11-14)
```

The server's admin-channel listener defaults to port 4310, which the localtest's play WebAdmin holds; the server stops
when it cannot bind its port. Under `aspire run` the AppHost gives it 4314 instead (`AdminChannelPort` in the AppHost's
configuration). Running the server without Aspire next to the localtest needs `ServerSettings__AdminChannel__Port`.

The `openbf2` resource in the Aspire dashboard is a game server container (OpenBF2 with the server extension) that
starts only when you press Start; nothing is pulled or created before. Its image is local and never pulled:
`bf2wa-localtest-openbf2:latest`, built by the localtest's `openbf2/build.sh` (the production 2v2 image of bf2-docker,
without ModManager or rcon, with local builds of OpenBF2 and the module); other machines set `OpenBF2:Image`.
The AppHost connects both ends: the container's `ENV_ADMIN_CONNECT` (the dev admin port), `ENV_ADMIN_SERVER_ID
127.0.0.1:16575`, a new admin key per run and the pin of the dev certificate
(`%LOCALAPPDATA%\BF2WebAdmin\admin-channel.pfx`, created by the AppHost if missing), and the matching
`ServerSettings:GameServers` entry for the server, so the game server is in the server list without adding it. Ports:
game 16575/udp, query 29907/udp, bot channel 127.0.0.1:16708; no rcon (console lines go through WebAdmin).
Configuration section `OpenBF2` in the AppHost (`Image`, `GamePort`, `QueryPort`, `BotPort`, `AdminKey`,
`ServerGroup`, `ServerName`, `CertificatePath`). Do not save that server in the edit form: a database row wins over the
configured entry and keeps a key from an earlier run.

End-to-end tests run in the localtest; see `system.md`. `setup.ps1` publishes this working tree, so uncommitted changes
are included; it records the commit and the changed paths in its `app\source.txt`.

## Rules

- The author may have uncommitted work here. Never stage, commit, revert or modify files you did not change; commit
  with explicit paths.
- Local commits are allowed. Don't push, don't merge `admin-channel` into `master`, and don't open PRs or issues.
- Port 16567 is reserved for the author's sessions. WebAdmin's admin-channel port must never be 16567.
- In the localtest, run `play-2v2.ps1 -Mode Status` before you stop, restart or republish anything. The author may be
  playing.
