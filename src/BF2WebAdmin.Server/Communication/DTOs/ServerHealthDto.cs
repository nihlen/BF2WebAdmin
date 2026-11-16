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

/// <summary>How a player's connection over one window is rated (<see cref="AdminChannel.PlayerConnectionRules"/>).</summary>
public enum PlayerConnectionStatus
{
    Good,
    Warn,
    Bad,
}

/// <summary>
/// A human player's connection over one ServerHealth window, with its rating. Name is the player's when WebAdmin
/// received the window (ids are reused), null if the player had left by then. Reasons names what made it Warn or Bad,
/// the Bad ones first: lossToPlayer, lossFromPlayer, pingSpread, lowSendRate. Partial: the player joined during the
/// window, so there is one reading instead of two and the figures are too low. Pings are in milliseconds; the packets,
/// bytes and lost packets are per second (the lost ones the mean of the readings, Peak the highest reading); the loss
/// percentages are the mean lost packets of the packets of that direction.
/// </summary>
public record PlayerConnectionDto(
    int PlayerId,
    string? Name,
    PlayerConnectionStatus Status,
    IReadOnlyList<string> Reasons,
    bool Partial,
    int Readings,
    int Ping,
    int PingMin,
    int PingMax,
    float PacketsToPlayer,
    float PacketsFromPlayer,
    float BytesToPlayer,
    float BytesFromPlayer,
    float LostToPlayer,
    float LostToPlayerPeak,
    float LostFromPlayer,
    float LostFromPlayerPeak,
    double LossToPlayerPercent,
    double LossFromPlayerPercent);

/// <summary>
/// The connections of the human players over one ServerHealth window: the window with the same Tick. Time is when
/// WebAdmin received it (UTC). No players when nobody is connected.
/// </summary>
public record ServerConnectionsDto(DateTimeOffset Time, uint Tick, IReadOnlyList<PlayerConnectionDto> Players);

/// <summary>
/// A server's health: the latest window (null when its module has sent none), the kept windows, oldest first, and the
/// players' connections of the latest window that came with them (null when its module has sent none).
/// </summary>
public record ServerHealthDto(ServerHealthWindowDto? Latest, IReadOnlyList<ServerHealthWindowDto> History, ServerConnectionsDto? Connections);

/// <summary>
/// A started 2v2 round of the server (Heli2v2), for the health panel to mark: its match, its number in the match from 1,
/// and when it started and ended (UTC); no end while it is played.
/// </summary>
public record HealthRoundDto(Guid MatchId, Guid RoundId, int Number, DateTimeOffset Start, DateTimeOffset? End);
