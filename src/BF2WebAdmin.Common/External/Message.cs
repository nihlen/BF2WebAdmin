// ReSharper disable All
using System;

namespace Nihlen.Message;

/// <summary>
/// Start a stream a game or optional match
/// </summary>
public record StartGameStream
{
    public string GameServerIp { get; init; }
    public int GameServerPort { get; init; }
    public string GameServerPassword { get; init; }
    public int GameQueryPort { get; init; }
    public Guid? MatchId { get; init; }
    public string? MatchMode { get; init; }
}

/// <summary>
/// Stop stream for the specified server
/// </summary>
public record StopGameStream
{
    public string GameServerIp { get; init; }
    public int GameServerPort { get; init; }
    public Guid? MatchId { get; init; }
}

/// <summary>
/// Game stream has been started with the specified bot and stream URL
/// </summary>
public record GameStreamStarted
{
    public string GameServerIp { get; init; }
    public int GameServerPort { get; init; }
    public Guid? MatchId { get; init; }
    public string StreamUrl { get; set; }
    public string BotName { get; set; }
}

/// <summary>
/// Game stream has been stopped
/// </summary>
public record GameStreamStopped
{
    public string GameServerIp { get; init; }
    public int GameServerPort { get; init; }
    public Guid? MatchId { get; init; }
}