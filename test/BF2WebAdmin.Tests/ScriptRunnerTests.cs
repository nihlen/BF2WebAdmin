using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// Game scripts from WebAdmin's side (docs/scripts.md): <see cref="ScriptRunner"/> against a fake module over loopback
/// TCP, with the server's own applier handling what the module sends, and the scripts the build embeds.
/// </summary>
public class ScriptRunnerTests
{
    private const string ServerId = "127.0.0.1:16569";
    private const byte AllCapabilities = Welcome.CapConsole | Welcome.CapObjects | Welcome.CapIdentity | Welcome.CapScripts;
    private static readonly ConcurrentBag<TcpClient> KeepAlive = new();
    private static readonly AdminGameState Game = new(1, 3, -1, 100, 100, 10, 0, 0, 0);

    /// <summary>The module's end of a link that is past its handshake.</summary>
    private sealed class FakeModule(Stream stream)
    {
        private readonly AdminFrameDecoder _decoder = new(AdminProtocol.MaxOutboundPayload);

        public Task SendAsync(AdminMessage m) => stream.WriteAsync(AdminProtocol.Encode(m)).AsTask();

        public Task OkAsync(AdminCommand command) => SendAsync(new OkReply(command.RequestId));

        /// <summary>The next command; the subscriptions and pings WebAdmin sends by itself are skipped.</summary>
        public async Task<T> ReadAsync<T>() where T : AdminMessage
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var buffer = new byte[65536];
            while (true)
            {
                while (_decoder.TryRead(out var type, out var payload))
                {
                    var message = AdminProtocol.Decode(type, payload);
                    if (message is Subscribe or Unsubscribe or Ping or Pong)
                        continue;

                    return Assert.IsType<T>(message);
                }

                var n = await stream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                    throw new EndOfStreamException();
                _decoder.Append(buffer.AsSpan(0, n));
            }
        }
    }

    private sealed record Link(GameServer Server, AdminSession Session, FakeModule Module, Task Run, ScriptRunner Runner, ConcurrentQueue<IEvent> Published)
    {
        public async Task CloseAsync()
        {
            Session.Close("test");
            await Run;
        }
    }

    /// <summary>
    /// A game server with a link whose module has sent its Welcome (with <paramref name="capabilities"/>) and Snapshot.
    /// The runner is the server's own, so the applier routes the module's script messages to it; what it would hand to
    /// the modules is collected instead.
    /// </summary>
    private static async Task<Link> ConnectAsync(byte capabilities = AllCapabilities, Func<string, string?>? source = null)
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
        var run = session.RunAsync();

        var module = new FakeModule(client.GetStream());
        await module.SendAsync(new Welcome(1, 1, 99, 30, "fake", 16569, 29901, 16, capabilities, "dalian_2_v_2", "gpm_cq", 16, 0,
            [new MapListEntry("dalian_2_v_2", "gpm_cq", 16)], "CH", "US", Game));
        await module.SendAsync(new Snapshot(0, 0, 100, 3.3, Game, [], [], [], []));
        await WaitAsync(() => session.Capabilities == capabilities);

        // Its own runner for the tests that hand it messages themselves; the others use the server's
        var published = new ConcurrentQueue<IEvent>();
        var runner = new ScriptRunner(server, NullLogger.Instance, source ?? (name => name is "nasa" or "dance" ? $"// {name}" : null), e =>
        {
            published.Enqueue(e);
            return ValueTask.CompletedTask;
        });
        return new Link(server, session, module, run, runner, published);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    private static async Task<IScriptRun> StartAsync(IScriptRunner runner, FakeModule module, string name, object? args = null)
    {
        var starting = runner.StartAsync(name, args);
        await module.OkAsync(await module.ReadAsync<CmdStartScript>());
        return await starting;
    }

    [Fact]
    public async Task AScriptIsStarted_ToldThings_AndItsOutputAndEndReachTheModules()
    {
        var link = await ConnectAsync();
        var (runner, module) = (link.Runner, link.Module);
        Assert.True(runner.Available);

        var starting = runner.StartAsync("nasa", new { Altitude = 300 });
        var start = await module.ReadAsync<CmdStartScript>();
        Assert.Equal((1u, CmdStartScript.Api, (byte)0, "nasa", "{\"altitude\":300}", "// nasa"),
            (start.Instance, start.ApiVersion, start.Flags, start.Name, start.Arguments, start.Source));
        Assert.False(starting.IsCompleted);
        await module.OkAsync(start);
        var script = await starting;
        Assert.Equal((1u, "nasa", true), (script.Id, script.Name, script.IsRunning));

        var sending = script.SendAsync(new { altitude = 250 });
        var message = await module.ReadAsync<CmdScriptMessage>();
        Assert.Equal((1u, "{\"altitude\":250}"), (message.Instance, message.Message));
        await module.OkAsync(message);
        await sending;

        var time = DateTimeOffset.UtcNow;
        await runner.OnEventAsync(new EvScriptEvent(1, 10, 1, EvScriptEvent.KindData, "{\"player\":3,\"y\":301.5}"), time);
        await runner.OnEventAsync(new EvScriptEvent(2, 10, 1, EvScriptEvent.KindLog, "above the ceiling"), time);
        await runner.OnEventAsync(new EvScriptEvent(3, 10, 1, EvScriptEvent.KindData, "not json"), time);
        await runner.OnEventAsync(new EvScriptEvent(4, 10, 99, EvScriptEvent.KindData, "{}"), time);
        var data = Assert.IsType<ScriptDataEvent>(link.Published.First());
        Assert.Same(script, data.Script);
        Assert.Equal((3, 301.5), (data.Data.GetProperty("player").GetInt32(), data.Data.GetProperty("y").GetDouble()));
        Assert.Equal("above the ceiling", Assert.IsType<ScriptLogEvent>(link.Published.Last()).Line);
        Assert.Equal(2, link.Published.Count);

        await runner.OnFinishedAsync(new EvScriptFinished(5, 20, 1, "nasa", EvScriptFinished.ReasonFinished, "{\"ticks\":90}", 90, 1234, 56), time);
        var result = await script.Finished.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(script.IsRunning);
        Assert.Equal((ScriptEndReason.Finished, 90u, false), (result.Reason, result.Calls, result.Failed));
        Assert.Equal((1234.0, 56.0), (result.CpuTime.TotalMicroseconds, result.LongestCall.TotalMicroseconds));
        Assert.Equal(90, result.Value!.Value.GetProperty("ticks").GetInt32());
        Assert.Same(result, Assert.IsType<ScriptFinishedEvent>(link.Published.Last()).Result);
        Assert.Empty(runner.Running);

        // Ended: stopping it sends nothing, and a message is refused here
        await script.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => script.SendAsync(new { altitude = 1 }));

        // The next script gets the next id; a stop goes to the module, and a script it no longer knows is no error
        var second = await StartAsync(runner, module, "dance");
        Assert.Equal(2u, second.Id);
        var stopping = second.StopAsync();
        var stop = await module.ReadAsync<CmdStopScript>();
        Assert.Equal(2u, stop.Instance);
        await module.SendAsync(new ErrorMessage(stop.RequestId, AdminErrorCode.Unavailable, "no such script"));
        await stopping;

        await link.CloseAsync();
    }

    [Fact]
    public async Task AScriptTheModuleRefuses_FailsWithItsMessage_AndLeavesNoRun()
    {
        var link = await ConnectAsync();

        var starting = link.Runner.StartAsync("nasa", new { altitude = 300 });
        var start = await link.Module.ReadAsync<CmdStartScript>();
        await link.Module.SendAsync(new ErrorMessage(start.RequestId, AdminErrorCode.InvalidValue, "SyntaxError: unexpected token at nasa:3:12"));
        var e = await Assert.ThrowsAsync<AdminCommandException>(() => starting);
        Assert.Equal(AdminErrorCode.InvalidValue, e.Code);
        Assert.Contains("SyntaxError: unexpected token at nasa:3:12", e.Message);
        Assert.Empty(link.Runner.Running);

        // No such script, and one too large for a frame, never reach the module
        await Assert.ThrowsAsync<ArgumentException>(() => link.Runner.StartAsync("nope"));
        var big = await ConnectAsync(source: _ => new string('x', CmdStartScript.MaxSourceBytes + 1));
        await Assert.ThrowsAsync<ArgumentException>(() => big.Runner.StartAsync("nasa"));

        await link.CloseAsync();
        await big.CloseAsync();
    }

    [Fact]
    public async Task AModuleWithoutTheCapability_IsNeverSentAScript()
    {
        var link = await ConnectAsync(Welcome.CapConsole | Welcome.CapObjects | Welcome.CapIdentity);
        Assert.False(link.Runner.Available);
        Assert.False(link.Server.Scripts.Available);
        var e = await Assert.ThrowsAsync<AdminCommandException>(() => link.Runner.StartAsync("nasa"));
        Assert.Contains("does not run scripts", e.Message);

        // Nothing was sent: the next thing the module reads is this console line
        link.Session.Fire(id => new CmdConsole(id, "marker"));
        Assert.Equal("marker", (await link.Module.ReadAsync<CmdConsole>()).Line);
        await link.CloseAsync();

        // And without a link there is nothing to run on
        Assert.False(link.Server.Scripts.Available);
        await Assert.ThrowsAsync<AdminCommandException>(() => link.Server.Scripts.StartAsync("nasa"));
    }

    [Fact]
    public async Task TheModulesScriptEvents_GoThroughTheApplier_AndScriptsEndWithTheirLink()
    {
        var link = await ConnectAsync();
        var scripts = link.Server.Scripts;
        Assert.True(scripts.Available);

        // The embedded script, as built from GameScripts/src/nasa.ts
        var starting = scripts.StartAsync("nasa", new { altitude = 300 });
        var start = await link.Module.ReadAsync<CmdStartScript>();
        Assert.Equal(GameScripts.Source("nasa"), start.Source);
        await link.Module.OkAsync(start);
        var first = await starting;

        await link.Module.SendAsync(new EvScriptFinished(1, 120, first.Id, "nasa", EvScriptFinished.ReasonError, "TypeError: x is undefined\n    at nasa:12:5", 7, 300, 80));
        var result = await first.Finished.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((ScriptEndReason.Error, true), (result.Reason, result.Failed));
        Assert.Contains("nasa:12:5", result.Text);
        Assert.Null(result.Value);

        var second = await StartAsync(scripts, link.Module, "dance", new { players = new[] { 1 }, seconds = 5 });
        await link.CloseAsync();
        Assert.Equal(ScriptEndReason.LinkClosed, (await second.Finished.WaitAsync(TimeSpan.FromSeconds(5))).Reason);
        Assert.Empty(link.Server.ScriptRunner.Running);
    }

    [Fact]
    public async Task AfterLostEvents_TheScriptsTheModuleNoLongerLists_AreEnded()
    {
        var link = await ConnectAsync();
        var (scripts, module) = (link.Server.Scripts, link.Module);
        var gone = await StartAsync(scripts, module, "nasa", new { altitude = 300 });
        var kept = await StartAsync(scripts, module, "dance");

        // Event 2 is lost (it was the first script's end): WebAdmin asks for a snapshot, and with it for the scripts
        await module.SendAsync(new EvProjectileHit(1, 101, 5, "agm114_hellfire_tv", -1, 0, 0, default));
        await module.SendAsync(new EvProjectileHit(3, 102, 5, "agm114_hellfire_tv", -1, 0, 0, default));
        var request = await module.ReadAsync<RequestSnapshot>();
        await module.SendAsync(new Snapshot(request.RequestId, 3, 110, 4, Game, [], [], [], []));
        var list = await module.ReadAsync<CmdListScripts>();

        // A script started before the module answers is not in its list, and is not one WebAdmin asked about
        var late = scripts.StartAsync("nasa", new { altitude = 100 });
        var lateStart = await module.ReadAsync<CmdStartScript>();
        await module.SendAsync(new ScriptList(list.RequestId, [(kept.Id, "dance")]));
        await module.OkAsync(lateStart);
        var started = await late;

        Assert.Equal(ScriptEndReason.Lost, (await gone.Finished.WaitAsync(TimeSpan.FromSeconds(5))).Reason);
        Assert.True(kept.IsRunning);
        Assert.True(started.IsRunning);
        Assert.Equal(new[] { kept.Id, started.Id }, link.Server.ScriptRunner.Running.Select(r => r.Id).Order().ToArray());

        await link.CloseAsync();
    }

    [Fact]
    public void TheBuildEmbedsTheScripts_AsJavaScriptWithTheTypesBlankedOut()
    {
        Assert.Contains("nasa", GameScripts.Names);
        Assert.Contains("dance", GameScripts.Names);
        Assert.Null(GameScripts.Source("no-such-script"));

        foreach (var name in GameScripts.Names)
        {
            var source = GameScripts.Source(name)!;
            Assert.InRange(Encoding.UTF8.GetByteCount(source), 1, CmdStartScript.MaxSourceBytes);
            Assert.Contains("script.onTick(", source);
            // No TypeScript left, and no modules
            Assert.DoesNotContain("interface ", StripComments(source));
            Assert.DoesNotContain("): number", StripComments(source));
            Assert.DoesNotContain(" as Partial<", StripComments(source));
            Assert.DoesNotMatch(@"(?m)^\s*(import|export)\b", source);
        }

        // Line for line the TypeScript: an error from the game server points into the .ts file
        var typescript = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BF2WebAdmin.Server", "GameScripts", "src", "nasa.ts");
        if (File.Exists(typescript))
            Assert.Equal(File.ReadAllText(typescript).Length, GameScripts.Source("nasa")!.Length);
    }

    private static string StripComments(string source) =>
        string.Join('\n', source.Split('\n').Select(line => line.Contains("//") ? line[..line.IndexOf("//", StringComparison.Ordinal)] : line));
}
