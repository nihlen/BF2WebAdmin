using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.AdminChannel;

namespace BF2WebAdmin.Server.Metrics;

/// <summary>
/// The admin channel (the links of the game servers' modules) as metrics on the application's meter
/// (<see cref="Telemetry.MeterName"/>): connections, sessions, frames and bytes, round trips, the apply loop. Measurements
/// of a session have the tag <c>bf2.server.id</c>. docs/metrics.md lists them.
/// </summary>
public static class AdminChannelMetrics
{
    public const string ServerIdTag = "bf2.server.id";

    // Round trips and apply times: from a tenth of a millisecond to seconds
    private static readonly double[] LatencyBuckets = [0.0001, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    // Session lengths: from seconds (a refused or flapping link) to days
    private static readonly double[] DurationBuckets = [10, 60, 300, 900, 1800, 3600, 3 * 3600, 6 * 3600, 12 * 3600, 86400, 3 * 86400, 7 * 86400];

    private static readonly ConcurrentDictionary<AdminSession, byte> Sessions = new();

    private static readonly Counter<long> Connections = Telemetry.Meter.CreateCounter<long>("bf2wa.admin.connections", "{connection}",
        "Admin channel connections by result (accepted, refused) and, for refused ones, reason");

    private static readonly Counter<long> Closes = Telemetry.Meter.CreateCounter<long>("bf2wa.admin.session.closes", "{session}",
        "Admin channel sessions that closed, by reason");

    private static readonly Histogram<double> SessionDuration = Telemetry.Meter.CreateHistogram("bf2wa.admin.session.duration", "s",
        "How long admin channel sessions lasted", advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    private static readonly Counter<long> Frames = Telemetry.Meter.CreateCounter<long>("bf2wa.admin.frames", "{frame}",
        "Admin channel frames by direction (received, sent) and message type");

    private static readonly Counter<long> Bytes = Telemetry.Meter.CreateCounter<long>("bf2wa.admin.bytes", "By",
        "Admin channel bytes by direction (received, sent), after TLS");

    private static readonly Histogram<double> PingRoundTrip = Telemetry.Meter.CreateHistogram("bf2wa.admin.ping", "s",
        "Round trip of WebAdmin's keepalive Ping to the module and its Pong (every 15 s)",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = LatencyBuckets });

    private static readonly Histogram<double> RequestDuration = Telemetry.Meter.CreateHistogram("bf2wa.admin.request.duration", "s",
        "Commands to the module awaited for their reply, by message type and result (ok, error, timeout, closed)",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = LatencyBuckets });

    private static readonly Histogram<double> ApplyDuration = Telemetry.Meter.CreateHistogram("bf2wa.admin.apply.duration", "s",
        "Time to apply a received message to the server's state and modules, by kind (snapshot, event, stream, other)",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = LatencyBuckets });

    private static readonly Counter<long> SequenceGaps = Telemetry.Meter.CreateCounter<long>("bf2wa.admin.sequence_gaps", "{gap}",
        "Gaps in the module's event sequence (each one requests a new snapshot)");

    static AdminChannelMetrics()
    {
        Telemetry.Meter.CreateObservableGauge("bf2wa.admin.sessions", ObserveSessions, "{session}", "Open admin channel sessions");
        Telemetry.Meter.CreateObservableGauge("bf2wa.admin.queue", ObserveQueues, "{message}",
            "Messages waiting in an open session, by direction: inbound (received, not yet applied), outbound (not yet written)");
        Telemetry.Meter.CreateObservableGauge("bf2wa.admin.requests.pending", () =>
            Sessions.Keys.Select(s => new Measurement<int>(s.PendingRequests, Server(s.ServerId))), "{request}",
            "Commands sent to the module that wait for their reply");
    }

    /// <summary>A connection that passed the handshake and became a session.</summary>
    public static void Accepted() => Connections.Add(1, new KeyValuePair<string, object?>("result", "accepted"));

    /// <summary>A connection refused before it became a session; <paramref name="reason"/> is one of a few words.</summary>
    public static void Refused(string reason) =>
        Connections.Add(1, new KeyValuePair<string, object?>("result", "refused"), new KeyValuePair<string, object?>("reason", reason));

    public static void SessionOpened(AdminSession session) => Sessions.TryAdd(session, 0);

    public static void SessionClosed(AdminSession session, string? reason)
    {
        if (!Sessions.TryRemove(session, out _))
            return;

        var tags = Server(session.ServerId);
        tags.Add("reason", CloseKind(reason));
        Closes.Add(1, tags);
        SessionDuration.Record((DateTimeOffset.UtcNow - session.ConnectedAt).TotalSeconds, Server(session.ServerId));
    }

    /// <summary>A close reason (<see cref="AdminSession.CloseReason"/>, free text) as one of a few words for the tag.</summary>
    public static string CloseKind(string? reason) => reason switch
    {
        null => "other",
        _ when reason.StartsWith("replaced", StringComparison.Ordinal) => "replaced",
        _ when reason.StartsWith("nothing received", StringComparison.Ordinal) => "timeout",
        _ when reason.StartsWith("the module closed", StringComparison.Ordinal) => "closed_by_module",
        _ when reason.StartsWith("read failed", StringComparison.Ordinal) || reason.StartsWith("write failed", StringComparison.Ordinal) => "io_error",
        _ when reason.StartsWith("protocol error", StringComparison.Ordinal) => "protocol_error",
        _ when reason.StartsWith("server settings", StringComparison.Ordinal) => "settings_changed",
        _ => "other",
    };

    public static void Received(string serverId, AdminMessageType type) => Frames.Add(1, Frame(serverId, "received", type));

    public static void Sent(string serverId, AdminMessageType type, int bytes)
    {
        Frames.Add(1, Frame(serverId, "sent", type));
        Bytes.Add(bytes, Direction(serverId, "sent"));
    }

    public static void BytesReceived(string serverId, int bytes) => Bytes.Add(bytes, Direction(serverId, "received"));

    public static void Ping(string serverId, TimeSpan roundTrip) => PingRoundTrip.Record(roundTrip.TotalSeconds, Server(serverId));

    public static void Request(string serverId, AdminMessageType type, string result, TimeSpan duration)
    {
        var tags = Server(serverId);
        tags.Add("type", type.ToString());
        tags.Add("result", result);
        RequestDuration.Record(duration.TotalSeconds, tags);
    }

    public static void Applied(string serverId, AdminMessage message, TimeSpan duration)
    {
        var tags = Server(serverId);
        tags.Add("kind", message switch
        {
            Snapshot => "snapshot",
            AdminEvent => "event",
            PlayerStream or VehicleStream or ProjectileStream or GameStream or AimStream => "stream",
            _ => "other",
        });
        ApplyDuration.Record(duration.TotalSeconds, tags);
    }

    public static void SequenceGap(string serverId) => SequenceGaps.Add(1, Server(serverId));

    private static IEnumerable<Measurement<int>> ObserveSessions() =>
        Sessions.Keys.GroupBy(s => s.ServerId).Select(g => new Measurement<int>(g.Count(), Server(g.Key)));

    private static IEnumerable<Measurement<int>> ObserveQueues()
    {
        foreach (var session in Sessions.Keys)
        {
            yield return new Measurement<int>(session.InboundQueued, Direction(session.ServerId, "inbound"));
            yield return new Measurement<int>(session.OutboundQueued, Direction(session.ServerId, "outbound"));
        }
    }

    private static TagList Server(string serverId) => new() { { ServerIdTag, serverId } };

    private static TagList Direction(string serverId, string direction) => new() { { ServerIdTag, serverId }, { "direction", direction } };

    private static TagList Frame(string serverId, string direction, AdminMessageType type) =>
        new() { { ServerIdTag, serverId }, { "direction", direction }, { "type", type.ToString() } };
}
