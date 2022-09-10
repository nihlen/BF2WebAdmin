using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules.BF2;
using BF2WebAdmin.Shared.Communication.DTOs;
using BF2WebAdmin.Shared.Communication.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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

    public ServerHub(ISocketServer socketServer, IServerSettingsRepository serverSettingsRepository, IServiceProvider serviceProvider)
    {
        _socketServer = socketServer;
        _serverSettingsRepository = serverSettingsRepository;
        _serviceProvider = serviceProvider;
    }

    // Events used to send to all servers' WebModules (TODO: change to some GetModule loop instead)
    public static event EventHandler<string> UserConnectEvent; 
    public static event EventHandler<(string, string)> UserSelectServerEvent; 

    public async Task UserConnect()
    {
        UserConnectEvent?.Invoke(this, GetUserId());
    }

    public async Task SelectServer(string serverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serverId);
        UserSelectServerEvent?.Invoke(this, (GetUserId(), serverId));
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

    private IEnumerable<WebModule> GetWebModules(string serverGroup)
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
        await GetWebModule(serverId).HandleAdminChatAsync(GetUserId(), message);
    }

    public async Task SendRconCommand(string serverId, string message)
    {
        await GetWebModule(serverId).HandleRconCommandAsync(GetUserId(), message);
    }

    public async Task SendCustomCommand(string serverId, string message)
    {
        await GetWebModule(serverId).HandleCustomCommandAsync(GetUserId(), message);
    }

    public async Task<ServerDataDto> GetServer(string serverId)
    {
        // return await GetWebModule(serverId).GetServerAsync(serverId);
        var server = await _serverSettingsRepository.GetServerAsync(serverId) ?? new Data.Entities.Server
        {
            ServerId = serverId,
            ServerGroup = "default"
        };
        
        return new ServerDataDto
        {
            ServerId = server.ServerId,
            ServerGroup = server.ServerGroup
        };
    }

    public async Task SetServer(string serverId, string serverGroup)
    {
        await _serverSettingsRepository.SetServerAsync(new Data.Entities.Server { ServerId = serverId, ServerGroup = serverGroup });
        var gameServer = _socketServer.GetGameServer(serverId);
        if (gameServer is not null)
        {
            await ReloadModulesAsync(gameServer);
        }
    }

    private static async Task ReloadModulesAsync(IGameServer gameServer)
    {
        // TODO: disable whatever the old modmanager is doing, dispose and finish all long-running tasks
        // gameServer.ModManager.dispose?
        await gameServer.CreateModManagerAsync(true);
    }

    public async Task<IEnumerable<string>> GetServerGroupModules(string serverId, string serverGroup)
    {
        // return await GetWebModule(serverId).GetServerGroupModulesAsync(serverGroup);
        var modules = await _serverSettingsRepository.GetModulesAsync(serverGroup);
        return modules.Concat(ModManager.DefaultModuleNames).Distinct();
    }

    public IEnumerable<string> GetAllModules()
    {
        return ModuleResolver.AllModuleNames;
    }

    public async Task SetServerGroupModules(string serverId, string serverGroup, IEnumerable<string> moduleNames)
    {
        // await GetWebModule(serverId).SetServerGroupModulesAsync(serverGroup, moduleNames);
        await _serverSettingsRepository.SetModulesAsync(serverGroup, moduleNames);
    }

    public async Task ReloadServerGroupModules(string serverGroup)
    {
        var gameServers = _socketServer.GetGameServers(serverGroup);
        foreach (var gameServer in gameServers)
        {
            await ReloadModulesAsync(gameServer);
        }
    }
}
