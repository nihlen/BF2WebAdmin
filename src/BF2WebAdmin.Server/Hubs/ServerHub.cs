using System.Net;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules.BF2;
using BF2WebAdmin.Shared;
using BF2WebAdmin.Shared.Communication.DTOs;
using BF2WebAdmin.Shared.Communication.Events;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nihlen.Common.Telemetry;
using Nihlen.Gamespy;

namespace BF2WebAdmin.Server.Hubs;

public interface IServerHubClient
{
    Task ChatEvent(ChatEvent e);
    Task GameStateEvent(GameStateEvent e);
    Task MapChangeEvent(MapChangeEvent e);
    Task PlayerDeathEvent(Shared.Communication.Events.PlayerDeathEvent e);
    Task PlayerJoinEvent(Shared.Communication.Events.PlayerJoinEvent e);
    Task PlayerKillEvent(Shared.Communication.Events.PlayerKillEvent e);
    Task PlayerLeftEvent(Shared.Communication.Events.PlayerLeftEvent e);
    Task PlayerPositionEvent(Shared.Communication.Events.PlayerPositionEvent e);
    Task PlayerScoreEvent(Shared.Communication.Events.PlayerScoreEvent e);
    Task PlayerSpawnEvent(Shared.Communication.Events.PlayerSpawnEvent e);
    Task PlayerTeamEvent(Shared.Communication.Events.PlayerTeamEvent e);
    Task PlayerVehicleEvent(Shared.Communication.Events.PlayerVehicleEvent e);
    Task ProjectilePositionEvent(Shared.Communication.Events.ProjectilePositionEvent e);
    Task ServerUpdateEvent(Shared.Communication.Events.ServerUpdateEvent e);
    Task ServerRemoveEvent(Shared.Communication.Events.ServerRemoveEvent e);
    Task SocketStateEvent(SocketStateEvent e);
    Task ServerSnapshotEvent(ServerSnapshotEvent e);
    Task RequestResponseEvent(RequestResponseEvent e);
}

[Authorize(Roles = "Administrator")]
public class ServerHub : Hub<IServerHubClient>
{
    private readonly ISocketServer _socketServer;
    private readonly IServerSettingsRepository _serverSettingsRepository;
    private readonly IServiceProvider _serviceProvider;

    // TODO: remove socketserver dependency and use mediatr instead?
    // TODO: possibly user the server-specific mediator instead of WebModule directly
    public ServerHub(ISocketServer socketServer, IServerSettingsRepository serverSettingsRepository, IServiceProvider serviceProvider)
    {
        _socketServer = socketServer;
        _serverSettingsRepository = serverSettingsRepository;
        _serviceProvider = serviceProvider;
    }

    public async Task UserConnect()
    {
        await SendServerUpdatesAsync();
    }

    private async Task SendServerUpdatesAsync()
    {
        var modules = GetWebModules();
        foreach (var module in modules)
        {
            await module.SendServerInfo(GetUserId(), false);
        }

        var disconnectedServers = await _socketServer.GetDisconnectedServersAsync();
        foreach (var serverInfo in disconnectedServers)
        {
            var serverId = $"{serverInfo.IpAddress}:{serverInfo.GamePort}";
            await Clients.All.ServerUpdateEvent(new Shared.Communication.Events.ServerUpdateEvent
            {
                Id = serverId,
                Name = serverId,
                IpAddress = serverInfo.IpAddress,
                GamePort = serverInfo.GamePort,
                QueryPort = serverInfo.QueryPort,
                ServerGroup = serverInfo.ServerGroup,
                TimeStamp = DateTimeOffset.UtcNow,
                GameState = GameState.NotConnected,
                SocketState = SocketState.Disconnected
            });
        }
    }

    public async Task SelectServer(string serverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serverId);

        var module = GetWebModule(serverId);
        await module.SendServerInfo(GetUserId(), true);
    }

    public async Task DeselectServer(string serverId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, serverId);
    }

    private WebModule GetWebModule(string serverId)
    {
        // TODO: send new event type via mediator instead like GameStreamConsumer?
        var module = _socketServer.GetGameServer(serverId)?.ModManager.GetModule<WebModule>();
        if (module == null)
            throw new Exception("WebModule not found for" + serverId);

        return module;
    }

    private IEnumerable<WebModule> GetWebModules(string? serverGroup = null)
    {
        // TODO: send new event type via mediator instead like GameStreamConsumer?
        return _socketServer.GetGameServers(serverGroup).Select(s => s.ModManager.GetModule<WebModule>());
    }

    private string GetUserId()
    {
        //return Context.ConnectionId ?? "";
        return Context.UserIdentifier ?? "";
    }

    public async Task SendChatMessage(string serverId, string message)
    {
        using var activity = Telemetry.StartRootActivity($"{nameof(SendChatMessage)}:{message}");
        activity?.SetTag("bf2wa.server-id", serverId);
        activity?.SetTag("bf2wa.custom-command", message);

        await GetWebModule(serverId).HandleAdminChatAsync(GetUserId(), message);
    }

    public async Task SendRconCommand(string serverId, string message)
    {
        using var activity = Telemetry.StartRootActivity($"{nameof(SendRconCommand)}:{message}");
        activity?.SetTag("bf2wa.server-id", serverId);
        activity?.SetTag("bf2wa.custom-command", message);

        await GetWebModule(serverId).HandleRconCommandAsync(GetUserId(), message);
    }

    public async Task SendCustomCommand(string serverId, string message)
    {
        using var activity = Telemetry.StartRootActivity($"{nameof(SendCustomCommand)}:{message}");
        activity?.SetTag("bf2wa.server-id", serverId);
        activity?.SetTag("bf2wa.custom-command", message);

        await GetWebModule(serverId).HandleCustomCommandAsync(GetUserId(), message);
    }

    public async Task<ServerDataDto> GetServer(string serverId)
    {
        using var _ = Telemetry.StartRootActivity();

        var server = await _serverSettingsRepository.GetServerAsync(serverId) ?? new Data.Entities.Server
        {
            ServerId = serverId,
            ServerGroup = "default"
        };

        return server.Adapt<ServerDataDto>();
    }

    public async Task SetServer(ServerDataDto server)
    {
        using var _ = Telemetry.StartRootActivity();
        var serverEntity = server.Adapt<Data.Entities.Server>();
        await _serverSettingsRepository.SetServerAsync(serverEntity);
        await _socketServer.HandleServerUpdateAsync(server.ServerId, ServerUpdateType.AddOrUpdate);
        await SendServerUpdatesAsync();
    }

    public async Task RemoveServer(string serverId)
    {
        using var _ = Telemetry.StartRootActivity();
        await _serverSettingsRepository.RemoveServerAsync(serverId);
        await _socketServer.HandleServerUpdateAsync(serverId, ServerUpdateType.Remove);
        await Clients.All.ServerRemoveEvent(new ServerRemoveEvent { ServerId = serverId });
    }

    public async Task<IEnumerable<TestServerResult>> TestServer(ServerDataDto server)
    {
        using var _ = Telemetry.StartRootActivity();

        bool validRcon;
        bool validGamespy;

        try
        {
            var rconClient = new RconClient(IPAddress.Parse(server.IpAddress), server.RconPort, server.RconPassword, _serviceProvider.GetRequiredService<ILogger<RconClient>>());
            var rconResponse = await rconClient.SendAsync("iga listAdmins");
            validRcon = !string.IsNullOrWhiteSpace(rconResponse);
        }
        catch (Exception ex)
        {
            validRcon = false;
        }

        try
        {
            var gamespyClient = new Gamespy3Service();
            var gamespyResponse = await gamespyClient.QueryServerAsync(IPAddress.Parse(server.IpAddress), server.QueryPort);
            validGamespy = !string.IsNullOrWhiteSpace(gamespyResponse.Name);
        }
        catch (Exception ex)
        {
            validGamespy = false;
        }

        return new[]
        {
            new TestServerResult("RCON", validRcon),
            new TestServerResult("Gamespy query", validGamespy)
        };
    }

    public async Task<IEnumerable<string>> GetServerGroupModules(string serverId, string serverGroup)
    {
        using var _ = Telemetry.StartRootActivity();

        var modules = await _serverSettingsRepository.GetModulesAsync(serverGroup);
        return modules.Concat(ModManager.DefaultModuleNames).Distinct();
    }

    public IEnumerable<string> GetAllModules()
    {
        using var _ = Telemetry.StartRootActivity();
        return ModuleResolver.AllModuleNames;
    }

    public async Task SetServerGroupModules(string serverId, string serverGroup, IEnumerable<string> moduleNames)
    {
        using var _ = Telemetry.StartRootActivity();

        await _serverSettingsRepository.SetModulesAsync(serverGroup, moduleNames);
    }

    public async Task ReloadServerGroupModules(string serverGroup)
    {
        using var _ = Telemetry.StartRootActivity();
        var gameServers = _socketServer.GetGameServers(serverGroup);
        foreach (var gameServer in gameServers)
        {
            await ReloadModulesAsync(gameServer);
        }
    }

    private static async Task ReloadModulesAsync(IGameServer gameServer)
    {
        // TODO: disable whatever the old modmanager is doing, dispose and finish all long-running tasks
        await gameServer.CreateModManagerAsync(true);
    }
}
