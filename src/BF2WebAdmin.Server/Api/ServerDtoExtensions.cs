using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Api;

/// <summary>The server DTOs sent by the web API and the server hub.</summary>
public static class ServerDtoExtensions
{
    public static Communication.Events.ServerUpdateEvent ToServerUpdate(this IGameServer server, DateTimeOffset? timeStamp = null) => new()
    {
        Id = server.Id,
        Name = server.Name,
        IpAddress = server.IpAddress.ToString(),
        GamePort = server.GamePort,
        QueryPort = server.QueryPort,
        Map = server.Map?.Name,
        Players = server.Players.Count(),
        MaxPlayers = server.MaxPlayers,
        GameState = server.State,
        SocketState = server.SocketState,
        ServerGroup = server.ModManager.ServerSettings.ServerGroup,
        TimeStamp = timeStamp ?? DateTimeOffset.UtcNow
    };

    /// <summary>A configured server that has not connected.</summary>
    public static Communication.Events.ServerUpdateEvent ToServerUpdate(this ServerInfo serverInfo) => new()
    {
        Id = serverInfo.ServerId,
        Name = serverInfo.ServerId,
        IpAddress = serverInfo.IpAddress,
        GamePort = serverInfo.GamePort,
        QueryPort = serverInfo.QueryPort,
        ServerGroup = serverInfo.ServerGroup,
        GameState = GameState.NotConnected,
        SocketState = SocketState.Disconnected,
        TimeStamp = DateTimeOffset.UtcNow
    };

    public static ServerSnapshotDto ToSnapshot(this IGameServer server) => new()
    {
        Server = server.ToServerUpdate(),
        Maps = server.Maps.Select(m => m.ToDto()).ToList(),
        Teams = server.Teams.Select(t => t.ToDto()).ToList(),
        Players = server.Players.Select(p => p.ToDto()).ToList(),
        EventLog = server.Events.Select(e => new EventLogDto { Message = e.Message, Timestamp = e.Time }).ToList(),
        ChatLog = server.Messages.Select(m => new ChatLogDto { Message = m.Message, Timestamp = m.Time }).ToList()
    };

    public static ServerSnapshotDto ToSnapshot(this ServerInfo serverInfo) => new()
    {
        Server = serverInfo.ToServerUpdate(),
        Maps = [],
        Teams = [],
        Players = [],
        ChatLog = [],
        EventLog = []
    };
}
