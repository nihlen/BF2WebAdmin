using System.Diagnostics;
using System.Diagnostics.Metrics;
using BF2WebAdmin.Common.External;

namespace BF2WebAdmin.Server;

/// <summary>
/// How the modules handle the game's events and the players' commands, as metrics on the application's meter
/// (<see cref="Telemetry.MeterName"/>). The runtime's figures (GC, thread pool, process CPU and memory) come from the
/// runtime's own meter, and the admin channel's from <see cref="Metrics.AdminChannelMetrics"/>.
/// </summary>
public static class AppDiagnostics
{
    // From a few microseconds (most handlers) to seconds (a handler that waits on the database or Discord)
    private static readonly double[] DurationBuckets = [0.00001, 0.00005, 0.0001, 0.0005, 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5];

    private static readonly Histogram<double> EventDuration = Telemetry.Meter.CreateHistogram("bf2wa.module.event.duration", "s",
        "Time a module took to handle a game event, by event and module",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    private static readonly Histogram<double> CommandDuration = Telemetry.Meter.CreateHistogram("bf2wa.module.command.duration", "s",
        "Time a module took to handle a player's command, by command and module",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    private static readonly Counter<long> Failures = Telemetry.Meter.CreateCounter<long>("bf2wa.module.failures", "{failure}",
        "Event and command handlers that threw, by kind (event, command) and module");

    private static readonly Counter<long> DiscordMessages = Telemetry.Meter.CreateCounter<long>("bf2wa.discord.messages", "{message}",
        "Messages the servers' Discord bots sent to their channels, by result (sent, failed)");

    // A save retries for up to ~34 minutes while the database is away
    private static readonly Histogram<double> MatchSaveDuration = Telemetry.Meter.CreateHistogram("bf2wa.match.save.duration", "s",
        "Heli2v2 match writes to the database, with their retries, by kind (match, match_end, round) and result (ok, failed)",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 5, 30, 120, 600, 2400] });

    internal static void DiscordMessage(string serverId, bool sent) =>
        DiscordMessages.Add(1, new KeyValuePair<string, object?>(Metrics.GameMetrics.ServerIdTag, serverId),
            new KeyValuePair<string, object?>("result", sent ? "sent" : "failed"));

    internal static void MatchSave(string kind, bool ok, TimeSpan duration) =>
        MatchSaveDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("result", ok ? "ok" : "failed"));

    /// <summary>A module handled an event (since <paramref name="startTimestamp"/>, a <see cref="Stopwatch"/> timestamp).</summary>
    internal static void TrackEvent(string eventType, string moduleType, long startTimestamp, bool failed)
    {
        EventDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            new KeyValuePair<string, object?>("event", eventType), new KeyValuePair<string, object?>("module", moduleType));
        if (failed)
            Failure("event", moduleType);
    }

    /// <summary>A module handled a command (since <paramref name="startTimestamp"/>, a <see cref="Stopwatch"/> timestamp).</summary>
    internal static void TrackCommand(string commandType, string moduleType, long startTimestamp, bool failed)
    {
        CommandDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            new KeyValuePair<string, object?>("command", commandType), new KeyValuePair<string, object?>("module", moduleType));
        if (failed)
            Failure("command", moduleType);
    }

    private static void Failure(string kind, string moduleType) =>
        Failures.Add(1, new KeyValuePair<string, object?>("kind", kind), new KeyValuePair<string, object?>("module", moduleType));
}
