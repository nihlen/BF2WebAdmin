using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using BF2WebAdmin.Server.Communication.DTOs;
using BF2WebAdmin.Server.Communication.Events;
using BF2WebAdmin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// Rates a ServerHealth window (openbf2-server-ext docs/admin-channel.md, "Server health"). The one place the rules
/// are: the web UI shows the status and the reasons it is given.
/// </summary>
public static class ServerHealthRules
{
    public const string LateTicks = "lateTicks", ShortTicks = "shortTicks", LostTicks = "lostTicks", Cpu = "cpu", PageFaults = "pageFaults",
        Steal = "steal";

    private const double TickRate = 30;

    /// <summary>Late or lost ticks in one window from which it is Bad; any late tick is a Warn.</summary>
    public const int BadTicks = 3;

    /// <summary>The lost-ticks figure is only exact within one tick, so one lost tick is not a Warn.</summary>
    public const int WarnLostTicks = 2;

    /// <summary>The 99th percentile of the main thread's CPU time per tick, microseconds: half a tick, and nearly a whole one (33 333).</summary>
    public const uint WarnCpuP99 = 16_667, BadCpuP99 = 30_000;

    /// <summary>Host CPU steal, hundredths of a percent.</summary>
    public const ushort WarnSteal = 500, BadSteal = 1500;

    /// <summary>
    /// The ticks the engine dropped in a window: a window always has its ticks, so its real time is longer by the time
    /// they stood for. Exact within one tick.
    /// </summary>
    public static int Lost(uint wallMicroseconds, int ticks) =>
        Math.Max(0, (int)Math.Round(wallMicroseconds * TickRate / 1e6, MidpointRounding.AwayFromZero) - ticks);

    /// <summary>The window's status and what made it Warn or Bad, the Bad reasons first. No reasons for the other statuses.</summary>
    public static (ServerHealthStatus Status, IReadOnlyList<string> Reasons) Rate(EvServerHealth e)
    {
        // A level load stops the ticks, and stepped time is not real time: neither window describes the server
        if ((e.Flags & EvServerHealth.FlagLevelLoad) != 0)
            return (ServerHealthStatus.Loading, []);
        if ((e.Flags & EvServerHealth.FlagStepped) != 0)
            return (ServerHealthStatus.Stepped, []);

        var lost = Lost(e.WallMicroseconds, e.Ticks);
        var hostValid = (e.Flags & EvServerHealth.FlagHostValid) != 0;
        List<string>? bad = null, warn = null;

        void Check(string reason, bool isWarn, bool isBad)
        {
            if (isBad)
                (bad ??= []).Add(reason);
            else if (isWarn)
                (warn ??= []).Add(reason);
        }

        Check(LateTicks, e.LateTicks > 0, e.LateTicks >= BadTicks);
        Check(ShortTicks, e.ShortTicks > 0, false);
        Check(LostTicks, lost >= WarnLostTicks, lost >= BadTicks);
        Check(Cpu, e.CpuP99 >= WarnCpuP99, e.CpuP99 >= BadCpuP99);
        Check(PageFaults, e.MajorFaults > 0, false);
        Check(Steal, hostValid && e.HostSteal >= WarnSteal, hostValid && e.HostSteal >= BadSteal);

        if (bad != null)
            return (ServerHealthStatus.Bad, warn != null ? [.. bad, .. warn] : bad);

        return warn != null ? (ServerHealthStatus.Warn, warn) : (ServerHealthStatus.Good, []);
    }

    /// <summary>The window as the web API and the hub send it, received at <paramref name="time"/>.</summary>
    public static ServerHealthWindowDto ToWindow(EvServerHealth e, DateTimeOffset time)
    {
        var (status, reasons) = Rate(e);
        return new ServerHealthWindowDto(time, e.Tick, status, reasons, e.Ticks, e.WallMicroseconds, Lost(e.WallMicroseconds, e.Ticks),
            e.IntervalMedian, e.IntervalP99, e.IntervalMax, e.LateTicks, e.ShortTicks, e.CpuMedian, e.CpuP99, e.CpuMax, e.HandlerP99,
            e.HandlerMax, e.InvoluntarySwitches, e.MajorFaults, (e.Flags & EvServerHealth.FlagHostValid) != 0, e.HostCpus,
            e.HostBusy / 100.0, e.HostSteal / 100.0, e.HostIoWait / 100.0, e.ResidentKib);
    }
}

/// <summary>
/// Rates a player's connection over one ServerHealth window (a PlayerConnections entry; openbf2-server-ext
/// docs/admin-channel.md, "Server health"). The one place the rules are, like <see cref="ServerHealthRules"/>.
/// </summary>
public static class PlayerConnectionRules
{
    public const string LossToPlayer = "lossToPlayer", LossFromPlayer = "lossFromPlayer", PingSpread = "pingSpread", LowSendRate = "lowSendRate";

    /// <summary>The mean lost packets of a direction as a part of its packets.</summary>
    public const double WarnLoss = 0.01, BadLoss = 0.05;

    /// <summary>Lost packets per second in the worse of a window's readings: a burst that the mean hides.</summary>
    public const float WarnPeakLoss = 1.0f;

    /// <summary>The highest ping of the readings above the lowest, milliseconds.</summary>
    public const int WarnPingSpread = 40, BadPingSpread = 100;

    /// <summary>A client sends 10 or 30 packets a second, by its connection setting: fewer than this is the low rate.</summary>
    public const float LowSendRatePackets = 12;

    /// <summary>A full window has two readings; a player who joined during it has one, and its figures are too low.</summary>
    public const int FullReadings = 2;

    /// <summary>The lost packets of a direction as a part of its packets; 0 when there were no packets.</summary>
    public static double LossRatio(float lostPerSecond, float packetsPerSecond) =>
        packetsPerSecond > 0 ? (double)lostPerSecond / packetsPerSecond : 0;

    /// <summary>The entry's status and what made it Warn or Bad, the Bad reasons first.</summary>
    public static (PlayerConnectionStatus Status, IReadOnlyList<string> Reasons) Rate(AdminPlayerConnection c)
    {
        var lossTo = LossRatio(c.LostSent, c.PacketsSent);
        var lossFrom = LossRatio(c.LostReceived, c.PacketsReceived);
        var spread = c.PingMax - c.PingMin;
        List<string>? bad = null, warn = null;

        void Check(string reason, bool isWarn, bool isBad)
        {
            if (isBad)
                (bad ??= []).Add(reason);
            else if (isWarn)
                (warn ??= []).Add(reason);
        }

        Check(LossToPlayer, lossTo >= WarnLoss || c.LostSentPeak >= WarnPeakLoss, lossTo >= BadLoss);
        Check(LossFromPlayer, lossFrom >= WarnLoss || c.LostReceivedPeak >= WarnPeakLoss, lossFrom >= BadLoss);
        Check(PingSpread, spread >= WarnPingSpread, spread >= BadPingSpread);
        Check(LowSendRate, c.PacketsReceived < LowSendRatePackets, false);

        if (bad != null)
            return (PlayerConnectionStatus.Bad, warn != null ? [.. bad, .. warn] : bad);

        return warn != null ? (PlayerConnectionStatus.Warn, warn) : (PlayerConnectionStatus.Good, []);
    }

    /// <summary>
    /// The entry as the web API and the hub send it, with the player's name. A rate that is not a finite number of 0 or
    /// more counts as 0, as the module sends it (and JSON has no such numbers).
    /// </summary>
    public static PlayerConnectionDto ToDto(AdminPlayerConnection c, string? name)
    {
        c = c with
        {
            PacketsSent = Clean(c.PacketsSent), PacketsReceived = Clean(c.PacketsReceived), BytesSent = Clean(c.BytesSent),
            BytesReceived = Clean(c.BytesReceived), LostSent = Clean(c.LostSent), LostSentPeak = Clean(c.LostSentPeak),
            LostReceived = Clean(c.LostReceived), LostReceivedPeak = Clean(c.LostReceivedPeak),
        };
        var (status, reasons) = Rate(c);
        return new PlayerConnectionDto(c.Player, name, status, reasons, c.Readings < FullReadings, c.Readings, c.Ping, c.PingMin, c.PingMax,
            c.PacketsSent, c.PacketsReceived, c.BytesSent, c.BytesReceived, c.LostSent, c.LostSentPeak, c.LostReceived, c.LostReceivedPeak,
            Percent(LossRatio(c.LostSent, c.PacketsSent)), Percent(LossRatio(c.LostReceived, c.PacketsReceived)));

        static float Clean(float rate) => float.IsFinite(rate) && rate > 0 ? rate : 0;
        static double Percent(double ratio) => Math.Round(ratio * 100, 2);
    }
}

/// <summary>
/// The ServerHealth windows of every game server from the last six hours (<see cref="MaxWindows"/> windows of 10 s)
/// and the players' connections that come with each (PlayerConnections), by server id. Kept in memory only: a restart
/// of WebAdmin empties it. A server's windows outlive its admin links and a change of its settings, and go when the
/// server is removed. Every new window, and its connections after it, is also sent to the server's hub group, and
/// counted in the metrics (<see cref="ServerHealthMetrics"/>; only with a meter factory, which the host has).
/// </summary>
public sealed class ServerHealthStore
{
    public const int MaxWindows = 2160;

    private sealed class History
    {
        public readonly Queue<ServerHealthWindowDto> Windows = new();
        public readonly Queue<ServerConnectionsDto> Connections = new();
        public string? Name;
    }

    /// <summary>A server's latest window and connections, with its name as it was then; for the metrics.</summary>
    public sealed record LatestHealth(string ServerId, string? Name, ServerHealthWindowDto? Window, ServerConnectionsDto? Connections);

    private readonly ConcurrentDictionary<string, History> _servers = new();
    private readonly IHubContext<ServerHub, IServerHubClient>? _hub;
    private readonly ILogger<ServerHealthStore>? _logger;
    private readonly ServerHealthMetrics? _metrics;

    public ServerHealthStore(IHubContext<ServerHub, IServerHubClient>? hub = null, ILogger<ServerHealthStore>? logger = null,
        IMeterFactory? meterFactory = null, TimeProvider? timeProvider = null)
    {
        _hub = hub;
        _logger = logger;
        _metrics = meterFactory != null ? new ServerHealthMetrics(meterFactory, this, timeProvider ?? TimeProvider.System) : null;
    }

    /// <summary>
    /// Rates and keeps a window, received at <paramref name="time"/>, of the server named <paramref name="serverName"/>.
    /// Called from the session's apply loop.
    /// </summary>
    public async ValueTask<ServerHealthWindowDto> AddAsync(string serverId, EvServerHealth e, DateTimeOffset time, string? serverName = null)
    {
        var window = ServerHealthRules.ToWindow(e, time);
        var history = _servers.GetOrAdd(serverId, _ => new History());
        string? name;
        lock (history)
        {
            Keep(history.Windows, window);
            name = history.Name = serverName ?? history.Name;
        }

        _metrics?.OnWindow(serverId, name, window);
        await SendAsync(serverId, clients => clients.ServerHealthEvent(new ServerHealthEvent { ServerId = serverId, Window = window }));
        return window;
    }

    /// <summary>
    /// Rates and keeps the players' connections of a window (the window with the same tick), received at
    /// <paramref name="time"/>. <paramref name="nameOf"/> gives a player id's name now, or null: ids are reused, so the
    /// name is kept with the entry.
    /// </summary>
    public async ValueTask<ServerConnectionsDto> AddConnectionsAsync(string serverId, EvPlayerConnections e, Func<int, string?> nameOf,
        DateTimeOffset time)
    {
        var connections = new ServerConnectionsDto(time, e.Tick, e.Players.Select(p => PlayerConnectionRules.ToDto(p, nameOf(p.Player))).ToArray());
        var history = _servers.GetOrAdd(serverId, _ => new History());
        string? name;
        lock (history)
        {
            Keep(history.Connections, connections);
            name = history.Name;
        }

        _metrics?.OnConnections(serverId, name, connections);
        await SendAsync(serverId, clients => clients.ServerConnectionsEvent(new ServerConnectionsEvent { ServerId = serverId, Connections = connections }));
        return connections;
    }

    private static void Keep<T>(Queue<T> queue, T item)
    {
        queue.Enqueue(item);
        while (queue.Count > MaxWindows)
            queue.Dequeue();
    }

    private async ValueTask SendAsync(string serverId, Func<IServerHubClient, Task> send)
    {
        if (_hub == null)
            return;

        try
        {
            await send(_hub.Clients.Group(serverId));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Server health {ServerId}: send failed", serverId);
        }
    }

    /// <summary>
    /// The server's latest window, its windows received at or after <paramref name="since"/> (all of them without it),
    /// oldest first, and the latest connections.
    /// </summary>
    public ServerHealthDto Get(string serverId, DateTimeOffset? since = null)
    {
        if (!_servers.TryGetValue(serverId, out var history))
            return new ServerHealthDto(null, [], null);

        ServerHealthWindowDto[] all;
        ServerConnectionsDto? connections;
        lock (history)
        {
            all = history.Windows.ToArray();
            connections = history.Connections.LastOrDefault();
        }

        var windows = since is { } from ? Array.FindAll(all, w => w.Time >= from) : all;
        return new ServerHealthDto(all.Length > 0 ? all[^1] : null, windows, connections);
    }

    /// <summary>
    /// The players' connections per window received at or after <paramref name="since"/> (all that are kept without it),
    /// oldest first. A window's connections have its tick.
    /// </summary>
    public IReadOnlyList<ServerConnectionsDto> GetConnections(string serverId, DateTimeOffset? since = null)
    {
        if (!_servers.TryGetValue(serverId, out var history))
            return [];

        ServerConnectionsDto[] all;
        lock (history)
            all = history.Connections.ToArray();

        return since is { } from ? Array.FindAll(all, c => c.Time >= from) : all;
    }

    /// <summary>Every server's latest window and connections (null when it has none), with its name.</summary>
    public IReadOnlyList<LatestHealth> GetLatest()
    {
        var result = new List<LatestHealth>();
        foreach (var (serverId, history) in _servers)
        {
            lock (history)
                result.Add(new LatestHealth(serverId, history.Name, history.Windows.LastOrDefault(), history.Connections.LastOrDefault()));
        }

        return result;
    }

    public void Remove(string serverId) => _servers.TryRemove(serverId, out _);
}
