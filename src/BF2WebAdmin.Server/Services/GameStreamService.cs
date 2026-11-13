using MassTransit;
using Nihlen.Message;

namespace BF2WebAdmin.Server.Services;

public interface IGameStreamService
{
    Task StartGameStreamAsync(string ipAddress, int gamePort, int queryPort);
    Task StopGameStreamAsync(string ipAddress, int gamePort);
}

public class RabbitMqGameStreamService : IGameStreamService
{
    private readonly IBus? _messageBus;

    public RabbitMqGameStreamService(IBus? messageBus)
    {
        _messageBus = messageBus;
    }

    public async Task StartGameStreamAsync(string ipAddress, int gamePort, int queryPort)
    {
        if (_messageBus is null)
            throw new Exception("No message bus found");

        var endpoint = await _messageBus.GetSendEndpoint(new Uri("queue:game-stream-controller"));

        await endpoint.Send(
            new StartGameStream
            {
                GameServerIp = ipAddress,
                GameServerPort = gamePort,
                GameQueryPort = queryPort,
                MatchId = null,
                MatchMode = null
            },
            c => c.TimeToLive = TimeSpan.FromHours(1),
            CancellationToken.None
        );
    }

    public async Task StopGameStreamAsync(string ipAddress, int gamePort)
    {
        if (_messageBus is null)
            throw new Exception("No message bus found");

        await _messageBus.Publish(new StopGameStream
        {
            GameServerIp = ipAddress,
            GameServerPort = gamePort,
            MatchId = null,
        });
    }
}

public class RabbitMqSettings
{
    public string Host { get; set; }
    public ushort Port { get; set; }
    public string VirtualHost { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
