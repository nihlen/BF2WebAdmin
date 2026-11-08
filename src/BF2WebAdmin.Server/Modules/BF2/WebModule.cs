using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Api;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Shared.Communication.DTOs;
using BF2WebAdmin.Shared.Communication.Events;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.Modules.BF2;

public class WebModule : BaseModule,
    IHandleEventAsync<ServerUpdateEvent>,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleEventAsync<ChatMessageEvent>,
    IHandleEventAsync<PlayerJoinEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleEventAsync<PlayerDeathEvent>,
    IHandleEventAsync<PlayerSpawnEvent>,
    IHandleEventAsync<PlayerTeamEvent>,
    IHandleEventAsync<PlayerScoreEvent>,
    IHandleEventAsync<PlayerVehicleEvent>,
    IHandleEventAsync<GameStateChangedEvent>,
    IHandleEventAsync<ProjectilePositionEvent>,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleCommandAsync<StartEventRecordingCommand>,
    IHandleCommandAsync<StopEventRecordingCommand>
{
    public const string WebAdminHashGod = "WebAdminHashGod";

    private readonly IGameServer _gameServer;
    private readonly IHubContext<ServerHub, IServerHubClient> _serverHub;
    private readonly IServerSettingsRepository _serverSettingsRepository;

    private IServerHubClient ClientsAll => _serverHub.Clients.All;
    private IServerHubClient ClientsGroup => _serverHub.Clients.Group(_gameServer.Id);

    public WebModule(IGameServer server, IHubContext<ServerHub, IServerHubClient> serverHub, IServerSettingsRepository serverSettingsRepository, ILogger<WebModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameServer = server;
        _serverHub = serverHub;
        _serverSettingsRepository = serverSettingsRepository;
    }

    public async ValueTask HandleAsync(ServerUpdateEvent e)
    {
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));
    }

    public async ValueTask HandleAsync(PlayerPositionEvent e)
    {
        await ClientsGroup.PlayerPositionEvent(new Shared.Communication.Events.PlayerPositionEvent
        {
            PlayerId = e.Player.Index,
            Position = e.Position.ToDto(),
            Rotation = e.Rotation.ToDto(),
            Ping = e.Ping,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        await ClientsGroup.ChatEvent(new ChatEvent
        {
            Message = e.Message.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerJoinEvent e)
    {
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));

        await ClientsGroup.PlayerJoinEvent(new Shared.Communication.Events.PlayerJoinEvent
        {
            Player = e.Player.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));

        await ClientsGroup.PlayerLeftEvent(new Shared.Communication.Events.PlayerLeftEvent
        {
            PlayerId = e.Player.Index,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerKillEvent e)
    {
        await ClientsGroup.PlayerKillEvent(new Shared.Communication.Events.PlayerKillEvent
        {
            AttackerId = e.Attacker.Index,
            AttackerPosition = e.AttackerPosition.ToDto(),
            VictimId = e.Victim.Index,
            VictimPosition = e.VictimPosition.ToDto(),
            Weapon = e.Weapon,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerDeathEvent e)
    {
        await ClientsGroup.PlayerDeathEvent(new Shared.Communication.Events.PlayerDeathEvent
        {
            PlayerId = e.Player.Index,
            Position = e.Position.ToDto(),
            //IsSuicide = isSuicide // TODO: add property
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        await ClientsGroup.PlayerSpawnEvent(new Shared.Communication.Events.PlayerSpawnEvent
        {
            PlayerId = e.Player.Index,
            Position = e.Position.ToDto(),
            Rotation = e.Rotation.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerTeamEvent e)
    {
        await ClientsGroup.PlayerTeamEvent(new Shared.Communication.Events.PlayerTeamEvent
        {
            PlayerId = e.Player.Index,
            TeamId = e.Team.Id,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerScoreEvent e)
    {
        await ClientsGroup.PlayerScoreEvent(new Shared.Communication.Events.PlayerScoreEvent
        {
            PlayerId = e.Player.Index,
            TeamScore = e.TeamScore,
            Kills = e.Kills,
            Deaths = e.Deaths,
            TotalScore = e.TotalScore,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerVehicleEvent e)
    {
        await ClientsGroup.PlayerVehicleEvent(new Shared.Communication.Events.PlayerVehicleEvent
        {
            PlayerId = e.Player.Index,
            Vehicle = e.Vehicle.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(GameStateChangedEvent e)
    {
        // The server list shows the game state too, and the web UI confirms a pause by it
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));

        await ClientsGroup.GameStateEvent(new GameStateEvent
        {
            State = e.GameState,
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(ProjectilePositionEvent e)
    {
        await ClientsGroup.ProjectilePositionEvent(new Shared.Communication.Events.ProjectilePositionEvent
        {
            ProjectileId = e.Projectile.Id,
            Template = e.Projectile.Template,
            Position = e.Position.ToDto(),
            Rotation = e.Rotation.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));

        await ClientsGroup.MapChangeEvent(new MapChangeEvent
        {
            Map = e.Map?.Name ?? "?",
            Size = e.Map?.Size ?? 0,
            Index = e.Map?.Index ?? 0,
            Teams = e.Teams.Select(t => t.ToDto()),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        await ClientsAll.ServerUpdateEvent(_gameServer.ToServerUpdate(e.TimeStamp));

        await ClientsGroup.SocketStateEvent(new SocketStateEvent
        {
            State = e.SocketState,
            TimeStamp = e.TimeStamp
        });
    }

    /// <summary>A message from the web chat box: "." runs a custom command, "!" an rcon command (its response is returned), anything else is said in game.</summary>
    public async ValueTask<string?> HandleAdminChatAsync(string userId, string message)
    {
        if (message.StartsWith("."))
        {
            var output = await HandleCustomCommandAsync(userId, message);
            return output.Count > 0 ? string.Join('\n', output) : null;
        }

        if (message.StartsWith("!"))
            return await HandleRconCommandAsync(message);

        GameServer.GameWriter.SendText($"[§C1001{userId}§C1001] {message}", false, true);
        return null;
    }

    /// <summary>Runs an rcon command ("!" shortcuts such as "!k" are expanded) and returns its response with IP addresses removed.</summary>
    public async ValueTask<string> HandleRconCommandAsync(string message)
    {
        var command = GetRconCommand(message);
        var response = await SendRconCommandAsync(command);
        return GetObfuscatedResponse(response);
    }

    /// <summary>
    /// Runs a "." chat command as a God-level web user and returns what it said in game while it ran
    /// (<see cref="GameTextCapture"/>), or "Unknown command".
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> HandleCustomCommandAsync(string userId, string message)
    {
        var alias = message.TrimStart('.').Split(' ', 2)[0];
        if (!GameServer.ModManager.IsCommand(alias))
            return [$"Unknown command .{alias}"];

        var capture = GameTextCapture.Begin();
        try
        {
            await RunCustomCommandAsync(userId, message);
        }
        finally
        {
            GameTextCapture.End();
        }

        return capture.Lines;
    }

    private async ValueTask RunCustomCommandAsync(string userId, string message)
    {
        await GameServer.ModManager.HandleFakeChatMessageAsync(new Message
        {
            Channel = ChatChannel.Global,
            Type = MessageType.Player,
            Player = new Player
            {
                Index = -1,
                Name = userId,
                Hash = WebAdminHashGod
            },
            Text = message
        });
    }

    public async ValueTask HandleAsync(StartEventRecordingCommand command)
    {
        // Save log files in /app/Data
        var path = Path.Combine("./Data/", $"gameevents-{GameServer.Id.Replace(".", "-").Replace(":", "-")}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.txt");
        GameServer.StartRecording(path);
    }

    public async ValueTask HandleAsync(StopEventRecordingCommand command)
    {
        GameServer.StopRecording();
    }
}
