using System;
using System.Linq;
using System.Net;
using System.Threading;
using BF2WebAdmin.Server.AdminChannel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>The 3D viewer's missile store and frames (AdminChannel/ViewerFeed.cs).</summary>
public class ViewerFeedTests
{
    private const string ServerId = "127.0.0.1:16569";

    private static (BF2WebAdmin.Server.Entities.GameServer Server, ViewerRegistry Viewers) Server()
    {
        var viewers = new ViewerRegistry();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(viewers)
            .BuildServiceProvider();
        var info = new BF2WebAdmin.Server.ServerInfo { ServerId = ServerId };
        return (new BF2WebAdmin.Server.Entities.GameServer(info, IPAddress.Loopback, services, CancellationToken.None), viewers);
    }

    /// <summary>A missile flying along +z at 4 m per tick past a target at x = 3 whose tested pose trails it by 6 m in z.</summary>
    private static EvProjectileTrace Trace(uint id, uint firstTick, int samples = 10, byte flags = 0) =>
        new(1, firstTick + (uint)samples, id, "agm114_hellfire_tv", 2, firstTick, flags,
            [new AdminTraceTarget(1799, "ahe_ah1z")],
            [.. Enumerable.Range(0, samples).Select(k => new AdminTraceSample(new Vec3(0, 100, 4 * k), new Vec3(0, 0, 120), 0.15f,
                [new AdminTracePose(new Vec3(3, 100, 20), new Vec3(90, 0, 0), new Vec3(3, 100, 14), new Vec3(90, 0, 0))]))]);

    private static EvProjectileCollision Collision(uint id, uint tick, byte kind, byte flags) =>
        new(1, tick, id, "agm114_hellfire_tv", 2, kind, flags, kind == 0 ? 0u : 1799, 29312, kind == 0 ? "" : "ahe_ah1z",
            kind == 0 ? "" : "agm114_hellfiredummy", -1, new Vec3(1, 100, 14), new Vec3(-1, 0, 0), new Vec3(0, 0, 120), 56, 0.15f,
            default, default, default, default);

    [Fact]
    public void ClosestApproachIsMeasuredAlongThePath()
    {
        var trace = Trace(1852, 5000);
        Assert.Equal(3f, ViewerFeed.Closest(trace.Samples, 0, tested: true), 3);
        Assert.Equal(3f, ViewerFeed.Closest(trace.Samples, 0, tested: false), 3);

        // A short flight that ends before reaching the current pose: the end point is the closest
        var shortTrace = Trace(1852, 5000, samples: 3);
        Assert.Equal(MathF.Sqrt(9 + 12 * 12), ViewerFeed.Closest(shortTrace.Samples, 0, tested: false), 3);
    }

    [Fact]
    public void TraceTakesItsCollisionsAndNamesTheOutcome()
    {
        var (server, _) = Server();
        server.Viewer.OnCollision(Collision(1852, 5003, EvProjectileCollision.KindObject, EvProjectileCollision.FlagRewound));
        server.Viewer.OnTrace(Trace(1852, 5000, flags: EvProjectileTrace.FlagCollided), DateTimeOffset.UtcNow);
        server.Viewer.OnTrace(Trace(1853, 6000), DateTimeOffset.UtcNow);
        server.Viewer.OnCollision(Collision(1854, 7002, EvProjectileCollision.KindTerrain, 0));
        server.Viewer.OnTrace(Trace(1854, 7000, flags: EvProjectileTrace.FlagCollided), DateTimeOffset.UtcNow);

        var missiles = server.Viewer.Missiles;
        Assert.Equal([3, 2, 1], missiles.Select(m => m.Key));
        Assert.Equal(["terrain", "miss", "hit"], missiles.Select(m => m.Outcome));

        var hit = missiles[2];
        Assert.Equal("agm114_hellfiredummy", hit.HitPart);
        Assert.Equal("ahe_ah1z", hit.HitRoot);
        Assert.True(hit.HitRewound);
        Assert.Equal(150, hit.LagMs);
        Assert.Equal("#2", hit.ShooterName);
        Assert.Equal(3f, hit.Targets.Single().ClosestTested, 2);
        // The target trails its tested pose by 6 m: it moved
        Assert.True(hit.Targets.Single().Moved);

        var replay = server.Viewer.GetMissile(1)!;
        Assert.Single(replay.Collisions);
        Assert.Equal(10, replay.Samples.Length);
        Assert.Equal([3f, 100f, 20f, 90f, 0f, 0f, 3f, 100f, 14f, 90f, 0f, 0f], replay.Samples[0].T[0]);
        Assert.Empty(server.Viewer.GetMissile(2)!.Collisions);
    }

    [Fact]
    public void CollisionOfAnEarlierProjectileWithTheSameIdIsNotTaken()
    {
        var (server, _) = Server();
        server.Viewer.OnCollision(Collision(1852, 100, EvProjectileCollision.KindObject, 0));
        server.Viewer.OnTrace(Trace(1852, 5000), DateTimeOffset.UtcNow);
        Assert.Equal("miss", server.Viewer.Missiles.Single().Outcome);
    }

    [Fact]
    public void ReusedIdsKeepTheirCollisionsInALongSession()
    {
        // The play server reuses networkable ids 1851 and 1852 for every TV missile. A taken collision list left its
        // id in the eviction queue, and once the queue was full a new collision evicted its own id: the hit became a miss
        var (server, _) = Server();
        for (var i = 0; i < 300; i++)
        {
            var id = (uint)(1851 + i % 2);
            var first = (uint)(1000 * (i + 1));
            server.Viewer.OnCollision(Collision(id, first + 30, EvProjectileCollision.KindObject, EvProjectileCollision.FlagRoot));
            server.Viewer.OnTrace(Trace(id, first, samples: 46, flags: EvProjectileTrace.FlagCollided), DateTimeOffset.UtcNow);
        }

        Assert.All(server.Viewer.Missiles, m => Assert.Equal("hit", m.Outcome));
    }

    [Fact]
    public void ParkedTargetDidNotMove()
    {
        var parked = new EvProjectileTrace(1, 10, 1852, "agm114_hellfire_tv", 2, 0, 0, [new AdminTraceTarget(1817, "ahe_z10")],
            [.. Enumerable.Range(0, 5).Select(k => new AdminTraceSample(new Vec3(0, 100, 4 * k), default, 0.15f,
                [new AdminTracePose(new Vec3(30, 145, 10), new Vec3(0, 0, 0), new Vec3(30, 145, 10), new Vec3(0, 0, 0))]))]);
        Assert.False(ViewerFeed.Moved(parked.Samples, 0));
    }

    [Fact]
    public void KeepsTheLastTwentyMissiles()
    {
        var (server, _) = Server();
        for (var i = 0; i < 25; i++)
            server.Viewer.OnTrace(Trace((uint)(2000 + i), (uint)(1000 * i)), DateTimeOffset.UtcNow);

        var missiles = server.Viewer.Missiles;
        Assert.Equal(ViewerFeed.MaxMissiles, missiles.Count);
        Assert.Equal(25, missiles[0].Key);
        Assert.Equal(6, missiles[^1].Key);
        Assert.Null(server.Viewer.GetMissile(5));
        Assert.NotNull(server.Viewer.GetMissile(6));
    }

    [Fact]
    public void ViewerIsActiveWhileAConnectionWatches()
    {
        var (server, viewers) = Server();
        var changes = 0;
        viewers.ActiveChanged += (id, _) => changes += id == ServerId ? 1 : 0;

        Assert.False(server.Viewer.IsActive);
        viewers.Add("a", ServerId);
        viewers.Add("b", ServerId);
        Assert.True(server.Viewer.IsActive);
        viewers.Remove("a", ServerId);
        Assert.True(server.Viewer.IsActive);
        viewers.RemoveConnection("b");
        Assert.False(server.Viewer.IsActive);
        Assert.Equal(2, changes);
    }
}
