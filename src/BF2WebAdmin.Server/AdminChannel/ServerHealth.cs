using System.Collections.Concurrent;
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
/// The ServerHealth windows of every game server from the last six hours (<see cref="MaxWindows"/> windows of 10 s),
/// by server id. Kept in memory only: a restart of WebAdmin empties it. A server's windows outlive its admin links and
/// a change of its settings, and go when the server is removed. Every new window is also sent to the server's hub group.
/// </summary>
public sealed class ServerHealthStore(IHubContext<ServerHub, IServerHubClient>? hub = null, ILogger<ServerHealthStore>? logger = null)
{
    public const int MaxWindows = 2160;

    private readonly ConcurrentDictionary<string, Queue<ServerHealthWindowDto>> _servers = new();

    /// <summary>Rates and keeps a window, received at <paramref name="time"/>. Called from the session's apply loop.</summary>
    public async ValueTask<ServerHealthWindowDto> AddAsync(string serverId, EvServerHealth e, DateTimeOffset time)
    {
        var window = ServerHealthRules.ToWindow(e, time);
        var windows = _servers.GetOrAdd(serverId, _ => new Queue<ServerHealthWindowDto>());
        lock (windows)
        {
            windows.Enqueue(window);
            while (windows.Count > MaxWindows)
                windows.Dequeue();
        }

        if (hub != null)
        {
            try
            {
                await hub.Clients.Group(serverId).ServerHealthEvent(new ServerHealthEvent { ServerId = serverId, Window = window });
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Server health {ServerId}: send failed", serverId);
            }
        }

        return window;
    }

    /// <summary>The server's latest window and its windows received at or after <paramref name="since"/> (all of them without it), oldest first.</summary>
    public ServerHealthDto Get(string serverId, DateTimeOffset? since = null)
    {
        if (!_servers.TryGetValue(serverId, out var windows))
            return new ServerHealthDto(null, []);

        ServerHealthWindowDto[] all;
        lock (windows)
            all = windows.ToArray();

        var history = since is { } from ? Array.FindAll(all, w => w.Time >= from) : all;
        return new ServerHealthDto(all.Length > 0 ? all[^1] : null, history);
    }

    public void Remove(string serverId) => _servers.TryRemove(serverId, out _);
}
