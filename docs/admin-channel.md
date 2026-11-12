# Admin channel (OpenBF2 serverext) backend

BF2WebAdmin's game-server backend is the native admin channel of the OpenBF2 engine module `libserverext.so`
(openbf2-server-ext): the game server dials WebAdmin over TLS 1.3, so it needs no open port of its own.

The wire format is admin protocol version 1, "Wire format (admin protocol version 1)" in openbf2-server-ext
`docs/admin-channel-draft.md`, with decisions B1-B6. This document covers the C# side.

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
| `AdminCertificate.cs` | WebAdmin's self-signed ECDSA P-256 certificate (100 years): from `ServerSettings:AdminChannel:Certificate` (a secret: base64 PKCS#12 or PEM), else from the file, created once. Computes the pin: SHA-256 of the SubjectPublicKeyInfo, lowercase hex |
| `AdminListener.cs` | TCP listener (default port 4310), `SslStream` TLS 1.3 only, then the handshake; TLS and handshake together within 10 s of the connect. Optional plain listener on 127.0.0.1 for `adminTls 0` |
| `AdminHandshake.cs` | `Hello` → `Challenge` (WebAdmin proves the key first) → `Auth`, proofs compared with `CryptographicOperations.FixedTimeEquals`; version ≠ 1 is `Error` 1, unknown id or no key `Error` 8 |
| `AdminSession.cs` | One link: read loop (answers `Ping`, completes requests), write loop (one ordered queue), apply loop (sequence check and `RequestSnapshot` on a gap, stream frames latest-wins), keepalive (`Ping` every 15 s, close after 45 s of silence), optional recording of every received message |
| `AdminStateApplier.cs` | Maps `Welcome`, `Snapshot` (reconciled with the existing players, so modules keep their `Player` objects across resyncs and reconnects), the streams and every event onto `GameServer`; the B6 player key; the subscriptions (at 30 Hz while a 3D viewer is open, with the modules still fed at their own rate) |
| `ViewerFeed.cs` | The 3D viewer: `ViewerRegistry` (which hub connections watch which server), the 30 Hz stream frames to the hub group `viewer:<serverId>` (sent from its own task, so a slow browser never holds up the link), and the last 20 traced TV missiles (`ProjectileTrace` with its `ProjectileCollision`s); every trace also goes to `MissileArchive`, ten minutes of every server's missiles for the export API |
| `AdminGameWriter.cs` | `IGameWriter` on the current session: typed commands, `Console` lines in order with them, `ListObjects`, `SpawnObject`, `SetObjectActive`, `Subscribe` |
| `GameServerRegistry.cs` | `IGameServerRegistry`: configured servers (DB + config), the `GameServer` per server id, key lookup |

Modules see `IGameServer`, the `GameEvents.cs` records and `IGameWriter`. `GameServer`'s mutators are not on
`IGameServer`; only the applier calls them.

### Identity and keys

- The module's `adminServerId` is the server's `Server.ServerId` (for example `1.2.3.4:16567`). The source address
  of the connection does not matter.
- `Server.AdminKey` holds the key as hex (32-128 digits, 16-64 bytes), the same text as `adminKey` in
  `serverext.cfg`. No key, or a malformed one: the server cannot connect. Changing it in the edit form closes the
  current link; the module redials and must match the new key.
- `Server.IsTestServer` (decision B6): players are keyed (`Player.Hash`) by the CD-key hash when the module sends one.
  Without a hash (every player on a LAN server, bots, `adminSendIdentity 0`) a test server keys them by name as
  `name:<name>`, which cannot collide with a 32-digit hash. On other servers such a player has an empty key: no auth
  level, and it is logged. A player without a CD-key hash is banned by address.

### Ordering and resync

- Every command, typed or `Console`, is queued on one ordered link, and the module runs them in arrival order (at
  most 64 per tick). So `SetObjectActive` followed by raw `object.*` lines, or `activeSafe` followed by a getter,
  run in that order.
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
`GET /api/servers/{id}/missiles/{key}`: per tick the missile (true size, its centre the single point the engine sweeps)
and each target's visual model where it is now (red), the hull's collision mesh where the server tested it (blue,
rewound by the shooter's lag) and the child parts' collision meshes at the red pose, where they are tested (orange:
rotors as their blade sweep, the AH-1Z's Hellfire dummies, turret, sensors, gear, the Z-10's tail), with the part hit
lit at the pose the `ProjectileCollision` reports. Its
player view follows `PlayerState`'s view mode: the server's camera object (`AimStream`) for the inside view and the
TV launcher, the chase, front chase, nose and fly-by views computed from the vehicle with the camera templates'
values. The map and helicopter models are BF2v2's (`public/viewer/`, only `dalian_2_v_2` has a 3D map); the hitboxes
(`public/viewer/models/heli-hitboxes.json`) are built by `tools/viewer/build_heli_hitboxes.py` from the server's
collision meshes.

## Setup

1. **Certificate and pin.** Two sources:

   - `ServerSettings:AdminChannel:Certificate`, for a secret in an env var (`ServerSettings__AdminChannel__Certificate`).
     The primary format is base64 PKCS#12, one line (line breaks are ignored). PEM text with a `CERTIFICATE` and a
     `PRIVATE KEY` (or `ENCRYPTED PRIVATE KEY`) block also works. `CertificatePassword` decrypts either. When it is set
     it wins over `CertificatePath`, and WebAdmin never creates or writes a certificate.
     `BF2WebAdmin.Server --admin-cert-create` prints a new one (base64 PKCS#12, or PEM with `--pem`; encrypted with
     `CertificatePassword` if one is configured) and its `adminCertSha256` line, writes nothing and exits.
   - Otherwise the PKCS#12 file at `ServerSettings:AdminChannel:CertificatePath`, created on first start (default
     `%LOCALAPPDATA%\BF2WebAdmin\admin-channel.pfx`, or `~/.local/share/BF2WebAdmin/admin-channel.pfx` on Linux; keep it
     on a volume in Docker, outside the repo).

   The key is imported with `UserKeySet` on Windows (SChannel cannot use an ephemeral key; a PEM goes through PKCS#12
   first) and `EphemeralKeySet` on Linux (OpenSSL). The certificate is loaded before the host starts: an unusable
   value stops WebAdmin with the setting's name in the error (never the value). WebAdmin logs:

   `Admin channel: TLS certificate CN=BF2WebAdmin admin channel from <source>, pin for serverext.cfg: adminCertSha256 <64 hex>`

   `BF2WebAdmin.Server --admin-pin` prints only the pin of the configured certificate (creating the file if neither
   source exists) and exits. Both commands work without `appsecrets.json`. Rotating the certificate means updating
   every server's pin.
2. **Config** (`appsettings`/`appsecrets`):

   ```json
   "ServerSettings": {
     "IpAddress": "...",
     "AdminChannel": { "Port": 4310, "BindAddress": "0.0.0.0", "Certificate": "<base64 PKCS#12>", "CertificatePath": "/data/admin-channel.pfx", "CertificatePassword": "" }
   }
   ```

   `Port` must not be the game port. Set either `Certificate` or `CertificatePath`.
3. **Servers.** Set the key (and the test-server flag) in the server edit form, or in `ServerSettings:GameServers`.
   The columns `Server.AdminKey` and `Server.IsTestServer` come with the schema ([database.md](database.md)).
4. **Game server** (`serverext.cfg` beside the OpenBF2 `bf2` binary, module loaded with `openbf2.addModDll serverext`):

   ```
   adminConnect webadmin.example.org:4310
   adminServerId 1.2.3.4:16567
   adminKey <the same hex as Server.AdminKey>
   adminCertSha256 <the pin WebAdmin logs>
   ```

   Optional: `adminSendIdentity` (default 1 with TLS; WebAdmin needs the IPs and hashes), `adminAllowConsole` (default
   1; with 0 every raw console script of the modules is refused, see below), `adminAllowObjects` (default 1;
   `.nofences` needs it).

## Production configuration

WebAdmin in a container, with settings from env vars and the certificate from a secret store. The game servers dial
WebAdmin's public name and admin-channel port (`webadmin.example.com:4310` below).

| Setting | Value |
|---|---|
| `ServerSettings__AdminChannel__Port` | The listener port, default 4310 |
| `ServerSettings__AdminChannel__Certificate` | The base64 PKCS#12 (or PEM) from `--admin-cert-create`, kept as a secret. With it set, a new container uses the same certificate and never writes one. Without it (and without a `CertificatePath` on a volume), every new container would create its own certificate and break every server's pin |
| `ServerSettings__AdminChannel__CertificatePassword` | Not set: the secret store protects the value |
| `Server.AdminKey` | One key per server (server edit form) |

Each game server's `serverext.cfg`:

```
adminConnect webadmin.example.com:4310
adminServerId <public ip>:<game port>
adminKey <the server's AdminKey>
adminCertSha256 <the pin>
```

`adminServerId` is the server's `Server.ServerId`, at most 20 characters in SQL Server. `--admin-pin` with the secret
in the environment prints the pin, and WebAdmin logs it at startup. A certificate setting that cannot be used stops
WebAdmin at startup with the setting's name in the error. Replacing the certificate means updating `adminCertSha256`
on every server at the same time. The game servers need restart policy `unless-stopped` (or Kubernetes' `Always`):
`.restartserver` quits the server with exit code 0.

## Kicks, bans and the `!` commands (`ServerAdmin`)

WebAdmin uses no classic rcon, so the game servers need no ModManager. `Services/ServerAdmin.cs` does it all
for the web API (`POST /api/servers/{id}/players/{index}/warn|kick|ban`, `GET` and `DELETE /api/servers/{id}/bans`), the
web chat box and Discord (`!` commands, `ServerAdmin.Help`) and the in-game `.kick`/`.ban`:

- **Kick** is the typed `Kick`; **warn** a chat message to everyone.
- **Ban** is the typed `Ban`, by CD-key hash, or by address for a player without one: permanent, this round, or for a
  time (`BanLength`: `Perm`, `Round`, seconds, `30m`, `12h`, `7d`, `2w`). The engine keeps the ban in its own list and
  saves permanent and timed bans to the server's `settings/banlist.con`, so it holds without WebAdmin. WebAdmin records
  the name, who banned and the reason in `ServerBan` (`server_ban` on Postgres), keyed by server and identifier.
- **The ban list** is the engine's (`admin.listBannedKeys`, `admin.listBannedAddresses` over the console) joined with
  those records; a record whose ban the engine no longer has (lifted elsewhere, expired, a round ban after the round) is
  not shown. **Unban** is `admin.removeKeyFromBanList` or `admin.removeAddressFromBanList` (a 32-digit hex identifier
  is a key), **clear** `admin.clearBanList`; both remove the records too.
- **Other `!` commands** are console lines (`!sv.serverName`, `!exec <line>`), `!list` (`admin.listPlayers`) and
  `!map <map> <mode> [size]` (the typed `Map`, maps of the rotation only). They need `adminAllowConsole 1`.
- **Both prefixes.** `.` and `!` work alike everywhere: in game every chat command takes either (`!k`, `.kick`,
  `!switch`); kick (`k`), warn (`w`), ban (`b`), unban, banlist, clearbans, map (`m`) and list are chat commands too
  (`Commands/KickBanCommands.cs`, kick, warn, map and list at Admin, ban and unban at SuperAdmin, clearbans at God), and
  their answers go privately to the player who asked.
  In the web chat box and Discord an admin command runs directly with either prefix, a module's command with either,
  and a `!` line that is neither is a console line (web users and Discord's admin channel only, never in game).

## Player joins (`JoinHistory`)

WebAdmin writes a row per player join to `JoinHistory` (a statistics site can use it for country flags from the
newest IP per name and aliases by the same `Guid` or IP). `AdminStateApplier` handles every `PlayerConnect` event,
whatever modules the server group enables, and queues one row per human player for `JoinHistoryWriter`, a background
service that inserts them one at a time through `IJoinHistoryRepository` (`IsLikelySpoof` takes the column default).
The apply loop never waits for the database, and a failed insert only logs a warning.

| Column | Value |
|---|---|
| `Name` | the player name (clan tag included), trimmed, at most 100 characters |
| `Guid` | the CD-key hash |
| `IpAddress` | the player's IP without a port (IPv4-mapped IPv6 as IPv4) |
| `ServerIpAddress` | the server's configured `IpAddress` (`Server.IpAddress`); its resolved address if it is a host name |
| `JoinDate` | the time WebAdmin received the event, UTC |

No row for bots (AI or created by the bot channel), for players without an IP or with a loopback IP, and for players
without a CD-key hash (withheld with `adminSendIdentity 0`, LAN and test servers; the `name:` keys of test servers are
never stored). A snapshot (WebAdmin restart, reconnect, resync) is not a join: players who joined while WebAdmin was
down get no row. On SQL Server `Schema.sql` creates the table only when it is missing; on SQLite the EF migration
`JoinHistory` creates it. On Postgres the table is `player_join` (without `IsLikelySpoof`; [database.md](database.md)).
Other programs read the joins through the export API ([export-api.md](export-api.md)).

## Known limitations

1. **The typed `SpawnObject` does not fit map mods.** Most map-mod templates have no networkable, so
   `createObjectOnAllClients` creates server-only objects. `MapModule` keeps the console `ObjectSpawner` script
   (`RconScript.AddObject`), which makes the template networkable first and needs `adminAllowConsole 1`. With
   `adminAllowConsole 0`, map mods, noclip, freeze, gravity, TV-missile settings and `InitServer` all stop working. The
   `Welcome` capability bit is logged as a warning.
2. **Changing a key costs up to a minute.** After a failed attempt the module backs off up to 60 s, so a key fixed
   in the edit form takes effect at the next retry.
3. **`Server.ServerId` is `nvarchar(20)` on SQL Server.** It is one character short of `255.255.255.255:65535`, and it
   is the module's `adminServerId`.
4. **The key is visible to edit-form users.** `GET /api/servers/{id}/settings` returns `AdminKey` to Administrator-role
   users of the edit form (shown as a password field). It cannot be hashed: HMAC needs the raw key.
