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
| `AdminStateApplier.cs` | Maps `Welcome`, `Snapshot` (reconciled with the existing players, so modules keep their `Player` objects across resyncs and reconnects), the four streams and every event onto `GameServer`; the B6 player key |
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
  `.interval`), vehicles and game state at 1 Hz.

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

## Results

(Filled in after the end-to-end run.)

## Contract points that proved wrong or need attention

(Filled in after the end-to-end run.)
