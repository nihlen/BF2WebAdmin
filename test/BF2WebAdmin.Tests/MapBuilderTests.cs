using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Builder;
using BF2WebAdmin.Server.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// The map builder (<see cref="MapBuilder"/>, docs/map-builder.md) against a fake module over loopback TCP: what it
/// sends to make, move and remove items, and when it resyncs clients.
/// </summary>
public class MapBuilderTests
{
    private const string ServerId = "127.0.0.1:16569";
    private const byte Capabilities = Welcome.CapConsole | Welcome.CapObjects | Welcome.CapIdentity;
    private static readonly ConcurrentBag<TcpClient> KeepAlive = new();
    private static readonly AdminGameState Game = new(1, 3, -1, 100, 100, 10, 0, 0, 0);
    private static readonly double[] Origin = [100, 150, 200];
    private static readonly double[] NoTurn = [0, 0, 0];

    private sealed class FakeModule(Stream stream)
    {
        private readonly AdminFrameDecoder _decoder = new(AdminProtocol.MaxOutboundPayload);

        public Task SendAsync(AdminMessage m) => stream.WriteAsync(AdminProtocol.Encode(m)).AsTask();

        public async Task<AdminMessage> ReadAnyAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var buffer = new byte[65536];
            while (true)
            {
                while (_decoder.TryRead(out var type, out var payload))
                {
                    var message = AdminProtocol.Decode(type, payload);
                    if (message is Subscribe or Unsubscribe or Ping or Pong or RequestSnapshot)
                        continue;
                    return message;
                }

                var n = await stream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                    throw new EndOfStreamException();
                _decoder.Append(buffer.AsSpan(0, n));
            }
        }

        public async Task<T> ReadAsync<T>() where T : AdminMessage => Assert.IsType<T>(await ReadAnyAsync());

        /// <summary>
        /// Answers the builder's check whether the level uses the template (LevelTemplates): with an object the level
        /// placed, or with none.
        /// </summary>
        public async Task AnswerOnLevelAsync(string template, bool onLevel = true)
        {
            // After any console lines (the map's setup in ConnectAsync)
            var (_, next) = await ReadConsoleAsync();
            var list = Assert.IsType<CmdListObjects>(next);
            Assert.Equal(template, list.Template);
            await SendAsync(new ObjectList(list.RequestId, 0,
                onLevel ? [new ObjectListEntry(12, 0, template, new Vec3(-300, 160, 420), new Vec3(0, 0, 0), 1, 0)] : []));
        }

        /// <summary>Console lines up to the next command of another type, which is returned.</summary>
        public async Task<(string[] Lines, AdminMessage Next)> ReadConsoleAsync()
        {
            var lines = new System.Collections.Generic.List<string>();
            while (true)
            {
                var m = await ReadAnyAsync();
                if (m is not CmdConsole console)
                    return (lines.ToArray(), m);
                lines.Add(console.Line);
            }
        }
    }

    private sealed record Link(GameServer Server, FakeModule Module);

    private static AdminPlayerInfo Human(byte id, byte life = AdminPlayerState.LifeAlive) =>
        new(id, $"player{id}", AdminPlayerInfo.FlagConnected | AdminPlayerInfo.FlagIdentity, 1, "10.0.0.7", "0123456789abcdef0123456789abcdef",
            new AdminPlayerScore(0, 0, 0, 0, 0, 0),
            new AdminPlayerState(2, 0, life, 0, 20, 0, 0, 0, new Vec3(0, 150, 0), new Vec3(0, 0, 0), new Vec3(0, 0, 0), 1, 1, 0));

    private static async Task<Link> ConnectAsync(byte capabilities, AdminPlayerInfo[]? players = null, AdminControlPointState[]? controlPoints = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new TcpClient();
        var connect = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var accepted = await listener.AcceptTcpClientAsync();
        await connect;
        listener.Stop();
        KeepAlive.Add(client);
        KeepAlive.Add(accepted);

        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var server = new GameServer(new ServerInfo { ServerId = ServerId }, IPAddress.Loopback, services, CancellationToken.None);
        var session = new AdminSession(ServerId, (IPEndPoint)client.Client.LocalEndPoint!, accepted.GetStream(), new AdminFrameDecoder(), server.Applier,
            NullLogger.Instance, CancellationToken.None);
        server.AttachSession(session);
        _ = session.RunAsync();

        var module = new FakeModule(client.GetStream());
        await module.SendAsync(new Welcome(1, 1, 99, 30, "fake", 16569, 29901, 16, capabilities, "dalian_2_v_2", "gpm_cq", 16, 0,
            [new MapListEntry("dalian_2_v_2", "gpm_cq", 16)], "CH", "US", Game));
        await module.SendAsync(new Snapshot(0, 0, 100, 3.3, Game, players ?? [], [], controlPoints ?? [], []));
        await WaitAsync(() => session.Capabilities == capabilities && server.Players.Count() == (players?.Length ?? 0));

        // Without modules the Welcome stops before the map (UpdateServerInfoAsync has no module manager to publish to);
        // the console lines UpdateMapAsync sends come before any the tests read
        await server.UpdateMapAsync(new BF2WebAdmin.Common.Entities.Game.Map { Name = "dalian_2_v_2", GameMode = "gpm_cq", Size = 16 }, "CH", "US",
            DateTimeOffset.UtcNow);
        server.SetControlPoints(controlPoints ?? []);
        Assert.Equal("dalian_2_v_2", server.Builder.GetState().Map);
        return new Link(server, module);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    /// <summary>Adds a static object, answering its spawn with console id <paramref name="consoleId"/>.</summary>
    private static async Task<BuilderApplyResult> AddObjectAsync(Link link, string template, uint consoleId)
    {
        var builder = link.Server.Builder;
        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.Object, template, 0, Origin, NoTurn)], [], []), "test");
        await link.Module.AnswerOnLevelAsync(template);
        var (lines, next) = await link.Module.ReadConsoleAsync();
        Assert.Contains($"objecttemplate.active {template}", lines);
        Assert.Contains("objecttemplate.setNetworkableInfo StaticObjectInfo", lines);
        var spawn = Assert.IsType<CmdSpawnObject>(next);
        Assert.Equal(template, spawn.Template);
        Assert.Equal(new Vec3(100, 150, 200), spawn.Position);
        await link.Module.SendAsync(new ObjectSpawned(spawn.RequestId, consoleId, 40000 + consoleId));
        return await applying;
    }

    [Fact]
    public async Task AnObjectIsSpawnedWithAServerOnlyNetworkable()
    {
        var link = await ConnectAsync(Capabilities);
        var result = await AddObjectAsync(link, "house_01", 900);

        Assert.Empty(result.Errors);
        var item = Assert.Single(result.State.Items);
        Assert.Equal(("house_01", MapModObjectKind.Object, true), (item.Template, item.Kind, item.Spawned));
        Assert.Equal([item.Id], result.Added);
    }

    [Fact]
    public async Task ATemplateWithoutNetworkableIsDeletedAgainAndReported()
    {
        var link = await ConnectAsync(Capabilities);
        var builder = link.Server.Builder;
        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.Object, "lamp", 0, Origin, NoTurn)], [], []), "test");
        await link.Module.AnswerOnLevelAsync("lamp");
        var (_, next) = await link.Module.ReadConsoleAsync();
        await link.Module.SendAsync(new ObjectSpawned(((CmdSpawnObject)next).RequestId, 77, 0));

        var delete = await link.Module.ReadAsync<CmdDeleteObject>();
        Assert.Equal(ObjectRef.Console(77), delete.Target);
        var result = await applying;
        Assert.Empty(result.State.Items);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ItemsBuiltBeforeTheFirstRoundStartAreKept()
    {
        // A module that loads a layout when the link comes up can be quicker than the builder's first round start
        var link = await ConnectAsync(Capabilities);
        await AddObjectAsync(link, "house_01", 900);

        var starting = link.Server.Builder.OnRoundStartAsync("dalian_2_v_2");
        var list = Assert.IsType<CmdListObjects>((await link.Module.ReadConsoleAsync()).Next);
        Assert.Equal("house_01", list.Template);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(900, 40900, "house_01", new Vec3(100, 150, 200), default, 1, 0)]));
        await starting;

        var item = Assert.Single(link.Server.Builder.GetState().Items);
        Assert.True(item.Spawned);
    }

    [Fact]
    public async Task ATemplateTheLevelDoesNotUseIsNotSpawned()
    {
        var link = await ConnectAsync(Capabilities);
        var builder = link.Server.Builder;
        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.Object, "coolingtower_01", 0, Origin, NoTurn)], [], []), "test");
        await link.Module.AnswerOnLevelAsync("coolingtower_01", onLevel: false);

        var result = await applying;
        Assert.Empty(result.State.Items);
        Assert.Contains("level has no coolingtower_01", Assert.Single(result.Errors));

        // The answer is kept for the map: the next try sends nothing at all
        result = await builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.Object, "coolingtower_01", 0, Origin, NoTurn)], [], []), "test");
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task OnlyObjectsTheLevelPlacedMakeATemplateOneItUses()
    {
        var link = await ConnectAsync(Capabilities);
        var added = await AddObjectAsync(link, "house_01", 900);
        Assert.Empty(added.Errors);

        var filtering = link.Server.Builder.FilterTemplatesAsync(["house_01", "barrel", "crate"], CancellationToken.None);
        // house_01 is known from the spawn above; barrel has only the builder's own object and one in the object cache
        // at the origin; crate has one the level placed
        for (var i = 0; i < 2; i++)
        {
            var list = Assert.IsType<CmdListObjects>((await link.Module.ReadConsoleAsync()).Next);
            ObjectListEntry[] objects = list.Template == "barrel"
                ? [new ObjectListEntry(900, 0, "barrel", new Vec3(100, 150, 200), default, 1, 0), new ObjectListEntry(950, 0, "barrel", new Vec3(0, 0, 0), default, 1, 0)]
                : [new ObjectListEntry(30, 0, "crate", new Vec3(-20, 155, 80), default, 1, 0)];
            await link.Module.SendAsync(new ObjectList(list.RequestId, 0, objects));
        }

        Assert.Equal(["house_01", "crate"], await filtering);
    }

    [Fact]
    public async Task RemovingNeedsAModuleThatResyncs()
    {
        var link = await ConnectAsync(Capabilities);
        var added = await AddObjectAsync(link, "house_01", 900);

        var builder = link.Server.Builder;
        var e = await Assert.ThrowsAsync<BuilderException>(() => builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [], [], [added.Added[0]]), "test"));
        Assert.False(e.Conflict);
        Assert.Single(builder.Items);
    }

    [Fact]
    public async Task RemovingDeletesOnTheServerAndResyncsEachHumanClient()
    {
        var link = await ConnectAsync(Capabilities | Welcome.CapResync, [Human(3)]);
        var added = await AddObjectAsync(link, "house_01", 900);

        var builder = link.Server.Builder;
        var result = await builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [], [], [added.Added[0]]), "test");
        Assert.Empty(result.State.Items);

        var delete = await link.Module.ReadAsync<CmdDeleteObject>();
        Assert.Equal(ObjectRef.Console(900), delete.Target);
        var resync = await link.Module.ReadAsync<CmdResyncClient>();
        Assert.Equal(3, resync.Player);
        await link.Module.SendAsync(new OkReply(resync.RequestId));
        await WaitAsync(() => builder.GetState().PendingResync.Count == 0);
    }

    [Fact]
    public async Task DroppingAllForARestartDeletesWithoutAResync_AndTheRoundStartSpawnsNothingAgain()
    {
        var link = await ConnectAsync(Capabilities | Welcome.CapResync, [Human(3)]);
        await AddObjectAsync(link, "house_01", 900);
        var builder = link.Server.Builder;

        await builder.DropAllAsync("vote");
        var delete = await link.Module.ReadAsync<CmdDeleteObject>();
        Assert.Equal(ObjectRef.Console(900), delete.Target);
        var state = builder.GetState();
        Assert.Empty(state.Items);
        Assert.Empty(state.PendingResync);
        Assert.Null(builder.Layout);

        await builder.OnRoundStartAsync("dalian_2_v_2");
        Assert.Empty(builder.GetState().Items);
    }

    [Fact]
    public async Task ADeadPlayersResyncWaitsUntilItIsAlive()
    {
        var link = await ConnectAsync(Capabilities | Welcome.CapResync, [Human(3, AdminPlayerState.LifeDead)]);
        var added = await AddObjectAsync(link, "house_01", 900);
        var builder = link.Server.Builder;
        await builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [], [], [added.Added[0]]), "test");
        await link.Module.ReadAsync<CmdDeleteObject>();

        await Task.Delay(600);
        var pending = Assert.Single(builder.GetState().PendingResync);
        Assert.Equal((3, "dead"), (pending.Index, pending.Blocker));

        link.Server.FindPlayer(3)!.IsAlive = true;
        var resync = await link.Module.ReadAsync<CmdResyncClient>();
        Assert.Equal(3, resync.Player);
        await link.Module.SendAsync(new OkReply(resync.RequestId));
    }

    [Fact]
    public async Task MovingIsRemovingAndSpawningAgain()
    {
        var link = await ConnectAsync(Capabilities | Welcome.CapResync);
        var added = await AddObjectAsync(link, "house_01", 900);
        var builder = link.Server.Builder;

        var moving = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [], [new BuilderUpdateDto(added.Added[0], [1, 2, 3], [90, 0, 0])], []), "test");
        Assert.Equal(ObjectRef.Console(900), (await link.Module.ReadAsync<CmdDeleteObject>()).Target);
        var (_, next) = await link.Module.ReadConsoleAsync();
        var spawn = Assert.IsType<CmdSpawnObject>(next);
        Assert.Equal((new Vec3(1, 2, 3), new Vec3(90, 0, 0)), (spawn.Position, spawn.Rotation));
        await link.Module.SendAsync(new ObjectSpawned(spawn.RequestId, 901, 40901));

        var item = Assert.Single((await moving).State.Items);
        Assert.Equal(added.Added[0], item.Id);
        Assert.Equal([1.0, 2, 3], item.Position);
    }

    [Fact]
    public async Task AnApplyAgainstAnOldRevisionIsAConflict()
    {
        var link = await ConnectAsync(Capabilities);
        await AddObjectAsync(link, "house_01", 900);

        var e = await Assert.ThrowsAsync<BuilderException>(() => link.Server.Builder.ApplyAsync(new BuilderApplyRequest(0, [], [], []), "test"));
        Assert.True(e.Conflict);
    }

    [Fact]
    public async Task ASpawnPointJoinsTheGroupOfItsTeamsNearestControlPoint()
    {
        AdminControlPointState Cp(uint id, string template, byte team, float x) => new(id, 0, template, team, team, 0, 0, new Vec3(x, 150, 200));
        var link = await ConnectAsync(Capabilities, controlPoints: [Cp(1, "Z10", 1, 110), Cp(2, "AH1Z", 2, 900), Cp(3, "AH1Z_far", 2, 3000)]);
        var builder = link.Server.Builder;

        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.SpawnPoint, "", 2, Origin, [45, 0, 0])], [], []), "test");
        // Team 2's nearest control point, not team 1's even nearer one
        // The getter is a console line too, answered with its output
        string? previous = null;
        CmdConsole getter;
        while ((getter = await link.Module.ReadAsync<CmdConsole>()).Line != "ObjectTemplate.controlPointId")
            previous = getter.Line;
        Assert.Equal("ObjectTemplate.activeSafe ControlPoint AH1Z", previous);
        await link.Module.SendAsync(new ConsoleResult(getter.RequestId, 0, ["401"]));

        var (lines, next) = await link.Module.ReadConsoleAsync();
        Assert.Contains("ObjectTemplate.setControlPointId 401", lines);
        // Alone in a group of its own (in the control point's group 0 it would be one of its points at random)
        Assert.Contains($"ObjectTemplate.group {BuilderSpawning.FirstGroup}", lines);
        Assert.DoesNotContain("ObjectTemplate.setEnterOnSpawn 1", lines);
        Assert.Contains("Object.absolutePosition 100.000/150.000/200.000", lines);
        Assert.Contains("Object.rotation 45/0/0", lines);
        var template = lines.Single(l => l.StartsWith("Object.create ")).Split(' ')[1];
        var list = Assert.IsType<CmdListObjects>(next);
        Assert.Equal(template, list.Template);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(555, 0, template, new Vec3(100, 150, 200), new Vec3(45, 0, 0), -1, 0)]));

        var item = Assert.Single((await applying).State.Items);
        Assert.Equal((MapModObjectKind.SpawnPoint, 2, "spawnpoint"), (item.Kind, item.Team, item.Template));
        // A new group gets its team at the next round start
        Assert.False(item.SpawnReady);
    }

    /// <summary>Adds a spawn point near team 2's control point (id 401), answering the module; returns the console lines.</summary>
    private static async Task<(BuilderApplyResult Result, string[] Lines)> AddSpawnPointAsync(Link link, uint consoleId, bool askedId)
    {
        var builder = link.Server.Builder;
        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.SpawnPoint, "", 2, Origin, NoTurn)], [], []), "test");
        if (askedId)
        {
            CmdConsole getter;
            while ((getter = await link.Module.ReadAsync<CmdConsole>()).Line != "ObjectTemplate.controlPointId")
            {
            }
            await link.Module.SendAsync(new ConsoleResult(getter.RequestId, 0, ["401"]));
        }

        var (lines, next) = await link.Module.ReadConsoleAsync();
        var list = Assert.IsType<CmdListObjects>(next);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(consoleId, 0, list.Template, new Vec3(100, 150, 200), default, -1, 0)]));
        return (await applying, lines);
    }

    [Fact]
    public async Task AModuleThatSetsControlPointTeamsMakesANewSpawnPointUsableAtOnce()
    {
        var link = await ConnectAsync(Capabilities | Welcome.CapResync, controlPoints: [new AdminControlPointState(77, 0, "AH1Z", 2, 2, 0, 0, new Vec3(900, 150, 200))]);
        var builder = link.Server.Builder;
        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.SpawnPoint, "", 2, Origin, NoTurn)], [], []), "test");
        CmdConsole getter;
        while ((getter = await link.Module.ReadAsync<CmdConsole>()).Line != "ObjectTemplate.controlPointId")
        {
        }
        await link.Module.SendAsync(new ConsoleResult(getter.RequestId, 0, ["401"]));
        var list = Assert.IsType<CmdListObjects>((await link.Module.ReadConsoleAsync()).Next);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(555, 0, list.Template, new Vec3(100, 150, 200), default, -1, 0)]));

        // The control point to the team it has, which gives the new group its team
        var set = await link.Module.ReadAsync<CmdSetControlPointTeam>();
        Assert.Equal((ObjectRef.Console(77), (byte)2), (set.Target, set.Team));
        await link.Module.SendAsync(new OkReply(set.RequestId));

        var item = Assert.Single((await applying).State.Items);
        Assert.True(item.SpawnReady);
    }

    [Fact]
    public async Task ARemovedSpawnPointsGroupGoesToTheNextOneOnTheSameControlPoint()
    {
        var link = await ConnectAsync(Capabilities, controlPoints: [new AdminControlPointState(2, 0, "AH1Z", 2, 2, 0, 0, new Vec3(900, 150, 200))]);
        var builder = link.Server.Builder;
        // The map's first round (to the builder, a new map)
        await builder.OnRoundStartAsync("dalian_2_v_2");
        var (first, lines) = await AddSpawnPointAsync(link, 555, askedId: true);
        Assert.Contains($"ObjectTemplate.group {BuilderSpawning.FirstGroup}", lines);
        var (second, _) = await AddSpawnPointAsync(link, 556, askedId: false);

        Assert.All(second.State.Items, i => Assert.False(i.SpawnReady));

        // A round start gives the groups their team; both points are still there (the builder lists them to see)
        var starting = builder.OnRoundStartAsync("dalian_2_v_2");
        foreach (var consoleId in new uint[] { 555, 556 })
        {
            var list = Assert.IsType<CmdListObjects>((await link.Module.ReadConsoleAsync()).Next);
            await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(consoleId, 0, list.Template, new Vec3(100, 150, 200), default, -1, 0)]));
        }
        await starting;
        Assert.All(builder.GetState().Items, i => Assert.True(i.SpawnReady));

        await builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [], [], [first.Added[0]]), "test");
        Assert.Equal(ObjectRef.Console(555), (await link.Module.ReadAsync<CmdDeleteObject>()).Target);

        // The next one takes the free group, which has its team already: players can pick it at once
        var (third, thirdLines) = await AddSpawnPointAsync(link, 557, askedId: false);
        Assert.Contains($"ObjectTemplate.group {BuilderSpawning.FirstGroup}", thirdLines);
        Assert.True(third.State.Items.Single(i => i.Id == third.Added[0]).SpawnReady);
        Assert.Equal(2, third.State.Items.Count);
    }

    [Fact]
    public async Task AHelipadIsThePadASpawnerForItsTeamsHelicopterAndASpawnPointIntoIt()
    {
        var link = await ConnectAsync(Capabilities, controlPoints: [new AdminControlPointState(1, 0, "Z10", 1, 1, 0, 0, new Vec3(110, 150, 200))]);
        var builder = link.Server.Builder;

        var applying = builder.ApplyAsync(new BuilderApplyRequest(builder.Revision, [new BuilderAddDto(MapModObjectKind.Helipad, "", 1, Origin, NoTurn)], [], []), "test");
        await link.Module.AnswerOnLevelAsync(BuilderSpawning.HelipadTemplate);
        var (_, next) = await link.Module.ReadConsoleAsync();
        var pad = Assert.IsType<CmdSpawnObject>(next);
        Assert.Equal(BuilderSpawning.HelipadTemplate, pad.Template);
        await link.Module.SendAsync(new ObjectSpawned(pad.RequestId, 600, 40600));

        // The helicopter must be one the level has
        await link.Module.AnswerOnLevelAsync("ahe_z10");
        (var lines, next) = await link.Module.ReadConsoleAsync();
        Assert.Contains("ObjectTemplate.setObjectTemplate 1 ahe_z10", lines);
        Assert.Contains("ObjectTemplate.team 1", lines);
        Assert.Contains("Object.team 1", lines);
        var list = Assert.IsType<CmdListObjects>(next);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(601, 0, list.Template, new Vec3(100, 150.5f, 200), new Vec3(0, 0, 0), -1, 0)]));

        // The spawn point into the helicopter: a group of its own on team 1's control point
        CmdConsole getter;
        while ((getter = await link.Module.ReadAsync<CmdConsole>()).Line != "ObjectTemplate.controlPointId")
        {
        }
        await link.Module.SendAsync(new ConsoleResult(getter.RequestId, 0, ["402"]));
        (lines, next) = await link.Module.ReadConsoleAsync();
        Assert.Contains("ObjectTemplate.setControlPointId 402", lines);
        Assert.Contains("ObjectTemplate.setEnterOnSpawn 1", lines);
        Assert.Contains("ObjectTemplate.setAIEnterOnSpawn 1", lines);
        Assert.Contains(lines, l => l.StartsWith("ObjectTemplate.group 10"));
        list = Assert.IsType<CmdListObjects>(next);
        await link.Module.SendAsync(new ObjectList(list.RequestId, 0, [new ObjectListEntry(602, 0, list.Template, new Vec3(100, 150, 200), new Vec3(0, 0, 0), -1, 0)]));

        var item = Assert.Single((await applying).State.Items);
        Assert.Equal((MapModObjectKind.Helipad, 1, "ahe_z10"), (item.Kind, item.Team, item.Template));
    }
}
