using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Modules;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// .nasa and dance on a server that runs game scripts (docs/scripts.md), and what they fall back to when a script cannot
/// be started or fails. The way without scripts is in <see cref="Heli2v2PracticeTests"/> and below.
/// </summary>
public class ScriptModuleTests
{
    private sealed class FakeRun(uint id, string name, string args) : IScriptRun
    {
        private readonly TaskCompletionSource<ScriptResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public uint Id { get; } = id;
        public string Name { get; } = name;
        public string Args { get; } = args;
        public List<string> Messages { get; } = [];
        public int Stops { get; private set; }
        public bool IsRunning => !_finished.Task.IsCompleted;
        public Task<ScriptResult> Finished => _finished.Task;

        public Task SendAsync(object message)
        {
            Messages.Add(JsonSerializer.Serialize(message));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stops++;
            End(ScriptEndReason.Stopped);
            return Task.CompletedTask;
        }

        public ScriptResult End(ScriptEndReason reason, string text = "")
        {
            var result = new ScriptResult(reason, text, 10, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(0.2));
            _finished.TrySetResult(result);
            return result;
        }
    }

    private sealed class FakeRunner : IScriptRunner
    {
        public bool Available { get; set; } = true;
        public List<FakeRun> Started { get; } = [];
        public Exception? Refuse { get; set; }

        public Task<IScriptRun> StartAsync(string name, object? args = null)
        {
            if (Refuse != null)
                return Task.FromException<IScriptRun>(Refuse);

            var run = new FakeRun((uint)Started.Count + 1, name, JsonSerializer.Serialize(args));
            Started.Add(run);
            return Task.FromResult<IScriptRun>(run);
        }
    }

    /// <summary>Runs a module's background work at once, as the module thread would between events.</summary>
    private sealed class InlineTaskRunner : ITaskRunner
    {
        public Task Last { get; private set; } = Task.CompletedTask;
        public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct) => Last = func();
    }

    private static (IGameServer Server, FakeRunner Scripts) Server()
    {
        var server = Substitute.For<IGameServer>();
        var scripts = new FakeRunner();
        server.Scripts.Returns(scripts);
        server.Id.Returns("127.0.0.1:16569");
        server.Players.Returns([]);
        return (server, scripts);
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    // ---------------------------------------------------------------- .nasa

    private static async Task<(IGameServer Server, FakeRunner Scripts, PracticeModule Practice, CancellationTokenSource Reload)> PracticeAsync()
    {
        var (server, scripts) = Server();
        var reload = new CancellationTokenSource();
        var practice = new PracticeModule(server, Substitute.For<IDelayProvider>(), new InlineTaskRunner(), NullLogger<PracticeModule>.Instance, reload);
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], Now));
        return (server, scripts, practice, reload);
    }

    private static PlayerPositionEvent Above(Player player) => new(player, new Position(1, 300, 2), Rotation.Neutral, 0, Now);

    [Fact]
    public async Task Nasa_IsTheScript_WhereTheServerRunsScripts()
    {
        var (server, scripts, practice, _) = await PracticeAsync();
        var player = Data.Setup2v2Players().players[0];
        Assert.Empty(scripts.Started);

        // The altitude is the world height: the command's metres above the map's ground level
        await practice.HandleAsync(new NasaCommand { Value = 100 });
        var script = Assert.Single(scripts.Started);
        Assert.Equal((PracticeModule.NasaScript, "{\"altitude\":240}"), (script.Name, script.Args));

        // The script holds the ceiling: no teleport from a position update
        await practice.HandleAsync(Above(player));
        server.GameWriter.DidNotReceiveWithAnyArgs().SendTeleport(default!, default!);

        // Another altitude is a message to the running script; the same one again is nothing
        await practice.HandleAsync(new NasaCommand { Value = 200 });
        await practice.HandleAsync(new NasaCommand { Value = 200 });
        Assert.Equal(["{\"altitude\":340}"], script.Messages);
        Assert.Single(scripts.Started);

        await practice.HandleAsync(new NasaCommand { Value = 0 });
        Assert.Equal(1, script.Stops);
        await practice.HandleAsync(Above(player));
        server.GameWriter.DidNotReceiveWithAnyArgs().SendTeleport(default!, default!);
    }

    [Fact]
    public async Task Nasa_StartsItsScriptAgain_AfterAReconnectAndALevelUnload_ButNotOnAnotherMap()
    {
        var (_, scripts, practice, reload) = await PracticeAsync();
        await practice.HandleAsync(new NasaCommand { Value = 100 });
        var first = scripts.Started[0];

        // The link closed: the script is gone with it, and comes back with the next link
        await practice.HandleAsync(new ScriptFinishedEvent(first, first.End(ScriptEndReason.LinkClosed), Now));
        scripts.Available = false;
        await practice.HandleAsync(new SocketStateChangedEvent(SocketState.Disconnected, Now));
        Assert.Single(scripts.Started);
        scripts.Available = true;
        await practice.HandleAsync(new SocketStateChangedEvent(SocketState.Connected, Now));
        Assert.Equal(2, scripts.Started.Count);
        Assert.Equal("{\"altitude\":240}", scripts.Started[1].Args);

        // The welcome's map event finds it running
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], Now));
        Assert.Equal(2, scripts.Started.Count);

        // A restarted round unloads the level, which ends the script; the same map keeps the limit
        var second = scripts.Started[1];
        await practice.HandleAsync(new ScriptFinishedEvent(second, second.End(ScriptEndReason.LevelUnload), Now));
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], Now));
        Assert.Equal(3, scripts.Started.Count);

        // Another map ends the limit and its script
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "daqing_2_v_2" }, [], Now));
        Assert.Equal(1, scripts.Started[2].Stops);
        await practice.HandleAsync(new SocketStateChangedEvent(SocketState.Connected, Now));
        Assert.Equal(3, scripts.Started.Count);

        // A module reload stops the script: it is the old module's
        await practice.HandleAsync(new NasaCommand { Value = 50 });
        reload.Cancel();
        Assert.Equal(1, scripts.Started[3].Stops);
    }

    [Fact]
    public async Task Nasa_AScriptThatFailsOrCannotStart_LeavesThePositionUpdatesToIt()
    {
        var (server, scripts, practice, _) = await PracticeAsync();
        var player = Data.Setup2v2Players().players[0];

        // The module refuses it (say adminAllowScripts was turned off): the old way holds the limit
        scripts.Refuse = new AdminCommandException("CmdStartScript refused: Disabled", AdminErrorCode.Disabled);
        await practice.HandleAsync(new NasaCommand { Value = 100 });
        await practice.HandleAsync(Above(player));
        server.GameWriter.Received(1).SendTeleport(player, Arg.Is<Position>(p => p.Height == 240));

        // It starts, then fails: one line to the players, the old way again, and no new try until .nasa is used again
        scripts.Refuse = null;
        await practice.HandleAsync(new NasaCommand { Value = 100 });
        var script = Assert.Single(scripts.Started);
        server.GameWriter.ClearReceivedCalls();
        await practice.HandleAsync(new ScriptFinishedEvent(script, script.End(ScriptEndReason.Error, "TypeError at nasa:40"), Now));
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.Contains("script failed")));
        await practice.HandleAsync(Above(player));
        server.GameWriter.Received(1).SendTeleport(player, Arg.Is<Position>(p => p.Height == 240));
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], Now));
        Assert.Single(scripts.Started);

        await practice.HandleAsync(new NasaCommand { Value = 100 });
        Assert.Equal(2, scripts.Started.Count);

        // Another script's end is not this module's business
        var other = new FakeRun(99, "dance", "{}");
        await practice.HandleAsync(new ScriptFinishedEvent(other, other.End(ScriptEndReason.Error), Now));
        Assert.True(scripts.Started[1].IsRunning);
    }

    // ---------------------------------------------------------------- dance

    private static (IGameServer Server, FakeRunner Scripts, FunModule Fun, Player[] Players) Fun()
    {
        var (server, scripts) = Server();
        var players = Data.Setup2v2Players().players;
        server.Players.Returns(players);
        server.GetPlayerByName("CH Pilot").Returns(players[0]);
        var fun = new FunModule(server, NullLogger<FunModule>.Instance, new CancellationTokenSource());
        return (server, scripts, fun, players);
    }

    [Fact]
    public async Task Dance_IsTheScript_UntilItFinishes()
    {
        var (server, scripts, fun, _) = Fun();

        var dancing = fun.HandleAsync(new DanceCommand { Name = "CH Pilot" }).AsTask();
        var script = Assert.Single(scripts.Started);
        Assert.Equal((FunModule.DanceScript, "{\"players\":[1],\"seconds\":5}"), (script.Name, script.Args));
        Assert.False(dancing.IsCompleted);

        script.End(ScriptEndReason.Finished, "{\"players\":1}");
        await dancing.WaitAsync(TimeSpan.FromSeconds(5));
        server.GameWriter.DidNotReceiveWithAnyArgs().SendTeleport(default!, default!, default!);
        server.GameWriter.DidNotReceiveWithAnyArgs().SendText(default!);

        var all = fun.HandleAsync(new DanceAllCommand()).AsTask();
        Assert.Equal("{\"players\":[1,2,3,4],\"seconds\":5}", scripts.Started[1].Args);
        scripts.Started[1].End(ScriptEndReason.Finished);
        await all.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Dance_StopStopsTheScript()
    {
        var (server, scripts, fun, _) = Fun();

        var dancing = fun.HandleAsync(new DanceCommand { Name = "CH Pilot" }).AsTask();
        var script = Assert.Single(scripts.Started);
        fun.Handle(new StopCommand());
        Assert.Equal(1, script.Stops);
        await dancing.WaitAsync(TimeSpan.FromSeconds(5));
        server.GameWriter.DidNotReceiveWithAnyArgs().SendTeleport(default!, default!, default!);
    }

    [Fact]
    public async Task Dance_AFailedScript_PutsThePlayersBack_AndSaysSo()
    {
        var (server, scripts, fun, players) = Fun();

        var dancing = fun.HandleAsync(new DanceCommand { Name = "CH Pilot" }).AsTask();
        scripts.Started[0].End(ScriptEndReason.OverBudget, "interrupted");
        await dancing.WaitAsync(TimeSpan.FromSeconds(5));

        server.GameWriter.Received(1).SendText("Dance script failed");
        server.GameWriter.Received(1).SendTeleport(players[0], players[0].Position, Arg.Is<Rotation>(r => r.Pitch == 0 && r.Roll == 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dance_WithoutScripts_OrWhenTheScriptCannotStart_IsTheOldLoop(bool refused)
    {
        var (server, scripts, fun, players) = Fun();
        scripts.Available = refused;
        scripts.Refuse = refused ? new AdminCommandException("CmdStartScript refused: Refused 8 scripts run", AdminErrorCode.Refused) : null;

        var dancing = fun.HandleAsync(new DanceCommand { Name = "CH Pilot" }).AsTask();
        for (var i = 0; i < 200 && server.GameWriter.ReceivedCalls().Count() < 3; i++)
            await Task.Delay(10);
        fun.Handle(new StopCommand());
        await dancing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(scripts.Started);
        server.GameWriter.Received().SendTeleport(players[0], players[0].Position, Arg.Any<Rotation>());
    }
}
