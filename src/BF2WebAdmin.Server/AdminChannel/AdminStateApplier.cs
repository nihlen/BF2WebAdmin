using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Communication;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// Maps the admin channel's Welcome, Snapshot, stream frames and events onto a <see cref="GameServer"/>.
/// Runs on the session's apply loop, one message at a time.
/// </summary>
public class AdminStateApplier : IAdminSessionHandler
{
    /// <summary>The projectiles tracked for Heli2v2's TV missile stats.</summary>
    public static readonly string[] TrackedProjectiles = ["agm114_hellfire_tv"];

    private readonly GameServer _server;
    private readonly ILogger<AdminStateApplier> _logger;
    private readonly IJoinHistoryWriter? _joinHistory;
    private readonly ICountryResolver? _countries;
    private readonly Dictionary<int, KillKind> _lastKillKind = new();
    private AdminSession? _session;
    // While a viewer is open the streams run at 30 Hz; the modules still get them at the tracker rate
    private uint _nextPlayerTick, _nextProjectileTick, _nextVehicleTick;
    // What the player stream reports differently from the model (team, alive, seat), and since when (stream time)
    private readonly Dictionary<Player, (string Mismatch, double Since)> _streamMismatches = new();

    /// <summary>
    /// How long (game seconds) the player stream must disagree with the model before it corrects it. A stream frame is
    /// latest-wins, so it can be applied before an older event that is still queued; a second is plenty for that event.
    /// </summary>
    public const double StreamCorrectionDelay = 1.0;

    /// <summary>The corrections the player stream made since startup: each is an event the model missed.</summary>
    public int StreamCorrections { get; private set; }

    public AdminStateApplier(GameServer server, ILogger<AdminStateApplier> logger, IJoinHistoryWriter? joinHistory = null,
        ICountryResolver? countries = null)
    {
        _server = server;
        _logger = logger;
        _joinHistory = joinHistory;
        _countries = countries;
    }

    public async ValueTask OnMessageAsync(AdminSession session, AdminMessage message)
    {
        var time = DateTimeOffset.UtcNow;
        switch (message)
        {
            case Welcome welcome:
                await ApplyWelcomeAsync(session, welcome, time);
                break;
            case Snapshot snapshot:
                await ApplySnapshotAsync(snapshot, time);
                // A snapshot WebAdmin asked for follows lost events, and a script's end may be among them
                if (snapshot.RequestId != 0 && session != null)
                    _server.ScriptRunner.RequestList(session);
                break;
            case ScriptList list:
                await _server.ScriptRunner.OnListAsync(session, list, time);
                break;
            case PlayerStream stream:
                _server.Viewer.OnPlayers(stream);
                if (Due(ref _nextPlayerTick, stream.Tick, TrackerRate))
                    await ApplyPlayerStreamAsync(stream, time);
                break;
            case VehicleStream stream:
                _server.Viewer.OnVehicles(stream);
                if (Due(ref _nextVehicleTick, stream.Tick, 1))
                    ApplyVehicles(stream.Vehicles);
                break;
            case ProjectileStream stream:
                _server.Viewer.OnProjectiles(stream);
                if (Due(ref _nextProjectileTick, stream.Tick, TrackerRate))
                    await ApplyProjectileStreamAsync(stream, time);
                break;
            case AimStream stream:
                _server.Viewer.OnAims(stream);
                break;
            case GameStream stream:
                await ApplyGameStateAsync(stream.Game, time);
                break;
            case AdminEvent e:
                await ApplyEventAsync(e, time);
                break;
        }
    }

    public async ValueTask OnClosedAsync(AdminSession session)
    {
        _server.DetachSession(session);
        await _server.ScriptRunner.OnSessionClosedAsync(session, DateTimeOffset.UtcNow);
        if (_server.Session == null)
            await _server.UpdateSocketStateAsync(SocketState.Disconnected, DateTimeOffset.UtcNow);
    }

    // ---------------------------------------------------------------- session

    private async ValueTask ApplyWelcomeAsync(AdminSession session, Welcome welcome, DateTimeOffset time)
    {
        _logger.LogInformation(
            "Admin channel {ServerId}: session {Session}, '{Name}' on {Map} {Mode}, {MapCount} maps, capabilities {Caps}",
            _server.Id, welcome.Session, welcome.ServerName, welcome.Map, welcome.Mode, welcome.Maps.Count, welcome.Capabilities);

        if ((welcome.Capabilities & Welcome.CapIdentity) == 0)
            _logger.LogWarning("Admin channel {ServerId}: the server withholds IPs and CD-key hashes (adminSendIdentity 0); players get no key", _server.Id);
        if ((welcome.Capabilities & Welcome.CapConsole) == 0)
            _logger.LogWarning("Admin channel {ServerId}: raw console disabled (adminAllowConsole 0); module scripts will be refused", _server.Id);
        if ((welcome.Capabilities & Welcome.CapScripts) == 0)
            _logger.LogInformation("Admin channel {ServerId}: the module runs no game scripts; .nasa and dance use WebAdmin's own slower way", _server.Id);
        if ((welcome.Capabilities & Welcome.CapSeats) == 0)
            _logger.LogInformation("Admin channel {ServerId}: the module takes no seat preferences; .pilot and .gunner do nothing", _server.Id);

        _server.ResetStartTime();
        await _server.UpdateSocketStateAsync(SocketState.Connected, time);
        await _server.UpdateServerInfoAsync(welcome.ServerName, welcome.GamePort, welcome.GameSpyPort, welcome.MaxPlayers, time);

        var maps = welcome.Maps
            .Select((m, index) => new Map { Index = index, Name = m.Name.ToLowerInvariant(), GameMode = m.Mode, Size = m.Size })
            .ToList();
        await _server.UpdateMapsAsync(maps, time);
        await ApplyGameStateAsync(welcome.Game, time, force: true);

        // Subscriptions do not survive a reconnect
        _session = session;
        _nextPlayerTick = _nextProjectileTick = _nextVehicleTick = 0;
        _server.Viewer.RequestKeyframe();
        Subscribe(session);

        await _server.UpdateMapAsync(FindMap(welcome.Map, welcome.Mode, welcome.MapSize, welcome.MapIndex), welcome.Team1, welcome.Team2, time);
    }

    private byte TrackerRate => AdminGameWriter.RateHz(_server.GameWriter.CurrentTrackerInterval);

    private void Subscribe(AdminSession session)
    {
        SubscribeStreams(session);
        session.Fire(id => new Subscribe(id, AdminStream.Game, 1, []));
        // The hit/miss replay is kept for the viewer's missile list whether a viewer is open or not
        session.Fire(id => new Subscribe(id, AdminStream.Replay, 0, TrackedProjectiles));
    }

    /// <summary>
    /// The position streams: at the tracker rate (vehicles at 1 Hz), or at 30 Hz with the aim stream while a 3D viewer is
    /// open. Called again when the tracker interval changes or a viewer opens or closes.
    /// </summary>
    private void SubscribeStreams(AdminSession session)
    {
        var viewer = _server.Viewer.IsActive;
        var rate = viewer ? (byte)30 : TrackerRate;
        session.Fire(id => new Subscribe(id, AdminStream.Players, rate, []));
        session.Fire(id => new Subscribe(id, AdminStream.Projectiles, rate, TrackedProjectiles));
        session.Fire(id => new Subscribe(id, AdminStream.Vehicles, viewer ? (byte)30 : (byte)1, []));
        if (viewer)
            session.Fire(id => new Subscribe(id, AdminStream.Aim, 30, []));
        else
            session.Fire(id => new Unsubscribe(id, AdminStream.Aim));
    }

    /// <summary>Subscribes the position streams again on the current link, at the rates <see cref="SubscribeStreams"/> picks.</summary>
    public void Resubscribe()
    {
        if (_session is { IsOpen: true } session && _server.Session == session)
        {
            if (_server.Viewer.IsActive)
                _server.Viewer.RequestKeyframe();
            SubscribeStreams(session);
        }
    }

    /// <summary>
    /// Whether a stream frame goes on to the modules: at most rateHz frames a second, so a 30 Hz viewer subscription does
    /// not change what the modules see. A tick that goes backwards (a new link) starts over.
    /// </summary>
    private static bool Due(ref uint next, uint tick, int rateHz)
    {
        if (tick < next && next - tick <= 30)
            return false;
        next = tick + (uint)Math.Max(1, (int)Math.Round(30.0 / Math.Clamp(rateHz, 1, 30)));
        return true;
    }

    private Map FindMap(string name, string mode, int size, int index)
    {
        name = name.ToLowerInvariant();
        var maps = _server.Maps.ToList();
        return maps.FirstOrDefault(m => m.Index == index && m.Name == name)
               ?? maps.FirstOrDefault(m => m.Name == name && m.GameMode == mode && (size == 0 || m.Size == size))
               ?? maps.FirstOrDefault(m => m.Name == name)
               ?? new Map { Index = -1, Name = name, GameMode = mode, Size = size };
    }

    private async ValueTask ApplyGameStateAsync(AdminGameState game, DateTimeOffset time, bool force = false)
    {
        var state = ToGameState(game.Status);
        if (state != null && (force || state != _server.State))
            await _server.UpdateGameStateAsync(state.Value, time);
    }

    private static GameState? ToGameState(byte status) => status switch
    {
        1 => GameState.Playing,
        2 => GameState.EndGame,
        3 => GameState.PreGame,
        4 => GameState.Paused,
        5 => GameState.Restart,
        6 => GameState.NotConnected,
        _ => null,
    };

    // ---------------------------------------------------------------- snapshot

    /// <summary>Reconciles the snapshot with the current state, so modules keep their Player objects across resyncs and reconnects.</summary>
    private async ValueTask ApplySnapshotAsync(Snapshot snapshot, DateTimeOffset time)
    {
        _logger.LogInformation("Admin channel {ServerId}: snapshot {RequestId} lastSeq {LastSeq}: {Players} players, {Vehicles} vehicles, {Cps} control points",
            _server.Id, snapshot.RequestId, snapshot.LastSeq, snapshot.Players.Count, snapshot.Vehicles.Count, snapshot.ControlPoints.Count);

        await ApplyGameStateAsync(snapshot.Game, time);
        ApplyVehicles(snapshot.Vehicles);
        _streamMismatches.Clear();

        var present = new HashSet<int>();
        foreach (var info in snapshot.Players)
        {
            present.Add(info.Id);
            var existing = _server.FindPlayer(info.Id);
            if (existing != null && existing.Name == info.Name && existing.Hash == PlayerKey(info))
            {
                await UpdateExistingPlayerAsync(existing, info, time);
                continue;
            }

            if (existing != null)
                await _server.RemovePlayerAsync(existing, time);

            var player = CreatePlayer(info);
            await _server.AddPlayerAsync(player, time);
            await ApplySeatAsync(player, info.State, time);
        }

        foreach (var gone in _server.Players.Where(p => !present.Contains(p.Index)).ToList())
            await _server.RemovePlayerAsync(gone, time);

        var projectileIds = new HashSet<int>();
        foreach (var p in snapshot.Projectiles)
        {
            projectileIds.Add((int)p.Id);
            var projectile = _server.GetOrCreateProjectile((int)p.Id, p.Template, p.Owner >= 0 ? _server.FindPlayer(p.Owner) : null);
            projectile.Position = ToPosition(p.Position);
            projectile.Rotation = ToRotation(p.Rotation);
        }

        _server.PruneProjectiles(projectileIds);
    }

    private async ValueTask UpdateExistingPlayerAsync(Player player, AdminPlayerInfo info, DateTimeOffset time)
    {
        var state = info.State;
        if (player.Team?.Id != state.Team)
            await _server.UpdatePlayerTeamAsync(player, state.Team, time);

        if (player.Score.Total != info.Score.Score || player.Score.Team != info.Score.TeamScore || player.Score.Kills != info.Score.Kills ||
            player.Score.Deaths != info.Score.Deaths)
            await ApplyScoreAsync(player, info.Score, time);

        player.IsAlive = state.IsAlive;
        player.Squad = state.Squad;
        if (state.IsAlive)
        {
            player.Position = ToPosition(state.Position);
            player.Rotation = ToRotation(state.Rotation);
        }

        await ApplySeatAsync(player, state, time);
    }

    /// <summary>Puts the player in the vehicle the state reports, or takes it out.</summary>
    private async ValueTask ApplySeatAsync(Player player, AdminPlayerState state, DateTimeOffset time)
    {
        if (!IsSeated(state))
        {
            if (player.Vehicle != null)
                await _server.UpdatePlayerVehicleAsync(player, null, null, time);
            return;
        }

        if (player.Vehicle?.RootVehicleId == (int)state.RootNetId)
            return;

        var known = _server.FindVehicle((int)state.RootNetId);
        var vehicle = _server.GetOrCreateVehicle((int)state.RootNetId, ConsoleId(state.RootConsoleId), known?.RootVehicleTemplate ?? "", null);
        await _server.UpdatePlayerVehicleAsync(player, vehicle, vehicle.RootVehicleTemplate, time);
    }

    private Player CreatePlayer(AdminPlayerInfo info)
    {
        var state = info.State;
        var player = new Player
        {
            Name = info.Name,
            Id = info.ProfileId,
            Index = info.Id,
            IpAddress = IPAddress.TryParse(info.IpAddress, out var ip) ? ip : IPAddress.None,
            Hash = PlayerKey(info),
            IsBot = info.IsAi,
            Team = _server.GetTeam(state.Team),
            Squad = state.Squad,
            IsAlive = state.IsAlive,
            Rank = info.Score.Rank >= 0 ? (Rank)info.Score.Rank : Rank.Private,
            Position = ToPosition(state.Position),
            Rotation = ToRotation(state.Rotation),
        };
        player.Score.Total = info.Score.Score;
        player.Score.Team = info.Score.TeamScore;
        player.Score.Kills = info.Score.Kills;
        player.Score.Deaths = info.Score.Deaths;
        player.Score.TeamKills = info.Score.TeamKills;
        player.Score.Ping = state.Ping;
        SetCountry(player);

        if (string.IsNullOrEmpty(player.Hash))
            _logger.LogWarning("Admin channel {ServerId}: player {Index} '{Name}' has no CD-key hash and the server is not a test server: no player key",
                _server.Id, info.Id, info.Name);

        return player;
    }

    /// <summary>The country of the player's address (GeoIP), before the join event so every module sees it.</summary>
    private void SetCountry(Player player)
    {
        if (_countries == null || player.IpAddress.Equals(IPAddress.None))
            return;

        try
        {
            var country = _countries.GetCountryResponse(player.IpAddress.ToString())?.Country;
            player.Country.Code = country?.IsoCode;
            player.Country.Name = country?.Name;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin channel {ServerId}: no country for {Address}", _server.Id, player.IpAddress);
        }
    }

    /// <summary>
    /// Decision B6: the CD-key hash when the server sends one; otherwise, only on servers marked as test servers,
    /// the player name as a distinct name key ("name:" + name). Otherwise no key.
    /// </summary>
    public string PlayerKey(AdminPlayerInfo info)
    {
        if (!string.IsNullOrEmpty(info.CdKeyHash))
            return info.CdKeyHash;

        return _server.ServerInfo.IsTestServer ? Player.NameKeyPrefix + info.Name : "";
    }

    // ---------------------------------------------------------------- streams

    private async ValueTask ApplyPlayerStreamAsync(PlayerStream stream, DateTimeOffset time)
    {
        foreach (var (id, state) in stream.Players)
        {
            var player = _server.FindPlayer(id);
            if (player == null)
                continue;

            player.Squad = state.Squad;
            await ReconcileAsync(player, state, stream.Time, time);
            if (!state.IsAlive)
            {
                // The root position is 0/0/0 while dead; keep the last one
                player.Score.Ping = state.Ping;
                continue;
            }

            await _server.UpdatePlayerAsync(player, ToPosition(state.Position), ToRotation(state.Rotation), state.Ping, time);
        }
    }

    /// <summary>
    /// Team, alive and seat come from events and snapshots. The player stream reports them too: when it disagrees with
    /// the model for <see cref="StreamCorrectionDelay"/>, an event was missed, and the stream's values win (with the
    /// same team and vehicle events a snapshot sends) and a warning is logged.
    /// </summary>
    private async ValueTask ReconcileAsync(Player player, AdminPlayerState state, double streamTime, DateTimeOffset time)
    {
        var mismatch = Mismatch(player, state);
        if (mismatch == null)
        {
            _streamMismatches.Remove(player);
            return;
        }

        if (!_streamMismatches.TryGetValue(player, out var seen) || seen.Mismatch != mismatch || streamTime < seen.Since)
        {
            _streamMismatches[player] = (mismatch, streamTime);
            return;
        }

        if (streamTime - seen.Since < StreamCorrectionDelay)
            return;

        _streamMismatches.Remove(player);
        StreamCorrections++;
        _logger.LogWarning("Admin channel {ServerId}: player {Index} '{Name}' corrected from the player stream ({Mismatch}): an event was missed",
            _server.Id, player.Index, player.Name, mismatch);

        if (player.Team?.Id != state.Team)
            await _server.UpdatePlayerTeamAsync(player, state.Team, time);

        player.IsAlive = state.IsAlive;
        await ApplySeatAsync(player, state, time);
    }

    /// <summary>How the stream's team, alive and seat differ from the model, or null when they agree.</summary>
    private static string? Mismatch(Player player, AdminPlayerState state)
    {
        var differences = new List<string>(3);
        if (player.Team?.Id != state.Team)
            differences.Add($"team {player.Team?.Id.ToString() ?? "none"} -> {state.Team}");

        if (player.IsAlive != state.IsAlive)
            differences.Add(state.IsAlive ? "alive" : "dead");

        var vehicleId = IsSeated(state) ? (int)state.RootNetId : 0;
        if ((player.Vehicle?.RootVehicleId ?? 0) != vehicleId)
            differences.Add($"vehicle {player.Vehicle?.RootVehicleId ?? 0} -> {vehicleId}");

        return differences.Count == 0 ? null : string.Join(", ", differences);
    }

    private static bool IsSeated(AdminPlayerState state) =>
        (state.Flags & AdminPlayerState.FlagSeated) != 0 && state.IsAlive && state.RootNetId != 0;

    private void ApplyVehicles(IReadOnlyList<AdminVehicleState> vehicles)
    {
        var ids = new HashSet<int>();
        foreach (var v in vehicles)
        {
            if (v.NetId == 0)
                continue;

            ids.Add((int)v.NetId);
            var vehicle = _server.GetOrCreateVehicle((int)v.NetId, ConsoleId(v.ConsoleId), v.Template, null);
            vehicle.Team = v.Team;
            vehicle.Health = v.Health;
            vehicle.IsDestroyed = (v.Flags & AdminVehicleState.FlagDestroyed) != 0;
            vehicle.Position = ToPosition(v.Position);
            vehicle.Rotation = ToRotation(v.Rotation);
        }

        _server.PruneVehicles(ids);
    }

    private async ValueTask ApplyProjectileStreamAsync(ProjectileStream stream, DateTimeOffset time)
    {
        var ids = new HashSet<int>();
        foreach (var p in stream.Projectiles)
        {
            ids.Add((int)p.Id);
            var projectile = _server.GetOrCreateProjectile((int)p.Id, p.Template, p.Owner >= 0 ? _server.FindPlayer(p.Owner) : null);
            await _server.UpdateProjectileAsync(projectile, ToPosition(p.Position), ToRotation(p.Rotation), time);
        }

        _server.PruneProjectiles(ids);
    }

    // ---------------------------------------------------------------- events

    private async ValueTask ApplyEventAsync(AdminEvent e, DateTimeOffset time)
    {
        switch (e)
        {
            case EvGameState ev:
                await ApplyGameStateAsync(ev.Game, time);
                break;

            case EvRoundStart ev:
                _server.AddEventLog($"RoundStart {ev.Map} {ev.Mode} tickets {ev.Tickets1}/{ev.Tickets2}", time);
                var teams = _server.Teams.ToList();
                await _server.UpdateMapAsync(FindMap(ev.Map, ev.Mode, 0, -1),
                    teams.FirstOrDefault(t => t.Id == 1)?.Name ?? "", teams.FirstOrDefault(t => t.Id == 2)?.Name ?? "", time);
                break;

            case EvRoundEnd ev:
                _server.AddEventLog($"RoundEnd tickets {ev.Tickets1}/{ev.Tickets2} winner {ev.Winner} victory {ev.VictoryType}", time);
                break;

            case EvPlayerConnect ev:
            {
                var stale = _server.FindPlayer(ev.Player.Id);
                if (stale != null)
                    await _server.RemovePlayerAsync(stale, time);

                await _server.AddPlayerAsync(CreatePlayer(ev.Player), time);
                RecordJoin(ev.Player, time);
                break;
            }

            case EvPlayerDisconnect ev:
                if (FindPlayer(ev.Player) is { } leaving)
                {
                    _streamMismatches.Remove(leaving);
                    await _server.RemovePlayerAsync(leaving, time);
                }
                break;

            case EvSpawn ev:
                if (FindPlayer(ev.Player) is { } spawned)
                {
                    _lastKillKind.Remove(ev.Player);
                    if (spawned.Team?.Id != ev.Team)
                        await _server.UpdatePlayerTeamAsync(spawned, ev.Team, time);
                    await _server.SetPlayerSpawnAsync(spawned, ToPosition(ev.Position), ToRotation(ev.Rotation), time);
                }
                break;

            case EvKill ev:
                await ApplyKillAsync(ev, time);
                break;

            case EvDeath ev:
                if (FindPlayer(ev.Player) is { } dead)
                {
                    // Kill precedes Death for one victim. A death without a kill by another player is a suicide
                    var isSuicide = !_lastKillKind.TryGetValue(ev.Player, out var kind) || kind is not (KillKind.Kill or KillKind.RoadKill);
                    _lastKillKind.Remove(ev.Player);
                    await _server.SetPlayerDeathAsync(dead, ToPosition(ev.Position), isSuicide, time);

                    // A dead player is in no vehicle (as in a snapshot and the player stream), whether or not an
                    // ExitVehicle comes with the death
                    if (dead.Vehicle != null)
                        await _server.UpdatePlayerVehicleAsync(dead, null, null, time);
                }
                break;

            case EvRevive ev:
                if (FindPlayer(ev.Player) is { } revived)
                {
                    revived.IsAlive = true;
                    _server.AddEventLog($"Revive {revived.Name} by {(ev.Medic >= 0 ? FindPlayer(ev.Medic)?.Name : "?")}", time);
                }
                break;

            case EvTeamChange ev:
                if (FindPlayer(ev.Player) is { } switched)
                    await _server.UpdatePlayerTeamAsync(switched, ev.NewTeam, time);
                break;

            case EvScoreChange ev:
                if (FindPlayer(ev.Player) is { } scored)
                    await ApplyScoreAsync(scored, ev.Score, time);
                break;

            case EvVehicle { Type: AdminMessageType.EvEnterVehicle } ev:
                if (FindPlayer(ev.Player) is { } entering)
                {
                    var vehicle = _server.GetOrCreateVehicle((int)ev.RootNetId, ConsoleId(ev.RootConsoleId), ev.RootTemplate, ev.SeatTemplate);
                    await _server.UpdatePlayerVehicleAsync(entering, vehicle, ev.SeatTemplate, time);
                }
                break;

            case EvVehicle { Type: AdminMessageType.EvExitVehicle } ev:
                // Nothing to do when the player already left it (a death takes the player out)
                if (FindPlayer(ev.Player) is { } exiting && exiting.Vehicle?.RootVehicleId == (int)ev.RootNetId)
                    await _server.UpdatePlayerVehicleAsync(exiting, null, null, time);
                break;

            case EvVehicleDestroyed ev:
                if (_server.FindVehicle((int)ev.NetId) is { } destroyed)
                    destroyed.IsDestroyed = true;
                _server.AddEventLog($"VehicleDestroyed {ev.Template} {ev.NetId} by {(ev.Attacker >= 0 ? FindPlayer(ev.Attacker)?.Name : "-")}", time);
                break;

            case EvControlPoint ev:
                _server.AddEventLog($"ControlPoint {(ev.Kind == 1 ? "captured" : "neutralised")} {ev.ControlPoint.Template} team {ev.ControlPoint.Team}", time);
                break;

            case EvChat ev:
                await ApplyChatAsync(ev, time);
                break;

            case EvKick ev:
                _server.AddEventLog($"Kick {ev.Player} {ev.Name}", time);
                break;

            case EvBan ev:
                _server.AddEventLog($"Ban {ev.Player} {ev.Name} type {ev.BanType} expiry {ev.Expiry}", time);
                break;

            case EvBot ev:
                if (FindPlayer(ev.Player) is { } bot)
                    bot.IsBot = true;
                _server.AddEventLog($"Bot {ev.Player} {(ev.Kind switch { 1 => "created", 2 => "removed", _ => "team change" })} team {ev.Team}", time);
                break;

            case EvProjectileCollision ev:
                _server.Viewer.OnCollision(ev);
                break;

            case EvProjectileTrace ev:
                _server.Viewer.OnTrace(ev, time);
                break;

            case EvScriptFinished ev:
                await _server.ScriptRunner.OnFinishedAsync(ev, time);
                break;

            case EvScriptEvent ev:
                await _server.ScriptRunner.OnEventAsync(ev, time);
                break;

            case EvDamage or EvProjectileFired or EvProjectileHit:
                // Opt-in streams 16 and 17; WebAdmin does not subscribe to them
                break;
        }
    }

    /// <summary>
    /// A new connect (not a snapshot after a reconnect or resync) goes to dbo.JoinHistory, for every
    /// server whatever its modules. Queued; the database never holds up the apply loop.
    /// </summary>
    private void RecordJoin(AdminPlayerInfo info, DateTimeOffset time)
    {
        if (_joinHistory == null)
            return;

        try
        {
            if (JoinHistoryWriter.CreateJoin(info, _server.ServerInfo.IpAddress, _server.IpAddress, time) is { } join)
                _joinHistory.Enqueue(join);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin channel {ServerId}: could not record the join of player {Index} '{Name}'", _server.Id, info.Id, info.Name);
        }
    }

    private async ValueTask ApplyKillAsync(EvKill ev, DateTimeOffset time)
    {
        _lastKillKind[ev.Victim] = ev.Kind;
        var victim = FindPlayer(ev.Victim);
        if (victim == null)
            return;

        if (ev.Kind is KillKind.Kill or KillKind.RoadKill && ev.Attacker >= 0 && FindPlayer(ev.Attacker) is { } attacker)
        {
            // Roadkills get the weapon "roadkill"
            var weapon = ev.Kind == KillKind.RoadKill && string.IsNullOrEmpty(ev.Weapon) ? "roadkill" : ev.Weapon;
            await _server.SetPlayerKillAsync(attacker, ToPosition(ev.AttackerPosition), victim, ToPosition(ev.VictimPosition), weapon, time);
            return;
        }

        // Team kills, suicides and crashes are reported by the Death that follows, as before
        _server.AddEventLog($"Kill {ev.Kind} {victim.Name} by {(ev.Attacker >= 0 ? FindPlayer(ev.Attacker)?.Name : "-")} {ev.Weapon}", time);
    }

    private ValueTask ApplyScoreAsync(Player player, AdminPlayerScore score, DateTimeOffset time)
    {
        player.Score.TeamKills = score.TeamKills;
        if (score.Rank >= 0)
            player.Rank = (Rank)score.Rank;
        return _server.UpdatePlayerScoreAsync(player, score.TeamScore, score.Kills, score.Deaths, score.Score, time);
    }

    private async ValueTask ApplyChatAsync(EvChat ev, DateTimeOffset time)
    {
        var sender = ev.Sender >= 0 ? FindPlayer(ev.Sender) : null;
        if (ev.Sender >= 0 && sender == null)
            return;

        var message = new Message
        {
            Player = sender,
            Channel = ev.ChannelName,
            Flags = ev.EngineFlags.ToString(),
            Text = sender != null ? StripChatPrefixes(ev.Text) : ev.Text,
            Type = sender != null ? MessageType.Player : MessageType.Server,
        };
        await _server.AddMessageAsync(message, time);
    }

    /// <summary>The module removes the dead prefix; the HUD channel tokens are removed here.</summary>
    public static string StripChatPrefixes(string text) =>
        text.Replace("HUD_TEXT_CHAT_TEAM", "")
            .Replace("HUD_TEXT_CHAT_SQUAD", "")
            .Replace("HUD_TEXT_CHAT_COMMANDER", "")
            .Replace("HUD_CHAT_DEADPREFIX", "")
            .Replace("*§1DEAD§0*", "")
            .Replace("*DEAD*", "")
            .Replace("§1DEAD§0", "");

    // ---------------------------------------------------------------- helpers

    private Player? FindPlayer(int id) => _server.FindPlayer(id);

    private static long ConsoleId(uint id) => id == AdminProtocol.NoConsoleId ? -1 : id;

    /// <summary>Wire x, y (up), z to WebAdmin's x, height, y.</summary>
    public static Position ToPosition(Vec3 v) => new(v.X, v.Y, v.Z);

    public static Rotation ToRotation(Vec3 v) => new(v.X, v.Y, v.Z);

    public static Vec3 ToVec3(Position p) => new((float)p.X, (float)p.Height, (float)p.Y);

    public static Vec3 ToVec3(Rotation r) => new((float)r.Yaw, (float)r.Pitch, (float)r.Roll);
}
