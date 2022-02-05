using BF2WebAdmin.Server.Modules.BF2;
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

    public ServerHub(ISocketServer socketServer)
    {
        _socketServer = socketServer;
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
}
