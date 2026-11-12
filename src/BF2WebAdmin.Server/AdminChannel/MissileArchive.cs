namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>A traced TV missile's crew member: CD-key hash (the player key), name and team when the missile flew.</summary>
public sealed record MissileCrewDto(string Hash, string Name, int Team);

/// <summary>
/// What the export adds to a traced missile (<see cref="MissileReplayDto"/>): where it was fired (server, map, the 2v2
/// round), when, and the players by their CD-key hashes. Per target in the same order as the replay's targets.
/// </summary>
public sealed record MissileContext(
    string ServerId,
    string Map,
    Guid? MatchId,
    Guid? RoundId,
    int RoundNumber,
    DateTime LaunchTime,
    string ShooterHash,
    uint ShooterRoot,
    string? HitPlayerHash,
    IReadOnlyList<MissileCrewDto[]> TargetCrews,
    IReadOnlyList<int> TargetTeams);

/// <summary>A missile in the archive: its feed position (<see cref="Seq"/>), the replay and its context.</summary>
public sealed record ArchivedMissile(long Seq, MissileReplayDto Replay, MissileContext Context);

/// <summary>
/// The traced TV missiles of every server from the last <see cref="Retention"/>, in memory, for the export API's missile
/// feed (/api/export/v1/missiles). The sequence starts again at 1 with a new <see cref="Epoch"/> when WebAdmin starts:
/// nothing is stored, so a reader that is away longer than the retention misses missiles.
/// </summary>
public class MissileArchive(TimeProvider timeProvider)
{
    public static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);

    /// <summary>A bound on memory (about 30 KB per missile) for a burst of missiles within the retention.</summary>
    public const int MaxMissiles = 1000;

    private readonly object _lock = new();
    private readonly Queue<(DateTimeOffset Added, ArchivedMissile Missile)> _missiles = new();
    private long _lastSeq;

    /// <summary>This process's archive; the sequence numbers are only comparable within one epoch.</summary>
    public Guid Epoch { get; } = Guid.NewGuid();

    /// <summary>Adds a missile and gives it the next sequence number. Numbers are given in the order missiles become readable.</summary>
    public ArchivedMissile Add(MissileReplayDto replay, MissileContext context)
    {
        lock (_lock)
        {
            var missile = new ArchivedMissile(++_lastSeq, replay, context);
            _missiles.Enqueue((timeProvider.GetUtcNow(), missile));
            Evict();
            return missile;
        }
    }

    /// <summary>The missiles after <paramref name="since"/>, oldest first, at most <paramref name="limit"/>.</summary>
    public (IReadOnlyList<ArchivedMissile> Items, bool HasMore) GetSince(long since, int limit)
    {
        lock (_lock)
        {
            Evict();
            var newer = _missiles.Select(m => m.Missile).Where(m => m.Seq > since);
            var page = newer.Take(limit + 1).ToList();
            return (page.Take(limit).ToList(), page.Count > limit);
        }
    }

    private void Evict()
    {
        var oldest = timeProvider.GetUtcNow() - Retention;
        while (_missiles.Count > 0 && (_missiles.Count > MaxMissiles || _missiles.Peek().Added < oldest))
            _missiles.Dequeue();
    }
}
