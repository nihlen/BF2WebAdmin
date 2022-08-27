using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Configuration.Models;
using BF2WebAdmin.Shared;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace BF2WebAdmin.Server.Modules.BF2;

public class LogModule : IModule,
    IHandleEventAsync<ChatMessageEvent>,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleEventAsync<PlayerJoinEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<GameStateChangedEvent>,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<ServerUpdateEvent>
{
    private readonly IGameServer _gameServer;

    private readonly Serilog.Core.Logger _serverChatLogger;
    private readonly Serilog.Core.Logger _serverEventLogger;

    private static string TimeString => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    public LogModule(IGameServer server, IOptions<LogConfig> options)
    {
        _gameServer = server;

        var serverId = _gameServer.Id.Replace(".", "-").Replace(":", "_");

        _serverChatLogger = new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(options.Value.ServerLogDirectory, serverId, "bf2chat-.log"),
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                outputTemplate: "{Message}{NewLine}",
                rollingInterval: RollingInterval.Day,
                formatProvider: null,
                fileSizeLimitBytes: 5000000,
                retainedFileCountLimit: 12
            )
            .CreateLogger();

        _serverEventLogger = new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(options.Value.ServerLogDirectory, serverId, "bf2event-.log"),
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                outputTemplate: "{Message}{NewLine}",
                rollingInterval: RollingInterval.Day,
                formatProvider: null,
                fileSizeLimitBytes: 5000000,
                retainedFileCountLimit: 12
            )
            .CreateLogger();
    }

    public ValueTask HandleAsync(ChatMessageEvent e)
    {
        var playerId = e.Message.Player?.Index ?? -1;
        var playerName = e.Message.Player?.Name ?? "Admin";
        var team = e.Message.Player?.Team?.Id.ToString() ?? "None";
        var channel = e.Message.Channel;
        var time = e.TimeStamp.ToShortDateTime();
        var text = e.Message.Text;
        _serverChatLogger.Information($"{playerId}\t{playerName}\t{team}\t{channel}\t[{time}]\t{text}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tSocketState\t{e.SocketState}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerJoinEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tPlayerJoin\t{e.Player.Hash}\t{e.Player.Name}\t{e.Player.IpAddress}\t{e.Player.Country?.Code}\t{_gameServer.Players.Count()}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tPlayerLeft\t{e.Player.Hash}\t{e.Player.Name}\t{e.Player.IpAddress}\t{e.Player.Country?.Code}\t{_gameServer.Players.Count()}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GameStateChangedEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tGameState\t{e.GameState}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(MapChangedEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tMapChanged\t{e.Map?.Index}\t{e.Map?.Name}\t{e.Map?.Size}");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ServerUpdateEvent e)
    {
        _serverEventLogger.Information($"[{TimeString}]\tServerUpdate\t{e.Name}\t{e.GamePort}\t{e.QueryPort}\t{e.MaxPlayers}");
        return ValueTask.CompletedTask;
    }
}