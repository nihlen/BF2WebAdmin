using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Modules;
using MaxMind.GeoIP2.Responses;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// The player stream corrects team, alive and seat when it disagrees with the model (from events and snapshots) for
/// <see cref="AdminStateApplier.StreamCorrectionDelay"/>, and not before: a latest-wins frame can be ahead of an event.
/// </summary>
public class AdminStateApplierTests
{
    private const byte PlayerId = 3;
    private const uint Heli = 77;

    // Streams run on the 30 Hz tick; the modules get the player stream at 4 Hz, every 8th tick
    private uint _tick = 1000;

    private static GameServer Server()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var info = new ServerInfo { ServerId = "127.0.0.1:16569", IsTestServer = true };
        return new GameServer(info, IPAddress.Loopback, services, CancellationToken.None);
    }

    private static AdminPlayerState State(byte team, bool alive = true, uint vehicle = 0) => new(
        team, 0, alive ? AdminPlayerState.LifeAlive : AdminPlayerState.LifeDead, vehicle != 0 ? AdminPlayerState.FlagSeated : (byte)0, 50,
        vehicle, vehicle != 0 ? 12 : AdminProtocol.NoConsoleId, vehicle != 0 ? (byte)0 : (byte)255, new Vec3(1, 2, 3), default, default, 1,
        AdminPlayerState.ViewInside, 0);

    private static async Task<GameServer> WithPlayerAsync(AdminPlayerState state)
    {
        var server = Server();
        var info = new AdminPlayerInfo(PlayerId, "pilot", AdminPlayerInfo.FlagConnected, 0, "", "", new AdminPlayerScore(0, 0, 0, 0, 0, -1), state);
        await server.Applier.OnMessageAsync(null!, new Snapshot(1, 10, 900, 30, new AdminGameState(1, 3, -1, 100, 100, 10, 0, 0, 0), [info],
            [new AdminVehicleState(Heli, 12, "ahe_ah1z", 1, 0, default, default, default, 1, 2, 0)], [], []));
        return server;
    }

    /// <summary>A player stream frame <paramref name="seconds"/> after the previous one (8 ticks: the next one the modules get).</summary>
    private Task StreamAsync(GameServer server, AdminPlayerState state, double seconds = 8 / 30.0)
    {
        _tick += (uint)System.Math.Round(seconds * 30);
        return server.Applier.OnMessageAsync(null!, new PlayerStream(_tick, _tick / 30.0, [(PlayerId, state)])).AsTask();
    }

    [Fact]
    public async Task AMissedTeamChangeIsCorrectedAfterASecond()
    {
        var server = await WithPlayerAsync(State(team: 1));
        var player = server.GetPlayer(PlayerId)!;

        await StreamAsync(server, State(team: 2));
        await StreamAsync(server, State(team: 2));
        await StreamAsync(server, State(team: 2));
        Assert.Equal(1, player.Team.Id);

        await StreamAsync(server, State(team: 2), seconds: 0.5);
        Assert.Equal(2, player.Team.Id);
        Assert.Equal(1, server.Applier.StreamCorrections);
    }

    [Fact]
    public async Task AFrameAheadOfItsEventChangesNothing()
    {
        var server = await WithPlayerAsync(State(team: 1));
        var player = server.GetPlayer(PlayerId)!;

        // The frame was applied before the queued event that explains it
        await StreamAsync(server, State(team: 2, vehicle: Heli));
        await server.Applier.OnMessageAsync(null!, new EvTeamChange(11, _tick, PlayerId, 1, 2, 0));
        await server.Applier.OnMessageAsync(null!, new EvVehicle(AdminMessageType.EvEnterVehicle, 12, _tick, PlayerId, 0, 0, Heli, 12, "ahe_ah1z", "ahe_ah1z"));
        for (var i = 0; i < 6; i++)
            await StreamAsync(server, State(team: 2, vehicle: Heli));

        Assert.Equal((2, (int)Heli), (player.Team.Id, player.Vehicle!.RootVehicleId));
        Assert.Equal(0, server.Applier.StreamCorrections);
    }

    [Fact]
    public async Task AMissedSeatIsCorrectedAndAChangingReportWaitsAgain()
    {
        var server = await WithPlayerAsync(State(team: 1));
        var player = server.GetPlayer(PlayerId)!;

        // Seated, then dead before a second passed: a different report, so the wait starts again
        await StreamAsync(server, State(team: 1, vehicle: Heli));
        await StreamAsync(server, State(team: 1, alive: false), seconds: 0.9);
        await StreamAsync(server, State(team: 1, alive: false), seconds: 0.5);
        Assert.True(player.IsAlive);

        await StreamAsync(server, State(team: 1, alive: false), seconds: 0.6);
        Assert.False(player.IsAlive);

        await server.Applier.OnMessageAsync(null!, new EvSpawn(11, _tick, PlayerId, 1, 0, AdminProtocol.NoConsoleId, "", default, default));
        await StreamAsync(server, State(team: 1, vehicle: Heli));
        await StreamAsync(server, State(team: 1, vehicle: Heli), seconds: 1.0);
        Assert.Equal((int)Heli, player.Vehicle?.RootVehicleId);
        Assert.Equal("ahe_ah1z", player.Vehicle!.RootVehicleTemplate);
        Assert.Equal(2, server.Applier.StreamCorrections);
    }

    [Fact]
    public async Task ADeathTakesThePlayerOutOfTheVehicle()
    {
        var server = await WithPlayerAsync(State(team: 1, vehicle: Heli));
        var player = server.GetPlayer(PlayerId)!;
        Assert.Equal((int)Heli, player.Vehicle?.RootVehicleId);

        await server.Applier.OnMessageAsync(null!, new EvDeath(11, _tick, PlayerId, 1, Heli, "ahe_ah1z", default));
        Assert.Null(player.Vehicle);
        Assert.Equal((int)Heli, player.PreviousVehicle?.RootVehicleId);
        Assert.Empty(server.Vehicles.Single(v => v.RootVehicleId == Heli).Players);

        // An ExitVehicle after the death changes nothing, and the dead player's stream agrees with the model
        await server.Applier.OnMessageAsync(null!, new EvVehicle(AdminMessageType.EvExitVehicle, 12, _tick, PlayerId, 0, 0, Heli, 12, "ahe_ah1z", "ahe_ah1z"));
        for (var i = 0; i < 6; i++)
            await StreamAsync(server, State(team: 1, alive: false));

        Assert.Equal(0, server.Applier.StreamCorrections);
    }

    [Fact]
    public async Task ANewPlayerHasItsCountryBeforeTheJoinEvent()
    {
        var countries = Substitute.For<ICountryResolver>();
        countries.GetCountryResponse("198.51.100.7").Returns(System.Text.Json.JsonSerializer.Deserialize<CountryResponse>("""{"country":{"iso_code":"SE","names":{"en":"Sweden"}}}"""));
        var services = new ServiceCollection().AddLogging().AddSingleton(countries).BuildServiceProvider();
        var server = new GameServer(new ServerInfo { ServerId = "127.0.0.1:16569", IsTestServer = true }, IPAddress.Loopback, services, CancellationToken.None);

        var info = new AdminPlayerInfo(PlayerId, "pilot", AdminPlayerInfo.FlagConnected, 0, "198.51.100.7", "", new AdminPlayerScore(0, 0, 0, 0, 0, -1), State(1));
        await server.Applier.OnMessageAsync(null!, new EvPlayerConnect(1, 1, info));

        Assert.Equal(("SE", "Sweden"), (server.GetPlayer(PlayerId)!.Country.Code, server.GetPlayer(PlayerId)!.Country.Name));
    }
}

public class PingStatisticsTests
{
    [Fact]
    public void AverageAndMedianOfTheSamples()
    {
        // 40 x3, 50 x1, 200 x1: the median is the middle sample, not the last one seen
        var samples = new SortedDictionary<int, int> { [200] = 1, [40] = 3, [50] = 1 };
        Assert.Equal((74, 40), BF2Module.PingStatistics(samples));
        Assert.Equal((60, 60), BF2Module.PingStatistics(new SortedDictionary<int, int> { [60] = 2 }));
    }
}
