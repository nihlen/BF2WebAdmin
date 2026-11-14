using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Modules;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Heli2v2's practice modules: loaded with it, and a few of their commands.</summary>
public class Heli2v2PracticeTests
{
    [Fact]
    public void TheModulesThatComeWithHeli2v2_LoadWithIt_AndAreNotListedOnTheirOwn()
    {
        Type[] companions = [typeof(PracticeModule), typeof(TvMissileModule), typeof(RecordingModule)];

        var with2v2 = ModManager.ModuleTypesFor(["Heli2v2Module"]);
        Assert.Contains(typeof(Heli2v2Module), with2v2);
        Assert.All(companions, c => Assert.Contains(c, with2v2));
        Assert.Contains(typeof(BF2Module), with2v2);

        var without = ModManager.ModuleTypesFor(["MapModule", "PracticeModule"]);
        Assert.All(companions, c => Assert.DoesNotContain(c, without));
        Assert.Contains(typeof(MapModule), without);

        Assert.Contains("Heli2v2Module", ModuleResolver.AllModuleNames);
        Assert.All(companions, c => Assert.DoesNotContain(c.Name, ModuleResolver.AllModuleNames));
    }

    [Fact]
    public async Task Nasa_KeepsPlayersBelowTheLimit_UntilTheMapChanges()
    {
        var (server, practice, _) = Setup();
        var (_, _, players) = Data.Setup2v2Players();
        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], DateTimeOffset.UtcNow));

        await practice.HandleAsync(new NasaCommand { Value = 100 });
        await practice.HandleAsync(new PlayerPositionEvent(players[0], new Position(1, 300, 2), Rotation.Neutral, 0, DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SendTeleport(players[0], Arg.Is<Position>(p => p.Height == 240 && p.X == 1 && p.Y == 2));

        await practice.HandleAsync(new MapChangedEvent(new Map { Name = "other" }, [], DateTimeOffset.UtcNow));
        server.GameWriter.ClearReceivedCalls();
        await practice.HandleAsync(new PlayerPositionEvent(players[0], new Position(1, 300, 2), Rotation.Neutral, 0, DateTimeOffset.UtcNow));
        server.GameWriter.DidNotReceiveWithAnyArgs().SendTeleport(default!, default!);
    }

    [Fact]
    public async Task AutoPad_NeedsTheModeOfHeli2v2()
    {
        var (server, practice, heli) = Setup();
        var (_, _, players) = Data.Setup2v2Players();
        server.GetPlayerByName("CH Pilot").Returns(players[0]);

        heli.Handle(new Toggle2v2Command());
        await practice.HandleAsync(new AutoPadCommand { Name = "CH Pilot" });
        server.GameWriter.Received(1).SendText("2v2 mode is not currently active");

        heli.Handle(new Toggle2v2Command());
        await practice.HandleAsync(new AutoPadCommand { Name = "CH Pilot" });
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.StartsWith("Autopad enabled for")));
    }

    [Fact]
    public async Task Noclip_IsTurnedOffWhenTheLastPlayerIsOut_OnlyForTheVehicleItWasUsedOn()
    {
        var (server, practice, heli) = Setup();
        var (_, vehicles, players) = Data.Setup2v2Players();
        server.GetPlayerByName("CH Pilot").Returns(players[0]);
        practice.Handle(new NoclipCommand { Name = "CH Pilot", Value = 1 });
        server.GameWriter.Received(1).SetVehicleCollision(vehicles[0], false);

        // The 2v2 mode was turned off meanwhile: the vehicle still gets its collision back
        heli.Handle(new Toggle2v2Command());

        await practice.HandleAsync(Exit(players[1]));
        server.GameWriter.DidNotReceive().SetVehicleCollision(vehicles[0], true);
        await practice.HandleAsync(Exit(players[0]));
        server.GameWriter.Received(1).SetVehicleCollision(vehicles[0], true);

        // Once only, and nothing for the US heli, which .noclip never touched
        await practice.HandleAsync(Exit(players[0]));
        await practice.HandleAsync(Exit(players[2]));
        await practice.HandleAsync(Exit(players[3]));
        server.GameWriter.Received(2).SetVehicleCollision(Arg.Any<Vehicle>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Noclip_IsTurnedOffWhenTheLastPlayerLeavesTheServerInTheVehicle()
    {
        var (server, practice, _) = Setup();
        var (_, vehicles, players) = Data.Setup2v2Players();
        server.GetPlayerByName("US Pilot").Returns(players[2]);
        practice.Handle(new NoclipCommand { Name = "US Pilot", Value = 1 });

        await practice.HandleAsync(Exit(players[3]));

        // GameServer.RemovePlayerAsync: the vehicle lets go of the player, the player keeps its vehicle
        vehicles[1].Players.Remove(players[2]);
        await practice.HandleAsync(new PlayerLeftEvent(players[2], DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SetVehicleCollision(vehicles[1], true);
    }

    /// <summary>What GameServer.UpdatePlayerVehicleAsync does for an exit, and its event.</summary>
    private static PlayerVehicleEvent Exit(Player player)
    {
        if (player.Vehicle is { } vehicle)
        {
            player.PreviousVehicle = vehicle;
            vehicle.Players.Remove(player);
        }

        player.Vehicle = null!;
        return new PlayerVehicleEvent(player, null!, DateTimeOffset.UtcNow);
    }

    private static (IGameServer Server, PracticeModule Practice, Heli2v2Module Heli) Setup()
    {
        var server = Substitute.For<IGameServer>();
        var heli = new Heli2v2Module(server, Substitute.For<IMatchRepository>(), Substitute.For<IDelayProvider>(), TimeProvider.System, Substitute.For<ITaskRunner>(),
            Substitute.For<IReadOnlyPolicyRegistry<string>>(), NullLogger<Heli2v2Module>.Instance, new CancellationTokenSource());
        server.ModManager.GetModule<Heli2v2Module>().Returns(heli);
        var practice = new PracticeModule(server, Substitute.For<IDelayProvider>(), Substitute.For<ITaskRunner>(), NullLogger<PracticeModule>.Instance, new CancellationTokenSource());
        return (server, practice, heli);
    }
}
