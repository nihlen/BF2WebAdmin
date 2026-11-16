# Metrics

WebAdmin exports OpenTelemetry metrics over OTLP (gRPC) to `Telemetry:OtlpEndpoint` (default `http://localhost:4317`)
every 10 s. The resource is `service.name` `bf2-webadmin` with the version of the build. Grafana dashboards of these
metrics are kept with the deployment, not in this repository.

There are three sources:

- **The runtime and ASP.NET Core**: the runtime's own meter (`dotnet.gc.*`, `dotnet.thread_pool.*`,
  `dotnet.process.*`, `dotnet.exceptions`), the web API and hub (`http.server.request.duration`,
  `signalr.server.active_connections`, `kestrel.*`) and outgoing HTTP. WebAdmin adds nothing to these.
- **The application's meter `BF2WebAdmin`**: everything below. `bf2wa.*` is about WebAdmin itself, `bf2.*` about the
  game servers.
- Traces go to the same endpoint, from ASP.NET Core, HttpClient and EF Core only.

The Prometheus names are those of Prometheus's OTLP receiver: dots become underscores, the unit becomes a suffix
(`s` `_seconds`, `By` `_bytes`, `%` `_percent`, `ms` `_milliseconds`; a `{...}` unit adds none), and counters end in
`_total`. A histogram is `<name>_bucket`, `_sum` and `_count`.

**No tag names a player.** Game server measurements have `bf2.server.id` (the game server's `ip:port`), and those about
the game also `bf2.server.name`.

## The admin channel

`Metrics/AdminChannelMetrics.cs`. The links of the game servers' modules ([admin-channel.md](admin-channel.md)). All
but `bf2wa.admin.connections` have `bf2.server.id`.

| Metric | Prometheus name | Kind | Tags | What |
|---|---|---|---|---|
| `bf2wa.admin.connections` | `bf2wa_admin_connections_total` | counter | `result` (`accepted`, `refused`), `reason` (refused: `timeout`, `tls`, `handshake`, `io_error`, `shutdown`, `other`) | connections to the listener; `tls` is usually a module whose pin differs, `handshake` a wrong key or server id |
| `bf2wa.admin.sessions` | `bf2wa_admin_sessions` | gauge | | open sessions: 1 per connected server |
| `bf2wa.admin.session.closes` | `bf2wa_admin_session_closes_total` | counter | `reason` (`replaced`, `timeout`, `closed_by_module`, `io_error`, `protocol_error`, `settings_changed`, `other`) | sessions that ended; `other` includes WebAdmin's shutdown |
| `bf2wa.admin.session.duration` | `bf2wa_admin_session_duration_seconds` | histogram | | how long the sessions lasted |
| `bf2wa.admin.frames` | `bf2wa_admin_frames_total` | counter | `direction` (`received`, `sent`), `type` (the message type) | frames |
| `bf2wa.admin.bytes` | `bf2wa_admin_bytes_total` | counter | `direction` | bytes received (after TLS) and frames' bytes sent |
| `bf2wa.admin.ping` | `bf2wa_admin_ping_seconds` | histogram | | round trip of the keepalive Ping (every 15 s); the module answers on its game thread, so it includes up to a tick |
| `bf2wa.admin.request.duration` | `bf2wa_admin_request_duration_seconds` | histogram | `type`, `result` (`ok`, `error`, `timeout`, `closed`) | commands awaited for their reply (console, kick, ban, chat, ...) |
| `bf2wa.admin.requests.pending` | `bf2wa_admin_requests_pending` | gauge | | commands that wait for their reply |
| `bf2wa.admin.apply.duration` | `bf2wa_admin_apply_duration_seconds` | histogram | `kind` (`snapshot`, `event`, `stream`, `other`) | time to apply a message to the server's state and modules |
| `bf2wa.admin.queue` | `bf2wa_admin_queue` | gauge | `direction` (`inbound`, `outbound`) | messages waiting: received and not yet applied (the apply loop is behind), or not yet written |
| `bf2wa.admin.sequence_gaps` | `bf2wa_admin_sequence_gaps_total` | counter | | gaps in the event sequence; each requests a snapshot |

## WebAdmin's work

`AppDiagnostics.cs`.

| Metric | Prometheus name | Kind | Tags | What |
|---|---|---|---|---|
| `bf2wa.module.event.duration` | `bf2wa_module_event_duration_seconds` | histogram | `event`, `module` | a module handling a game event (position events too) |
| `bf2wa.module.command.duration` | `bf2wa_module_command_duration_seconds` | histogram | `command`, `module` | a module handling a player's `!` or `.` command |
| `bf2wa.module.failures` | `bf2wa_module_failures_total` | counter | `kind` (`event`, `command`), `module` | handlers that threw (each is also logged) |
| `bf2wa.match.save.duration` | `bf2wa_match_save_duration_seconds` | histogram | `kind` (`match`, `match_end`, `round`), `result` (`ok`, `failed`) | Heli2v2 writes to the database, with their retries (up to ~34 minutes) |
| `bf2wa.discord.messages` | `bf2wa_discord_messages_total` | counter | `bf2.server.id`, `result` (`sent`, `failed`) | messages of the servers' Discord bots |
| `bf2wa.discord.queue` | `bf2wa_discord_queue` | gauge | `bf2.server.id` | messages waiting to be sent to Discord |

## The game

`Metrics/GameMetrics.cs`, from the admin channel's events (`AdminStateApplier`), the TV missile traces (`ViewerFeed`)
and Heli2v2.

| Metric | Prometheus name | Kind | Tags | What |
|---|---|---|---|---|
| `bf2.players` | `bf2_players` | gauge | `kind` (`human`, `bot`) | players on each connected server |
| `bf2.player.joins` | `bf2_player_joins_total` | counter | `country` (ISO code, `unknown`) | human players who connected |
| `bf2.kills` | `bf2_kills_total` | counter | `kind` (`kill`, `roadkill`, `teamkill`, `suicide`, `no_attacker`), `weapon` (kills and roadkills; else empty) | kills as the engine reports them |
| `bf2.chat.messages` | `bf2_chat_messages_total` | counter | `channel` | human players' chat messages |
| `bf2.map.loads` | `bf2_map_loads_total` | counter | `map` | rounds that started (a map loaded or restarted) |
| `bf2.admin.actions` | `bf2_admin_actions_total` | counter | `action` (`kick`, `ban`) | kicks and bans from any source (WebAdmin, Discord, the game, the console) |
| `bf2.tv_missiles` | `bf2_tv_missiles_total` | counter | `outcome` (`hit`, `terrain`, `miss`, `cut`) | traced TV missiles |
| `bf2.heli2v2.rounds` | `bf2_heli2v2_rounds_total` | counter | `map`, `result` (`win`, `draw`) | Heli2v2 rounds played (2v2 mode on) |
| `bf2.heli2v2.round.duration` | `bf2_heli2v2_round_duration_seconds` | histogram | `map` | how long they lasted |
| `bf2.heli2v2.matches` | `bf2_heli2v2_matches_total` | counter | `map` | Heli2v2 matches that ended |

## Server health

`AdminChannel/ServerHealthMetrics.cs`, from the module's `ServerHealth` and `PlayerConnections`
([admin-channel.md](admin-channel.md#server-health)). Every measurement has `bf2.server.id` and `bf2.server.name`.

| Metric | Prometheus name | Kind | Tags | What |
|---|---|---|---|---|
| `bf2.server.health.windows` | `bf2_server_health_windows_total` | counter | `status` | windows by status |
| `bf2.server.ticks` | `bf2_server_ticks_total` | counter | `kind` (`run`, `late`, `short`, `lost`) | ticks of the rated windows |
| `bf2.server.page_faults.major` | `bf2_server_page_faults_major_total` | counter | | of the rated windows |
| `bf2.server.context_switches.involuntary` | `bf2_server_context_switches_involuntary_total` | counter | | of the rated windows |
| `bf2.player.connection.windows` | `bf2_player_connection_windows_total` | counter | `status` | players' connection windows by status, partial ones left out |
| `bf2.server.health.status` | `bf2_server_health_status` | gauge | | 0 `Good`, 1 `Warn`, 2 `Bad`, 3 `Loading`, 4 `Stepped` |
| `bf2.server.tick.interval` | `bf2_server_tick_interval_seconds` | gauge | `stat` (`median`, `p99`, `max`) | rated windows |
| `bf2.server.tick.cpu` | `bf2_server_tick_cpu_seconds` | gauge | `stat` (`median`, `p99`, `max`) | rated windows |
| `bf2.server.module.handler` | `bf2_server_module_handler_seconds` | gauge | `stat` (`p99`, `max`) | rated windows |
| `bf2.server.host.cpu` | `bf2_server_host_cpu_percent` | gauge | `state` (`busy`, `steal`, `iowait`) | rated windows with valid host figures |
| `bf2.server.memory.resident` | `bf2_server_memory_resident_bytes` | gauge | | |
| `bf2.server.players.connected` | `bf2_server_players_connected` | gauge | `status` | players of the latest connections by status, 0 for a status without any |
| `bf2.player.ping` | `bf2_player_ping_milliseconds` | gauge | `stat` (`median`, `max`) | the median of the players' pings, the highest ping of the window |
| `bf2.player.loss` | `bf2_player_loss_percent` | gauge | `direction` (`to_player`, `from_player`), `stat` (`mean`, `max`) | loss over the players |

- **Counters** are added to as each window comes, so a late tick between two exports is never lost. `Loading` and
  `Stepped` windows count only in `bf2.server.health.windows`.
- **Gauges** are of the latest window or connections while they are at most 25 s old, so a server that stops sending
  drops out instead of keeping its last values. The player gauges leave the partial entries out and have no
  measurement when no player has a full window.

## Adding a metric

Create the instrument on `Telemetry.Meter` (or a meter named `Telemetry.MeterName` from `IMeterFactory`), so the
exporter picks it up. Use seconds for durations, a `{...}` unit for counts, the tags above for servers, and no tag
whose values grow without bound (player names or hashes, free text). Add it to this page.
