using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>.replay: the frames of a kill's replay (KillReplay), the vehicles' recent poses (RootPoseHistory) and the module.</summary>
public class ReplayTests
{
    private const string ServerId = "127.0.0.1:16569";

    // ---------------------------------------------------------------- KillReplay

    /// <summary>
    /// A missile flying along +z at 4 m per tick past a target (1799) that moves 1 m east per tick and is tested 6 m
    /// behind in z; it hits in the sweep after sample <paramref name="hitSample"/>.
    /// </summary>
    private static MissileReplayDto Missile(int samples = 10, int? hitSample = 5, uint firstTick = 5000, int shooter = 2, int key = 1,
        int kind = EvProjectileCollision.KindObject, uint root = 1799, int partPlayer = -1)
    {
        var summary = new MissileSummaryDto(key, 1852, "agm114_hellfire_tv", shooter, "shooter", 1, "ahe_z10", firstTick, samples, 0,
            hitSample == null ? "miss" : "hit", null, null, -1, false, 150, [new MissileTargetDto(1799, "ahe_ah1z", [], 1, 1, true)],
            DateTimeOffset.UtcNow);
        var sampleDtos = Enumerable.Range(0, samples).Select(k => new MissileSampleDto([0, 100, 4 * k], [0, 0, 120], 0.15f,
            [[k, 100, 20, 90, 0, 0, k, 100, 14, 90, 0, 0]])).ToArray();
        var collisions = hitSample is { } h
            ? new[] { new MissileCollisionDto(firstTick + (uint)h, kind, 1, root, "ahe_ah1z", "ahe_ah1z", partPlayer, [1, 100, 21], [-1, 0, 0],
                [0, 0, 120], 56, 0.15f, [], [], [], []) }
            : [];
        return new MissileReplayDto(summary, sampleDtos, collisions);
    }

    private static float[] Frame(ReplayItem item, int frame) => item.Frames.Skip(frame * 6).Take(6).ToArray();

    [Fact]
    public void Build_PlaysTheMissileToItsImpact_WithTheTracedHelicopterWhereItWas()
    {
        var plan = KillReplay.Build(Missile(), [(1799, "ahe_ah1z")], (_, _) => null);

        // Samples 0 to 5, then the impact
        Assert.Equal(7, plan.Frames);
        var missile = plan.Items[0];
        Assert.True(missile.Missile);
        Assert.Equal("agm114_hellfire_tv", missile.Template);
        Assert.Equal([0, 100, 20, 0, 0, 0], Frame(missile, 5));
        Assert.Equal([1, 100, 21, 0, 0, 0], Frame(missile, 6));

        var heli = plan.Items[1];
        Assert.Equal(("ahe_ah1z", 1799u), (heli.Template, heli.SourceNetId));
        Assert.Equal([5, 100, 20, 90, 0, 0], Frame(heli, 5));
        // At the impact the helicopter is where the sample after the hit has it
        Assert.Equal([6, 100, 20, 90, 0, 0], Frame(heli, 6));
    }

    [Fact]
    public void Build_PlacesTheTracedHelicoptersWhereTheServerTestedThem_WhenAskedTo()
    {
        var plan = KillReplay.Build(Missile(), [(1799, "ahe_ah1z")], (_, _) => null, tested: true);
        Assert.Equal([5, 100, 14, 90, 0, 0], Frame(plan.Items[1], 5));
    }

    [Fact]
    public void Build_TakesTheShootersHelicopterFromThePoses_AndLeavesOutOneWithout()
    {
        var asked = new List<(uint, uint)>();
        var plan = KillReplay.Build(Missile(), [(1797, "ahe_z10"), (1800, "ahe_ah1z")], (root, tick) =>
        {
            asked.Add((root, tick));
            return root == 1797 ? (new Vec3(-50, 120, tick - 5000), new Vec3(10, 0, 0)) : null;
        });

        Assert.Equal(2, plan.Items.Count);
        var shooter = plan.Items[1];
        Assert.Equal("ahe_z10", shooter.Template);
        Assert.Equal([-50, 120, 3, 10, 0, 0], Frame(shooter, 3));
        // The impact frame is the tick after the hit's sweep
        Assert.Equal([-50, 120, 6, 10, 0, 0], Frame(shooter, 6));
        Assert.Contains((1800u, 5000u), asked);
    }

    [Fact]
    public void Build_PlaysAMissThroughToItsLastSample()
    {
        var plan = KillReplay.Build(Missile(samples: 8, hitSample: null), [], (_, _) => null);
        Assert.Equal(8, plan.Frames);
        Assert.Equal([0, 100, 28, 0, 0, 0], Frame(plan.Items.Single(), 7));
    }

    [Fact]
    public void Build_TellsAVehicleHit_FromTheTerrainAStaticObjectOrNothing()
    {
        Assert.True(KillReplay.Build(Missile(), [], (_, _) => null).HitVehicle);
        // A vehicle the trace does not follow, with a player in the part it hit
        Assert.True(KillReplay.HitsVehicle(Missile(root: 2000, partPlayer: 4)));
        Assert.False(KillReplay.HitsVehicle(Missile(kind: EvProjectileCollision.KindTerrain, root: 0)));
        // A static object: no vehicle of the trace, nobody in it
        Assert.False(KillReplay.HitsVehicle(Missile(root: 0)));
        Assert.False(KillReplay.Build(Missile(hitSample: null), [], (_, _) => null).HitVehicle);
    }

    [Fact]
    public void Heading_IsTheWiresYawAndPitch()
    {
        Assert.Equal([0, 0, 0], KillReplay.Heading([0, 0, 120]));
        Assert.Equal(90, KillReplay.Heading([120, 0, 0])[0], 3);
        Assert.Equal(-90, KillReplay.Heading([-1, 0, 0])[0], 3);
        // Nose down is positive pitch
        Assert.Equal(45, KillReplay.Heading([0, -1, 1])[1], 3);
        Assert.Equal([0, 0, 0], KillReplay.Heading([0, 0, 0]));
    }

    // ---------------------------------------------------------------- RootPoseHistory

    [Fact]
    public void Poses_AreBlendedBetweenFrames_YawTheShortWayRound()
    {
        var history = new RootPoseHistory();
        history.Add(100, [new RootPose(100, 3, 1797, "ahe_z10", new Vec3(0, 100, 0), new Vec3(170, 0, 0))]);
        history.Add(108, [new RootPose(108, 3, 1797, "ahe_z10", new Vec3(8, 100, 0), new Vec3(-170, 0, 0))]);

        var pose = history.PoseAt(1797, 104)!.Value;
        Assert.Equal(4, pose.Position.X, 3);
        Assert.Equal(180, Math.Abs(pose.Rotation.X), 3);

        Assert.Equal(1797u, history.PlayerRootAt(3, 105)!.Value.RootNetId);
        Assert.Null(history.PlayerRootAt(3, 99));
        Assert.Null(history.PoseAt(1797, 200));
        Assert.Equal(8, history.PoseAt(1797, 120)!.Value.Position.X, 3);

        // A tick that goes back is another clock: the old frames go
        history.Add(50, []);
        Assert.Null(history.PoseAt(1797, 104));
    }

    [Fact]
    public void Poses_KeepThirtySeconds()
    {
        var history = new RootPoseHistory();
        history.Add(100, [new RootPose(100, 3, 1797, "ahe_z10", default, default)]);
        history.Add(100 + RootPoseHistory.KeptTicks + 1, []);
        Assert.Null(history.PlayerRootAt(3, 100));
    }

    // ---------------------------------------------------------------- the module

    private sealed class FakeRun(string name, string args, byte api) : IScriptRun
    {
        private readonly TaskCompletionSource<ScriptResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public uint Id => 1;
        public string Name { get; } = name;
        public string Args { get; } = args;
        public byte Api { get; } = api;
        public bool IsRunning => !_finished.Task.IsCompleted;
        public Task<ScriptResult> Finished => _finished.Task;
        public Task SendAsync(object message) => Task.CompletedTask;
        public Task StopAsync() { End(); return Task.CompletedTask; }
        public void End() => _finished.TrySetResult(new ScriptResult(ScriptEndReason.Finished, "{}", 1, TimeSpan.Zero, TimeSpan.Zero));
    }

    private sealed class FakeRunner : IScriptRunner
    {
        public bool Available { get; set; } = true;
        public List<FakeRun> Started { get; } = [];

        public Task<IScriptRun> StartAsync(string name, object? args = null, byte api = CmdStartScript.Api)
        {
            var run = new FakeRun(name, JsonSerializer.Serialize(args), api);
            Started.Add(run);
            return Task.FromResult<IScriptRun>(run);
        }
    }

    private sealed class InlineTaskRunner : ITaskRunner
    {
        public Task Last { get; private set; } = Task.CompletedTask;
        public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct) => Last = func();
    }

    private static (ReplayModule Module, IGameServer Server, FakeRunner Scripts, InlineTaskRunner Tasks, BF2WebAdmin.Server.Entities.GameServer Real) Module()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(new ViewerRegistry()).BuildServiceProvider();
        var real = new BF2WebAdmin.Server.Entities.GameServer(new BF2WebAdmin.Server.ServerInfo { ServerId = ServerId }, IPAddress.Loopback,
            services, CancellationToken.None);
        var server = Substitute.For<IGameServer>();
        var scripts = new FakeRunner();
        server.Scripts.Returns(scripts);
        server.Viewer.Returns(real.Viewer);
        server.Id.Returns(ServerId);
        var next = 1850u;
        server.GameWriter.SpawnObjectAsync(default!, default!, default!).ReturnsForAnyArgs(_ =>
        {
            next++;
            return Task.FromResult(new ObjectSpawned(0, 29000 + next, next));
        });
        var tasks = new InlineTaskRunner();
        var module = new ReplayModule(server, tasks, NullLogger<ReplayModule>.Instance, new CancellationTokenSource());
        return (module, server, scripts, tasks, real);
    }

    private static ReplayCommand Replay(Player player) => new() { Message = new Message { Player = player } };

    private static EvProjectileTrace Trace(uint firstTick, int shooter) =>
        new(1, firstTick + 6, 1852, "agm114_hellfire_tv", (short)shooter, firstTick, EvProjectileTrace.FlagCollided,
            [new AdminTraceTarget(1799, "ahe_ah1z")],
            [.. Enumerable.Range(0, 6).Select(k => new AdminTraceSample(new Vec3(0, 100, 4 * k), new Vec3(0, 0, 120), 0.15f,
                [new AdminTracePose(new Vec3(3, 100, 20), new Vec3(90, 0, 0), new Vec3(3, 100, 14), new Vec3(90, 0, 0))]))]);

    [Fact]
    public async Task WithoutAMatch_ItReplaysThePlayersOwnLastMissile_ThenRemovesIt()
    {
        var (module, server, scripts, tasks, real) = Module();
        var player = new Player { Index = 2, Name = "gunner", Hash = "h2" };
        real.Viewer.OnCollision(new EvProjectileCollision(1, 5003, 1852, "agm114_hellfire_tv", 2, 1, 1, 1799, 29312, "ahe_ah1z", "ahe_ah1z",
            -1, new Vec3(3, 100, 14), new Vec3(-1, 0, 0), new Vec3(0, 0, 120), 56, 0.15f, default, default, default, default));
        real.Viewer.OnTrace(Trace(5000, 2), DateTimeOffset.UtcNow);

        module.Handle(new ReplaySpeedCommand { Speed = 0.5, Message = new Message { Player = player } });
        var run = Assert.Single(scripts.Started);
        Assert.Equal((ReplayModule.ReplayScript, CmdStartScript.ApiObjects), (run.Name, run.Api));

        using var args = JsonDocument.Parse(run.Args);
        Assert.Equal(0.5, args.RootElement.GetProperty("speed").GetDouble());
        Assert.Equal(ReplayModule.HoldTicks, args.RootElement.GetProperty("hold").GetInt32());
        // Samples 0 to 3, then the impact; the missile alone, at its first sample
        Assert.Equal(5, args.RootElement.GetProperty("frames").GetInt32());
        var only = Assert.Single(args.RootElement.GetProperty("objects").EnumerateArray());
        Assert.True(only.GetProperty("missile").GetBoolean());
        Assert.Equal(1851u, only.GetProperty("id").GetUInt32());
        await server.GameWriter.Received(1).SpawnObjectAsync("agm114_hellfire_tv", Arg.Is<Position>(p => p.Height == 100),
            Arg.Any<Rotation>(), 0);
        server.GameWriter.DidNotReceiveWithAnyArgs().SetObjectCollision(default, default);

        // One at a time
        module.Handle(Replay(player));
        server.GameWriter.Received(1).SendPrivateMessage(player, "A replay is running");
        Assert.Single(scripts.Started);

        // The objects go when it ends
        server.GameWriter.DidNotReceiveWithAnyArgs().SendDeleteObject(default);
        run.End();
        await tasks.Last;
        server.GameWriter.Received(1).SendDeleteObject(30851);

        // And the next one may start
        module.Handle(Replay(player));
        Assert.Equal(2, scripts.Started.Count);
    }

    [Fact]
    public void AMissileThatHitTheGround_IsHeldTwoSeconds()
    {
        var (module, _, scripts, _, real) = Module();
        var player = new Player { Index = 2, Name = "gunner", Hash = "h2" };
        real.Viewer.OnCollision(new EvProjectileCollision(1, 5003, 1852, "agm114_hellfire_tv", 2, EvProjectileCollision.KindTerrain, 0, 0, 0,
            "", "", -1, new Vec3(0, 100, 14), new Vec3(0, 1, 0), new Vec3(0, 0, 120), 3, 0.15f, default, default, default, default));
        real.Viewer.OnTrace(Trace(5000, 2), DateTimeOffset.UtcNow);

        module.Handle(Replay(player));
        using var args = JsonDocument.Parse(Assert.Single(scripts.Started).Args);
        Assert.Equal(ReplayModule.MissHoldTicks, args.RootElement.GetProperty("hold").GetInt32());
    }

    [Fact]
    public void ItRefuses_ASpeedOutOfRange_NoMissile_OrAServerWithoutScripts()
    {
        var (module, server, scripts, _, _) = Module();
        var player = new Player { Index = 2, Name = "gunner", Hash = "h2" };

        module.Handle(new ReplaySpeedCommand { Speed = 10, Message = new Message { Player = player } });
        server.GameWriter.Received(1).SendPrivateMessage(player, "Replay speed is 0.05 to 4 (1 is real time)");

        module.Handle(Replay(player));
        server.GameWriter.Received(1).SendPrivateMessage(player, "You have no TV missile to replay");

        scripts.Available = false;
        module.Handle(Replay(player));
        server.GameWriter.Received(1).SendPrivateMessage(player, "This server cannot run replays");
        Assert.Empty(scripts.Started);
    }
}
