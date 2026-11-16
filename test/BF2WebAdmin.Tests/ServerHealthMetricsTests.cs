using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.AdminChannel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// The game servers' health as metrics (<see cref="ServerHealthMetrics"/>): counters added to per window, gauges of the
/// latest window while it is fresh.
/// </summary>
public sealed class ServerHealthMetricsTests : IDisposable
{
    private const string ServerId = "127.0.0.1:16569";
    private const string ServerName = "2v2 test";
    private static readonly DateTimeOffset Noon = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly FakeTimeProvider _time = new(Noon);
    private readonly MeterListener _listener = new();
    private readonly List<(string Name, double Value, Dictionary<string, string?> Tags)> _measurements = new();
    private readonly ServerHealthStore _store;

    public ServerHealthMetricsTests()
    {
        var factory = _services.GetRequiredService<IMeterFactory>();
        _store = new ServerHealthStore(meterFactory: factory, timeProvider: _time);

        // Only this test's meter: other tests create meters with the same name
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == Telemetry.MeterName && ReferenceEquals(instrument.Meter.Scope, factory))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<int>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Add(i, v, tags));
        _listener.Start();
    }

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        lock (_measurements)
            _measurements.Add((instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }

    private static EvServerHealth Window(byte flags = EvServerHealth.FlagHostValid, ushort late = 0, ushort shortTicks = 0, uint wall = 10_000_000,
        uint majorFaults = 0) =>
        new(1, 300, 300, wall, flags, 28_333, 56_667, 57_100, late, shortTicks, 310, 1250, 4800, 350, 900, 12, majorFaults, 4, 412, 75, 3, 159_744);

    private static AdminPlayerConnection Connection(byte player, byte readings = 2, ushort ping = 41, ushort pingMax = 44, float lostTo = 0) =>
        new(player, readings, ping, 38, pingMax, 20, 30, 2400.5f, 1100.25f, lostTo, lostTo, 0, 0);

    private double Sum(string name, string? key = null, string? value = null) =>
        _measurements.Where(m => m.Name == name && (key == null || m.Tags[key] == value)).Sum(m => m.Value);

    private List<(string Name, double Value, Dictionary<string, string?> Tags)> Observe()
    {
        _measurements.Clear();
        _listener.RecordObservableInstruments();
        return _measurements.ToList();
    }

    [Fact]
    public async Task EachWindowIsCountedByStatusAndItsTicksByKind()
    {
        await _store.AddAsync(ServerId, Window(), Noon, ServerName);
        await _store.AddAsync(ServerId, Window(late: 1, shortTicks: 2, wall: 10_070_000, majorFaults: 4), Noon.AddSeconds(10), ServerName);
        // A level load says nothing about how the server runs: counted as a window, its ticks are not
        await _store.AddAsync(ServerId, Window(flags: EvServerHealth.FlagLevelLoad, late: 9), Noon.AddSeconds(20), ServerName);

        Assert.Equal(1, Sum("bf2.server.health.windows", "status", "Good"));
        Assert.Equal(1, Sum("bf2.server.health.windows", "status", "Warn"));
        Assert.Equal(1, Sum("bf2.server.health.windows", "status", "Loading"));
        Assert.Equal(600, Sum("bf2.server.ticks", "kind", "run"));
        Assert.Equal(1, Sum("bf2.server.ticks", "kind", "late"));
        Assert.Equal(2, Sum("bf2.server.ticks", "kind", "short"));
        Assert.Equal(2, Sum("bf2.server.ticks", "kind", "lost"));
        Assert.Equal(4, Sum("bf2.server.page_faults.major"));
        Assert.Equal(24, Sum("bf2.server.context_switches.involuntary"));
        Assert.All(_measurements, m =>
        {
            Assert.Equal(ServerId, m.Tags[ServerHealthMetrics.ServerIdTag]);
            Assert.Equal(ServerName, m.Tags[ServerHealthMetrics.ServerNameTag]);
        });
    }

    [Fact]
    public async Task TheGaugesShowTheLatestWindowWhileItIsFresh()
    {
        await _store.AddAsync(ServerId, Window(late: 1), Noon, ServerName);

        var observed = Observe();
        Assert.Equal(1, Sum("bf2.server.health.status"));
        Assert.Equal(0.0571, Sum("bf2.server.tick.interval", "stat", "max"), 6);
        Assert.Equal(0.00125, Sum("bf2.server.tick.cpu", "stat", "p99"), 6);
        Assert.Equal(0.75, Sum("bf2.server.host.cpu", "state", "steal"), 6);
        Assert.Equal(159_744 * 1024.0, Sum("bf2.server.memory.resident"));

        // A server that stopped sending drops out
        _time.Advance(ServerHealthMetrics.FreshFor + TimeSpan.FromSeconds(1));
        Assert.Empty(Observe());
        Assert.NotEmpty(observed);
    }

    [Fact]
    public async Task ALevelLoadShowsOnlyItsStatusAndMemory()
    {
        await _store.AddAsync(ServerId, Window(flags: EvServerHealth.FlagLevelLoad | EvServerHealth.FlagHostValid), Noon, ServerName);

        var observed = Observe();
        Assert.Equal(["bf2.server.health.status", "bf2.server.memory.resident"], observed.Select(m => m.Name).Order());
        Assert.Equal(3, Sum("bf2.server.health.status"));
    }

    [Fact]
    public async Task ThePlayersConnectionsAreSummedUpPerServer()
    {
        await _store.AddAsync(ServerId, Window(), Noon, ServerName);
        await _store.AddConnectionsAsync(ServerId,
            new EvPlayerConnections(2, 300, [Connection(1, ping: 40), Connection(2, ping: 60, pingMax: 90, lostTo: 2), Connection(3, readings: 1, ping: 500)]),
            _ => "pilot", Noon);

        // The counter leaves the partial player out; one Good, one Bad (10 % loss to the player)
        Assert.Equal(1, Sum("bf2.player.connection.windows", "status", "Good"));
        Assert.Equal(1, Sum("bf2.player.connection.windows", "status", "Bad"));
        Assert.Equal(0, Sum("bf2.player.connection.windows", "status", "Warn"));

        Observe();
        // Every player is counted, every status reported
        Assert.Equal(2, Sum("bf2.server.players.connected", "status", "Good"));
        Assert.Equal(1, Sum("bf2.server.players.connected", "status", "Bad"));
        Assert.Single(_measurements, m => m.Name == "bf2.server.players.connected" && m.Tags["status"] == "Warn" && m.Value == 0);
        // The figures are of the full windows only
        Assert.Equal(50, Sum("bf2.player.ping", "stat", "median"));
        Assert.Equal(90, Sum("bf2.player.ping", "stat", "max"));
        Assert.Equal(10, _measurements.Single(m => m.Name == "bf2.player.loss" && m.Tags["direction"] == "to_player" && m.Tags["stat"] == "max").Value);
        Assert.Equal(5, _measurements.Single(m => m.Name == "bf2.player.loss" && m.Tags["direction"] == "to_player" && m.Tags["stat"] == "mean").Value);
    }
}
