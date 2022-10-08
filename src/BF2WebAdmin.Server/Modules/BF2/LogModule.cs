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
        _serverChatLogger.Information("{PlayerId}\t{PlayerName}\t{Team}\t{Channel}\t[{Time}]\t{Text}", playerId, playerName, team, channel, time, text);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tSocketState\t{SocketState}", TimeString, e.SocketState);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerJoinEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tPlayerJoin\t{PlayerHash}\t{PlayerName}\t{PlayerIpAddress}\t{CountryCode}\t{Count}", TimeString, e.Player.Hash, e.Player.Name, e.Player.IpAddress, e.Player.Country?.Code, _gameServer.Players.Count());
       return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tPlayerLeft\t{PlayerHash}\t{PlayerName}\t{PlayerIpAddress}\t{CountryCode}\t{Count}", TimeString, e.Player.Hash, e.Player.Name, e.Player.IpAddress, e.Player.Country?.Code, _gameServer.Players.Count());
       return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GameStateChangedEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tGameState\t{EGameState}", TimeString, e.GameState);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(MapChangedEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tMapChanged\t{MapIndex}\t{MapName}\t{MapSize}", TimeString, e.Map?.Index, e.Map?.Name, e.Map?.Size);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ServerUpdateEvent e)
    {
        _serverEventLogger.Information("[{TimeString}]\tServerUpdate\t{EName}\t{EGamePort}\t{EQueryPort}\t{EMaxPlayers}", TimeString, e.Name, e.GamePort, e.QueryPort, e.MaxPlayers);
        return ValueTask.CompletedTask;
    }
}
