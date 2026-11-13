using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.StreamBot;

/// <summary>Starts and stops the stream of a game server (.stream, .stopstream and each 2v2 match).</summary>
public interface IGameStreamService
{
    /// <summary>Asks a stream bot to stream the server; false when no bot is connected.</summary>
    Task<bool> StartGameStreamAsync(string serverId, string ipAddress, int gamePort, int queryPort, Guid? matchId);

    /// <summary>Asks the stream bots to stop streaming the server; false when no bot is connected.</summary>
    Task<bool> StopGameStreamAsync(string serverId, string ipAddress, int gamePort, Guid? matchId);
}

/// <summary>
/// The stream bots connected to <see cref="StreamBotHub"/>. A start goes to the bot that connected last (there is one
/// bot, and a reconnected one replaces its old connection); a stop goes to all of them. Nothing is queued: with no bot
/// connected a request is dropped, and the caller hears so.
/// </summary>
public class StreamBotService(IHubContext<StreamBotHub, IStreamBotClient> hub, ILogger<StreamBotService> logger) : IGameStreamService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _connections = new();

    public bool IsBotConnected => !_connections.IsEmpty;

    public void Connected(string connectionId)
    {
        _connections[connectionId] = DateTimeOffset.UtcNow;
        logger.LogInformation("Stream bot connected ({Connections} connected)", _connections.Count);
    }

    public void Disconnected(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        logger.LogInformation("Stream bot disconnected ({Connections} connected)", _connections.Count);
    }

    public async Task<bool> StartGameStreamAsync(string serverId, string ipAddress, int gamePort, int queryPort, Guid? matchId)
    {
        var newest = _connections.OrderByDescending(c => c.Value).Select(c => c.Key).FirstOrDefault();
        if (newest is null)
        {
            logger.LogInformation("No stream bot connected to stream {ServerId}", serverId);
            return false;
        }

        await hub.Clients.Client(newest).StartStream(new StartStreamRequest(serverId, ipAddress, gamePort, queryPort, matchId));
        return true;
    }

    public async Task<bool> StopGameStreamAsync(string serverId, string ipAddress, int gamePort, Guid? matchId)
    {
        if (_connections.IsEmpty)
            return false;

        await hub.Clients.All.StopStream(new StopStreamRequest(serverId, ipAddress, gamePort, matchId));
        return true;
    }
}
