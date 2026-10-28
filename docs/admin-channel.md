# Admin channel (OpenBF2 serverext) backend

BF2WebAdmin's game-server backend is the native admin channel of the OpenBF2 engine module `libserverext.so`
(openbf2-server-ext). It replaces `mm_webadmin.py` and its tab-separated text protocol on TCP 4300, which is gone
from this branch (decision B2: production keeps running an older WebAdmin image until the servers move to OpenBF2).

The wire format is admin protocol version 1, "Wire format (admin protocol version 1)" in openbf2-server-ext
`docs/admin-channel-draft.md` (approved 2026-10-23), with decisions B1-B6. This document covers the C# side.

## Design

```
module (game server) ── TLS 1.3, pinned ──> AdminListener ── AdminHandshake (HMAC) ──> AdminSession
                                                                                          │ apply loop, in order
                                         modules <── Mediator <── GameServer <── AdminStateApplier
modules ── IGameWriter (AdminGameWriter) ── typed commands / Console, in call order ──> AdminSession ──> module
```

| File (`src/BF2WebAdmin.Server/AdminChannel/`) | Role |
|---|---|
| `AdminProtocol.cs` | Message types, records for every message and structure, `Encode`/`Decode`, `AdminFrameDecoder` (1 MiB inbound frames, nonzero reserved and oversize rejected), `AdminWriter`/`AdminReader` (strict UTF-8), the proof transcript |
| `AdminCertificate.cs` | Creates WebAdmin's self-signed ECDSA P-256 certificate once (100 years) and computes the pin: SHA-256 of the SubjectPublicKeyInfo, lowercase hex |
| `AdminListener.cs` | TCP listener (default port 4310), `SslStream` TLS 1.3 only, then the handshake; TLS and handshake together within 10 s of the connect. Optional plain listener on 127.0.0.1 for `adminTls 0` |
| `AdminHandshake.cs` | `Hello` → `Challenge` (WebAdmin proves the key first) → `Auth`, proofs compared with `CryptographicOperations.FixedTimeEquals`; version ≠ 1 is `Error` 1, unknown id or no key `Error` 8 |
| `AdminSession.cs` | One link: read loop (answers `Ping`, completes requests), write loop (one ordered queue), apply loop (sequence check and `RequestSnapshot` on a gap, stream frames latest-wins), keepalive (`Ping` every 15 s, close after 45 s of silence), optional recording of every received message |
| `AdminStateApplier.cs` | Maps `Welcome`, `Snapshot` (reconciled with the existing players, so modules keep their `Player` objects across resyncs and reconnects), the streams and every event onto `GameServer`; the B6 player key; the subscriptions (at 30 Hz while a 3D viewer is open, with the modules still fed at their own rate) |
| `ViewerFeed.cs` | The 3D viewer: `ViewerRegistry` (which hub connections watch which server), the 30 Hz stream frames to the hub group `viewer:<serverId>` (sent from its own task, so a slow browser never holds up the link), and the last 20 traced TV missiles (`ProjectileTrace` with its `ProjectileCollision`s) |
| `AdminGameWriter.cs` | `IGameWriter` on the current session: typed commands, `Console` lines in order with them, `ListObjects`, `SpawnObject`, `SetObjectActive`, `Subscribe` |
| `GameServerRegistry.cs` | Replaces `SocketServer`/`ISocketServer` as `IGameServerRegistry`: configured servers (DB + config), the `GameServer` per server id, key lookup |

The seam the inventory suggested holds: modules still see `IGameServer`, the `GameEvents.cs` records and
`IGameWriter`. `GameServer`'s mutators left `IGameServer`; only the applier calls them.

### Identity and keys

- The module's `adminServerId` is the server's `Server.ServerId` (for example `127.0.0.1:16569`). The source address
  of the connection does not matter any more (no private-IP acceptance, no `wa connect` rcon trigger).
- `Server.AdminKey` holds the key as hex (32-128 digits, 16-64 bytes), the same text as `adminKey` in
  `serverext.cfg`. No key, or a malformed one: the server cannot connect. Changing it in the edit form closes the
  current link; the module redials and must match the new key.
- `Server.IsTestServer` (decision B6): players are keyed (`Player.Hash`) by the CD-key hash when the module sends one.
  Without a hash (every player on a LAN server, bots, `adminSendIdentity 0`) a test server keys them by name as
  `name:<name>`, which cannot collide with a 32-digit hash. On other servers such a player has an empty key: no auth
  level, and it is logged. Name keys are never sent to ModManager's `iga addAdmin`.

### Ordering and resync

- Every command, typed or `Console`, is queued on one ordered link, and the module runs them in arrival order (at
  most 64 per tick). So `SetObjectActive` followed by raw `object.*` lines, or `activeSafe` followed by a getter,
  behave as the old single socket did.
- Replies are matched by request id. Fire-and-forget commands log an `Error` reply; awaited ones throw
  `AdminCommandException` with the module's code.
- Events carry sequence numbers. After a `Snapshot` (`lastSeq`), events at or below it are dropped; a jump starts one
  `RequestSnapshot`. A reconnect is a new session with a full snapshot. Subscriptions are made again after every
  `Welcome`: players and `agm114_hellfire_tv` projectiles at the module's tracker interval (default 0.25 s, Heli2v2's
  `.interval`), vehicles and game state at 1 Hz, and the hit/miss replay (stream 20) for `agm114_hellfire_tv`.
  While a 3D viewer is open on the server, players, vehicles, projectiles and aim (stream 5) run at 30 Hz; the
  applier still hands the modules players and projectiles at the tracker rate and vehicles at 1 Hz.

### 3D viewer

The web UI's 3D viewer (`/server/<id>/viewer`, `src/BF2WebAdmin.Web/src/viewer/`, ported from BF2v2's round replay)
draws the server live from the hub's `ViewerFrame` events (hub method `SubscribeViewer`), keeps ten minutes of them for
its timeline (play, drag, step one tick), and replays any of the last 20 TV missiles from
`GET /api/servers/{id}/missiles/{key}`: per tick the missile and each target where it is now (red) and where the server
tested its hull (blue, rewound by the shooter's lag; rotors and other child parts are tested at the red pose). Its
player view follows `PlayerState`'s view mode: the server's camera object (`AimStream`) for the inside view and the
TV launcher, the chase, front chase, nose and fly-by views computed from the vehicle with the camera templates'
values. The map and helicopter models are BF2v2's (`public/viewer/`, only `dalian_2_v_2` has a 3D map).

## Setup

1. **Certificate and pin.** WebAdmin creates its certificate on first start at `ServerSettings:AdminChannel:CertificatePath`
   (default `%LOCALAPPDATA%\BF2WebAdmin\admin-channel.pfx`, or `~/.local/share/BF2WebAdmin/admin-channel.pfx` on Linux;
   keep it on a volume in Docker, outside the repo) and logs:

   `Admin channel: TLS certificate CN=BF2WebAdmin admin channel, pin for serverext.cfg: adminCertSha256 <64 hex>`

   `BF2WebAdmin.Server --admin-pin` creates or loads the certificate, prints only the pin and exits. Rotating the
   certificate means updating every server's pin.
2. **Config** (`appsettings`/`appsecrets`):

   ```json
   "ServerSettings": {
     "IpAddress": "...",
     "AdminChannel": { "Port": 4310, "BindAddress": "0.0.0.0", "CertificatePath": "/data/admin-channel.pfx", "CertificatePassword": "" }
   }
   ```

   `Port` must not be 16567. `ServerSettings:Port`, `PrintSendLog` and `PrintRecvLog` are gone.
3. **Database.** Run the EF migration `AdminChannel` (SQLite) or the new `ALTER TABLE` lines in `Schema.sql`
   (SQL Server): `Server.AdminKey nvarchar(128) NULL`, `Server.IsTestServer bit NOT NULL DEFAULT 0`. Then set the key
   (and the test-server flag) in the server edit form.
4. **Game server** (`serverext.cfg` beside the OpenBF2 `bf2` binary, module loaded with `openbf2.addModDll serverext`):

   ```
   adminConnect webadmin.example.org:4310
   adminServerId 1.2.3.4:16567
   adminKey <the same hex as Server.AdminKey>
   adminCertSha256 <the pin WebAdmin logs>
   ```

   Optional: `adminSendIdentity` (default 1 with TLS; WebAdmin needs the IPs and hashes), `adminAllowConsole` (default
   1; with 0 every raw console script of the modules is refused, see below), `adminAllowObjects` (default 1;
   `.nofences` needs it). `mm_webadmin` must not be loaded.

## What changed in the modules

| Where | Before | Now |
|---|---|---|
| `IGameWriter` | `SendRcon`, `GetRconResponseAsync` (`\b`-joined text, `response<N>` codes) | `SendConsole(lines)`, `ConsoleAsync(line)` → output lines; typed `KickAsync`, `BanAsync`, `SendKill`, `SendHealthFraction`, `SendTeleport(pos, rot)`, `SendSetObjectActive`, `ListObjectsAsync`, `SpawnObjectAsync`, `SendDeleteObject`, `SendRestartMap` |
| Heli2v2 `.nofences` | `object.listObjectsOfTemplate` parsed with `ID ([0-9]+)`, then `object.active idN` + `object.delete` | `ListObjects` (console ids) + `DeleteObject` by console id |
| Heli2v2 `.noclip`, leaving a noclip heli; BF2 `.freeze`; ChopperMayhem anti-NASA | `object.active id{RootVehicleId}` (a `findId` console id) | `SetObjectActive` with `Vehicle.ConsoleId`, then the same console lines. `RootVehicleId` is now the networkable id |
| Heli2v2 TV missile getters | `activeSafe` then `GetRconResponseAsync` | `activeSafe` then `ConsoleAsync`, ordered on the link |
| Heli2v2 team mismatch | `FixTeamMismatchAsync` re-queried GameSpy | removed: `TeamChange` covers every source |
| `.kill`, `.killid`, switching (`SwitchPlayer`) | `damage <id> 1` (`mm_webadmin` also deleted the vehicle) | `KillPlayer` with the vehicle flag (wrecks the vehicle when nobody else is in it; nothing deleted); a switch is one `SetTeam` with its kill flag |
| `.repair` | `damage <id> 10000` | `SetHealth` fraction 1 |
| Teleport + rotate pairs (pad, dance, playback, portals) | two text commands | one `Teleport` with both |
| `.clear`, `.undo` | `admin.restartmap` console line | `Map` restart |
| `.kick <Name>`, `.ban <Name>` | (none; `!kick` goes to ModManager's `iga` over classic rcon) | new chat commands on the typed `Kick`/`Ban`; the refusal is said in chat |
| Kill classification | event-name split, kill/death order guessed, victim position wrong | `Kill` kind and positions; `PlayerKillEvent` for kills and roadkills (`"roadkill"` weapon as before), team kills, suicides and crashes as `PlayerDeathEvent` with `IsSuicide`, as before. ChopperMayhem's baserape check uses the correct victim position |
| Projectile owner | nearest `ahe_` player guessed | the module's owner |
| Text | Windows-1252 out, UTF-8 in (`§` arrived as `�`, `Message.Text` mapped it back) | UTF-8 both ways; the module converts at the engine, `§` intact; the `�` hack is gone |
| Event recording (`.starteventrecorder`) | raw text lines | every received admin message (`GameServer.StartRecording`) |
| Removed | `SocketServer.cs`, `GameReader.cs`, `EventHandler.cs`, `GameWriter.cs`, `IGameReader.cs`, `IEventHandler.cs`, the `wa connect` loop, the mm_autobalancer team workaround | |

Unchanged on purpose: ModManager's classic rcon (`RconClient`) for `iga` admin sync, `!` commands from the web UI and
Discord, and the web UI's rcon box. `MapModule`'s object spawning keeps the console `ObjectSpawner` script
(`RconScript.AddObject`): the typed `SpawnObject` creates server-only objects for templates without a networkable
(the module's finding 17), and the script makes the template networkable first.

## Results (2026-10-23)

### Unit tests

`test/BF2WebAdmin.Tests`: 220 pass (5 existing, 215 new).

- **Golden file.** `Fixtures/admin-v1-golden.txt` is openbf2-server-ext `tests/fixtures/admin-v1-golden.txt` at
  `0aab582`. All 60 frames are encoded from the same sample values as `adminclient.py golden_samples()` and match
  byte for byte; all 60 also decode and re-encode identically.
- **Malformed input.** Every payload cut short at each byte and one byte too long is rejected. Also rejected:
  oversize and nonzero-reserved frames, unknown types, and five invalid UTF-8 forms (truncated, a bare 0xA7, overlong,
  surrogate, beyond U+10FFFF).
- **Byte-at-a-time framing.** All 60 frames fed one byte at a time decode.
- **Handshake.** The transcript layout is checked, and an HMAC proof against a Python `hmac` reference value. Over
  loopback TCP against a fake module: the right key passes; the module refuses another key; a wrong module proof
  gets `Error` 8; an unknown id gets `Error` 8; version 2 gets `Error` 1; key parsing is checked.
- **TLS 1.3.** A generated certificate passes with a client that pins the SPKI exactly as the module does.
- **Session.** A gap brings one `RequestSnapshot`; after the snapshot, events at or below `lastSeq` are dropped.
  Replies and an `Error` reply arrive as `AdminCommandException`. `Ping` gets a `Pong`. Commands keep call order. A
  malformed frame gets `Error` 2 and closes the link. Stream frames are latest-wins.
- **B6 key.** A hash, a name key on a test server, and no key elsewhere.

### End to end (localtest, OpenBF2 variant)

**Setup**

| Item | Value |
|---|---|
| WebAdmin | This branch, published by `setup.ps1 -Repo <worktree> -OpenBF2`, on Windows 11 (`SslStream`/SChannel TLS 1.3) |
| Server | Container `bf2wa-localtest-openbf2`: OpenBF2 `873d1b1` and module `0aab582`, built on bullseye against OpenSSL 1.1.1w |
| Map and content | netsky content, `dalian_2_v_2` |
| Ports | game 16569, GameSpy 29901, rcon 4721, bot channel 127.0.0.1:16705 |
| Admin channel | Dials `host.docker.internal:4310` |
| Clients | Bots from openbf2-server-ext `tools/testclient/serverext_client.py` at `0aab582`; web actions through `tools\hubclient`, i.e. the same hub methods the Blazor UI uses |

**Build on OpenSSL 1.1.1.** This was new ground; phase 1 only ran 3.0.2. The module built with no warnings and its
ctest suites (including the TLS test) passed. It links `libssl.so.1.1`/`libcrypto.so.1.1`, which the base image
already ships, so nothing was installed.

| Check | Result |
|---|---|
| TLS 1.3 with the pin | **Pass.** `TLS_AES_256_GCM_SHA384`, "certificate pin matches". The peer pin the module printed when it was given a wrong pin equals the pin WebAdmin logs, so both sides compute the SPKI hash the same way |
| Handshake, Welcome, Snapshot, subscriptions | **Pass.** Welcome 1029 B with 34 maps (`MapsChanged` lists all 34 with mode and size) and capabilities 7. Streams 1 and 3 at 4 Hz, 2 and 4 at 1 Hz. None of the 30 `InitServer` console lines was refused |
| Wrong key | **Pass.** The key was changed through the edit form's hub `SetServer`. The link closed, the module refused WebAdmin's proof (`Error` 8), and WebAdmin logged "adminKey differs from the Server.AdminKey". After the key was restored, the module reconnected within its 60 s backoff |
| Wrong pin | **Pass.** The container was recreated with a bad `adminCertSha256`. The module aborted TLS and WebAdmin logged `UnknownCA`. Restored afterwards |
| Players, positions, scores live | **Pass.** Bots appear with name keys `name:[BOT] alpha` on the test server (B6), teams CH/US, spawns, `PlayerVehicleEvent` (`ahe_z10`, `ahe_ah1z`) and scores. `PlayerPositionEvent` reaches the hub at 4 Hz per player, with the bots' own yaw/pitch/roll. Server list and snapshot are correct over REST and the hub |
| Kill, death, crash | **Pass.** The bot client's `damage --crash` scenario passed 18/18. In WebAdmin: `PlayerKillEvent` (A → C, `ahe_z10_S8Launcher`), then `PlayerDeathEvent` C `IsSuicide=False`. The crash is a `NoAttacker` kill, then `PlayerDeathEvent` `IsSuicide=True`; `VehicleDestroyed` and bot created/removed events also arrived |
| Heli2v2 `.pad` | **Pass.** Both helis were teleported (position and rotation) to their team's pad with `SetHealth` 875. It also works after a reconnect, where the snapshot seated the players |
| Heli2v2 `.noclip` on/off, `.mg 0/1` | **Pass.** No refusal (`SetObjectActive` by console id, then the console lines) |
| Heli2v2 `.tv` (TV-missile getters) | **Pass.** All 10 values were read through ordered `activeSafe` + `ConsoleAsync` |
| Heli2v2 `.nofences` | **Pass.** 71 fences and lamp posts listed and deleted by console id (`ListObjects` + `DeleteObject`) |
| `.repair` | **Pass.** No refusal (`SetHealth` fraction 1) |
| `.kick`, `.ban` on bots | **Pass (refused).** `Error` 6 "bots cannot be kicked or banned", said in chat |
| `.load localtest-towers` | **Pass.** Three `coolingtower_01` spawned with the console `ObjectSpawner` script |
| `.clear` | **Pass.** Typed `Map` restart: game status 1 → 3 → 1, `RoundStart`, the bots respawned |
| `.o` from the web | Refused in chat ("needs an in-game player position"). It used to throw `NullReferenceException`. In-game `.o` needs a human |
| Chat, WebAdmin → game | **Pass.** Web chat → `Say` → it came back as `Chat` channel 12, with `§` intact (UTF-8) |
| Chat, game → WebAdmin | **Pass** for server chat: `exec game.sayAll ...` from the web rcon box arrived in WebAdmin's chat log. Bot player chat (bot channel `Say`) arrives as player chat since 2026-10-24; human chat is untested |
| Map change | **Pass.** `!map daqing_2_v_2 gpm_cq 16` (web rcon box) gave EndGame, `RoundEnd`, PreGame, Playing, `RoundStart`, `MapChangedEvent 1,daqing_2_v_2`. The link stayed up through the level load, and the map was changed back afterwards |
| WebAdmin restart | **Pass.** The module redialled with backoff (1, 2.3, 4.2 s ...). Session 2's snapshot rebuilt both bots, in their helis |
| Game server restart | **Pass.** A new server process gave session 1; the snapshot had 0 players, so the stale bots left through `PlayerLeftEvent` |
| Module cost | p50 10 µs, p99 30-140 µs per tick. 0 events dropped |

**Not exercised: needs a human client or an internet server**

- Heli2v2 rounds and matches with a human. They start on player chat (`rdy`/`go` in Global). Since 2026-10-24 bots
  chat as players (bot channel `Say`, openbf2-server-ext module `100d53e`), and Heli2v2 started, ended and stored 11
  rounds of bot crews on a separate instance; no human has played a round yet.
- Human chat on channels 0/1 (Global/Team): the channel name WebAdmin gets, and the `HUD_TEXT_CHAT_*` stripping.
- In-game `.o`, `Revive`, `ControlPoint` captures, a real kick or ban (and its `Kick`/`Ban` events and disconnect
  reason).
- The CD-key hash (internet server only), country lookup from a real IP, and the `iga addAdmin` sync of real hashes.
- A sequence gap at runtime. It is covered by unit tests; the module's own phase-1 test exercised the drop path.
- Discord: not configured locally.
- The Blazor pages themselves were not looked at. Their data path (the hub events and methods) was exercised with
  hubclient.

## Contract points that proved wrong or need attention

1. **Bot channel chat is not UTF-8.** The bot channel's `Chat` delivered `§` (0xA7) as U+FFFD (`[�C1001netsky…`) to
   the bot client, and the Windows console crashed printing it. The admin channel converts correctly. This is a
   module finding for the bot channel, not for WebAdmin.
2. **The typed `SpawnObject` does not fit map mods.** Most map-mod templates have no networkable, so
   `createObjectOnAllClients` creates server-only objects (phase-1 finding 17). `MapModule` keeps the console
   `ObjectSpawner` script, which needs `adminAllowConsole 1`. With `adminAllowConsole 0`, map mods, noclip, freeze,
   gravity, TV-missile settings and `InitServer` all stop working. The `Welcome` capability bit is logged as a warning.
3. **Changing a key costs up to a minute.** After a failed attempt the module backs off up to 60 s, so a key fixed
   in the edit form takes effect at the next retry.
4. **Teleport rotation.** The module applies it (it logs `position rotation`), but a hovering bot's heading hold
   turned the heli back within seconds. Whether a human's heli keeps the pad heading is unverified.
5. **`Server.ServerId` is `nvarchar(20)`.** It is one character short of `255.255.255.255:65535`, and it is now the
   module's `adminServerId`. This is a latent limit, unchanged.
6. **The key is visible to edit-form users.** `GET /api/servers/{id}/settings` (the hub's `GetServer` before the React
   web UI) returns `AdminKey` to Administrator-role users of the edit
   form (shown as a password field). It cannot be hashed: HMAC needs the raw key.
7. **Kick and ban.** The web UI and Discord still kick and ban through ModManager's `iga` (classic rcon). The typed
   commands are reached only through the new `.kick`/`.ban` chat commands.

## Local test environment changes (`BF2WebAdmin-localtest`, not a git repo; originals in `scratch\orig-2026-10-23\`)

| File | Change |
|---|---|
| `setup.ps1` | `-Repo <tree>` (default `..\BF2WebAdmin`, unchanged) and `-OpenBF2` |
| `.env` | `ADMIN_KEY` generated once (`setup.ps1`); `ADMIN_CERT_SHA256` written from `BF2WebAdmin.Server.exe --admin-pin` |
| `appsecrets.json` | `AdminChannel` (port 4310, certificate in `secrets\admin-channel.pfx`) |
| Containers | With `-OpenBF2`, the `bf2` container is stopped and `bf2-openbf2` recreated; without it, the reverse |
| `docker-compose.openbf2.yml` | New: service `bf2-openbf2` (profile `openbf2`, container `bf2wa-localtest-openbf2`, image `bf2wa-localtest-openbf2`). Ports 16569/udp, 29901/udp, 127.0.0.1:4721→4711, 127.0.0.1:16705 (bot channel). `SERVEREXT_CFG_EXTRA` carries `adminConnect host.docker.internal:4310`, `adminServerId 127.0.0.1:16569`, the key, the pin, `allowTeleport 1`, `allowRoundRestart 1`. `docker-compose.yml` is unchanged |
| `openbf2\build.sh`, `openbf2\Dockerfile` | New: `git archive` of OpenBF2 `873d1b1` and openbf2-server-ext `0aab582` into a docker volume. Builds in `debian:bullseye` with `libssl-dev`, runs ctest, then builds the image FROM `cr.nihlen.net/library/bf2-netsky:2v2-nocheck`: binaries replaced, `openbf2.addModDll serverext`, `mm_webadmin` not loaded (ModManager and its rcon kept), `sv.internet 0`, the `SERVEREXT_CFG_EXTRA` hook, and an `ldd` check (`libssl1.1` is in the base image) |
| `init-db.sql` | `AdminKey` and `IsTestServer` columns (create, or `ALTER` on an existing database). The local server row gets `AdminKey = $(AdminKey)` and `IsTestServer = 1` |
| `start-webadmin.ps1` | Skips the BF2 container restart when the OpenBF2 variant runs |
| `tools\hubclient` | Modes `--watch <s>`, `--rcon "<cmd>"`, `--get-server`, `--set-admin-key <hex>`. Uses the form login `/api/login`, MessagePack and the hub methods (`UserConnect`, `SelectServer`, `SendRconCommand`, `GetServer`, `SetServer`, `SendChatMessage`) that the React web UI replaced with the web API on 2026-10-27; it needs porting before it works again (`--watch` only needs `Subscribe` instead of `SelectServer`, JSON instead of MessagePack and the JSON login `POST /api/auth/login`) |
| `README.md` | The OpenBF2 variant |
| `openbf2\build.sh` (2026-10-23, play session) | Module default `0d5967f` (protocol v3 + launch number); `ldd --version \| head -1` under `pipefail` could die of SIGPIPE (exit 141), now `sed -n 1p` |
| `play-2v2.ps1`, `grant-admin.ps1`, `sidecar\` (2026-10-23) | New: a human plays 2v2 against bf2-bots' sidecar (published into `sidecar\`), `-Mode Pilot\|Gunner\|Status\|Stop`, WebAdmin in the background, logs in `logs\play\`; `grant-admin.ps1 -Name <n>` writes `name:<n>` into `ServerPlayerAuth` (B6) and restarts that WebAdmin, which reads auth only at start. README "Play session" |
