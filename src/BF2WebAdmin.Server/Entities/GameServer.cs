using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Shared;
using BF2WebAdmin.Shared.Communication.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.Entities;

// TODO: separate Buffer/Audit Module for chatbuffer and eventbuffer? Or use LogModule
/// <summary>
/// One configured game server. It outlives admin channel links: a reconnect brings a new
/// <see cref="AdminSession"/> and a snapshot that is reconciled with the existing state.
/// The mutators are called only by <see cref="AdminStateApplier"/>, one message at a time.
/// </summary>
public class GameServer : IGameServer
{
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;

    private readonly AdminGameWriter _gameWriter;
    private readonly IServiceProvider _globalServices;
    private readonly ILogger<GameServer> _logger;
    private readonly CancellationToken _cancellationToken;
    public ServerInfo ServerInfo { get; private set; }
    public IGameWriter GameWriter => _gameWriter;
    public IModManager? ModManager { get; private set; }

    public string Id => ServerInfo.ServerId;
    public string Name { get; private set; }
    public IPAddress IpAddress { get; }
    public IPAddress ConnectedIpAddress { get; private set; } = IPAddress.None;
    public int GamePort { get; private set; }
    public int QueryPort { get; private set; }
    public int RconPort => ServerInfo.RconPort;
    public int MaxPlayers { get; private set; }

    public GameState State { get; private set; } = GameState.NotConnected;
    public SocketState SocketState { get; private set; } = SocketState.Disconnected;

    public Map? Map { get; private set; }

    public IEnumerable<Map> Maps { get { lock (_maps) return _maps.ToArray(); } }
    private readonly List<Map> _maps = new();

    public IEnumerable<Team> Teams { get { lock (_teams) return _teams.ToArray(); } }
    private readonly List<Team> _teams = new();

    public IEnumerable<Player> Players { get { lock (_players) return _players.ToArray(); } }
    private readonly List<Player> _players = new();

    public IEnumerable<Vehicle> Vehicles { get { lock (_vehicles) return _vehicles.ToArray(); } }
    private readonly List<Vehicle> _vehicles = new();

    public IEnumerable<Projectile> Projectiles { get { lock (_projectiles) return _projectiles.ToArray(); } }
    private readonly List<Projectile> _projectiles = new();

    public IEnumerable<(MessageDto Message, DateTimeOffset Time)> Messages => _chatBuffer.Get().Where(x => x.Message != null).OrderBy(x => x.Time);
    private readonly CircularBuffer<(MessageDto Message, DateTimeOffset Time)> _chatBuffer = new(500);

    public IEnumerable<(string Message, DateTimeOffset Time)> Events => _eventBuffer.Get().Where(x => x.Message != null).OrderBy(x => x.Time);
    private readonly CircularBuffer<(string Message, DateTimeOffset Time)> _eventBuffer = new(500);

    private readonly bool _enablePositionUpdates = true;
    private readonly SemaphoreSlim _modManagerLock = new(1);
    private AdminSession? _session;
    private string? _recordingPath;

    public GameServer(ServerInfo serverInfo, IPAddress ipAddress, IServiceProvider globalServices, CancellationToken cancellationToken)
    {
        ServerInfo = serverInfo;
        IpAddress = ipAddress;
        GamePort = serverInfo.GamePort;
        QueryPort = serverInfo.QueryPort;
        Name = serverInfo.ServerId;
        _globalServices = globalServices;
        _logger = _globalServices.GetRequiredService<ILogger<GameServer>>();
        _cancellationToken = cancellationToken;
        _gameWriter = new AdminGameWriter(this, _globalServices.GetRequiredService<ILogger<AdminGameWriter>>());
        Applier = new AdminStateApplier(this, _globalServices.GetRequiredService<ILogger<AdminStateApplier>>());

        var viewers = _globalServices.GetService<ViewerRegistry>();
        Viewer = new ViewerFeed(this, viewers, _globalServices.GetService<IHubContext<ServerHub, IServerHubClient>>(), _logger, cancellationToken);
        if (viewers != null)
            viewers.ActiveChanged += (serverId, _) =>
            {
                if (serverId == Id)
                    Applier.Resubscribe();
            };
    }

    /// <summary>The 3D viewer's frames and the last traced missiles.</summary>
    public ViewerFeed Viewer { get; }

    /// <summary>Maps admin channel messages onto this server.</summary>
    public AdminStateApplier Applier { get; }

    /// <summary>The current admin channel link, or null while disconnected.</summary>
    public AdminSession? Session => _session;

    public void UpdateServerInfo(ServerInfo serverInfo) => ServerInfo = serverInfo;

    public void AttachSession(AdminSession session)
    {
        _session = session;
        ConnectedIpAddress = session.RemoteEndPoint.Address;
        if (_recordingPath != null)
            session.StartRecording(_recordingPath);
    }

    public void DetachSession(AdminSession session)
    {
        Interlocked.CompareExchange(ref _session, null, session);
    }

    public void StartRecording(string path)
    {
        _recordingPath = path;
        _session?.StartRecording(path);
    }

    public void StopRecording()
    {
        _recordingPath = null;
        _session?.StopRecording();
    }

    public void ResetStartTime() => StartTime = DateTime.UtcNow;

    public void AddEventLog(string message, DateTimeOffset time) => _eventBuffer.Add((message, time));

    public async ValueTask UpdateServerInfoAsync(string name, int gamePort, int queryPort, int maxPlayers, DateTimeOffset time)
    {
        Name = name;
        GamePort = gamePort;
        QueryPort = queryPort;
        MaxPlayers = maxPlayers;
        await CreateModManagerAsync();
        await ModManager.Mediator.PublishAsync(new ServerUpdateEvent(name, gamePort, queryPort, maxPlayers, time));
    }

    public async ValueTask CreateModManagerAsync(bool forceReinitialize = false)
    {
        // TODO: don't run this here since errors are not logged/handled, and remove after running once so it doesn't create new ModManagers
        // Server has the correct id now, so we can load player/server settings and setup mods
        using var activity = Telemetry.ActivitySource.StartActivity();

        try
        {
            // Avoid creating multiple modmanagers if called at the same time
            await _modManagerLock.WaitAsync(_cancellationToken);
            activity?.AddEvent(new ("Received lock"));

            try
            {
                if (ModManager == null || forceReinitialize)
                {
                    ModManager?.Dispose();
                    ModManager = await Server.ModManager.CreateAsync(this, _globalServices, _cancellationToken);
                    await ModManager.Mediator.PublishAsync(new SocketStateChangedEvent(SocketState, DateTimeOffset.UtcNow));
                }
            }
            finally
            {
                _modManagerLock.Release();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "ModManager creation failed on ServerUpdate");
        }
    }

    private ValueTask PublishEventAsync(IEvent e)
    {
        return ModManager?.Mediator?.PublishAsync(e) ?? ValueTask.CompletedTask;
    }

    public ValueTask UpdateGameStateAsync(GameState state, DateTimeOffset time)
    {
        State = state;
        _eventBuffer.Add(($"{nameof(GameStateChangedEvent)} {state}", time));
        return PublishEventAsync(new GameStateChangedEvent(state, time));
    }

    public ValueTask UpdateSocketStateAsync(SocketState state, DateTimeOffset time)
    {
        // TODO: Stop all running tasks/commands when disconnecting - wait for reconnection
        if (state != SocketState)
        {
            SocketState = state;
            _eventBuffer.Add(($"{nameof(SocketStateChangedEvent)} {state}", time));
            return PublishEventAsync(new SocketStateChangedEvent(state, time));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateMapsAsync(IList<Map> maps, DateTimeOffset time)
    {
        lock (_maps)
        {
            _maps.Clear();
            _maps.AddRange(maps);
        }

        _eventBuffer.Add(($"{nameof(MapsChangedEvent)} {string.Join("|", maps.Select(m => $"{m.Index},{m.Name},{m.GameMode},{m.Size}"))}", time));
        return PublishEventAsync(new MapsChangedEvent(Maps, time));
    }

    public ValueTask UpdateMapAsync(Map map, string team1Name, string team2Name, DateTimeOffset time)
    {
        Map = map;

        lock (_teams)
        {
            _teams.Clear();
            _teams.Add(new Team { Id = 1, Name = team1Name });
            _teams.Add(new Team { Id = 2, Name = team2Name });
        }

        // Some settings get cleared when we change map so we need to do it again
        GameWriter.SendConsole(RconScript.InitServer);

        _eventBuffer.Add(($"{nameof(MapChangedEvent)} {Map.Index},{Map.Name},{Map.Size}", time));
        return PublishEventAsync(new MapChangedEvent(Map, Teams.ToList(), time));
    }

    /// <summary>Team 1 or 2 from the map's team names; any other id gets a nameless team so modules never see null.</summary>
    public Team GetTeam(int teamId)
    {
        lock (_teams)
        {
            var team = _teams.FirstOrDefault(t => t.Id == teamId);
            if (team == null)
            {
                team = new Team { Id = teamId, Name = teamId == 0 ? "None" : $"Team {teamId}" };
                _teams.Add(team);
            }

            return team;
        }
    }

    public ValueTask AddPlayerAsync(Player player, DateTimeOffset time)
    {
        lock (_players)
            _players.Add(player);

        _eventBuffer.Add(($"{nameof(PlayerJoinEvent)} {player.Name}", time));
        return PublishEventAsync(new PlayerJoinEvent(player, time));
    }

    public ValueTask UpdatePlayerAsync(Player player, Position position, Rotation rotation, int ping, DateTimeOffset time)
    {
        player.Position = position;
        player.Rotation = rotation;

        if (!player.PingHistory.ContainsKey(ping))
            player.PingHistory.Add(ping, 0);

        player.PingHistory[ping]++;
        player.Score.Ping = ping;

        if (_enablePositionUpdates)
        {
            return PublishEventAsync(new PlayerPositionEvent(player, position, rotation, ping, time));
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask UpdatePlayerVehicleAsync(Player player, Vehicle? vehicle, string? subVehicleTemplate, DateTimeOffset time)
    {
        if (player.Vehicle != null)
        {
            player.PreviousVehicle = player.Vehicle;
            player.PreviousVehicle.Players.Remove(player);
        }

        if (vehicle != null && !vehicle.Players.Contains(player))
            vehicle.Players.Add(player);

        player.Vehicle = vehicle;
        player.SubVehicleTemplate = subVehicleTemplate;
        _eventBuffer.Add(($"{nameof(PlayerVehicleEvent)} {player.Name} {vehicle?.RootVehicleTemplate}", time));
        await PublishEventAsync(new PlayerVehicleEvent(player, vehicle, time));
    }

    public ValueTask UpdatePlayerTeamAsync(Player player, int teamId, DateTimeOffset time)
    {
        player.Team = GetTeam(teamId);
        _eventBuffer.Add(($"{nameof(PlayerTeamEvent)} {player.Name} {player.Team.Name}", time));
        return PublishEventAsync(new PlayerTeamEvent(player, player.Team, time));
    }

    public ValueTask UpdatePlayerScoreAsync(Player player, int teamScore, int kills, int deaths, int totalScore, DateTimeOffset time)
    {
        player.Score.Team = teamScore;
        player.Score.Kills = kills;
        player.Score.Deaths = deaths;
        player.Score.Total = totalScore;

        _eventBuffer.Add(($"{nameof(PlayerScoreEvent)} {player.Name} {teamScore} {kills} {deaths} {totalScore}", time));
        return PublishEventAsync(new PlayerScoreEvent(player, teamScore, kills, deaths, totalScore, time));
    }

    public ValueTask UpdateProjectileAsync(Projectile projectile, Position position, Rotation rotation, DateTimeOffset time)
    {
        projectile.Position = position;
        projectile.Rotation = rotation;

        if (_enablePositionUpdates)
        {
            return PublishEventAsync(new ProjectilePositionEvent(projectile, position, rotation, time));
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask RemovePlayerAsync(Player player, DateTimeOffset time)
    {
        lock (_players)
            _players.Remove(player);

        player.Vehicle?.Players.Remove(player);
        _eventBuffer.Add(($"{nameof(PlayerLeftEvent)} {player.Name}", time));
        await PublishEventAsync(new PlayerLeftEvent(player, time));
    }

    public ValueTask SetPlayerSpawnAsync(Player player, Position position, Rotation rotation, DateTimeOffset time)
    {
        player.IsAlive = true;
        player.Position = position;
        player.Rotation = rotation;

        _eventBuffer.Add(($"{nameof(PlayerSpawnEvent)} {player.Name} {position}", time));
        return PublishEventAsync(new PlayerSpawnEvent(player, position, rotation, time));
    }

    public ValueTask SetPlayerKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon, DateTimeOffset time)
    {
        victim.IsAlive = false;
        _eventBuffer.Add(($"{nameof(PlayerKillEvent)} {attacker.Name} {victim.Name} {weapon}", time));
        return PublishEventAsync(new PlayerKillEvent(attacker, attackerPosition, victim, victimPosition, weapon, time));
    }

    public ValueTask SetPlayerDeathAsync(Player player, Position position, bool isSuicide, DateTimeOffset time)
    {
        player.IsAlive = false;
        _eventBuffer.Add(($"{nameof(PlayerDeathEvent)} {player.Name} {isSuicide}", time));
        return PublishEventAsync(new PlayerDeathEvent(player, position, isSuicide, time));
    }

    public async ValueTask AddMessageAsync(Message message, DateTimeOffset time)
    {
        _chatBuffer.Add((message.ToDto(), time));
        if (ModManager != null)
            await ModManager.HandleChatMessageAsync(message);
        await PublishEventAsync(new ChatMessageEvent(message, time));
    }

    /// <summary>The vehicle with this networkable id, created on first sight.</summary>
    public Vehicle GetOrCreateVehicle(int networkableId, long consoleId, string rootTemplate, string? seatTemplate)
    {
        lock (_vehicles)
        {
            var vehicle = networkableId != 0
                ? _vehicles.FirstOrDefault(v => v.RootVehicleId == networkableId)
                : _vehicles.FirstOrDefault(v => v.RootVehicleId == 0 && v.ConsoleId == consoleId);
            if (vehicle != null)
            {
                if (vehicle.RootVehicleTemplate != rootTemplate)
                {
                    // A networkable id that the engine gave to a new object
                    vehicle.RootVehicleTemplate = rootTemplate;
                    vehicle.Template = seatTemplate ?? rootTemplate;
                    vehicle.IsDestroyed = false;
                }

                vehicle.ConsoleId = consoleId;
                return vehicle;
            }

            vehicle = new Vehicle
            {
                RootVehicleId = networkableId,
                ConsoleId = consoleId,
                RootVehicleTemplate = rootTemplate,
                Template = seatTemplate ?? rootTemplate,
            };
            _vehicles.Add(vehicle);
            return vehicle;
        }
    }

    public Vehicle? FindVehicle(int networkableId)
    {
        lock (_vehicles)
            return _vehicles.FirstOrDefault(v => v.RootVehicleId == networkableId);
    }

    /// <summary>Drops vehicles the module no longer reports and nobody sits in.</summary>
    public void PruneVehicles(ISet<int> reportedNetworkableIds)
    {
        lock (_vehicles)
            _vehicles.RemoveAll(v => !reportedNetworkableIds.Contains(v.RootVehicleId) && v.Players.Count == 0);
    }

    public Projectile GetOrCreateProjectile(int id, string templateName, Player? owner)
    {
        lock (_projectiles)
        {
            var projectile = _projectiles.FirstOrDefault(p => p.Id == id);
            if (projectile == null)
            {
                projectile = new Projectile { Id = id, Template = templateName, Owner = owner };
                _projectiles.Add(projectile);
            }

            return projectile;
        }
    }

    public void PruneProjectiles(ISet<int> reportedIds)
    {
        lock (_projectiles)
            _projectiles.RemoveAll(p => !reportedIds.Contains(p.Id));
    }

    /// <summary>The player with this id, or null. Modules use <see cref="GetPlayer"/>.</summary>
    public Player? FindPlayer(int index)
    {
        lock (_players)
            return _players.FirstOrDefault(x => x.Index == index);
    }

    public Player? GetPlayer(int index)
    {
        // This can be null sometimes when connecting / reconnected player?
        return FindPlayer(index) ?? new Player();
    }

    public Player? GetPlayerByName(string namePart)
    {
        return Players.FirstOrDefault(p => p.Name.ToLower().Contains(namePart));
    }

    public Player? GetPlayerByHash(string playerHash)
    {
        return Players.FirstOrDefault(p => p.Hash == playerHash);
    }
}
