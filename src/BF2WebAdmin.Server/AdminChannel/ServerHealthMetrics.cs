using System.Diagnostics;
using System.Diagnostics.Metrics;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// The game servers' health (<see cref="ServerHealthStore"/>) as OpenTelemetry metrics on the application's meter
/// (<see cref="Telemetry.MeterName"/>), for a longer history than the six hours WebAdmin keeps. Every measurement has
/// the tags <c>bf2.server.id</c> and <c>bf2.server.name</c>; there are no tags per player.
/// <list type="bullet">
/// <item>Counters, added to as each window comes, so nothing between two exports is lost: the windows by status, and
/// of the rated windows (not Loading or Stepped) the ticks by kind, the major page faults and the involuntary context
/// switches; the players' connection windows by status (partial ones left out).</item>
/// <item>Gauges of the latest window, while it is fresh (<see cref="FreshFor"/>), so a server that stops sending drops
/// out instead of keeping its last values: the status, the tick timings and the host's CPU (rated windows only, and the
/// host only when its figures are valid), the resident memory, and the players' connections.</item>
/// </list>
/// docs/metrics.md lists them with their Prometheus names.
/// </summary>
public sealed class ServerHealthMetrics
{
    /// <summary>A window or connections older than this is not the server's state now: a window comes every 10 s.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(25);

    public const string ServerIdTag = "bf2.server.id", ServerNameTag = "bf2.server.name";

    private readonly ServerHealthStore _store;
    private readonly TimeProvider _time;
    private readonly Counter<long> _windows;
    private readonly Counter<long> _ticks;
    private readonly Counter<long> _majorFaults;
    private readonly Counter<long> _involuntarySwitches;
    private readonly Counter<long> _playerWindows;

    public ServerHealthMetrics(IMeterFactory meterFactory, ServerHealthStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
        var meter = meterFactory.Create(Telemetry.MeterName);

        _windows = meter.CreateCounter<long>("bf2.server.health.windows", "{window}", "ServerHealth windows (10 s) by status");
        _ticks = meter.CreateCounter<long>("bf2.server.ticks", "{tick}",
            "Ticks of the rated health windows by kind: run (all), late (over 66.7 ms), short (two in one frame), lost (dropped by the engine; within one tick)");
        _majorFaults = meter.CreateCounter<long>("bf2.server.page_faults.major", "{fault}", "Major page faults of the game server process");
        _involuntarySwitches = meter.CreateCounter<long>("bf2.server.context_switches.involuntary", "{switch}",
            "Involuntary context switches of the game server's main thread");
        _playerWindows = meter.CreateCounter<long>("bf2.player.connection.windows", "{window}",
            "Human players' connection windows (10 s) by status; partial ones left out");

        meter.CreateObservableGauge("bf2.server.health.status", ObserveStatus, null,
            "Status of the latest health window: 0 Good, 1 Warn, 2 Bad, 3 Loading, 4 Stepped");
        meter.CreateObservableGauge("bf2.server.tick.interval", () => ObserveRated((w, tags) =>
        [
            Seconds(w.IntervalMedianUs, tags, "median"), Seconds(w.IntervalP99Us, tags, "p99"), Seconds(w.IntervalMaxUs, tags, "max"),
        ]), "s", "Real time between ticks in the latest health window, by stat: median, p99, max");
        meter.CreateObservableGauge("bf2.server.tick.cpu", () => ObserveRated((w, tags) =>
        [
            Seconds(w.CpuMedianUs, tags, "median"), Seconds(w.CpuP99Us, tags, "p99"), Seconds(w.CpuMaxUs, tags, "max"),
        ]), "s", "Main thread CPU time per tick in the latest health window, by stat: median, p99, max");
        meter.CreateObservableGauge("bf2.server.module.handler", () => ObserveRated((w, tags) =>
        [
            Seconds(w.HandlerP99Us, tags, "p99"), Seconds(w.HandlerMaxUs, tags, "max"),
        ]), "s", "Time in the server extension's tick handler in the latest health window, by stat: p99, max");
        meter.CreateObservableGauge("bf2.server.host.cpu", () => ObserveRated((w, tags) => w.HostValid
            ?
            [
                new Measurement<double>(w.HostBusyPercent, With(tags, "state", "busy")),
                new Measurement<double>(w.HostStealPercent, With(tags, "state", "steal")),
                new Measurement<double>(w.HostIoWaitPercent, With(tags, "state", "iowait")),
            ]
            : []), "%", "The host's CPU in the latest health window, of all its CPUs, by state: busy, steal, iowait");
        meter.CreateObservableGauge("bf2.server.memory.resident", ObserveMemory, "By", "Resident memory of the game server process");

        meter.CreateObservableGauge("bf2.server.players.connected", ObservePlayers, "{player}",
            "Human players in the latest connections, by status of their connection");
        meter.CreateObservableGauge("bf2.player.ping", () => ObserveConnections((players, tags) =>
        [
            new Measurement<double>(Median(players.Select(p => (double)p.Ping)), With(tags, "stat", "median")),
            new Measurement<double>(players.Max(p => p.PingMax), With(tags, "stat", "max")),
        ]), "ms", "Human players' ping in the latest connections, by stat: median (of the players' last pings), max (the highest of the window)");
        meter.CreateObservableGauge("bf2.player.loss", () => ObserveConnections((players, tags) =>
        [
            new Measurement<double>(players.Average(p => p.LossToPlayerPercent), With(tags, "direction", "to_player", "stat", "mean")),
            new Measurement<double>(players.Max(p => p.LossToPlayerPercent), With(tags, "direction", "to_player", "stat", "max")),
            new Measurement<double>(players.Average(p => p.LossFromPlayerPercent), With(tags, "direction", "from_player", "stat", "mean")),
            new Measurement<double>(players.Max(p => p.LossFromPlayerPercent), With(tags, "direction", "from_player", "stat", "max")),
        ]), "%", "Human players' packet loss in the latest connections, by direction and stat: the mean and the highest of the players");
    }

    /// <summary>Counts a window as it comes.</summary>
    public void OnWindow(string serverId, string? serverName, ServerHealthWindowDto window)
    {
        var tags = Tags(serverId, serverName);
        _windows.Add(1, With(tags, "status", window.Status.ToString()));
        if (!IsRated(window))
            return;

        _ticks.Add(window.Ticks, With(tags, "kind", "run"));
        _ticks.Add(window.LateTicks, With(tags, "kind", "late"));
        _ticks.Add(window.ShortTicks, With(tags, "kind", "short"));
        _ticks.Add(window.LostTicks, With(tags, "kind", "lost"));
        _majorFaults.Add(window.MajorFaults, tags);
        _involuntarySwitches.Add(window.InvoluntarySwitches, tags);
    }

    /// <summary>Counts the players' connection windows as they come; partial ones (a player who joined) are left out.</summary>
    public void OnConnections(string serverId, string? serverName, ServerConnectionsDto connections)
    {
        var tags = Tags(serverId, serverName);
        foreach (var status in connections.Players.Where(p => !p.Partial).GroupBy(p => p.Status))
            _playerWindows.Add(status.Count(), With(tags, "status", status.Key.ToString()));
    }

    private static bool IsRated(ServerHealthWindowDto w) => w.Status is ServerHealthStatus.Good or ServerHealthStatus.Warn or ServerHealthStatus.Bad;

    private bool IsFresh(DateTimeOffset time) => _time.GetUtcNow() - time <= FreshFor;

    private IEnumerable<Measurement<int>> ObserveStatus() =>
        from s in _store.GetLatest()
        where s.Window != null && IsFresh(s.Window.Time)
        select new Measurement<int>((int)s.Window!.Status, Tags(s.ServerId, s.Name));

    private IEnumerable<Measurement<long>> ObserveMemory() =>
        from s in _store.GetLatest()
        where s.Window != null && IsFresh(s.Window.Time)
        select new Measurement<long>(s.Window!.ResidentKib * 1024L, Tags(s.ServerId, s.Name));

    private IEnumerable<Measurement<double>> ObserveRated(Func<ServerHealthWindowDto, TagList, IEnumerable<Measurement<double>>> measure) =>
        from s in _store.GetLatest()
        where s.Window != null && IsFresh(s.Window.Time) && IsRated(s.Window)
        from m in measure(s.Window!, Tags(s.ServerId, s.Name))
        select m;

    private IEnumerable<Measurement<int>> ObservePlayers()
    {
        foreach (var s in _store.GetLatest())
        {
            if (s.Connections == null || !IsFresh(s.Connections.Time))
                continue;

            // Every status, 0 too, so that a count goes down to 0 instead of stopping
            var tags = Tags(s.ServerId, s.Name);
            foreach (var status in Enum.GetValues<PlayerConnectionStatus>())
                yield return new Measurement<int>(s.Connections.Players.Count(p => p.Status == status), With(tags, "status", status.ToString()));
        }
    }

    /// <summary>The connections' figures of the players with a full window; none when there are no such players.</summary>
    private IEnumerable<Measurement<double>> ObserveConnections(Func<PlayerConnectionDto[], TagList, IEnumerable<Measurement<double>>> measure) =>
        from s in _store.GetLatest()
        where s.Connections != null && IsFresh(s.Connections.Time)
        let players = s.Connections!.Players.Where(p => !p.Partial).ToArray()
        where players.Length > 0
        from m in measure(players, Tags(s.ServerId, s.Name))
        select m;

    private static Measurement<double> Seconds(uint microseconds, TagList tags, string stat) =>
        new(microseconds / 1e6, With(tags, "stat", stat));

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static TagList Tags(string serverId, string? serverName) =>
        new() { { ServerIdTag, serverId }, { ServerNameTag, string.IsNullOrEmpty(serverName) ? serverId : serverName } };

    private static TagList With(TagList tags, string key, string value)
    {
        tags.Add(key, value);
        return tags;
    }

    private static TagList With(TagList tags, string key1, string value1, string key2, string value2)
    {
        tags.Add(key1, value1);
        tags.Add(key2, value2);
        return tags;
    }
}
