using System.Text.Json;
using MassTransit;
using Nihlen.Message;
using StackExchange.Redis;

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
        //var endpoint = await _messageBus.GetSendEndpoint(new Uri("exchange:game-stream-controller"));

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

public class RedisGameStreamService : IGameStreamService
{
    private readonly ISubscriber _subscriber;

    public RedisGameStreamService(ISubscriber subscriber)
    {
        _subscriber = subscriber;
    }

    public async Task StartGameStreamAsync(string ipAddress, int gamePort, int queryPort)
    {
        var message = new StartGameStream
        {
            GameServerIp = ipAddress,
            GameServerPort = gamePort,
            GameQueryPort = queryPort,
            MatchId = null,
            MatchMode = null
        };

        var json = JsonSerializer.Serialize(message);

        await _subscriber.PublishAsync(nameof(StartGameStream), json, CommandFlags.FireAndForget);
    }

    public async Task StopGameStreamAsync(string ipAddress, int gamePort)
    {
        var message = new StopGameStream
        {
            GameServerIp = ipAddress,
            GameServerPort = gamePort,
            MatchId = null,
        };

        var json = JsonSerializer.Serialize(message);

        await _subscriber.PublishAsync(nameof(StopGameStream), json, CommandFlags.FireAndForget);
    }
}
