using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.StreamBot;

/// <summary>
/// Where the stream bot connects (/hubs/stream-bot, with its own key: <see cref="StreamBotAuthentication"/>). WebAdmin
/// sends it start and stop requests (<see cref="StreamBotService"/>); the bot reports what it did, which goes to the
/// server's modules as <see cref="GameStreamStartedEvent"/> and <see cref="GameStreamStoppedEvent"/>.
/// </summary>
[Authorize(Policy = StreamBotAuthentication.Policy)]
public class StreamBotHub(StreamBotService service, IGameServerRegistry registry, ILogger<StreamBotHub> logger) : Hub<IStreamBotClient>
{
    public const string Path = "/hubs/stream-bot";

    public override Task OnConnectedAsync()
    {
        service.Connected(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        service.Disconnected(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public async Task StreamStarted(StreamStartedReport report)
    {
        logger.LogInformation("Stream bot {BotName} streams {ServerId} at {StreamUrl}", report.BotName, report.ServerId, report.StreamUrl);
        if (Find(report.ServerId) is { } server)
            await server.ModManager.Mediator.PublishAsync(new GameStreamStartedEvent(report.MatchId, report.StreamUrl, report.BotName, DateTimeOffset.UtcNow));
    }

    public async Task StreamStopped(StreamStoppedReport report)
    {
        logger.LogInformation("Stream bot stopped streaming {ServerId}", report.ServerId);
        if (Find(report.ServerId) is { } server)
            await server.ModManager.Mediator.PublishAsync(new GameStreamStoppedEvent(report.MatchId, DateTimeOffset.UtcNow));
    }

    private IGameServer? Find(string serverId)
    {
        var server = registry.GetGameServer(serverId);
        if (server?.ModManager is null)
            logger.LogWarning("Stream bot reported on {ServerId}, which is not connected", serverId);
        return server?.ModManager is null ? null : server;
    }
}
