using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
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
    private IServerHubClient ClientsUser(string userId) => _serverHub.Clients.All; // TODO: fix user messaging - doesn't send anything?

    public WebModule(IGameServer server, IHubContext<ServerHub, IServerHubClient> serverHub, IServerSettingsRepository serverSettingsRepository, ILogger<WebModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameServer = server;
        _serverHub = serverHub;
        _serverSettingsRepository = serverSettingsRepository;
    }

    public async Task SendServerInfo(string userId, bool fullInfo)
    {
        // Send server info to new user
        await ClientsUser(userId).ServerUpdateEvent(
            new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = _gameServer.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = _gameServer.GamePort,
                QueryPort = _gameServer.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = _gameServer.MaxPlayers,
                GameState = _gameServer.State,
                SocketState = _gameServer.SocketState,
                ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup
            }
        );

        if (!fullInfo)
            return;

        await ClientsUser(userId).ServerSnapshotEvent(
            new ServerSnapshotEvent
            {
                Server = new ServerDto
                {
                    Id = _gameServer.Id,
                    Name = _gameServer.Name,
                    IpAddress = _gameServer.IpAddress.ToString(),
                    GamePort = _gameServer.GamePort,
                    QueryPort = _gameServer.QueryPort,
                    Map = _gameServer.Map?.Name,
                    Players = _gameServer.Players.Count(),
                    MaxPlayers = _gameServer.MaxPlayers,
                    GameState = _gameServer.State,
                    SocketState = _gameServer.SocketState,
                    ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup
                },
                Maps = _gameServer.Maps.Select(m => m.ToDto()),
                Teams = _gameServer.Teams.Select(t => t.ToDto()),
                Players = _gameServer.Players.Select(p => p.ToDto()).ToList(),
                EventLog = _gameServer.Events.Select(e => new EventLogDto { Message = e.Message, Timestamp = e.Time }),
                ChatLog = _gameServer.Messages.Select(m => new ChatLogDto { Message = m.Message, Timestamp = m.Time })
            }
        );
    }
        
    public async ValueTask HandleAsync(ServerUpdateEvent e)
    {
        await ClientsGroup.ServerUpdateEvent(new Shared.Communication.Events.ServerUpdateEvent
        {
            Id = _gameServer.Id,
            Name = e.Name,
            IpAddress = _gameServer.IpAddress.ToString(),
            GamePort = e.GamePort,
            QueryPort = e.QueryPort,
            Map = _gameServer.Map?.Name,
            Players = _gameServer.Players.Count(),
            MaxPlayers = e.MaxPlayers,
            GameState = _gameServer.State,
            SocketState = _gameServer.SocketState,
            ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup,
            TimeStamp = e.TimeStamp
        });
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
        await ClientsAll.ServerUpdateEvent(
            new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = _gameServer.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = _gameServer.GamePort,
                QueryPort = _gameServer.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = _gameServer.MaxPlayers,
                GameState = _gameServer.State,
                SocketState = _gameServer.SocketState,
                ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup,
                TimeStamp = e.TimeStamp
            }
        );

        await ClientsGroup.PlayerJoinEvent(new Shared.Communication.Events.PlayerJoinEvent
        {
            Player = e.Player.ToDto(),
            TimeStamp = e.TimeStamp
        });
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        await ClientsAll.ServerUpdateEvent(
            new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = _gameServer.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = _gameServer.GamePort,
                QueryPort = _gameServer.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = _gameServer.MaxPlayers,
                GameState = _gameServer.State,
                SocketState = _gameServer.SocketState,
                ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup,
                TimeStamp = e.TimeStamp
            }
        );

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
        await ClientsAll.ServerUpdateEvent(
            new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = _gameServer.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = _gameServer.GamePort,
                QueryPort = _gameServer.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = _gameServer.MaxPlayers,
                GameState = _gameServer.State,
                SocketState = _gameServer.SocketState,
                ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup,
                TimeStamp = e.TimeStamp
            }
        );

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
        await ClientsAll.ServerUpdateEvent(
            new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = _gameServer.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = _gameServer.GamePort,
                QueryPort = _gameServer.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = _gameServer.MaxPlayers,
                GameState = _gameServer.State,
                SocketState = _gameServer.SocketState,
                ServerGroup = _gameServer.ModManager.ServerSettings.ServerGroup,
                TimeStamp = e.TimeStamp
            }
        );

        await ClientsGroup.SocketStateEvent(new SocketStateEvent
        {
            State = e.SocketState,
            TimeStamp = e.TimeStamp
        });
    }

    public ValueTask HandleAdminChatAsync(string userId, string message)
    {
        if (message.StartsWith("."))
        {
            return HandleCustomCommandAsync(userId, message);
        }

        if (message.StartsWith("!"))
        {
            return HandleRconCommandAsync(userId, message);
        }

        GameServer.GameWriter.SendText($"[§C1001{userId}§C1001] {message}", false, true);

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleRconCommandAsync(string userId, string message)
    {
        var command = GetRconCommand(message);
        var response = await SendRconCommandAsync(command);
        var obfuscatedResponse = GetObfuscatedResponse(response);

        await ClientsUser(userId).RequestResponseEvent(
            new Shared.Communication.Events.RequestResponseEvent
            {
                Request = message,
                Response = obfuscatedResponse
            }
        );
    }

    public async ValueTask HandleCustomCommandAsync(string userId, string message)
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
        GameServer.GameReader.StartRecording(path);
    }

    public async ValueTask HandleAsync(StopEventRecordingCommand command)
    {
        GameServer.GameReader.StopRecording();
    }
}
