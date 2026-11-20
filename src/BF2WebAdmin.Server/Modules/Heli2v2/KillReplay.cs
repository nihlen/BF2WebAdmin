using BF2WebAdmin.Server.AdminChannel;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>One object of a replay: what to spawn, and six numbers per frame (x, y, z, yaw, pitch, roll).</summary>
public sealed record ReplayItem(string Template, bool Missile, uint SourceNetId, float[] Frames);

/// <summary>
/// A replay of a traced missile: <see cref="Frames"/> frames one tick apart, from the launch to the impact (the last
/// frame, which the replay holds), for the missile and the helicopters in it. <see cref="HitVehicle"/>: the missile's
/// first collision was a vehicle (<see cref="KillReplay.HitsVehicle"/>), not the terrain or a static object.
/// </summary>
public sealed record ReplayPlan(int Frames, IReadOnlyList<ReplayItem> Items, string Outcome, bool HitVehicle);

/// <summary>Builds the frames of a replay from a missile's trace (<see cref="ViewerFeed"/>) and the vehicles' poses.</summary>
public static class KillReplay
{
    /// <summary>
    /// The frames of <paramref name="missile"/>'s flight up to its impact, for the missile and each of
    /// <paramref name="helis"/> (root networkable id and template). A helicopter the trace has is placed where it was
    /// (<paramref name="tested"/>: where the server tested it, rewound by the shooter's lag); one it does not have (the
    /// shooter's own) comes from <paramref name="poseAt"/>, and is left out when that has no pose of it.
    /// </summary>
    public static ReplayPlan Build(MissileReplayDto missile, IReadOnlyList<(uint NetId, string Template)> helis,
        Func<uint, uint, (Vec3 Position, Vec3 Rotation)?> poseAt, bool tested = false)
    {
        var samples = missile.Samples;
        if (samples.Length == 0)
            return new ReplayPlan(0, [], missile.Summary.Outcome, false);

        // A collision in the sweep from sample k to k + 1 carries tick first + k: the frames are samples 0 to k, then the
        // impact (the missile at the collision point, the vehicles as sample k + 1 has them)
        var hit = missile.Collisions.FirstOrDefault();
        var last = hit == null ? samples.Length - 1 : Math.Clamp((int)(hit.Tick - missile.Summary.FirstTick), 0, samples.Length - 1);
        var impact = hit != null;
        var frames = last + 1 + (impact ? 1 : 0);
        var after = Math.Min(last + 1, samples.Length - 1);

        var items = new List<ReplayItem>();
        var missileFrames = new float[frames * 6];
        for (var k = 0; k <= last; k++)
            Put(missileFrames, k, samples[k].P, Heading(samples[k].V));
        if (impact)
            Put(missileFrames, frames - 1, hit!.P, Heading(samples[last].V));
        items.Add(new ReplayItem(missile.Summary.Template, true, missile.Summary.ProjectileId, missileFrames));

        foreach (var (netId, template) in helis)
        {
            var target = Array.FindIndex(missile.Summary.Targets, t => t.NetId == netId);
            var f = new float[frames * 6];
            var complete = true;
            for (var k = 0; k < frames; k++)
            {
                var sample = k <= last ? k : after;
                if (target >= 0 && samples[sample].T.Length > target)
                {
                    var pose = samples[sample].T[target];
                    var o = tested ? 6 : 0;
                    Put(f, k, [pose[o], pose[o + 1], pose[o + 2]], [pose[o + 3], pose[o + 4], pose[o + 5]]);
                }
                else if (poseAt(netId, missile.Summary.FirstTick + (uint)sample) is { } p)
                {
                    Put(f, k, [p.Position.X, p.Position.Y, p.Position.Z], [p.Rotation.X, p.Rotation.Y, p.Rotation.Z]);
                }
                else
                {
                    complete = false;
                    break;
                }
            }

            if (complete)
                items.Add(new ReplayItem(target >= 0 ? missile.Summary.Targets[target].Template : template, false, netId, f));
        }

        return new ReplayPlan(frames, items, missile.Summary.Outcome, HitsVehicle(missile));
    }

    /// <summary>
    /// Whether the missile's first collision was a vehicle: an object that the trace follows (every vehicle near the
    /// launch but the shooter's) or a part with a player in it. Not the terrain, a static object or no collision.
    /// </summary>
    public static bool HitsVehicle(MissileReplayDto missile) =>
        missile.Collisions.FirstOrDefault() is { Kind: EvProjectileCollision.KindObject } hit &&
        (hit.PartPlayer >= 0 || missile.Summary.Targets.Any(t => t.NetId == hit.Root));

    /// <summary>Yaw and pitch of a direction (roll 0), in the wire's convention: yaw 0 faces +z, positive pitch is nose down.</summary>
    public static float[] Heading(float[] v)
    {
        var length = MathF.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        if (length < 1e-3f)
            return [0, 0, 0];
        var yaw = MathF.Atan2(v[0], v[2]) * 180 / MathF.PI;
        var pitch = -MathF.Asin(Math.Clamp(v[1] / length, -1, 1)) * 180 / MathF.PI;
        return [yaw, pitch, 0];
    }

    private static void Put(float[] f, int frame, float[] position, float[] rotation)
    {
        // Centimetres and tenths of a degree are plenty, and keep the script's arguments small
        for (var n = 0; n < 3; n++)
        {
            f[frame * 6 + n] = MathF.Round(position[n], 2);
            f[frame * 6 + 3 + n] = MathF.Round(rotation[n], 1);
        }
    }
}
