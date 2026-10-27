using Events = BF2WebAdmin.Shared.Communication.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.Hubs;

/// <summary>
/// Live updates for the web UI. Server list changes go to every client; the game events of a server go to the clients
/// that subscribed to it. Queries and commands are web API requests (Api/ApiEndpoints.cs), not hub methods.
/// </summary>
public interface IServerHubClient
{
    // All clients
    Task ServerUpdateEvent(Events.ServerUpdateEvent e);
    Task ServerRemoveEvent(Events.ServerRemoveEvent e);

    // Clients subscribed to the server
    Task ChatEvent(Events.ChatEvent e);
    Task GameStateEvent(Events.GameStateEvent e);
    Task MapChangeEvent(Events.MapChangeEvent e);
    Task PlayerDeathEvent(Events.PlayerDeathEvent e);
    Task PlayerJoinEvent(Events.PlayerJoinEvent e);
    Task PlayerKillEvent(Events.PlayerKillEvent e);
    Task PlayerLeftEvent(Events.PlayerLeftEvent e);
    Task PlayerPositionEvent(Events.PlayerPositionEvent e);
    Task PlayerScoreEvent(Events.PlayerScoreEvent e);
    Task PlayerSpawnEvent(Events.PlayerSpawnEvent e);
    Task PlayerTeamEvent(Events.PlayerTeamEvent e);
    Task PlayerVehicleEvent(Events.PlayerVehicleEvent e);
    Task ProjectilePositionEvent(Events.ProjectilePositionEvent e);
    Task SocketStateEvent(Events.SocketStateEvent e);
}

[Authorize(Roles = "Administrator")]
public class ServerHub : Hub<IServerHubClient>
{
    /// <summary>Receive the game events of a server. Fetch its snapshot (GET /api/servers/{id}) after this, so no event is missed.</summary>
    public Task Subscribe(string serverId) => Groups.AddToGroupAsync(Context.ConnectionId, serverId);

    public Task Unsubscribe(string serverId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, serverId);
}
