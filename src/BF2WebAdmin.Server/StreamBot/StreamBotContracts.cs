namespace BF2WebAdmin.Server.StreamBot;

// The stream bot's contract (hub /hubs/stream-bot, JSON with camelCase names). The bot (bf2-auto-spectator-controller)
// has its own copy of these records; a change here needs the same change there. docs/stream-bot.md describes it.

/// <summary>Join this game server as a spectator and stream it.</summary>
public record StartStreamRequest(string ServerId, string GameServerIp, int GameServerPort, int GameQueryPort, Guid? MatchId);

/// <summary>Stop streaming this game server (if the bot streams it).</summary>
public record StopStreamRequest(string ServerId, string GameServerIp, int GameServerPort, Guid? MatchId);

/// <summary>The bot streams the server: its player name and the stream's URL.</summary>
public record StreamStartedReport(string ServerId, Guid? MatchId, string BotName, string StreamUrl);

/// <summary>The bot stopped streaming the server.</summary>
public record StreamStoppedReport(string ServerId, Guid? MatchId);

/// <summary>What WebAdmin calls on a connected bot.</summary>
public interface IStreamBotClient
{
    Task StartStream(StartStreamRequest request);
    Task StopStream(StopStreamRequest request);
}
