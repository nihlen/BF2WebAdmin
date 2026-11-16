namespace BF2WebAdmin.Server.Communication.DTOs;

/// <summary>
/// How a ServerHealth window is rated (<see cref="AdminChannel.ServerHealthRules"/>). Loading (a level load fell in the
/// window) and Stepped (stepped time is on) windows say nothing about how the server runs and are left out of statistics.
/// </summary>
public enum ServerHealthStatus
{
    Good,
    Warn,
    Bad,
    Loading,
    Stepped,
}

/// <summary>
/// One ServerHealth window of a game server (10 s; openbf2-server-ext docs/admin-channel.md, "Server health"), with its
/// rating. Time is when WebAdmin received it (UTC). Reasons names what made it Warn or Bad, the Bad ones first:
/// lateTicks, shortTicks, lostTicks, cpu, pageFaults, steal. The host percentages are of all the host's CPUs and only
/// meaningful with HostValid.
/// </summary>
public record ServerHealthWindowDto(
    DateTimeOffset Time,
    uint Tick,
    ServerHealthStatus Status,
    IReadOnlyList<string> Reasons,
    int Ticks,
    uint WallUs,
    int LostTicks,
    uint IntervalMedianUs,
    uint IntervalP99Us,
    uint IntervalMaxUs,
    int LateTicks,
    int ShortTicks,
    uint CpuMedianUs,
    uint CpuP99Us,
    uint CpuMaxUs,
    uint HandlerP99Us,
    uint HandlerMaxUs,
    uint InvoluntarySwitches,
    uint MajorFaults,
    bool HostValid,
    int HostCpus,
    double HostBusyPercent,
    double HostStealPercent,
    double HostIoWaitPercent,
    uint ResidentKib);

/// <summary>
/// A server's health: the latest window (null when its module has sent none) and the kept windows, oldest first.
/// </summary>
public record ServerHealthDto(ServerHealthWindowDto? Latest, IReadOnlyList<ServerHealthWindowDto> History);
