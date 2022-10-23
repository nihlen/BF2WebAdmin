using MassTransit;
using Nihlen.Message;

namespace BF2WebAdmin.Server;

public class GameStreamConsumer :
    IConsumer<GameStreamStarted>,
    IConsumer<GameStreamStopped>
{
    private readonly ISocketServer _socketServer;
    private readonly ILogger<GameStreamConsumer> _logger;

    public GameStreamConsumer(ISocketServer socketServer, ILogger<GameStreamConsumer> logger)
    {
        _socketServer = socketServer;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GameStreamStarted> context)
    {
        var serverId = $"{context.Message.GameServerIp}:{context.Message.GameServerPort}";
        _logger.LogInformation("Received GameStreamStarted message for {ServerId}", serverId);

        var gameServer = _socketServer.GetGameServer(serverId);
        if (gameServer is null)
            throw new ArgumentException(nameof(serverId));

        await gameServer.ModManager.Mediator.PublishAsync(new GameStreamStartedEvent(context.Message.MatchId, context.Message.StreamUrl, context.Message.BotName, DateTimeOffset.UtcNow));

        _logger.LogInformation("Successfully handled GameStreamStarted");
    }

    public async Task Consume(ConsumeContext<GameStreamStopped> context)
    {
        var serverId = $"{context.Message.GameServerIp}:{context.Message.GameServerPort}";
        _logger.LogInformation("Received GameStreamStopped message for {ServerId}", serverId);

        var gameServer = _socketServer.GetGameServer(serverId);
        if (gameServer is null)
            throw new ArgumentException(nameof(serverId));

        await gameServer.ModManager.Mediator.PublishAsync(new GameStreamStoppedEvent(context.Message.MatchId, DateTimeOffset.UtcNow));

        _logger.LogInformation("Successfully handled GameStreamStopped");
    }
}
