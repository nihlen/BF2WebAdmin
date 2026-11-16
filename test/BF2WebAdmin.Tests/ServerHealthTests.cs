using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Communication.DTOs;
using BF2WebAdmin.Server.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// ServerHealth (openbf2-server-ext docs/admin-channel.md, "Server health") from WebAdmin's side: how a window is rated
/// (<see cref="ServerHealthRules"/>), the windows kept per server (<see cref="ServerHealthStore"/>), and the
/// subscription to stream 21, which only a module with capability bit 6 gets.
/// </summary>
public class ServerHealthTests
{
    private const string ServerId = "127.0.0.1:16569";
    private const byte BaseCapabilities = Welcome.CapConsole | Welcome.CapObjects | Welcome.CapIdentity;
    private const byte Valid = EvServerHealth.FlagHostValid;
    private static readonly ConcurrentBag<TcpClient> KeepAlive = new();
    private static readonly AdminGameState Game = new(1, 3, -1, 100, 100, 10, 0, 0, 0);
    private static readonly DateTimeOffset Noon = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A window of a server that runs well: frames of 28.3 ms, so a median of one frame and a longest of two.</summary>
    private static EvServerHealth Window(byte flags = Valid, uint wall = 10_000_000, ushort late = 0, ushort shortTicks = 0, uint cpuP99 = 1250,
        uint majorFaults = 0, ushort steal = 0, uint seq = 1, uint tick = 300) =>
        new(seq, tick, 300, wall, flags, 28_333, 56_667, 57_100, late, shortTicks, 310, cpuP99, 4800, 350, 900, 12, majorFaults, 4, 412, steal, 3,
            159_744);

    // ---------------------------------------------------------------- the status

    [Fact]
    public void AHealthyWindowIsGood()
    {
        var (status, reasons) = ServerHealthRules.Rate(Window());
        Assert.Equal(ServerHealthStatus.Good, status);
        Assert.Empty(reasons);
    }

    [Theory]
    // Late ticks: any is a Warn, three are Bad
    [InlineData(1, 0, 10_000_000u, 1250u, 0u, 0, ServerHealthStatus.Warn, "lateTicks")]
    [InlineData(2, 0, 10_000_000u, 1250u, 0u, 0, ServerHealthStatus.Warn, "lateTicks")]
    [InlineData(3, 0, 10_000_000u, 1250u, 0u, 0, ServerHealthStatus.Bad, "lateTicks")]
    // Short ticks only warn
    [InlineData(0, 1, 10_000_000u, 1250u, 0u, 0, ServerHealthStatus.Warn, "shortTicks")]
    [InlineData(0, 300, 10_000_000u, 1250u, 0u, 0, ServerHealthStatus.Warn, "shortTicks")]
    // Lost ticks: one is within the figure's accuracy, two warn, three are Bad
    [InlineData(0, 0, 10_033_333u, 1250u, 0u, 0, ServerHealthStatus.Good, "")]
    [InlineData(0, 0, 10_066_667u, 1250u, 0u, 0, ServerHealthStatus.Warn, "lostTicks")]
    [InlineData(0, 0, 10_100_000u, 1250u, 0u, 0, ServerHealthStatus.Bad, "lostTicks")]
    // CPU per tick, 99th percentile: half a tick warns, 30 ms is Bad
    [InlineData(0, 0, 10_000_000u, 16_666u, 0u, 0, ServerHealthStatus.Good, "")]
    [InlineData(0, 0, 10_000_000u, 16_667u, 0u, 0, ServerHealthStatus.Warn, "cpu")]
    [InlineData(0, 0, 10_000_000u, 29_999u, 0u, 0, ServerHealthStatus.Warn, "cpu")]
    [InlineData(0, 0, 10_000_000u, 30_000u, 0u, 0, ServerHealthStatus.Bad, "cpu")]
    // Major page faults only warn
    [InlineData(0, 0, 10_000_000u, 1250u, 1u, 0, ServerHealthStatus.Warn, "pageFaults")]
    [InlineData(0, 0, 10_000_000u, 1250u, 5000u, 0, ServerHealthStatus.Warn, "pageFaults")]
    // Steal, in hundredths of a percent: 5 % warns, 15 % is Bad
    [InlineData(0, 0, 10_000_000u, 1250u, 0u, 499, ServerHealthStatus.Good, "")]
    [InlineData(0, 0, 10_000_000u, 1250u, 0u, 500, ServerHealthStatus.Warn, "steal")]
    [InlineData(0, 0, 10_000_000u, 1250u, 0u, 1499, ServerHealthStatus.Warn, "steal")]
    [InlineData(0, 0, 10_000_000u, 1250u, 0u, 1500, ServerHealthStatus.Bad, "steal")]
    // Several at once: what is Bad comes first, then what only warns
    [InlineData(1, 2, 10_100_000u, 30_000u, 3u, 600, ServerHealthStatus.Bad, "lostTicks,cpu,lateTicks,shortTicks,pageFaults,steal")]
    [InlineData(1, 1, 10_000_000u, 1250u, 1u, 0, ServerHealthStatus.Warn, "lateTicks,shortTicks,pageFaults")]
    public void EveryRuleAndItsThreshold(int late, int shortTicks, uint wall, uint cpuP99, uint majorFaults, int steal, ServerHealthStatus expected,
        string expectedReasons)
    {
        var (status, reasons) = ServerHealthRules.Rate(Window(Valid, wall, (ushort)late, (ushort)shortTicks, cpuP99, majorFaults, (ushort)steal));
        Assert.Equal(expected, status);
        Assert.Equal(expectedReasons, string.Join(',', reasons));
    }

    [Theory]
    [InlineData(10_000_000u, 0)]
    [InlineData(10_000_123u, 0)]
    // Rounded to the nearest tick: 300.49998 and 300.50001 ticks
    [InlineData(10_016_666u, 0)]
    [InlineData(10_016_667u, 1)]
    [InlineData(10_033_333u, 1)]
    // Exactly 301.5 ticks rounds up
    [InlineData(10_050_000u, 2)]
    [InlineData(10_100_000u, 3)]
    [InlineData(20_000_000u, 300)]
    // A window shorter than its ticks (a server catching up) has lost none
    [InlineData(9_900_000u, 0)]
    [InlineData(0u, 0)]
    [InlineData(uint.MaxValue, 128_549)]
    public void LostTicksAreTheWindowsRealTimeInTicksMinusItsTicks(uint wall, int expected)
    {
        Assert.Equal(expected, ServerHealthRules.Lost(wall, 300));
        Assert.Equal(expected, ServerHealthRules.ToWindow(Window(wall: wall), Noon).LostTicks);
    }

    [Fact]
    public void ALevelLoadOrSteppedTimeTakesTheWindowOutOfTheRating()
    {
        // Figures that would be Bad several times over
        EvServerHealth Terrible(byte flags) => Window(flags, wall: 25_000_000, late: 9, shortTicks: 40, cpuP99: 90_000, majorFaults: 7, steal: 4000);

        Assert.Equal(ServerHealthStatus.Bad, ServerHealthRules.Rate(Terrible(Valid)).Status);

        var (loading, loadingReasons) = ServerHealthRules.Rate(Terrible(Valid | EvServerHealth.FlagLevelLoad));
        Assert.Equal(ServerHealthStatus.Loading, loading);
        Assert.Empty(loadingReasons);

        var (stepped, steppedReasons) = ServerHealthRules.Rate(Terrible(Valid | EvServerHealth.FlagStepped));
        Assert.Equal(ServerHealthStatus.Stepped, stepped);
        Assert.Empty(steppedReasons);

        Assert.Equal(ServerHealthStatus.Loading, ServerHealthRules.Rate(Terrible(EvServerHealth.FlagStepped | EvServerHealth.FlagLevelLoad)).Status);
    }

    [Fact]
    public void StealCountsOnlyWhenTheHostFiguresAreValid()
    {
        Assert.Equal(ServerHealthStatus.Good, ServerHealthRules.Rate(Window(flags: 0, steal: 9000)).Status);
        Assert.Equal(ServerHealthStatus.Bad, ServerHealthRules.Rate(Window(flags: Valid, steal: 9000)).Status);

        // The other rules do not need them
        var (status, reasons) = ServerHealthRules.Rate(Window(flags: 0, late: 3, steal: 9000));
        Assert.Equal(ServerHealthStatus.Bad, status);
        Assert.Equal(["lateTicks"], reasons);
    }

    [Fact]
    public void TheWindowForTheWebUiHasTheFiguresTheStatusAndTheTimeItWasReceived()
    {
        var window = ServerHealthRules.ToWindow(new EvServerHealth(30, 9000, 300, 10_100_000, Valid, 33334, 34100, 61234, 1, 2, 310, 1250, 4800, 350,
            900, 12, 0, 8, 412, 278, 3, 159_744), Noon);

        Assert.Equal((Noon, 9000u, ServerHealthStatus.Bad, 300, 10_100_000u, 3), (window.Time, window.Tick, window.Status, window.Ticks,
            window.WallUs, window.LostTicks));
        Assert.Equal(["lostTicks", "lateTicks", "shortTicks"], window.Reasons);
        Assert.Equal((33334u, 34100u, 61234u, 1, 2), (window.IntervalMedianUs, window.IntervalP99Us, window.IntervalMaxUs, window.LateTicks,
            window.ShortTicks));
        Assert.Equal((310u, 1250u, 4800u, 350u, 900u), (window.CpuMedianUs, window.CpuP99Us, window.CpuMaxUs, window.HandlerP99Us,
            window.HandlerMaxUs));
        Assert.Equal((12u, 0u, true, 8, 159_744u), (window.InvoluntarySwitches, window.MajorFaults, window.HostValid, window.HostCpus,
            window.ResidentKib));
        // Hundredths of a percent on the wire
        Assert.Equal((4.12, 2.78, 0.03), (window.HostBusyPercent, window.HostStealPercent, window.HostIoWaitPercent));

        Assert.False(ServerHealthRules.ToWindow(Window(flags: 0), Noon).HostValid);
    }

    // ---------------------------------------------------------------- the history

    [Fact]
    public async Task TheHistoryKeepsTheLastSixHoursOfWindows()
    {
        var store = new ServerHealthStore();
        Assert.Equal(6 * 60 * 6, ServerHealthStore.MaxWindows);
        Assert.Null(store.Get(ServerId).Latest);
        Assert.Empty(store.Get(ServerId).History);

        for (var i = 1; i <= ServerHealthStore.MaxWindows + 5; i++)
            await store.AddAsync(ServerId, Window(seq: (uint)i, tick: (uint)i * 300), Noon.AddSeconds(10 * i));

        var health = store.Get(ServerId);
        Assert.Equal(ServerHealthStore.MaxWindows, health.History.Count);
        Assert.Equal(6u * 300, health.History[0].Tick);
        Assert.Equal((ServerHealthStore.MaxWindows + 5) * 300u, health.History[^1].Tick);
        Assert.Same(health.History[^1], health.Latest);
        Assert.Equal(Noon.AddSeconds(10 * (ServerHealthStore.MaxWindows + 5)), health.Latest!.Time);

        // Another server's windows are its own
        Assert.Empty(store.Get("127.0.0.1:16571").History);
    }

    [Fact]
    public async Task APartOfTheHistoryIsCutByTheTimeTheWindowsWereReceived()
    {
        var store = new ServerHealthStore();
        for (var i = 0; i < 12; i++)
            await store.AddAsync(ServerId, Window(tick: (uint)(i + 1) * 300), Noon.AddSeconds(10 * i));

        var lastMinute = store.Get(ServerId, Noon.AddSeconds(60));
        Assert.Equal([2100u, 2400u, 2700u, 3000u, 3300u, 3600u], lastMinute.History.Select(w => w.Tick));
        Assert.Equal(3600u, lastMinute.Latest!.Tick);

        // The latest window is the latest whatever part is asked for
        var none = store.Get(ServerId, Noon.AddHours(1));
        Assert.Empty(none.History);
        Assert.Equal(3600u, none.Latest!.Tick);
    }

    [Fact]
    public async Task TheHistoryGoesWithTheServerAndStaysWhenItsSettingsChange()
    {
        var services = new ServiceCollection().AddLogging().AddMemoryCache().AddSingleton(Substitute.For<IServerSettingsRepository>())
            .AddSingleton<ServerHealthStore>().BuildServiceProvider();
        var registry = new GameServerRegistry(null, services);
        var store = services.GetRequiredService<ServerHealthStore>();
        const string other = "127.0.0.1:16571";
        await store.AddAsync(ServerId, Window(), Noon);
        await store.AddAsync(other, Window(), Noon);

        await registry.AddOrUpdateServerAsync(new BF2WebAdmin.Data.Entities.Server { ServerId = ServerId });
        Assert.Single(store.Get(ServerId).History);

        await registry.RemoveServerAsync(ServerId);
        Assert.Null(store.Get(ServerId).Latest);
        Assert.Empty(store.Get(ServerId).History);
        Assert.Single(store.Get(other).History);
    }

    // ---------------------------------------------------------------- the link

    /// <summary>The module's end of a link that is past its handshake.</summary>
    private sealed class FakeModule(Stream stream)
    {
        private readonly AdminFrameDecoder _decoder = new(AdminProtocol.MaxOutboundPayload);

        public Task SendAsync(AdminMessage m) => stream.WriteAsync(AdminProtocol.Encode(m)).AsTask();

        public async Task<AdminMessage> ReadAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var buffer = new byte[65536];
            while (true)
            {
                if (_decoder.TryRead(out var type, out var payload))
                    return AdminProtocol.Decode(type, payload);

                var n = await stream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                    throw new EndOfStreamException();
                _decoder.Append(buffer.AsSpan(0, n));
            }
        }
    }

    private static GameServer Server(out ServerHealthStore store)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<ServerHealthStore>().BuildServiceProvider();
        store = services.GetRequiredService<ServerHealthStore>();
        return new GameServer(new ServerInfo { ServerId = ServerId }, IPAddress.Loopback, services, CancellationToken.None);
    }

    /// <summary>A new link of the server whose module has sent its Welcome (with <paramref name="capabilities"/>) and Snapshot.</summary>
    private static async Task<(AdminSession Session, FakeModule Module, Task Run)> ConnectAsync(GameServer server, byte capabilities, uint sessionNumber = 1)
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

        var session = new AdminSession(ServerId, (IPEndPoint)client.Client.LocalEndPoint!, accepted.GetStream(), new AdminFrameDecoder(), server.Applier,
            NullLogger.Instance, CancellationToken.None);
        server.AttachSession(session);
        var run = session.RunAsync();

        var module = new FakeModule(client.GetStream());
        await module.SendAsync(new Welcome(1, sessionNumber, 99, 30, "fake", 16569, 29901, 16, capabilities, "dalian_2_v_2", "gpm_cq", 16, 0,
            [new MapListEntry("dalian_2_v_2", "gpm_cq", 16)], "CH", "US", Game));
        await module.SendAsync(new Snapshot(0, 0, 100, 3.3, Game, [], [], [], []));
        return (session, module, run);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    /// <summary>
    /// The streams the applier subscribes a new link to. It is asked directly: its Welcome does not get that far here,
    /// because it needs the server's modules.
    /// </summary>
    private static async Task<List<Subscribe>> SubscriptionsAsync(byte capabilities)
    {
        var server = Server(out _);
        var (session, module, run) = await ConnectAsync(server, capabilities);
        await WaitAsync(() => session.Capabilities == capabilities);
        server.Applier.Subscribe(session, session.Capabilities);

        // A ping marks the end of what it sent
        const ulong marker = 424242;
        session.Fire(id => new Ping(id, marker));

        var subscriptions = new List<Subscribe>();
        while (true)
        {
            var message = await module.ReadAsync();
            if (message is Ping { Clock: marker })
                break;
            if (message is Subscribe subscribe)
                subscriptions.Add(subscribe);
        }

        session.Close("test");
        await run;
        return subscriptions;
    }

    [Fact]
    public async Task Stream21IsSubscribedOnlyWhenTheModuleHasTheCapability()
    {
        Assert.Equal(64, Welcome.CapHealth);
        Assert.Equal(21, (byte)AdminStream.Health);

        var with = await SubscriptionsAsync(BaseCapabilities | Welcome.CapHealth);
        var health = Assert.Single(with, s => s.Stream == AdminStream.Health);
        Assert.Empty(health.Filters);

        // A module without it gets the subscriptions it got before, and no other
        var without = await SubscriptionsAsync(BaseCapabilities);
        Assert.DoesNotContain(without, s => s.Stream == AdminStream.Health);
        Assert.Equal(with.Where(s => s.Stream != AdminStream.Health).Select(s => s.Stream), without.Select(s => s.Stream));
        Assert.Contains(without, s => s.Stream == AdminStream.Replay);
    }

    [Fact]
    public async Task TheHistorySurvivesAReconnect()
    {
        var server = Server(out var store);
        var before = DateTimeOffset.UtcNow;

        var first = await ConnectAsync(server, BaseCapabilities | Welcome.CapHealth);
        await first.Module.SendAsync(Window(seq: 1, tick: 600, late: 1));
        await WaitAsync(() => store.Get(ServerId).History.Count == 1);
        first.Session.Close("test");
        await first.Run;

        // A new link numbers its events from 1 again
        var second = await ConnectAsync(server, BaseCapabilities | Welcome.CapHealth, sessionNumber: 2);
        await second.Module.SendAsync(Window(seq: 1, tick: 900));
        await WaitAsync(() => store.Get(ServerId).History.Count == 2);
        second.Session.Close("test");
        await second.Run;

        var health = store.Get(ServerId);
        Assert.Equal([600u, 900u], health.History.Select(w => w.Tick));
        Assert.Equal([ServerHealthStatus.Warn, ServerHealthStatus.Good], health.History.Select(w => w.Status));
        Assert.Equal(900u, health.Latest!.Tick);

        // Received now, in UTC
        Assert.All(health.History, w => Assert.Equal(TimeSpan.Zero, w.Time.Offset));
        Assert.All(health.History, w => Assert.InRange(w.Time, before, DateTimeOffset.UtcNow));
    }
}
