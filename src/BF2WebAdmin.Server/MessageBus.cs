using MassTransit;
using Nihlen.Message;
using Serilog;
using Serilog.Context;

namespace BF2WebAdmin.Server;

public class GameStreamConsumer :
    IConsumer<GameStreamStarted>,
    IConsumer<GameStreamStopped>
{
    private readonly ISocketServer _socketServer;

    public GameStreamConsumer(ISocketServer socketServer)
    {
        _socketServer = socketServer;
    }

    public async Task Consume(ConsumeContext<GameStreamStarted> context)
    {
        using (LogContext.PushProperty("Context", context, destructureObjects: true))
        {
            var serverId = $"{context.Message.GameServerIp}:{context.Message.GameServerPort}";
            Log.Information("Received GameStreamStarted message for {ServerId}", serverId);

            var gameServer = _socketServer.GetGameServer(serverId);
            if (gameServer is null)
                throw new ArgumentException(nameof(serverId));

            await gameServer.ModManager.Mediator.PublishAsync(new GameStreamStartedEvent(context.Message.MatchId, context.Message.StreamUrl, context.Message.BotName, DateTimeOffset.UtcNow));

            Log.Information("Successfully handled GameStreamStarted");
        }
    }

    public async Task Consume(ConsumeContext<GameStreamStopped> context)
    {
        using (LogContext.PushProperty("Context", context, destructureObjects: true))
        {
            var serverId = $"{context.Message.GameServerIp}:{context.Message.GameServerPort}";
            Log.Information("Received GameStreamStopped message for {ServerId}", serverId);

            var gameServer = _socketServer.GetGameServer(serverId);
            if (gameServer is null)
                throw new ArgumentException(nameof(serverId));

            await gameServer.ModManager.Mediator.PublishAsync(new GameStreamStoppedEvent(context.Message.MatchId, DateTimeOffset.UtcNow));

            Log.Information("Successfully handled GameStreamStopped");
        }
    }
}
