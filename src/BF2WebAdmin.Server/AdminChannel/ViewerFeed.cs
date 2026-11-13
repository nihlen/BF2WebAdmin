using System.Collections.Concurrent;
using System.Threading.Channels;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.AdminChannel;

// ---------------------------------------------------------------- DTOs (hub and API)

// Vectors are [x, y, z] in the wire's BF2 coordinates (y up); rotations are [yaw, pitch, roll] in degrees.

/// <summary>One player in a viewer frame: PlayerState, all players every frame.</summary>
public sealed record ViewerPlayerDto(int Id, int Team, int Life, int Flags, uint Root, int Seat, int View, int Lag, int Ping,
    float[] P, float[] R, float[] V);

/// <summary>One vehicle root in a viewer frame. Only the vehicles that changed, except in keyframes, which have all.</summary>
public sealed record ViewerVehicleDto(uint Id, string Template, int Team, float Health, float[] P, float[] R);

/// <summary>A living player's camera (and active weapon) transform.</summary>
public sealed record ViewerAimDto(int Id, int Flags, float[] Cp, float[] Cr, float[] Wp, float[] Wr);

public sealed record ViewerProjectileDto(uint Id, string Template, int Owner, float[] P, float[] R, float[] V);

/// <summary>
/// One admin channel stream frame for the 3D viewer, sent as it arrives: exactly one of the lists is set. Frames of
/// the same tick come from different streams; the module may drop a stream's frame under load (latest wins).
/// </summary>
public sealed record ViewerFrameDto(uint Tick, double Time, bool Keyframe, ViewerPlayerDto[]? Players, ViewerVehicleDto[]? Vehicles,
    ViewerAimDto[]? Aims, ViewerProjectileDto[]? Projectiles);

/// <summary>
/// A target of a missile: a vehicle root that came within 150 m, its crew, how close the missile came to its origin,
/// and whether it moved during the flight (parked, empty vehicles are targets too).
/// </summary>
public sealed record MissileTargetDto(uint NetId, string Template, string[] Crew, float ClosestTested, float ClosestCurrent, bool Moved);

/// <summary>A traced missile in the viewer's list (the last <see cref="ViewerFeed.MaxMissiles"/> per server).</summary>
public sealed record MissileSummaryDto(int Key, uint ProjectileId, string Template, int Shooter, string ShooterName, int ShooterTeam,
    string ShooterVehicle, uint FirstTick, int Samples, int Flags, string Outcome, string? HitPart, string? HitRoot, int HitPlayer,
    bool HitRewound, int LagMs, MissileTargetDto[] Targets, DateTimeOffset Time);

/// <summary>
/// One tick of a missile's flight: the missile, the shooter's lag in seconds and per target 12 values: position and
/// rotation now ("red"), then position and rotation as this tick's sweep tests it ("blue").
/// </summary>
public sealed record MissileSampleDto(float[] P, float[] V, float Lag, float[][] T);

public sealed record MissileCollisionDto(uint Tick, int Kind, int Flags, uint Root, string RootTemplate, string PartTemplate, int PartPlayer,
    float[] P, float[] N, float[] Speed, int Material, float Latency, float[] PartP, float[] PartR, float[] RootP, float[] RootR);

public sealed record MissileReplayDto(MissileSummaryDto Summary, MissileSampleDto[] Samples, MissileCollisionDto[] Collisions);

// ---------------------------------------------------------------- viewers

/// <summary>
/// Which hub connections have a 3D viewer open on which server. While a server has one, its admin channel streams run at
/// 30 Hz and its frames go to the viewer group (<see cref="GroupName"/>).
/// </summary>
public class ViewerRegistry
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _viewers = new();

    /// <summary>A server got its first viewer (true) or lost its last (false).</summary>
    public event Action<string, bool>? ActiveChanged;

    public static string GroupName(string serverId) => "viewer:" + serverId;

    public bool IsActive(string serverId) => _viewers.TryGetValue(serverId, out var set) && Count(set) > 0;

    public void Add(string connectionId, string serverId)
    {
        var set = _viewers.GetOrAdd(serverId, _ => []);
        bool first;
        lock (set)
            first = set.Add(connectionId) && set.Count == 1;
        if (first)
            ActiveChanged?.Invoke(serverId, true);
    }

    public void Remove(string connectionId, string serverId)
    {
        if (!_viewers.TryGetValue(serverId, out var set))
            return;
        bool last;
        lock (set)
            last = set.Remove(connectionId) && set.Count == 0;
        if (last)
            ActiveChanged?.Invoke(serverId, false);
    }

    public void RemoveConnection(string connectionId)
    {
        foreach (var serverId in _viewers.Keys)
            Remove(connectionId, serverId);
    }

    private static int Count(HashSet<string> set)
    {
        lock (set)
            return set.Count;
    }
}

/// <summary>
/// A server's 3D viewer feed: forwards the 30 Hz stream frames to the viewer group while a viewer is open, and keeps the
/// last <see cref="MaxMissiles"/> traced missiles (ProjectileTrace with its ProjectileCollisions) for the missile list.
/// The On* methods run on the session's apply loop; sending to the hub runs on its own task, so a slow browser never
/// holds up the admin channel.
/// </summary>
public class ViewerFeed
{
    public const int MaxMissiles = 20;
    private const int MaxPendingCollisions = 64;
    private const int KeyframeTicks = 30;

    /// <summary>The game's tick rate: a trace's samples are one tick apart</summary>
    private const float TickRate = 30;

    private readonly GameServer _server;
    private readonly ViewerRegistry? _registry;
    private readonly MissileArchive? _archive;
    private readonly IHubContext<ServerHub, IServerHubClient>? _hub;
    private readonly ILogger _logger;
    private readonly Channel<object> _outbox = Channel.CreateBounded<object>(new BoundedChannelOptions(256)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });

    private readonly Dictionary<uint, (ViewerVehicleDto Dto, Vec3 Position, Vec3 Rotation)> _vehicles = new();
    private uint _lastVehicleKeyframe;
    private bool _vehicleKeyframeDue = true;

    private readonly object _missileLock = new();
    private readonly List<MissileReplayDto> _missiles = new();
    private readonly Dictionary<uint, List<EvProjectileCollision>> _pendingCollisions = new();
    // Oldest first. A list taken by its trace stays queued until evicted, so eviction removes only the list it queued: the
    // module reuses networkable ids (1851 and 1852 for every TV missile on a 2v2 server), and evicting by id alone threw
    // away the newest projectile's collisions
    private readonly Queue<(uint Id, List<EvProjectileCollision> List)> _pendingOrder = new();
    // The crew of a vehicle when a projectile hit it: a trace ends when the missile's time to live runs out, by when a
    // crew it killed is gone
    private readonly Dictionary<uint, MissileCrewDto[]> _crewAtHit = new();
    private int _nextKey = 1;

    public ViewerFeed(GameServer server, ViewerRegistry? registry, IHubContext<ServerHub, IServerHubClient>? hub, ILogger logger,
        CancellationToken cancellationToken, MissileArchive? archive = null)
    {
        _server = server;
        _registry = registry;
        _archive = archive;
        _hub = hub;
        _logger = logger;
        if (_hub != null)
            _ = Task.Run(() => SendLoopAsync(cancellationToken), cancellationToken);
    }

    /// <summary>True while a viewer is open on this server: the streams run at 30 Hz.</summary>
    public bool IsActive => _registry?.IsActive(_server.Id) ?? false;

    /// <summary>A new viewer needs every vehicle; also after a reconnect.</summary>
    public void RequestKeyframe() => _vehicleKeyframeDue = true;

    // ---------------------------------------------------------------- stream frames

    public void OnPlayers(PlayerStream stream)
    {
        if (!IsActive)
            return;
        var players = stream.Players.Select(p => new ViewerPlayerDto(p.Id, p.State.Team, p.State.Life, p.State.Flags, p.State.RootNetId,
            p.State.Seat, p.State.ViewMode, p.State.LagMs, p.State.Ping, Pos(p.State.Position), Rot(p.State.Rotation), Pos(p.State.Velocity))).ToArray();
        Post(new ViewerFrameDto(stream.Tick, stream.Time, false, players, null, null, null));
    }

    public void OnVehicles(VehicleStream stream)
    {
        if (!IsActive)
            return;

        // Pooled entries (no armour, at 0/0/0) are not drawn. Between keyframes only the vehicles that moved are sent
        var keyframe = _vehicleKeyframeDue || stream.Tick - _lastVehicleKeyframe >= KeyframeTicks;
        var list = new List<ViewerVehicleDto>();
        var seen = new HashSet<uint>();
        foreach (var v in stream.Vehicles)
        {
            if (v.NetId == 0 || v.Health < 0 || v.Position is { X: 0, Y: 0, Z: 0 })
                continue;
            seen.Add(v.NetId);
            var changed = !_vehicles.TryGetValue(v.NetId, out var last) || last.Position != v.Position || last.Rotation != v.Rotation ||
                          last.Dto.Team != v.Team || Math.Abs(last.Dto.Health - v.Health) > 0.001f;
            if (!changed && !keyframe)
                continue;
            var dto = new ViewerVehicleDto(v.NetId, v.Template, v.Team, MathF.Round(v.Health, 3), Pos(v.Position), Rot(v.Rotation));
            _vehicles[v.NetId] = (dto, v.Position, v.Rotation);
            list.Add(dto);
        }

        foreach (var gone in _vehicles.Keys.Where(id => !seen.Contains(id)).ToList())
            _vehicles.Remove(gone);

        if (keyframe)
        {
            _lastVehicleKeyframe = stream.Tick;
            _vehicleKeyframeDue = false;
        }

        if (keyframe || list.Count > 0)
            Post(new ViewerFrameDto(stream.Tick, stream.Time, keyframe, null, list.ToArray(), null, null));
    }

    public void OnAims(AimStream stream)
    {
        if (!IsActive)
            return;
        var aims = stream.Aims.Select(a => new ViewerAimDto(a.Player, a.Flags, Pos(a.CameraPosition), Rot(a.CameraRotation),
            Pos(a.WeaponPosition), Rot(a.WeaponRotation))).ToArray();
        Post(new ViewerFrameDto(stream.Tick, stream.Time, false, null, null, aims, null));
    }

    public void OnProjectiles(ProjectileStream stream)
    {
        if (!IsActive)
            return;
        var projectiles = stream.Projectiles.Select(p => new ViewerProjectileDto(p.Id, p.Template, p.Owner, Pos(p.Position), Rot(p.Rotation),
            Pos(p.Velocity))).ToArray();
        Post(new ViewerFrameDto(stream.Tick, stream.Time, false, null, null, null, projectiles));
    }

    // ---------------------------------------------------------------- missiles

    public void OnCollision(EvProjectileCollision e)
    {
        var crew = e.RootNetId != 0 ? CrewOf(e.RootNetId) : [];
        lock (_missileLock)
        {
            if (crew.Length > 0)
            {
                _crewAtHit[e.RootNetId] = crew;
                if (_crewAtHit.Count > MaxPendingCollisions)
                    _crewAtHit.Remove(_crewAtHit.Keys.First());
            }

            if (!_pendingCollisions.TryGetValue(e.Id, out var list))
            {
                list = [];
                _pendingCollisions[e.Id] = list;
                _pendingOrder.Enqueue((e.Id, list));
                while (_pendingOrder.Count > MaxPendingCollisions)
                {
                    var (oldId, oldList) = _pendingOrder.Dequeue();
                    if (_pendingCollisions.TryGetValue(oldId, out var current) && ReferenceEquals(current, oldList))
                        _pendingCollisions.Remove(oldId);
                }
            }

            if (list.Count < 16)
                list.Add(e);
        }
    }

    public void OnTrace(EvProjectileTrace e, DateTimeOffset time)
    {
        List<EvProjectileCollision> collisions;
        int key;
        lock (_missileLock)
        {
            // Collisions come before their trace; a networkable id may be reused by a later projectile
            collisions = _pendingCollisions.Remove(e.Id, out var pending)
                ? pending.Where(c => c.Tick + 1 >= e.FirstTick && c.Tick <= e.FirstTick + e.Samples.Count + 1).ToList()
                : [];
            key = _nextKey++;
        }

        var (replay, context) = BuildReplay(key, e, collisions, time);
        lock (_missileLock)
        {
            _missiles.Add(replay);
            if (_missiles.Count > MaxMissiles)
                _missiles.RemoveRange(0, _missiles.Count - MaxMissiles);
        }

        _archive?.Add(replay, context);
        Post(replay.Summary);
    }

    /// <summary>The kept missiles, newest first.</summary>
    public IReadOnlyList<MissileSummaryDto> Missiles
    {
        get
        {
            lock (_missileLock)
                return _missiles.Select(m => m.Summary).Reverse().ToList();
        }
    }

    public MissileReplayDto? GetMissile(int key)
    {
        lock (_missileLock)
            return _missiles.FirstOrDefault(m => m.Summary.Key == key);
    }

    public void ClearMissiles()
    {
        lock (_missileLock)
        {
            _missiles.Clear();
            _pendingCollisions.Clear();
            _pendingOrder.Clear();
            _crewAtHit.Clear();
        }
    }

    private (MissileReplayDto Replay, MissileContext Context) BuildReplay(int key, EvProjectileTrace e, List<EvProjectileCollision> collisions,
        DateTimeOffset time)
    {
        var shooter = e.Shooter >= 0 ? _server.FindPlayer(e.Shooter) : null;
        var targets = new MissileTargetDto[e.Targets.Count];
        var crews = new MissileCrewDto[e.Targets.Count][];
        var teams = new int[e.Targets.Count];
        for (var t = 0; t < targets.Length; t++)
        {
            var target = e.Targets[t];
            var crew = CrewOf(target.NetId);
            if (crew.Length == 0 && collisions.Any(c => c.RootNetId == target.NetId))
                lock (_missileLock)
                    crew = _crewAtHit.GetValueOrDefault(target.NetId, []);
            crews[t] = crew;
            teams[t] = crew.FirstOrDefault()?.Team ?? _server.Vehicles.FirstOrDefault(v => v.RootVehicleId == (int)target.NetId)?.Team ?? 0;
            targets[t] = new MissileTargetDto(target.NetId, target.Template, crew.Select(c => c.Name).ToArray(),
                Round(Closest(e.Samples, t, tested: true)), Round(Closest(e.Samples, t, tested: false)), Moved(e.Samples, t));
        }

        // The projectile stops at its first collision with an object (a direct hit) or the terrain
        var hit = collisions.FirstOrDefault();
        var outcome = hit == null
            ? (e.Flags & EvProjectileTrace.FlagCut) != 0 ? "cut" : "miss"
            : hit.Kind == EvProjectileCollision.KindTerrain ? "terrain" : "hit";

        var lag = e.Samples.Count > 0 ? e.Samples[0].Lag : 0;
        var summary = new MissileSummaryDto(key, e.Id, e.Template, e.Shooter, shooter?.Name ?? (e.Shooter >= 0 ? $"#{e.Shooter}" : ""),
            shooter?.Team?.Id ?? 0, shooter?.Vehicle?.RootVehicleTemplate ?? "", e.FirstTick, e.Samples.Count, e.Flags, outcome,
            hit?.Kind == EvProjectileCollision.KindObject ? hit.PartTemplate : null,
            hit?.Kind == EvProjectileCollision.KindObject ? hit.RootTemplate : null,
            hit?.PartPlayer ?? -1, hit != null && (hit.Flags & EvProjectileCollision.FlagRewound) != 0,
            (int)MathF.Round(lag * 1000), targets, time);

        var samples = e.Samples.Select(s => new MissileSampleDto(Pos(s.Position), Pos(s.Velocity), MathF.Round(s.Lag, 4),
            s.Targets.Select(p => (float[])[.. Pos(p.Position), .. Rot(p.Rotation), .. Pos(p.TestedPosition), .. Rot(p.TestedRotation)]).ToArray())).ToArray();

        var collisionDtos = collisions.Select(c => new MissileCollisionDto(c.Tick, c.Kind, c.Flags, c.RootNetId, c.RootTemplate, c.PartTemplate,
            c.PartPlayer, Pos(c.Position), Pos(c.Normal), Pos(c.Speed), c.Material, MathF.Round(c.Latency, 4), Pos(c.PartPosition),
            Rot(c.PartRotation), Pos(c.RootPosition), Rot(c.RootRotation))).ToArray();

        // The trace comes when the flight ends: the launch was its sample count of ticks earlier
        var launch = (time - TimeSpan.FromSeconds(Math.Max(0, (long)e.Tick - e.FirstTick) / TickRate)).UtcDateTime;
        var round = _server.ModManager?.GetModule<Heli2v2Module>()?.FindRound(launch);
        var hitPlayer = hit is { PartPlayer: >= 0 } ? _server.FindPlayer(hit.PartPlayer) : null;
        var context = new MissileContext(_server.Id, _server.Map?.Name ?? "", round?.MatchId, round?.RoundId, round?.Number ?? 0, launch,
            shooter?.Hash ?? "", (uint)Math.Max(shooter?.Vehicle?.RootVehicleId ?? 0, 0), hitPlayer?.Hash, crews, teams);

        return (new MissileReplayDto(summary, samples, collisionDtos), context);
    }

    /// <summary>
    /// The closest the missile's path came to a target's origin, tested pose or current pose. The sweep from sample k to
    /// k + 1 meets the target at sample k's tested pose.
    /// </summary>
    public static float Closest(IReadOnlyList<AdminTraceSample> samples, int target, bool tested)
    {
        var best = float.MaxValue;
        for (var k = 0; k < samples.Count; k++)
        {
            var pose = samples[k].Targets[target];
            var point = tested ? pose.TestedPosition : pose.Position;
            var a = samples[k].Position;
            var b = k + 1 < samples.Count ? samples[k + 1].Position : a;
            best = Math.Min(best, SegmentDistance(a, b, point));
        }
        return best == float.MaxValue ? -1 : best;
    }

    /// <summary>Whether a target moved during the flight, or was tested anywhere but where it was</summary>
    public static bool Moved(IReadOnlyList<AdminTraceSample> samples, int target)
    {
        if (samples.Count == 0)
            return false;
        var first = samples[0].Targets[target].Position;
        foreach (var s in samples)
        {
            var pose = s.Targets[target];
            if (Distance(pose.Position, first) > 0.25f || Distance(pose.TestedPosition, pose.Position) > 0.25f)
                return true;
        }
        return false;
    }

    private static float Distance(Vec3 a, Vec3 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private MissileCrewDto[] CrewOf(uint rootNetId) =>
        _server.Players.Where(p => p.Vehicle?.RootVehicleId == (int)rootNetId)
            .Select(p => new MissileCrewDto(p.Hash ?? "", p.Name, p.Team?.Id ?? 0)).ToArray();

    private static float SegmentDistance(Vec3 a, Vec3 b, Vec3 p)
    {
        float abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z;
        float apx = p.X - a.X, apy = p.Y - a.Y, apz = p.Z - a.Z;
        var len2 = abx * abx + aby * aby + abz * abz;
        var u = len2 > 0 ? Math.Clamp((apx * abx + apy * aby + apz * abz) / len2, 0, 1) : 0;
        float dx = apx - u * abx, dy = apy - u * aby, dz = apz - u * abz;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // ---------------------------------------------------------------- sending

    private void Post(object message) => _outbox.Writer.TryWrite(message);

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var group = ViewerRegistry.GroupName(_server.Id);
        try
        {
            await foreach (var message in _outbox.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var clients = _hub!.Clients.Group(group);
                    if (message is ViewerFrameDto frame)
                        await clients.ViewerFrame(frame);
                    else if (message is MissileSummaryDto missile)
                        await clients.MissileEvent(missile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Viewer {ServerId}: send failed", _server.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static float Round(float value) => value < 0 ? value : MathF.Round(value, 2);

    private static float[] Pos(Vec3 v) => [MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3)];

    private static float[] Rot(Vec3 v) => [MathF.Round(v.X, 2), MathF.Round(v.Y, 2), MathF.Round(v.Z, 2)];
}
