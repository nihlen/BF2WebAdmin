using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using Serilog;

namespace BF2WebAdmin.Server.Entities
{
    public class GameServer : IGameServer
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<GameServer>();
        public DateTime StartTime { get; private set; } = DateTime.UtcNow;

        //private readonly IModManager _mm;
        //private readonly IGameWriter _writer;

        private IGameWriter _gameWriter;
        public ServerInfo ServerInfo { get; private set; }
        public IGameWriter GameWriter => _gameWriter;
        public IModManager? ModManager { get; private set; }

        public string Id => $"{IpAddress}:{GamePort}";
        public string Name { get; private set; }
        public IPAddress IpAddress { get; }
        public int GamePort { get; private set; }
        public int QueryPort { get; private set; }
        public int RconPort { get; private set; }
        public int MaxPlayers { get; private set; }

        public GameState State { get; private set; }
        public SocketState SocketState { get; private set; } = SocketState.Disconnected;

        public Map Map { get; private set; }

        public IEnumerable<Map> Maps => _maps.ToArray();
        private readonly IList<Map> _maps = new List<Map>();

        public IEnumerable<Team> Teams => _teams.ToArray();
        private readonly IList<Team> _teams = new List<Team>();

        public IEnumerable<Player> Players => _players.ToArray();
        private readonly IList<Player> _players = new List<Player>();

        public IEnumerable<Vehicle> Vehicles => _vehicles.ToArray();
        private readonly IList<Vehicle> _vehicles = new List<Vehicle>();

        public IEnumerable<Projectile> Projectiles => _projectiles.ToArray();
        private readonly IList<Projectile> _projectiles = new List<Projectile>();

        public IEnumerable<Message> Messages => _chatBuffer.Get().OrderBy(x => x.Time);
        private readonly CircularBuffer<Message> _chatBuffer = new CircularBuffer<Message>(100);

        // v1 - events
        //public event Action<string, int, int, int> ServerUpdate;
        //public event Action<GameState> GameStateChanged;
        //public event Action<SocketState> SocketStateChanged;
        //public event Action<IEnumerable<Map>> MapsChanged;
        //public event Action<Map> MapChanged;
        //public event Action<Message> ChatMessage;
        //public event Action<Player> PlayerJoin;
        //public event Action<Player> PlayerLeft;
        //public event Action<Player, Position, Rotation> PlayerSpawn;
        //public event Action<Player, Position, Player, Position, string> PlayerKill;
        //public event Action<Player, Position> PlayerDeath;
        //public event Action<Player, Vehicle> PlayerVehicle;
        //public event Action<Player, Team> PlayerTeam;
        //public event Action<Player, int, int, int, int> PlayerScore;
        //public event Action<Player, Position, Rotation, int> PlayerPosition;
        //public event Action<Projectile, Position, Rotation> ProjectilePosition;

        // v2 - async events
        //public event Func<string, int, int, int, Task> ServerUpdate
        //{
        //    add => _serverUpdateEvent.Add(value);
        //    remove => _serverUpdateEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<string, int, int, int, Task>> _serverUpdateEvent = new AsyncEvent<Func<string, int, int, int, Task>>();

        //public event Func<GameState, Task> GameStateChanged
        //{
        //    add => _gameStateChangedEvent.Add(value);
        //    remove => _gameStateChangedEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<GameState, Task>> _gameStateChangedEvent = new AsyncEvent<Func<GameState, Task>>();

        //public event Func<SocketState, Task> SocketStateChanged
        //{
        //    add => _socketStateChangedEvent.Add(value);
        //    remove => _socketStateChangedEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<SocketState, Task>> _socketStateChangedEvent = new AsyncEvent<Func<SocketState, Task>>();

        //public event Func<IEnumerable<Map>, Task> MapsChanged
        //{
        //    add => _mapsChangedEvent.Add(value);
        //    remove => _mapsChangedEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<IEnumerable<Map>, Task>> _mapsChangedEvent = new AsyncEvent<Func<IEnumerable<Map>, Task>>();

        //public event Func<Map, Task> MapChanged
        //{
        //    add => _mapChangedEvent.Add(value);
        //    remove => _mapChangedEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Map, Task>> _mapChangedEvent = new AsyncEvent<Func<Map, Task>>();

        //public event Func<Message, Task> ChatMessage
        //{
        //    add => _chatMessageEvent.Add(value);
        //    remove => _chatMessageEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Message, Task>> _chatMessageEvent = new AsyncEvent<Func<Message, Task>>();

        //public event Func<Player, Task> PlayerJoin
        //{
        //    add => _playerJoinEvent.Add(value);
        //    remove => _playerJoinEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Task>> _playerJoinEvent = new AsyncEvent<Func<Player, Task>>();

        //public event Func<Player, Task> PlayerLeft
        //{
        //    add => _playerLeftEvent.Add(value);
        //    remove => _playerLeftEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Task>> _playerLeftEvent = new AsyncEvent<Func<Player, Task>>();

        //public event Func<Player, Position, Rotation, Task> PlayerSpawn
        //{
        //    add => _playerSpawnEvent.Add(value);
        //    remove => _playerSpawnEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Position, Rotation, Task>> _playerSpawnEvent = new AsyncEvent<Func<Player, Position, Rotation, Task>>();

        //public event Func<Player, Position, Player, Position, string, Task> PlayerKill
        //{
        //    add => _playerKillEvent.Add(value);
        //    remove => _playerKillEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Position, Player, Position, string, Task>> _playerKillEvent = new AsyncEvent<Func<Player, Position, Player, Position, string, Task>>();

        //public event Func<Player, Position, bool, Task> PlayerDeath
        //{
        //    add => _playerDeathEvent.Add(value);
        //    remove => _playerDeathEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Position, bool, Task>> _playerDeathEvent = new AsyncEvent<Func<Player, Position, bool, Task>>();

        //public event Func<Player, Vehicle, Task> PlayerVehicle
        //{
        //    add => _playerVehicleEvent.Add(value);
        //    remove => _playerVehicleEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Vehicle, Task>> _playerVehicleEvent = new AsyncEvent<Func<Player, Vehicle, Task>>();

        //public event Func<Player, Team, Task> PlayerTeam
        //{
        //    add => _playerTeamEvent.Add(value);
        //    remove => _playerTeamEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Team, Task>> _playerTeamEvent = new AsyncEvent<Func<Player, Team, Task>>();

        //public event Func<Player, int, int, int, int, Task> PlayerScore
        //{
        //    add => _playerScoreEvent.Add(value);
        //    remove => _playerScoreEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, int, int, int, int, Task>> _playerScoreEvent = new AsyncEvent<Func<Player, int, int, int, int, Task>>();

        //public event Func<Player, Position, Rotation, int, Task> PlayerPosition
        //{
        //    add => _playerPositionEvent.Add(value);
        //    remove => _playerPositionEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Player, Position, Rotation, int, Task>> _playerPositionEvent = new AsyncEvent<Func<Player, Position, Rotation, int, Task>>();

        //public event Func<Projectile, Position, Rotation, Task> ProjectilePosition
        //{
        //    add => _projectilePositionEvent.Add(value);
        //    remove => _projectilePositionEvent.Remove(value);
        //}
        //private readonly AsyncEvent<Func<Projectile, Position, Rotation, Task>> _projectilePositionEvent = new AsyncEvent<Func<Projectile, Position, Rotation, Task>>();


        private bool _enablePositionUpdates = true;
        private readonly SemaphoreSlim _modManagerLock = new(1);

        private GameServer(IPAddress ipAddress, IGameWriter writer, ServerInfo serverInfo)
        {
            IpAddress = ipAddress;
            _gameWriter = writer;
            ServerInfo = serverInfo;

            //ServerUpdate += async (name, gamePort, queryPort, maxPlayers) =>
            //{
            //    // TODO: don't run this here since errors are not logged/handled, and remove after running once so it doesn't create new ModManagers
            //    // Server has the correct id now, so we can load player/server settings and setup mods
            //    try
            //    {
            //        // Avoid creating multiple modmanagers if called at the same time
            //        await _modManagerLock.WaitAsync();
            //        try
            //        {
            //            if (ModManager == null)
            //            {
            //                ModManager = await Server.ModManager.CreateAsync(this);
            //                await _socketStateChangedEvent.InvokeAsync(SocketState).ConfigureAwait(false);
            //            }
            //        }
            //        finally
            //        {
            //            _modManagerLock.Release();
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Log.Error(e, "ModManager creation failed on ServerUpdate");
            //    }
            //};
        }

        public static async Task<GameServer> CreateAsync(IPAddress ipAddress, IGameWriter writer, ServerInfo serverInfo)
        {
            // TODO: Pause everything until we receive data from the server (we don't know the gameport before so we cant tell serverId)
            var gameServer = new GameServer(ipAddress, writer, serverInfo);
            //gameServer.ModManager = await Server.ModManager.CreateAsync(gameServer);
            await gameServer.UpdateSocketStateAsync(SocketState.Connected);

            // Init kill command, fast pad repairs, heli startup
            gameServer.GameWriter.SendRcon(RconScript.InitServer);
            return gameServer;
        }

        public async ValueTask SetReconnectedAsync(IGameWriter gameWriter)
        {
            StartTime = DateTime.UtcNow;

            // Clear old data when reconnecting, otherwise we might get duplicates since the server resends player connections
            _maps.Clear();
            _players.Clear();
            _vehicles.Clear();
            _projectiles.Clear();
            _teams.Clear();

            _gameWriter = gameWriter;
            await UpdateSocketStateAsync(SocketState.Connected);

            // Init kill command, fast pad repairs, heli startup
            GameWriter.SendRcon(RconScript.InitServer);
        }

        public async ValueTask UpdateServerInfoAsync(string name, int gamePort, int queryPort, int maxPlayers)
        {
            Name = name;
            GamePort = gamePort;
            QueryPort = queryPort;
            MaxPlayers = maxPlayers;
            //return _serverUpdateEvent.InvokeAsync(name, gamePort, queryPort, maxPlayers);
            await CreateModManagerAsync();
            await ModManager.Mediator.PublishAsync(new ServerUpdateEvent(name, gamePort, queryPort, maxPlayers, DateTimeOffset.UtcNow));
        }

        //ServerUpdate += async(name, gamePort, queryPort, maxPlayers) =>
        private async ValueTask CreateModManagerAsync()
        {
            // TODO: don't run this here since errors are not logged/handled, and remove after running once so it doesn't create new ModManagers
            // Server has the correct id now, so we can load player/server settings and setup mods
            try
            {
                // Avoid creating multiple modmanagers if called at the same time
                await _modManagerLock.WaitAsync();
                try
                {
                    if (ModManager == null)
                    {
                        ModManager = await Server.ModManager.CreateAsync(this);
                        //await _socketStateChangedEvent.InvokeAsync(SocketState).ConfigureAwait(false);
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
                Log.Error(e, "ModManager creation failed on ServerUpdate");
            }
        }

        private ValueTask PublishEventAsync(IEvent e)
        {
            return ModManager?.Mediator?.PublishAsync(e) ?? ValueTask.CompletedTask;
        }

        public ValueTask UpdateGameStateAsync(GameState state)
        {
            State = state;
            //return _gameStateChangedEvent.InvokeAsync(state);
            return PublishEventAsync(new GameStateChangedEvent(state, DateTimeOffset.UtcNow));
        }

        public ValueTask UpdateSocketStateAsync(SocketState state)
        {
            // TODO: Stop all running tasks/commands when disconnecting - wait for reconnection
            if (state != SocketState)
            {
                SocketState = state;
                //return _socketStateChangedEvent.InvokeAsync(state);
                return PublishEventAsync(new SocketStateChangedEvent(state, DateTimeOffset.UtcNow));
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateMapsAsync(IList<string> maps)
        {
            _maps.Clear();

            for (var i = 0; i < maps.Count; i++)
            {
                _maps.Add(new Map
                {
                    Index = i,
                    Name = maps[i],
                    Size = 0 // TODO: split
                });
            }

            //return _mapsChangedEvent.InvokeAsync(Maps);
            return PublishEventAsync(new MapsChangedEvent(Maps, DateTimeOffset.UtcNow));
        }

        public ValueTask UpdateMapAsync(string mapName, string team1Name, string team2Name)
        {
            Map = _maps.FirstOrDefault(m => m.Name == mapName);

            _teams.Clear();
            _teams.Add(new Team { Id = 1, Name = team1Name });
            _teams.Add(new Team { Id = 2, Name = team2Name });

            // Some settings get cleared when we change map so we need to do it again
            GameWriter.SendRcon(RconScript.InitServer);

            //return _mapChangedEvent.InvokeAsync(Map);
            return PublishEventAsync(new MapChangedEvent(Map, DateTimeOffset.UtcNow));
        }

        public ValueTask AddPlayerAsync(Player player)
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
            //return _playerJoinEvent.InvokeAsync(player);
            return PublishEventAsync(new PlayerJoinEvent(player, DateTimeOffset.UtcNow));
        }

        public ValueTask UpdatePlayerAsync(Player player, Position position, Rotation rotation, int ping)
        {
            player.Position = position;
            player.Rotation = rotation;

            if (!player.PingHistory.ContainsKey(ping))
                player.PingHistory.Add(ping, 0);

            player.PingHistory[ping]++;
            player.Score.Ping = ping;

            if (_enablePositionUpdates)
            {
                //return _playerPositionEvent.InvokeAsync(player, position, rotation, ping);
                return PublishEventAsync(new PlayerPositionEvent(player, position, rotation, ping, DateTimeOffset.UtcNow));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask UpdatePlayerVehicleAsync(Player player, Vehicle vehicle, string subVehicleTemplate)
        {
            if (player.Vehicle != null)
            {
                player.PreviousVehicle = player.Vehicle;
                player.PreviousVehicle.Players.Remove(player);
            }

            //var tasks = new List<Task>(2);

            if (!player.IsAlive)
            {
                // Spawning directly in vehicle doesn't send a PlayerSpawn event
                player.IsAlive = true;
                //tasks.Add(_playerSpawnEvent.InvokeAsync(player, player.Position ?? new Position(0, 0, 0), player.Rotation ?? new Rotation(0, 0, 0)));
                await PublishEventAsync(new PlayerSpawnEvent(player, player.Position ?? new Position(0, 0, 0), player.Rotation ?? new Rotation(0, 0, 0), DateTimeOffset.UtcNow));
            }

            player.Vehicle = vehicle;
            player.SubVehicleTemplate = subVehicleTemplate;
            //tasks.Add(_playerVehicleEvent.InvokeAsync(player, vehicle));
            await PublishEventAsync(new PlayerVehicleEvent(player, vehicle, DateTimeOffset.UtcNow));

            //return Task.WhenAll(tasks);
        }

        public ValueTask UpdatePlayerTeamAsync(Player player, int teamId)
        {
            player.Team = _teams.First(t => t.Id == teamId);
            //return _playerTeamEvent.InvokeAsync(player, player.Team);
            return PublishEventAsync(new PlayerTeamEvent(player, player.Team, DateTimeOffset.UtcNow));
        }

        public ValueTask UpdatePlayerScoreAsync(Player player, int teamScore, int kills, int deaths, int totalScore)
        {
            //if (player.Score == null)
            //{
            //    player.Score = new Score();
            //}

            player.Score.Team = teamScore;
            player.Score.Kills = kills;
            player.Score.Deaths = deaths;
            player.Score.Total = totalScore;

            //return _playerScoreEvent.InvokeAsync(player, teamScore, kills, deaths, totalScore);
            return PublishEventAsync(new PlayerScoreEvent(player, teamScore, kills, deaths, totalScore, DateTimeOffset.UtcNow));
        }

        public ValueTask UpdateProjectileAsync(Projectile projectile, Position position, Rotation rotation)
        {
            projectile.Position = position;
            projectile.Rotation = rotation;

            if (_enablePositionUpdates)
            {
                //return _projectilePositionEvent.InvokeAsync(projectile, position, rotation);
                return PublishEventAsync(new ProjectilePositionEvent(projectile, position, rotation, DateTimeOffset.UtcNow));
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(Player player)
        {
            _players.Remove(player);
            //return _playerLeftEvent.InvokeAsync(player);
            return PublishEventAsync(new PlayerLeftEvent(player, DateTimeOffset.UtcNow));
        }

        public ValueTask SetPlayerSpawnAsync(Player player, Position position, Rotation rotation)
        {
            player.IsAlive = true;
            player.Position = position;
            player.Rotation = rotation;

            //return _playerSpawnEvent.InvokeAsync(player, position, rotation);
            return PublishEventAsync(new PlayerSpawnEvent(player, position, rotation, DateTimeOffset.UtcNow));
        }

        public ValueTask SetPlayerKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
        {
            victim.IsAlive = false;
            //return _playerKillEvent.InvokeAsync(attacker, attackerPosition, victim, victimPosition, weapon);
            return PublishEventAsync(new PlayerKillEvent(attacker, attackerPosition, victim, victimPosition, weapon, DateTimeOffset.UtcNow));
        }

        public ValueTask SetPlayerDeathAsync(Player player, Position position)
        {
            // TODO: it always thinks it's suicide - which order does kill/death events come?
            var isSuicide = player.IsAlive;
            player.IsAlive = false;
            //return _playerDeathEvent.InvokeAsync(player, position, isSuicide);
            return PublishEventAsync(new PlayerDeathEvent(player, position, isSuicide, DateTimeOffset.UtcNow));
        }

        public async ValueTask AddMessageAsync(Message message)
        {
            _chatBuffer.Add(message);
            //return _chatMessageEvent.InvokeAsync(message);
            await ModManager.HandleChatMessageAsync(message);
            await PublishEventAsync(new ChatMessageEvent(message, DateTimeOffset.UtcNow));
        }

        public void SetRconResponse(string responseCode, string value)
        {
            //RconResponse?.Invoke(message);
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

        public Player GetPlayer(int index)
        {
            // This can be null sometimes when connecting / reconnected player?
            return _players.FirstOrDefault(x => x.Index == index) ?? new Player();
            //return new Player();
        }

        public Player GetPlayer(string namePart)
        {
            return _players.FirstOrDefault(p => p.Name.ToLower().Contains(namePart));
        }

        public Projectile GetProjectile(int id, string templateName, Position position)
        {
            var projectile = _projectiles.FirstOrDefault(p => p.Id == id);
            if (projectile == null)
            {
                var closestGunner = _players
                    .Where(p => p.Vehicle?.Template?.ToLower().Contains("ahe_") ?? false)
                    .OrderBy(p => p.Position?.Distance(position) ?? double.MaxValue)
                    .FirstOrDefault();

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

        private class CircularBuffer<T>
        {
            private readonly T[] _buffer;
            private int _nextFree;

            public CircularBuffer(int length)
            {
                _buffer = new T[length];
                _nextFree = 0;
            }

            public void Add(T item)
            {
                _buffer[_nextFree] = item;
                _nextFree = (_nextFree + 1) % _buffer.Length;
            }

            public IEnumerable<T> Get()
            {
                return _buffer.ToList();
            }
        }
    }
}
