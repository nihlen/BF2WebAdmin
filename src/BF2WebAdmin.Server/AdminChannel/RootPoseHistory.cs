namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>Where a seated player's root object (their vehicle) was at the end of a tick.</summary>
public readonly record struct RootPose(uint Tick, int PlayerId, uint RootNetId, string Template, Vec3 Position, Vec3 Rotation);

/// <summary>
/// The last <see cref="KeptTicks"/> ticks of the player stream's seated players, for a replay of a kill: a missile's
/// trace has every vehicle near it but the shooter's own. The stream runs at the tracker rate (every tick while a 3D
/// viewer is open), so <see cref="PoseAt"/> blends between the frames it has. Written by the admin channel's apply loop
/// and read by the modules, so under a lock.
/// </summary>
public sealed class RootPoseHistory
{
    /// <summary>30 seconds.</summary>
    public const int KeptTicks = 900;

    private readonly object _lock = new();
    private readonly Queue<(uint Tick, RootPose[] Poses)> _frames = new();

    public void Add(uint tick, IEnumerable<RootPose> poses)
    {
        var frame = poses.ToArray();
        lock (_lock)
        {
            // A tick that goes back is a new link or a new level: the old frames are of another clock
            if (_frames.Count > 0 && tick <= _frames.Last().Tick)
                _frames.Clear();
            _frames.Enqueue((tick, frame));
            while (_frames.Count > 0 && tick - _frames.Peek().Tick > KeptTicks)
                _frames.Dequeue();
        }
    }

    /// <summary>The root object a player sat in at the last frame at or before <paramref name="tick"/>, or null.</summary>
    public RootPose? PlayerRootAt(int playerId, uint tick)
    {
        lock (_lock)
        {
            RootPose? found = null;
            foreach (var (frameTick, poses) in _frames)
            {
                if (frameTick > tick)
                    break;
                found = null;
                foreach (var pose in poses)
                    if (pose.PlayerId == playerId)
                        found = pose;
            }
            return found;
        }
    }

    /// <summary>
    /// A root object's position and rotation at a tick, blended between the frames around it (yaw and roll the short way
    /// round). Before its first frame or after its last it is where that frame has it, if that is at most
    /// <paramref name="maxGapTicks"/> away; null when there is no frame of it that close.
    /// </summary>
    public (Vec3 Position, Vec3 Rotation)? PoseAt(uint rootNetId, uint tick, uint maxGapTicks = 30)
    {
        RootPose? before = null, after = null;
        lock (_lock)
        {
            foreach (var (frameTick, poses) in _frames)
            {
                foreach (var pose in poses)
                {
                    if (pose.RootNetId != rootNetId)
                        continue;
                    if (frameTick <= tick)
                        before = pose;
                    else if (after == null)
                        after = pose;
                    break;
                }
                if (after != null)
                    break;
            }
        }

        if (before is { } b && after is { } a)
        {
            var u = (float)(tick - b.Tick) / (a.Tick - b.Tick);
            return (Lerp(b.Position, a.Position, u),
                new Vec3(Angle(b.Rotation.X, a.Rotation.X, u), b.Rotation.Y + (a.Rotation.Y - b.Rotation.Y) * u, Angle(b.Rotation.Z, a.Rotation.Z, u)));
        }

        if (before is { } only && tick - only.Tick <= maxGapTicks)
            return (only.Position, only.Rotation);
        if (after is { } next && next.Tick - tick <= maxGapTicks)
            return (next.Position, next.Rotation);
        return null;
    }

    private static Vec3 Lerp(Vec3 a, Vec3 b, float u) => new(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, a.Z + (b.Z - a.Z) * u);

    private static float Angle(float a, float b, float u) => a + ((((b - a) % 360) + 540) % 360 - 180) * u;
}
