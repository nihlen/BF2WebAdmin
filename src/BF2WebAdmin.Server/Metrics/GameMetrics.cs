using System.Diagnostics;
using System.Diagnostics.Metrics;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Communication;

namespace BF2WebAdmin.Server.Metrics;

/// <summary>
/// What happens on the game servers, as metrics on the application's meter (<see cref="Telemetry.MeterName"/>): players,
/// joins, kills, chat, maps, admin actions, TV missiles and Heli2v2's rounds and matches. Every measurement has the tags
/// <c>bf2.server.id</c> and <c>bf2.server.name</c>, like the health metrics (<see cref="AdminChannel.ServerHealthMetrics"/>);
/// no tag names a player. docs/metrics.md lists them.
/// </summary>
public static class GameMetrics
{
    public const string ServerIdTag = "bf2.server.id", ServerNameTag = "bf2.server.name";

    private static readonly double[] RoundBuckets = [15, 30, 45, 60, 90, 120, 180, 240, 300, 450, 600, 900];

    private static readonly Counter<long> Joins = Telemetry.Meter.CreateCounter<long>("bf2.player.joins", "{join}",
        "Human players who connected, by country (ISO code of their address, \"unknown\" without one)");

    private static readonly Counter<long> Kills = Telemetry.Meter.CreateCounter<long>("bf2.kills", "{kill}",
        "Kills by kind (kill, roadkill, teamkill, suicide, no_attacker) and, for kills and roadkills, weapon");

    private static readonly Counter<long> Chat = Telemetry.Meter.CreateCounter<long>("bf2.chat.messages", "{message}",
        "Players' chat messages by channel");

    private static readonly Counter<long> Maps = Telemetry.Meter.CreateCounter<long>("bf2.map.loads", "{load}", "Maps loaded (a round started), by map");

    private static readonly Counter<long> AdminActions = Telemetry.Meter.CreateCounter<long>("bf2.admin.actions", "{action}",
        "Kicks and bans as the engine reports them (from WebAdmin, Discord, the game or the console), by action");

    private static readonly Counter<long> Missiles = Telemetry.Meter.CreateCounter<long>("bf2.tv_missiles", "{missile}",
        "Traced TV missiles by outcome: hit (an object), terrain, miss, cut (the trace was cut short)");

    private static readonly Counter<long> Rounds = Telemetry.Meter.CreateCounter<long>("bf2.heli2v2.rounds", "{round}",
        "Heli2v2 rounds played, by map and result (win, draw)");

    private static readonly Histogram<double> RoundDuration = Telemetry.Meter.CreateHistogram("bf2.heli2v2.round.duration", "s",
        "How long Heli2v2 rounds lasted, by map", advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = RoundBuckets });

    private static readonly Counter<long> Matches = Telemetry.Meter.CreateCounter<long>("bf2.heli2v2.matches", "{match}", "Heli2v2 matches that ended, by map");

    private static Func<IEnumerable<IGameServer>>? _servers;

    static GameMetrics()
    {
        Telemetry.Meter.CreateObservableGauge("bf2.players", ObservePlayers, "{player}", "Players on each connected server, by kind (human, bot)");
    }

    /// <summary>The servers whose players <c>bf2.players</c> counts (the registry's; the latest registry wins).</summary>
    public static void ObserveServers(Func<IEnumerable<IGameServer>> servers) => _servers = servers;

    public static void Join(IGameServer server, string? countryCode) =>
        Joins.Add(1, With(Tags(server), "country", string.IsNullOrEmpty(countryCode) ? "unknown" : countryCode.ToUpperInvariant()));

    public static void Kill(IGameServer server, string kind, string? weapon)
    {
        var tags = With(Tags(server), "kind", kind);
        tags.Add("weapon", weapon ?? "");
        Kills.Add(1, tags);
    }

    public static void ChatMessage(IGameServer server, string channel) => Chat.Add(1, With(Tags(server), "channel", channel));

    public static void MapLoaded(IGameServer server, string map) => Maps.Add(1, With(Tags(server), "map", map));

    public static void AdminAction(IGameServer server, string action) => AdminActions.Add(1, With(Tags(server), "action", action));

    public static void TvMissile(IGameServer server, string outcome) => Missiles.Add(1, With(Tags(server), "outcome", outcome));

    public static void Heli2v2Round(IGameServer server, string map, bool draw, TimeSpan duration)
    {
        var tags = With(Tags(server), "map", map);
        RoundDuration.Record(duration.TotalSeconds, tags);
        tags.Add("result", draw ? "draw" : "win");
        Rounds.Add(1, tags);
    }

    public static void Heli2v2Match(IGameServer server, string map) => Matches.Add(1, With(Tags(server), "map", map));

    private static IEnumerable<Measurement<int>> ObservePlayers()
    {
        if (_servers == null)
            yield break;

        foreach (var server in _servers())
        {
            if (server.SocketState != SocketState.Connected)
                continue;

            var players = server.Players.ToList();
            var bots = players.Count(p => p.IsBot);
            yield return new Measurement<int>(players.Count - bots, With(Tags(server), "kind", "human"));
            yield return new Measurement<int>(bots, With(Tags(server), "kind", "bot"));
        }
    }

    private static TagList Tags(IGameServer server) =>
        new() { { ServerIdTag, server.Id }, { ServerNameTag, string.IsNullOrEmpty(server.Name) ? server.Id : server.Name } };

    private static TagList With(TagList tags, string key, string value)
    {
        tags.Add(key, value);
        return tags;
    }
}
