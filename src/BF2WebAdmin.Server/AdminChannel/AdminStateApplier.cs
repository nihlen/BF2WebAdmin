using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Shared;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// Maps the admin channel's Welcome, Snapshot, stream frames and events onto a <see cref="GameServer"/>
/// (the role EventHandler had for mm_webadmin's text lines). Runs on the session's apply loop, one message at a time.
/// </summary>
public class AdminStateApplier : IAdminSessionHandler
{
    /// <summary>mm_webadmin tracked these projectiles for Heli2v2's TV missile stats.</summary>
    public static readonly string[] TrackedProjectiles = ["agm114_hellfire_tv"];

    private readonly GameServer _server;
    private readonly ILogger<AdminStateApplier> _logger;
    private readonly Dictionary<int, KillKind> _lastKillKind = new();

    public AdminStateApplier(GameServer server, ILogger<AdminStateApplier> logger)
    {
        _server = server;
        _logger = logger;
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
                break;
            case PlayerStream stream:
                await ApplyPlayerStreamAsync(stream, time);
                break;
            case VehicleStream stream:
                ApplyVehicles(stream.Vehicles);
                break;
            case ProjectileStream stream:
                await ApplyProjectileStreamAsync(stream, time);
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

        _server.ResetStartTime();
        await _server.UpdateSocketStateAsync(SocketState.Connected, time);
        await _server.UpdateServerInfoAsync(welcome.ServerName, welcome.GamePort, welcome.GameSpyPort, welcome.MaxPlayers, time);

        var maps = welcome.Maps
            .Select((m, index) => new Map { Index = index, Name = m.Name.ToLowerInvariant(), GameMode = m.Mode, Size = m.Size })
            .ToList();
        await _server.UpdateMapsAsync(maps, time);
        await ApplyGameStateAsync(welcome.Game, time, force: true);

        // Subscriptions do not survive a reconnect
        Subscribe(session);

        await _server.UpdateMapAsync(FindMap(welcome.Map, welcome.Mode, welcome.MapSize, welcome.MapIndex), welcome.Team1, welcome.Team2, time);
    }

    private void Subscribe(AdminSession session)
    {
        var rate = AdminGameWriter.RateHz(_server.GameWriter.CurrentTrackerInterval);
        session.Fire(id => new Subscribe(id, AdminStream.Players, rate, []));
        session.Fire(id => new Subscribe(id, AdminStream.Projectiles, rate, TrackedProjectiles));
        session.Fire(id => new Subscribe(id, AdminStream.Vehicles, 1, []));
        session.Fire(id => new Subscribe(id, AdminStream.Game, 1, []));
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
        var seated = (state.Flags & AdminPlayerState.FlagSeated) != 0 && state.IsAlive && state.RootNetId != 0;
        if (!seated)
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

        if (string.IsNullOrEmpty(player.Hash))
            _logger.LogWarning("Admin channel {ServerId}: player {Index} '{Name}' has no CD-key hash and the server is not a test server: no player key",
                _server.Id, info.Id, info.Name);

        return player;
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
            if (!state.IsAlive)
            {
                // The root position is 0/0/0 while dead; keep the last one
                player.Score.Ping = state.Ping;
                continue;
            }

            await _server.UpdatePlayerAsync(player, ToPosition(state.Position), ToRotation(state.Rotation), state.Ping, time);
        }
    }

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
                break;
            }

            case EvPlayerDisconnect ev:
                if (FindPlayer(ev.Player) is { } leaving)
                    await _server.RemovePlayerAsync(leaving, time);
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
                    // Kill precedes Death for one victim. As with mm_webadmin, a death without a kill by another player is a suicide
                    var isSuicide = !_lastKillKind.TryGetValue(ev.Player, out var kind) || kind is not (KillKind.Kill or KillKind.RoadKill);
                    _lastKillKind.Remove(ev.Player);
                    await _server.SetPlayerDeathAsync(dead, ToPosition(ev.Position), isSuicide, time);
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
                if (FindPlayer(ev.Player) is { } exiting && (exiting.Vehicle == null || exiting.Vehicle.RootVehicleId == (int)ev.RootNetId))
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

            case EvDamage or EvProjectileFired or EvProjectileHit:
                // Opt-in streams 16 and 17; WebAdmin does not subscribe to them
                break;
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
            // mm_webadmin reported roadkills with the weapon "roadkill"
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

    /// <summary>The module removes the dead prefix; the HUD channel tokens are removed here, as mm_webadmin's stripmessage did.</summary>
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
