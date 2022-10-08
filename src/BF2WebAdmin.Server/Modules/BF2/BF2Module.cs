using System.Diagnostics;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Logging;
using BF2WebAdmin.Shared;
using Serilog;

namespace BF2WebAdmin.Server.Modules.BF2;

public class BF2Module : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<PlayerJoinEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerSpawnEvent>,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleCommand<TimeCommand>,
    IHandleCommand<RankCommand>,
    IHandleCommand<FlipCommand>,
    IHandleCommand<StopCommand>,
    IHandleCommand<RepairCommand>,
    IHandleCommand<KillCommand>,
    IHandleCommand<KillIdCommand>,
    IHandleCommand<ScoreCommand>,
    IHandleCommand<ScoreResetCommand>,
    IHandleCommand<RepairAllCommand>,
    IHandleCommand<TeleportLocationCommand>,
    IHandleCommand<TeleportCoordinatesCommand>,
    IHandleCommand<GravityDefaultCommand>,
    IHandleCommand<GravityCommand>,
    IHandleCommand<TimerIntervalCommand>,
    IHandleCommand<PositionCommand>,
    IHandleCommand<PositionSelfCommand>,
    IHandleCommand<PingCommand>,
    IHandleCommand<PingSelfCommand>,
    IHandleCommand<ScriptModCommand>,
    IHandleCommandAsync<CrashCommand>,
    IHandleCommandAsync<DanceCommand>,
    IHandleCommandAsync<DanceAllCommand>,
    IHandleCommandAsync<FreezeCommand>,
    IHandleCommandAsync<FreezeAllCommand>,
    IHandleCommandAsync<BlurCommand>,
    IHandleCommandAsync<SetAuthCommand>,
    IHandleCommandAsync<GetAuthCommand>,
    IHandleCommand<SwitchCommand>,
    IHandleCommand<SwitchIdCommand>,
    IHandleCommand<SwitchAllCommand>
{
    private readonly IGameServer _gameServer;
    private readonly ICountryResolver _countryResolver;
    private readonly IServerSettingsRepository _serverSettingsRepository;
    private readonly IChatLogger _chatLogger;
    private readonly Random _random;
    private readonly IDictionary<int, bool> _newPlayers = new Dictionary<int, bool>();
    private const int DefaultDelay = 25; // 1000/35 fps ~ 28.5
    private int _stopCounter;
    private Timer _heartbeatTimer;
    private bool _sendHeartbeat = false;

    // TODO: does this need to be static for all servers to get it?
    public event Action<string, string> ServerGroupMessage;

    public BF2Module(IGameServer server, ICountryResolver countryResolver, IServerSettingsRepository serverSettingsRepository, IChatLogger chatLogger, CancellationTokenSource cts) : base(server, cts)
    {
        _gameServer = server;
        _countryResolver = countryResolver;
        _serverSettingsRepository = serverSettingsRepository;
        _chatLogger = chatLogger;
        _random = new Random();
        _stopCounter = 0;

        ServerGroupMessage += (serverId, message) =>
        {
            // TODO: limit to the same group?
            if (serverId == _gameServer.Id)
                return;

            _gameServer.GameWriter.SendText(message);
        };
    }

    public void Handle(TimeCommand command)
    {
        var offsetHours = TimeZoneInfo.Local.BaseUtcOffset.Hours;
        var timeZone = offsetHours == 0 ? "UTC" : offsetHours > 0 ? $"UTC +{offsetHours}" : $"UTC -{offsetHours}";
        _gameServer.GameWriter.SendText($"Time: {DateTime.Now:HH:mm} ({timeZone})");
    }

    public void Handle(StopCommand command)
    {
        _stopCounter++;

        Unfreeze(_gameServer.Players.ToArray());
    }

    public void Handle(RankCommand command)
    {
        var validRank = Enum.IsDefined(typeof(Rank), command.Rank);
        if (!validRank)
            return;

        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendRank(player, (Rank)command.Rank, true);
    }

    public void Handle(FlipCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        var newRotation = new Rotation(player.Rotation.Yaw, player.Rotation.Pitch, 180);
        _gameServer.GameWriter.SendRotate(player, newRotation);
    }

    public void Handle(RepairCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendHealth(player, 10000);
    }

    public void Handle(RepairAllCommand command)
    {
        foreach (var player in _gameServer.Players)
        {
            _gameServer.GameWriter.SendHealth(player, 10000);
        }
    }

    public void Handle(KillCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendHealth(player, 1);
    }

    public void Handle(KillIdCommand command)
    {
        var player = _gameServer.GetPlayer(command.PlayerId);
        if (player == null)
            return;

        _gameServer.GameWriter.SendHealth(player, 1);
    }

    public void Handle(ScoreCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendScore(player, command.TotalScore, command.TeamScore, command.Kills, command.Deaths);
    }

    public void Handle(ScoreResetCommand command)
    {
        if (command.Reset.ToLower() != "reset")
            return;

        foreach (var player in _gameServer.Players)
        {
            _gameServer.GameWriter.SendScore(player, 0, 0, 0, 0);
        }
    }

    public void Handle(TeleportLocationCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendTeleport(player, new Position(0, 300, 0));
    }

    public void Handle(TeleportCoordinatesCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        var newPosition = new Position(command.X, command.Altitude, command.Y);
        _gameServer.GameWriter.SendTeleport(player, newPosition);
    }

    public void Handle(GravityDefaultCommand command)
    {
        // TODO: to script file
        _gameServer.GameWriter.SendRcon("physics.gravity -14.73");
        _gameServer.GameWriter.SendText("Gravity: -14.73 (Default)");
    }

    public void Handle(GravityCommand command)
    {
        // TODO: to script file
        _gameServer.GameWriter.SendRcon($"physics.gravity {command.Value}");
        _gameServer.GameWriter.SendText($"Gravity: {command.Value}");
    }

    public void Handle(TimerIntervalCommand command)
    {
        Log.Information("Timer interval set to {interval}", command.Value);
        _gameServer.GameWriter.SendTimerInterval(command.Value);
    }

    public void Handle(PositionCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        _gameServer.GameWriter.SendText($"Position: {player.Position}, Rotation: {player.Rotation}");
    }

    public void Handle(PositionSelfCommand command)
    {
        var player = command.Message.Player;
        _gameServer.GameWriter.SendText($"Position: {player.Position}, Rotation: {player.Rotation}");
    }

    public void Handle(PingCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        PrintPingDeviation(player);
    }

    public void Handle(PingSelfCommand command)
    {
        var player = command.Message.Player;
        PrintPingDeviation(player);
    }

    private void PrintPingDeviation(Player player)
    {
        var count = player.PingHistory.Sum(entry => entry.Value);

        // Average
        var averagePing = player.PingHistory.Sum(entry => entry.Key * entry.Value) / count;

        // Median
        var medianPing = player.Score.Ping;
        var temp = 0;
        foreach (var entry in player.PingHistory)
        {
            temp += entry.Value;
            if (temp >= count)
            {
                medianPing = entry.Key;
                break;
            }
        }

        _gameServer.GameWriter.SendText($"{player.DisplayName} ping: avg {averagePing}, median {medianPing}, jitter +/- ?");
    }

    public void Handle(ScriptModCommand command)
    {
        string[] commands;
        var activate = command.Value.ToLower() == "on";
        switch (command.Name)
        {
            case "sniper-arty":
                commands = activate ? RconScript.SniperArtilleryOn : RconScript.SniperArtilleryOff;
                break;

            case "hydra-arty":
                commands = activate ? RconScript.HydraArtilleryOn : RconScript.HydraArtilleryOff;
                break;

            case "less-bs-dmg":
                commands = activate ? RconScript.HeliLessBsDamageOn : RconScript.HeliLessBsDamageOff;
                break;

            case "flare-smoke":
                commands = activate ? RconScript.HeliSmokeFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-ah1z":
                commands = activate ? RconScript.HeliAh1zFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-z10":
                commands = activate ? RconScript.HeliZ10FlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-arty":
                commands = activate ? RconScript.HeliArtyFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-supply":
                commands = activate ? RconScript.HeliSupplyFlaresOn : RconScript.HeliFlareDefault;
                break;

            default:
                _gameServer.GameWriter.SendText($"Unknown command: {command.Name}");
                return;
        }

        _gameServer.GameWriter.SendText($"OK");
        _gameServer.GameWriter.SendRcon(commands);
    }

    public async ValueTask HandleAsync(DanceCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        await DanceAsync(5000, player);
    }

    public async ValueTask HandleAsync(DanceAllCommand command)
    {
        await DanceAsync(5000, _gameServer.Players.ToArray());
    }

    private async Task DanceAsync(long duration, params Player[] players)
    {
        var snapshots = players.Select(PlayerSnapshot.Save).ToList();
        var startTime = DateTime.UtcNow;
        var startCount = _stopCounter;
        while (IsActive(duration, startTime, startCount))
        {
            foreach (var playerSnapshot in snapshots)
            {
                var newRotation = new Rotation(_random.Next(-180, 180), _random.Next(-180, 180), _random.Next(-180, 180));
                _gameServer.GameWriter.SendTeleport(playerSnapshot.Player, playerSnapshot.Position);
                _gameServer.GameWriter.SendRotate(playerSnapshot.Player, newRotation);
            }
            await Task.Delay(DefaultDelay);
        }

        foreach (var playerSnapshot in snapshots)
        {
            _gameServer.GameWriter.SendTeleport(playerSnapshot.Player, playerSnapshot.Position);
            _gameServer.GameWriter.SendRotate(playerSnapshot.Player, new Rotation(playerSnapshot.Rotation.Yaw, 0, 0));
        }
    }

    public async ValueTask HandleAsync(FreezeCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        await FreezeAsync(1000 * 60 * 60, player);
    }

    public async ValueTask HandleAsync(FreezeAllCommand command)
    {
        await FreezeAsync(1000 * 60 * 60, _gameServer.Players.ToArray());
    }

    private async Task FreezeAsync(int duration, params Player[] players)
    {
        foreach (var player in players)
        {
            if (player.Vehicle == null)
                continue;

            // TODO: to script file
            var objectId = player.Vehicle.RootVehicleId;
            _gameServer.GameWriter.SendRcon(
                $"object.active id{objectId}",
                $"object.setIsDisabledRecursive 1"
            );
            player.Vehicle.IsDisabled = true;
        }

        await Task.Delay(duration);

        Unfreeze(players);
    }

    private void Unfreeze(params Player[] players)
    {
        foreach (var player in players)
        {
            var vehicle = player.Vehicle ?? player.PreviousVehicle;
            if (vehicle == null)
                continue;

            // TODO: to script file
            var objectId = vehicle.RootVehicleId;
            _gameServer.GameWriter.SendRcon(
                $"object.active id{objectId}",
                $"object.setIsDisabledRecursive 0"
            );
            vehicle.IsDisabled = false;
        }
    }

    public async ValueTask HandleAsync(BlurCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        await BlurAsync(2000, player);
    }

    private async Task BlurAsync(long duration, params Player[] players)
    {
        var startTime = DateTime.UtcNow;
        var startCount = _stopCounter;
        while (IsActive(duration, startTime, startCount))
        {
            foreach (var player in players)
            {
                _gameServer.GameWriter.SendGameEvent(player, 13, 1);
            }
            await Task.Delay(DefaultDelay);
        }
    }

    private bool IsActive(long duration, DateTime startTime, int startCount)
    {
        return (DateTime.UtcNow - startTime).TotalMilliseconds < duration && startCount == _stopCounter;
    }

    public async ValueTask HandleAsync(CrashCommand command)
    {
        var replacements = new Dictionary<string, string>
        {
            {"{TEMPLATE}", "air_j10"},
            {"{POSITION}", "0/0/0"},
            {"{ROTATION}", "0/0/0"}
        };

        _gameServer.GameWriter.SendText($"{command.Message.Player.DisplayName} is crashing the server with no survivors");
        await Task.Delay(TimeSpan.FromSeconds(1));
        _gameServer.GameWriter.SendText("3", false);
        await Task.Delay(TimeSpan.FromSeconds(1));
        _gameServer.GameWriter.SendText("2", false);
        await Task.Delay(TimeSpan.FromSeconds(1));
        _gameServer.GameWriter.SendText("1", false);

        var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
        _gameServer.GameWriter.SendRcon(script.ToArray());
    }

    public async ValueTask HandleAsync(GetAuthCommand command)
    {
        if (command.Name.Contains(" "))
            return;
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
        {
            _gameServer.GameWriter.SendText("No matching player found");
            return;
        }

        var playerAuth = _gameServer.ModManager.AuthPlayers[player.Hash].FirstOrDefault();
        if (playerAuth != null)
        {
            _gameServer.GameWriter.SendText($"Player {command.Name} auth level {playerAuth.AuthLevel}");
        }
        else
        {
            _gameServer.GameWriter.SendText($"No auth level set for {player.DisplayName}");
        }
    }

    public async ValueTask HandleAsync(SetAuthCommand command)
    {
        var player = command.Message.Player;
        var target = _gameServer.GetPlayer(command.Name);
        if (target == null)
        {
            _gameServer.GameWriter.SendText("Player not found");
            return;
        }

        var playerAuthLevel = _gameServer.ModManager.AuthPlayers[player.Hash].FirstOrDefault()?.AuthLevel ?? 0;
        var targetAuthLevel = _gameServer.ModManager.AuthPlayers[target.Hash].FirstOrDefault()?.AuthLevel ?? 0;
        if (playerAuthLevel <= command.Level || playerAuthLevel <= targetAuthLevel || player.Index == target.Index)
        {
            _gameServer.GameWriter.SendText("Insufficient permissions");
            return;
        }

        await _serverSettingsRepository.SetPlayerAuthAsync(_gameServer.ModManager.ServerSettings.ServerGroup, target.Hash, command.Level);
        await _gameServer.ModManager.GetAuthPlayersAsync();
        _gameServer.GameWriter.SendText($"Player {command.Name} auth level set to {command.Level}");
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        if (_gameServer.ServerInfo == null)
        {
            Log.Warning("Couldn't update admins - ServerInfo missing");
            return;
        }

        var rconClient = new RconClient(_gameServer.ConnectedIpAddress, _gameServer.ServerInfo.RconPort, _gameServer.ServerInfo.RconPassword);

        // Delete existing admins and add new admins from DB
        var ingameAdminResponse = await rconClient.SendAsync("iga listAdmins");
        var ingameAdmins = ingameAdminResponse.Split("\n").Where(l => l.Length > 40).Select(l => l.Substring(7, 32));
        var admins = _gameServer.ModManager.AuthPlayers.SelectMany(p => p.ToList());
        
        var commands = new List<string>();
        commands.AddRange(ingameAdmins.Select(a => $"iga delAdmin {a}"));
        commands.AddRange(admins.Select(a => $"iga addAdmin {a.PlayerHash} all"));
        var response = await rconClient.SendAsync(commands);
    }

    public async ValueTask HandleAsync(PlayerJoinEvent e)
    {
        _newPlayers.TryAdd(e.Player.Index, true);
        var countryResponse = _countryResolver.GetCountryResponse(e.Player.IpAddress.ToString());
        e.Player.Country.Code = countryResponse?.Country?.IsoCode;
        e.Player.Country.Name = countryResponse?.Country?.Name;

        // No new messages when the server is reconnecting
        if (DateTime.UtcNow - _gameServer.StartTime > TimeSpan.FromMinutes(1))
        {
            await _chatLogger.SendAsync(
                $"`{e.Player.DisplayName} ({e.Player.Country.Code}) joined {_gameServer.Name} ({_gameServer.Players.Count()}/{_gameServer.MaxPlayers})`",
                _gameServer.ModManager.ServerSettings?.ServerGroup,
                "player"
            );

            ServerGroupMessage?.Invoke(_gameServer.Id, $"{e.Player.DisplayName} ({e.Player.Country.Code}) joined {_gameServer.Name} ({_gameServer.Players.Count()}/{_gameServer.MaxPlayers})");
        }
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _newPlayers.Remove(e.Player.Index);
        await _chatLogger.SendAsync(
            $"`{e.Player.DisplayName} left {_gameServer.Name} ({_gameServer.Players.Count()}/{_gameServer.MaxPlayers})`",
            _gameServer.ModManager.ServerSettings?.ServerGroup,
            "player"
        );
    }

    public ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (!_newPlayers.ContainsKey(e.Player.Index))
            return ValueTask.CompletedTask;

        // TODO: send join message to all servers in group ([netsky] krische joined NeTskY [DE] 2v2?)
        _newPlayers.Remove(e.Player.Index);

        // No new messages when the server is reconnecting
        if (DateTime.UtcNow - _gameServer.StartTime > TimeSpan.FromMinutes(1))
        {
            _gameServer.GameWriter.SendText($"{e.Player.DisplayName} ({e.Player.Country.Code}) joined");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        var icon = e.SocketState == SocketState.Connected ? ":green_square:" : ":red_square:";
        await _chatLogger.SendAsync($"{icon} `{_gameServer.Name} => {e.SocketState} ({_gameServer.Id})`", _gameServer.ModManager.ServerSettings?.ServerGroup, "status");

        if (_sendHeartbeat)
        {
            if (e.SocketState == SocketState.Connected)
            {
                _heartbeatTimer = new Timer(o =>
                {
                    Log.Debug("Heartbeat");
                    _gameServer.GameWriter.SendHeartbeat();
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(25));
            }
            else
            {
                await _heartbeatTimer.DisposeAsync();
                _heartbeatTimer = null;
            }
        };
    }
        
    public void Handle(SwitchCommand command)
    {
        var player = _gameServer.GetPlayer(command.Name);
        if (player == null)
            return;

        SwitchPlayer(player);
    }

    public void Handle(SwitchIdCommand command)
    {
        var player = _gameServer.GetPlayer(command.PlayerId);
        if (player == null)
            return;

        SwitchPlayer(player);
    }

    public void Handle(SwitchAllCommand command)
    {
        SwitchAll();
    }


}

public class PlayerSnapshot
{
    public Player Player { get; }
    public Position Position { get; }
    public Rotation Rotation { get; }

    private PlayerSnapshot(Player player)
    {
        Player = player;
        Position = Player.Position;
        Rotation = Player.Rotation;
    }

    public static PlayerSnapshot Save(Player player)
    {
        return new PlayerSnapshot(player);
    }
}

public class Profiler : IDisposable
{
    private readonly string _name;
    private readonly Stopwatch _stopwatch;

    private Profiler(string name)
    {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    public static Profiler Start(string name)
    {
        return new Profiler(name);
    }

    public void Dispose()
    {
        Log.Information("{Name} took {ElapsedMs} ms", _name, _stopwatch.ElapsedMilliseconds);
        _stopwatch.Stop();
    }
}