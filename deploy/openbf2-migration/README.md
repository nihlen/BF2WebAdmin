# Migration to OpenBF2, the server extension and WebAdmin 0.1.0

This runbook switches every live game server and WebAdmin at once. Before: retail BF2 1.5 servers with `mm_webadmin.py`
and WebAdmin 0.0.2 (`master`). After: OpenBF2 servers with `libserverext.so` and WebAdmin 0.1.0 (`admin-channel`).
WebAdmin 0.1.0 no longer speaks mm_webadmin, so the two cannot run side by side. Downtime is acceptable.

Written 2026-11-08. **Done on 2026-11-08** (WebAdmin 0.1.0 at 05:39 UTC, de2 at 05:49, de at 05:55; all four servers
authenticated, on the BF2Hub list, `06-verify.ps1` clean). Run every script from this folder with `powershell -ExecutionPolicy Bypass -File .\<script>.ps1`.
Every script that changes something asks before it does.

## Decisions

| | |
|---|---|
| Rollout | Everything at once: the 2v2, 2v2-nocheck, dogfight and chopper-mayhem servers plus WebAdmin. The OpenBF2 test server on de2 (16570) is the canary beforehand and is stopped on migration day |
| Admin channel port | 4300, the old mm_webadmin port. The firewall, the OPNsense forward and the NodePort 30430 stay as they are. WebAdmin listens with TLS 1.3 there (`ServerSettings__AdminChannel__Port=4300`) |
| Certificate | Kept in 1Password, added by hand: the field `ServerSettings__AdminChannel__Certificate` (base64 PKCS#12 from `--admin-cert-create`) in the item `bf2-webadmin-env`. The operator syncs it into WebAdmin's env. Done on 2026-11-08 |
| Admin keys and pin | Nothing to copy by hand. `03-database.ps1` has SQL Server generate one key per server (`CRYPT_GEN_RANDOM`, 64 hex digits) in `dbo.Server.AdminKey`, which is where WebAdmin reads it. `05-gameservers.ps1` reads the keys from there and the pin from WebAdmin's startup log, then writes both into each server's env file. The scripts never use the 1Password CLI |
| GameSpy backend | The release zip is built with `GSI_DOMAIN_NAME=bf2hub.p.bf2.org`, like the earlier `openbf2-bf2hub` zip, so the servers report to BF2Hub's master list and CD-key checks (`5.252.33.100`, the same as the retail servers). Built with OpenBF2's default `openspy.net`, they were missing from the in-game list |
| Game server images | `cr.nihlen.net/library/bf2-netsky:openbf2-<name>` (bf2-docker `images/netsky-openbf2-*`, trixie). The server files come from the image, and the settings are rendered from env on every start. The only bind mount is `/volume`, at `/data/bf2/<name>/volume-openbf2` |
| TV missile settings | `tv-aim-from-missile` and `tv-turn-behind` are off (retail behaviour). See "Enabling the TV settings" |
| Accepted | No PunkBuster on OpenBF2. Kick and ban from the web UI and Discord go through rcon `iga`. The admin key is visible to Administrators. Q12 (TV launch refused while another TV missile flies) is left for the live playtests. `adminAllowConsole 1` |
| Later | Temporary servers from the BF2v2 API (see "Temporary servers") |

## The kit

| File | What it does | Changes production? |
|---|---|---|
| `servers.json` | Inventory: hosts, servers, images, ports and server ids. No secrets | |
| `lib.ps1` | Helpers: kubectl and flux through WSL, sqlcmd in the mssql pod, ssh and scp (Git's), the admin keys from `dbo.Server`, the pin from WebAdmin's log | |
| `01-preflight.ps1` | Checks tools, ssh, images on the hosts, the certificate in the cluster secret, the database and WebAdmin | no |
| `02-secrets.ps1` | Optional, not used: does the 1Password part with the 1Password CLI | 1Password only |
| `03-database.ps1` | `-Check` shows the `dbo.Server` rows. Without it: a backup job, the `AdminKey` and `IsTestServer` columns, and a generated key for each server that has none (`-RotateKeys`: new keys for all) | yes, asks first (`-Yes` skips) |
| `04-webadmin.ps1` | `-Phase Hold`, `Check`, `Migrate` or `Rollback` in pve-cluster (Flux) | yes, asks before each push |
| `05-gameservers.ps1` | `-HostAlias de2\|de -Phase Status\|Canary\|Migrate\|Rollback` | yes, asks first |
| `06-verify.ps1` | Checks that every server authenticated, the images and rcon | no |
| `pve-cluster/*.patch` | The two manifest changes, applied by `04-webadmin.ps1` | |
| `work/` | Git-ignored. It holds the rendered env files and the retail settings taken from the containers, **which include secrets**. Delete it when the migration is done and the rollback window is over | |

Requirements on this PC:
- `ssh de` and `ssh de2` (root, key auth).
- WSL Ubuntu with `kubectl` and `flux`, and `pve-cluster\kubernetes\kubeconfig`.
- Docker, used for `docker manifest inspect`.

## Before migration day

1. **Publish the sources.** Nothing is pushed yet.
   - OpenBF2: push branch `serverext/r1` (12 local commits on upstream `ccf3099`) to the fork `github.com/nihlen/OpenBF2`. Push only that branch, and never to the upstream B2BF repo.
   - openbf2-server-ext: add the private remote `github.com/nihlen/openbf2-server-ext` and push `master`. Its `.github/workflows/release.yml` builds the zip. If the fork is private it needs a token.
   - Upload `openbf2-server-ext\release\out\openbf2-serverext-824d4dc-c14b561-7e37854.zip` to `https://static.nihlen.net/bf2/server/`. The image assets point there (the sha512 is in `assets.sha512`).
2. **Game server images.** In bf2-docker, merge the branch `combined` (commit `7a32766`) into `master` and push. CI builds the four `netsky-openbf2-*` images, then pushes `openbf2-2v2`, `openbf2-2v2-nocheck`, `openbf2-dogfight` and `openbf2-chopper-mayhem`. The retail tags are not touched.
   - Local equivalents are tagged `...:openbf2-<name>-rc1` on this PC.
3. **Hold Flux.** Run `04-webadmin.ps1 -Phase Hold` **before** the WebAdmin release. Flux's image policy deploys the highest release tag within 5 minutes, and 0.1.0 must not go live before the game servers.
4. **WebAdmin release.**
   - Push `admin-channel`.
   - Create the GitHub release `0.1.0` on it. `.github/workflows/docker-image.yml` tests, builds and pushes `docker.io/nihlen/bf2-webadmin:0.1.0`.
5. **Certificate (done 2026-11-08).** `docker run --rm docker.io/nihlen/bf2-webadmin:0.1.0 --admin-cert-create` printed it, and its base64 line went into `bf2-webadmin-env` as `ServerSettings__AdminChannel__Certificate`. The cluster secret has it; 0.0.2 ignores it.
   - Keep the `adminCertSha256` line it printed. On migration day the pin WebAdmin logs must equal it.
6. **Server ids.** Run `03-database.ps1 -Check`.
   - Each `serverId` in `servers.json` must be a row in `dbo.Server`, because it becomes the module's `adminServerId`.
   - Each row needs `IpAddress`, `RconPort` and `RconPassword`, because WebAdmin now uses rcon at the configured address and no longer the address the server connected from.
   - Fix `servers.json` if the ids differ.
7. **Canary on the internet.** Run `05-gameservers.ps1 -HostAlias de2 -Phase Canary`. It runs the OpenBF2 test server's slot (`45.76.95.172:16570`) on the new image without the admin channel. Then check with real clients:
   - The server is on the BF2Hub list and a retail client joins from the internet. The CD-key check passes, or check what happens without it.
   - A round with two humans: start, tickets, scoreboard, end of round, map change.
   - TV missiles in a 2v2 heli fight, including two gunners firing TV at the same time (Q12).
   - `docker logs netsky-de-openbf2-2v2-nocheck` has no `[FRONTIER]` lines and no crash.
   - Dogfight and chopper-mayhem have only been booted locally, never played. Run them on the canary slot as well, by switching the canary's image in `servers.json` and running again, or accept that risk.
8. **Preflight.** Run `01-preflight.ps1` the day before. Everything must pass.

## Migration day

Expect 15 to 30 minutes of downtime. Retail servers cannot reach WebAdmin from step 3 until their host is migrated in step 4.

1. Run `01-preflight.ps1`.
2. Run `03-database.ps1`. It runs a backup job, adds the columns and generates the keys. This is additive, and 0.0.2 keeps working.
3. Run `04-webadmin.ps1 -Phase Migrate`. It deploys image 0.1.0 with `ServerSettings__AdminChannel__Port=4300` and sets the image policy back to `>=0.0.0`. It then pushes to pve-cluster, reconciles Flux, waits for the rollout and prints the pin from WebAdmin's log. Compare it with the `adminCertSha256` line you kept.
4. Run `05-gameservers.ps1 -HostAlias de2 -Phase Migrate`, then `05-gameservers.ps1 -HostAlias de -Phase Migrate`. On each host this:
   - Saves `/data/docker-compose.yml` as `docker-compose.retail.yml`, once.
   - Takes each server's name, player count, demo URL, rcon password and API key from its running container, its admin key from `dbo.Server` and the pin from WebAdmin's log.
   - Seeds `/data/bf2/<name>/volume-openbf2` with the ban lists and uploaded demos.
   - Replaces the compose file and starts the OpenBF2 containers under the same container names.
   - Waits for `admin: link session ... authenticated` in each container's log.
5. Run `06-verify.ps1`, then check by hand:
   - The web UI shows every server connected, with players.
   - Chat and a `.` command in game.
   - A kick and a ban (rcon `iga`), and the ban survives a container restart.
   - A map change and a restart map from the web UI.
   - The servers are on the BF2Hub list.
   - A player's CD-key hash shows in the player sheet, not `name:<name>`.
   - Discord notifications arrive.
   - Gatus is green. It checks the unchanged rcon ports.
6. Afterwards:
   - Copy the hosts' new `/data/docker-compose.yml` into the VPS repo (`de2/`, `de/`) yourself; `de2/docker-compose.yml` has uncommitted edits of yours.
   - The 06:00 restart cron needs no change, because the container names are the same.
   - Delete `work\` here once the rollback window is over.

## Rollback

All of these can run in any order and repeat safely:
- `05-gameservers.ps1 -HostAlias de2 -Phase Rollback` and the same for `de`. They put `docker-compose.retail.yml` back. The retail containers start on their untouched `/data/bf2/<name>/server` and `volume` directories.
- `04-webadmin.ps1 -Phase Rollback` reverts the migrate commit: WebAdmin 0.0.2, the old env, and the image policy held below 0.1.0 again.
- The database stays as it is. The two new columns are nullable or have a default, and 0.0.2 does not read them.
- Bans made while on OpenBF2 stay in `volume-openbf2`. Copy `banlist.con` and `mm_bans.xml` back by hand if they matter.
- A full database restore is the `mssql-restore` CronJob (pve-cluster `docs/recovery.md`). The backup from step 2 is the newest.

## Enabling the TV settings

Both are OpenBF2 engine variables and are off by default (retail behaviour). They were tested with bots only.
- `tv-aim-from-missile` (metres, e.g. `50`): a TV missile near a wall no longer stops turning when the gunner's lag puts the aim point beside or behind it. The aim is cast again from the server's missile.
- `tv-turn-behind` (`1`): the missile keeps turning when the gunner looks more than 90° away from its nose, where retail flies straight.

The two complement each other. The bot tests ran with both on.

- **For good:** set `gameplay.tvAimFromMissile` and `gameplay.tvTurnBehind` in `servers.json` and run `05-gameservers.ps1 -Phase Migrate` again for the host. That re-renders the env files and recreates the containers.
- **On one host by hand:** set `ENV_TV_AIM_FROM_MISSILE=50` and `ENV_TV_TURN_BEHIND=1` in `/data/openbf2/<name>.env`, then `docker compose -f /data/docker-compose.yml up -d`.
- **For a test, until the next restart:** use rcon (the web UI's rcon box) with `exec vars.set tv-turn-behind 1` and `exec vars.set tv-aim-from-missile 50`.

## Temporary servers (BF2v2 API): after the migration

`BF2v2Core.API` (`Services/VpsService.cs`) creates temporary Vultr servers. Its startup script uses the retail images and `ENV_BF2WEBADMIN_HOST`/`PORT=4300`, and it registers the server through `POST /api/v1/servers` without an `AdminKey`. Such a server cannot connect to WebAdmin 0.1.0.

This is accepted for the first version. To fix it after the migration and then test it:
- The startup script uses `bf2-netsky:openbf2-<name>`, drops `/home/bf2/srv` from the volumes, and sets `ENV_ADMIN_CONNECT`, `ENV_ADMIN_SERVER_ID` (`<ip>:<port>`), `ENV_ADMIN_KEY` (generated per instance) and `ENV_ADMIN_CERT_SHA256` (the pin).
- The `POST /api/v1/servers` call sends the same `AdminKey`.

## Other differences to know

- **OpenBF2 always waits for two humans before a round starts,** whatever `sv.numPlayersNeededToStart` says. This comes from upstream OpenBF2 (`ServerSettings::getNumPlayersNeededToStart` returns 2), not from our changes. Every live server already uses 2 except dogfight, which used 1.
- **No PunkBuster.** The images set `sv.punkBuster 0`.
- **WebAdmin now runs on Ubuntu 24.04** (`mcr.microsoft.com/dotnet/runtime-deps:10.0`, .NET 10) instead of Debian 12.
- **A changed admin key takes up to 60 s to take effect.** The module retries the connection with a backoff of up to 60 s.
