# Changelog for the OpenBF2 migration

What changes compared with what runs live today (2026-11-08):
- **WebAdmin:** 0.0.2 (`master` `a869c5c`).
- **Retail game servers:** images `bf2-netsky:2v2`, `2v2-nocheck`, `dogfight`, `chopper-mayhem`.
- **OpenBF2 test server:** `bf2-netsky:openbf2-2v2-nocheck` (OpenBF2 `ccf3099` + bf2hub + mm_webadmin).

## Game server engine

**Retail BF2 1.5.3153 becomes OpenBF2** (an open source reconstruction):
- No PunkBuster: the authentication step admits everyone and PunkBuster packets are dropped. The images set `sv.punkBuster 0`.
- A round always needs two humans to start, whatever `sv.numPlayersNeededToStart` says (upstream OpenBF2). Bots don't count. Every live server used 2 already except dogfight, which used 1.
- Ranked authorisation failures are ignored.
- Some engine paths that were never reconstructed still stop the server on purpose. Demo recording with a completion hook would hit one.
- ModManager, Python and rcon on 4711 work as before.

**On top of upstream `ccf3099`: 12 local commits** (OpenBF2 `serverext/r1`, `824d4dc`):
- Hooks for the server extension, which do nothing unless the module uses them:
  - The `IServerExtension` singleton and bot player creation.
  - Spawn groups, round restart and game status.
  - Observers for damage, launches and spotting.
  - A script-event observer.
  - Getters for the CD-key hash, the map list, kill and more.
  - A projectile-collision observer and a lag-compensation getter.
- Three null checks that only bots can reach; they prevent crashes.
- Stepped time, active only with `stepMode 1`. It is for bot tests only, and the images force 0.
- Two TV missile settings, off by default (retail behaviour):
  - `tv-aim-from-missile` (metres): with lag, a TV missile near a wall no longer stops steering.
  - `tv-turn-behind` (1): a TV missile keeps turning when the gunner looks more than 90° away from its nose.
  - Both are set with the image's `ENV_TV_AIM_FROM_MISSILE` / `ENV_TV_TURN_BEHIND`, or with rcon `exec vars.set ...`.

## Server extension `libserverext.so` (new)

- Loaded by `openbf2.addModDll serverext` after bf2hub, before Python.
- **Admin channel v1, which replaces `mm_webadmin.py`:**
  - The game server dials WebAdmin at `bf2wa.nihlen.net:4300` over TLS 1.3. WebAdmin's certificate is pinned, and both sides prove the shared key with HMAC.
  - On connect it sends a full snapshot, then numbered events: game, round, connect/disconnect, spawn, death, revive, team, score, kill (kind and positions), damage, vehicle, control point, chat, projectile, kick/ban.
  - Typed commands: Console, Say, Teleport, SetHealth, KillPlayer, SetTeam, SetScore, Rank, Medal, GameEvent, HudEvent, Kick, Ban, Map, ListObjects, SetObjectActive, Spawn/DeleteObject.
  - Text is UTF-8, so `§` arrives intact.
  - Streams for the 3D viewer: camera view mode, aim, WeaponFire, Weapons, and a hit/miss replay.
- **Bot channel, protocol v4:** a plain TCP channel with no authentication, always on `127.0.0.1` in the images. It is not used in this release.

## Game server images (bf2-docker `images/netsky-openbf2-*`, new)

- Base `debian:trixie-slim`. The binaries come from `openbf2-serverext-<openbf2>-<module>-<bf2hub>.zip`, built reproducibly on Ubuntu 22.04 with OpenSSL 3 (openbf2-server-ext `release/`).
- The server files come from the image, and the settings are rendered from env on every start. Before, they were copied into `/home/bf2/srv` once and never updated. That directory must no longer be mounted.
- Only `/volume` persists: the BF2 and ModManager ban lists, demos, and logs.
- New env:
  - `ENV_ADMIN_CONNECT`, `ENV_ADMIN_SERVER_ID`, `ENV_ADMIN_KEY`, `ENV_ADMIN_CERT_SHA256`.
  - `ENV_INTERNET`.
  - `ENV_TV_AIM_FROM_MISSILE`, `ENV_TV_TURN_BEHIND`.
  - `ENV_BOT_PORT`.
- Removed env: `ENV_BF2WEBADMIN_*`.
- `mm_webadmin` is removed. `mm_rcon` and `mm_iga` stay.

## WebAdmin 0.1.0 (`admin-channel`)

**Game server connection**
- The mm_webadmin text socket is replaced by the admin channel. It listens on 4300 in production (`ServerSettings__AdminChannel__Port`; the code default is 4310). Retail servers can no longer connect.
- Servers are identified by `ServerId` + `AdminKey` (HMAC), not by IP. The `wa connect` loop is gone.
- The certificate comes from a secret (`ServerSettings:AdminChannel:Certificate`, base64 PKCS#12 or PEM). `--admin-cert-create` makes one and `--admin-pin` prints its pin.
- Kill, death and suicide come from the module, with exact positions and projectile owner.
- Rcon remains for `iga`, the rcon box, the ban list, and kick/ban from the web UI and Discord. It now connects to the configured `IpAddress`.

**Web UI: Blazor is replaced by React** (Vite, TanStack, shadcn/Tailwind). Everything the old UI had is still there, plus:
- A 3D viewer: a live view at 30 Hz, and the last 20 TV missiles with the hitboxes the engine tests (dalian_2_v_2).
- Actions stay pending until the server confirms them. Confirmation dialogs for destructive actions.
- Ban length, game mode and search in the map list, run next map, restart server (`.restartserver`, a clean quit).
- A Commands tab with history and Tab completion, a player details sheet, an Activity log, player auth management, and dashboard totals.

**Web API and live updates**
- Queries and commands move to a REST API under `/api` (with an OpenAPI document). The SignalR hub only pushes live updates and uses JSON instead of MessagePack.
- Login is JSON (`/api/auth/login`, `logout`, `me`).
- `/api/v1/servers` is unchanged, plus `AdminKey` and `IsTestServer`.

**Modules**
- **Heli2v2:** `.pad` arrives level and facing the other pad. `.nofences` and `.noclip` use typed commands.
- **BF2Module:**
  - `.kill` wrecks the vehicle.
  - `.repair` sets full health.
  - `.tp` arrives level.
  - New `.kick`, `.ban` and `.restartserver`.
- **MapModule:** `.o` without a position no longer crashes, and spawned objects are sent to each client once.
- **ChopperMayhem:** typed commands, and the correct victim position.

**Platform**
- .NET 10, on the container base `runtime-deps:10.0` (Ubuntu 24.04; it was Debian 12).
- CI uses the .NET 10 SDK and Node 24.
- New database columns `Server.AdminKey` and `Server.IsTestServer` (`03-database.ps1`).
- Config: `ServerSettings:Port` and `Print*` are gone. New are `ServerSettings:AdminChannel` (`Port`, `Certificate`, `CertificatePath`, `CertificatePassword`, `BindAddress`) and, per server, `ServerId`, `AdminKey` and `IsTestServer`.

**Removed:** the mm_webadmin protocol, the Blazor UI, the MessagePack hub and its request/response methods, form login, the autobalancer team workaround, and the GameSpy team re-query.
