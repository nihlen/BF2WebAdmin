using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Shared;
using BF2WebAdmin.Shared.Communication.DTOs;
using Nihlen.Common.Telemetry;
using Nihlen.Gamespy;

namespace BF2WebAdmin.Server.Entities;

// TODO: separate Buffer/Audit Module for chatbuffer and eventbuffer? Or use LogModule
public class GameServer : IGameServer
{
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;

    private IGameWriter _gameWriter;
    private readonly IServiceProvider _globalServices;
    private readonly ILogger<GameServer> _logger;
    private readonly CancellationToken _cancellationToken;
    public ServerInfo ServerInfo { get; private set; }
    public IGameWriter GameWriter => _gameWriter;
    public IModManager? ModManager { get; private set; }

    public string Id => $"{IpAddress}:{GamePort}";
    public string Name { get; private set; }
    public IPAddress IpAddress { get; }
    public IPAddress ConnectedIpAddress { get; }
    public int GamePort { get; private set; }
    public int QueryPort { get; private set; }
    public int RconPort { get; private set; }
    public int MaxPlayers { get; private set; }

    public GameState State { get; private set; }
    public SocketState SocketState { get; private set; } = SocketState.Disconnected;

    public Map? Map { get; private set; }

    public IEnumerable<Map> Maps => _maps.ToArray();
    private readonly List<Map> _maps = new List<Map>();

    public IEnumerable<Team> Teams => _teams.ToArray();
    private readonly IList<Team> _teams = new List<Team>();

    public IEnumerable<Player> Players => _players.ToArray();
    private readonly IList<Player> _players = new List<Player>();

    public IEnumerable<Vehicle> Vehicles => _vehicles.ToArray();
    private readonly IList<Vehicle> _vehicles = new List<Vehicle>();

    public IEnumerable<Projectile> Projectiles => _projectiles.ToArray();
    private readonly IList<Projectile> _projectiles = new List<Projectile>();

    public IEnumerable<(MessageDto Message, DateTimeOffset Time)> Messages => _chatBuffer.Get().Where(x => x.Message != null).OrderBy(x => x.Time);
    private readonly CircularBuffer<(MessageDto Message, DateTimeOffset Time)> _chatBuffer = new(500);

    public IEnumerable<(string Message, DateTimeOffset Time)> Events => _eventBuffer.Get().Where(x => x.Message != null).OrderBy(x => x.Time);
    private readonly CircularBuffer<(string Message, DateTimeOffset Time)> _eventBuffer = new(500);
        
    private bool _enablePositionUpdates = true;
    private readonly SemaphoreSlim _modManagerLock = new(1);

    private GameServer(IPAddress publicIpAddress, IPAddress connectedIpAddress, IGameWriter writer, ServerInfo serverInfo, IServiceProvider globalServices, CancellationToken cancellationToken)
    {
        IpAddress = publicIpAddress;
        ConnectedIpAddress = connectedIpAddress;
        _gameWriter = writer;
        _globalServices = globalServices;
        _logger = _globalServices.GetRequiredService<ILogger<GameServer>>();
        _cancellationToken = cancellationToken;
        ServerInfo = serverInfo;
    }

    public static async Task<GameServer> CreateAsync(
        IPAddress publicIpAddress, 
        IPAddress connectedIpAddress,
        IGameWriter writer, 
        ServerInfo serverInfo, 
        IServiceProvider globalServices, 
        CancellationToken cancellationToken
    )
    {
        // TODO: Pause everything until we receive data from the server (we don't know the gameport before so we cant tell serverId)
        var gameServer = new GameServer(publicIpAddress, connectedIpAddress, writer, serverInfo, globalServices, cancellationToken);
        await gameServer.UpdateSocketStateAsync(SocketState.Connected, DateTimeOffset.UtcNow);

        // Init kill command, fast pad repairs, heli startup
        gameServer.GameWriter.SendRcon(RconScript.InitServer);
        return gameServer;
    }

    public async ValueTask SetReconnectedAsync(IGameWriter gameWriter, DateTimeOffset time)
    {
        StartTime = DateTime.UtcNow;

        // Clear old data when reconnecting, otherwise we might get duplicates since the server resends player connections
        _maps.Clear();
        _players.Clear();
        _vehicles.Clear();
        _projectiles.Clear();
        _teams.Clear();

        _gameWriter = gameWriter;
        await UpdateSocketStateAsync(SocketState.Connected, time);

        // Init kill command, fast pad repairs, heli startup
        GameWriter.SendRcon(RconScript.InitServer);
    }

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
        _maps.Clear();
        _maps.AddRange(maps);

        _eventBuffer.Add(($"{nameof(MapsChangedEvent)} {string.Join("|", _maps.Select(m => $"{m.Index},{m.Name},{m.Size}"))}", time));
        return PublishEventAsync(new MapsChangedEvent(Maps, time));
    }

    public ValueTask UpdateMapAsync(string mapName, string team1Name, string team2Name, DateTimeOffset time)
    {
        Map = _maps.FirstOrDefault(m => m.Name == mapName);

        _teams.Clear();
        _teams.Add(new Team { Id = 1, Name = team1Name });
        _teams.Add(new Team { Id = 2, Name = team2Name });

        // Some settings get cleared when we change map so we need to do it again
        GameWriter.SendRcon(RconScript.InitServer);

        _eventBuffer.Add(($"{nameof(MapChangedEvent)} {Map.Index},{Map.Name},{Map.Size}", time));
        return PublishEventAsync(new MapChangedEvent(Map, _teams.ToList(), time));
    }

    public ValueTask AddPlayerAsync(Player player, DateTimeOffset time)
    {
        if (DateTime.UtcNow - StartTime > TimeSpan.FromMinutes(1))
        {
            // Adjust for mm_autobalancer - it changes team after the player connects which does not create a new playerTeam event (ignore when reconnecting)
            var team1Count = _players.Count(p => p.Team.Id == _teams[0].Id);
            var team2Count = _players.Count(p => p.Team.Id == _teams[1].Id);
            player.Team = team2Count > team1Count ? _teams[0] : _teams[1];
            _gameWriter.SendTeam(player, player.Team.Id);
        }

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

    public async ValueTask UpdatePlayerVehicleAsync(Player player, Vehicle vehicle, string subVehicleTemplate, DateTimeOffset time)
    {
        if (player.Vehicle != null)
        {
            player.PreviousVehicle = player.Vehicle;
            player.PreviousVehicle.Players.Remove(player);
        }

        if (!player.IsAlive)
        {
            // Spawning directly in vehicle doesn't send a PlayerSpawn event
            player.IsAlive = true;
            _eventBuffer.Add(($"{nameof(PlayerSpawnEvent)} {player.Name} {player.Position}", time));
            await PublishEventAsync(new PlayerSpawnEvent(player, player.Position ?? new Position(0, 0, 0), player.Rotation ?? new Rotation(0, 0, 0), time));
        }

        player.Vehicle = vehicle;
        player.SubVehicleTemplate = subVehicleTemplate;
        _eventBuffer.Add(($"{nameof(PlayerVehicleEvent)} {player.Name} {vehicle?.RootVehicleTemplate}", time));
        await PublishEventAsync(new PlayerVehicleEvent(player, vehicle, time));
    }

    public ValueTask UpdatePlayerTeamAsync(Player player, int teamId, DateTimeOffset time)
    {
        player.Team = _teams.First(t => t.Id == teamId);
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

    public ValueTask RemovePlayerAsync(Player player, DateTimeOffset time)
    {
        _players.Remove(player);
        _eventBuffer.Add(($"{nameof(PlayerLeftEvent)} {player.Name}", time));
        return PublishEventAsync(new PlayerLeftEvent(player, time));
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

    public ValueTask SetPlayerDeathAsync(Player player, Position position, DateTimeOffset time)
    {
        // TODO: it always thinks it's suicide - which order does kill/death events come?
        var isSuicide = player.IsAlive;
        player.IsAlive = false;
        _eventBuffer.Add(($"{nameof(PlayerDeathEvent)} {player.Name} {isSuicide}", time));
        return PublishEventAsync(new PlayerDeathEvent(player, position, isSuicide, time));
    }

    public async ValueTask AddMessageAsync(Message message, DateTimeOffset time)
    {
        _chatBuffer.Add((message.ToDto(), time));
        await ModManager.HandleChatMessageAsync(message);
        await PublishEventAsync(new ChatMessageEvent(message, time));
    }

    public void SetRconResponse(string responseCode, string value)
    {
        GameWriter.SetRconResponse(responseCode, value);
    }

    public Vehicle GetVehicle(Player player, int rootVehicleId, string rootVehicleName, string vehicleName)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.RootVehicleId == rootVehicleId);
        if (vehicle != null)
        {
            if (!vehicle.Players.Contains(player))
                vehicle.Players.Add(player);

            return vehicle;
        }

        vehicle = new Vehicle
        {
            Template = vehicleName,
            RootVehicleId = rootVehicleId,
            RootVehicleTemplate = rootVehicleName,
            Players = new List<Player> { player }
        };
        _vehicles.Add(vehicle);

        return vehicle;
    }

    public Player? GetPlayer(int index)
    {
        // This can be null sometimes when connecting / reconnected player?
        return _players.FirstOrDefault(x => x.Index == index) ?? new Player();
    }

    public Player? GetPlayerByName(string namePart)
    {
        return _players.FirstOrDefault(p => p.Name.ToLower().Contains(namePart));
    }

    public Player? GetPlayerByHash(string playerHash)
    {
        return _players.FirstOrDefault(p => p.Hash == playerHash);
    }

    public Projectile GetProjectile(int id, string templateName, Position position)
    {
        var projectile = _projectiles.FirstOrDefault(p => p.Id == id);
        if (projectile == null)
        {
            var closestGunner = _players
                .Where(p => p.Vehicle?.Template?.ToLower().Contains("ahe_") ?? false)
                .MinBy(p => p.Position?.Distance(position) ?? double.MaxValue);

            projectile = new Projectile
            {
                Id = id,
                Template = templateName,
                Owner = closestGunner
            };

            _projectiles.Add(projectile);
        }

        return projectile;
    }

    public async Task<bool> FixTeamMismatchAsync(IEnumerable<Player> players)
    {
        var gamespy = new Gamespy3Service();
        var response = await gamespy.QueryServerAsync(IpAddress, QueryPort);

        foreach (var responsePlayer in response.Players)
        {
            var matchedPlayer = Players.FirstOrDefault(p => p.Id == responsePlayer.Pid);
            if (matchedPlayer is null)
            {
                _logger.LogWarning("Failed to find matching player from query response: {PlayerPid} {PlayerName}", responsePlayer.Pid, responsePlayer.Name);
                continue;
            }

            if (matchedPlayer.Team.Id != responsePlayer.Team)
            {
                _logger.LogInformation("Fixing team mismatch for {Player} ({OldTeam} => {NewTeam})", matchedPlayer.Name, matchedPlayer.Team.Id, responsePlayer.Team);
                await UpdatePlayerTeamAsync(matchedPlayer, responsePlayer.Team, DateTimeOffset.UtcNow);
                //if (player.Index == matchedPlayer.Index) isFixed = true;
            }
        }

        return players.Select(p => p.Team).Distinct().Count() == 1;
    }
}