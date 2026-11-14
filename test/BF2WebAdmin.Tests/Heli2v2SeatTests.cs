using System.Linq;
using System.Reflection;
using System.Threading;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Modules;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>.pilot and .gunner: one teammate's preference is enough, the latest one wins, admins set it for others.</summary>
public class Heli2v2SeatTests
{
    [Fact]
    public void ThePlayerCommandsAreForEveryone_SettingItForAnotherIsForAdmins()
    {
        static CommandAttribute Attr<T>() => typeof(T).GetCustomAttribute<CommandAttribute>()!;

        Assert.Equal(("pilot,fly", Auth.All), (string.Join(",", Attr<PilotCommand>().Aliases), Attr<PilotCommand>().AuthLevel));
        Assert.Equal(("gunner,gun", Auth.All), (string.Join(",", Attr<GunnerCommand>().Aliases), Attr<GunnerCommand>().AuthLevel));
        Assert.Equal(("pilot,fly", Auth.Admin), (string.Join(",", Attr<PilotForCommand>().Aliases), Attr<PilotForCommand>().AuthLevel));
        Assert.Equal(("gunner,gun", Auth.Admin), (string.Join(",", Attr<GunnerForCommand>().Aliases), Attr<GunnerForCommand>().AuthLevel));
        Assert.Contains(typeof(SeatModule), ModManager.ModuleTypesFor(["Heli2v2Module"]));
    }

    [Fact]
    public void OnePlayersPreferenceIsSent_AndTheLatestOneWinsWithinTheTeam()
    {
        var (server, seats, players) = Setup();

        seats.Handle(new GunnerCommand { Message = From(players[0]) });
        server.GameWriter.Received(1).SetSeatPreference(players[0], SeatPreference.Gunner);
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.Contains("CH Pilot") && s.Contains("gunner")));

        // The other team's choice changes nothing for this one
        seats.Handle(new GunnerCommand { Message = From(players[2]) });
        Assert.Equal(SeatPreference.Gunner, seats.PreferenceOf(players[0]));

        // The teammate wants the gunner seat too: the latest one wins
        seats.Handle(new GunnerCommand { Message = From(players[1]) });
        server.GameWriter.Received(1).SetSeatPreference(players[0], SeatPreference.None);
        Assert.Equal((SeatPreference.None, SeatPreference.Gunner, SeatPreference.Gunner),
            (seats.PreferenceOf(players[0]), seats.PreferenceOf(players[1]), seats.PreferenceOf(players[2])));

        // A pilot preference beside it stays
        seats.Handle(new PilotCommand { Message = From(players[0]) });
        Assert.Equal((SeatPreference.Pilot, SeatPreference.Gunner), (seats.PreferenceOf(players[0]), seats.PreferenceOf(players[1])));
    }

    [Fact]
    public void AnAdminSetsItForAnotherPlayer()
    {
        var (server, seats, players) = Setup();
        server.GetPlayerByName("US Gun").Returns(players[3]);

        seats.Handle(new PilotForCommand { Name = "US Gun", Message = From(players[0]) });
        server.GameWriter.Received(1).SetSeatPreference(players[3], SeatPreference.Pilot);

        seats.Handle(new GunnerForCommand { Name = "nobody", Message = From(players[0]) });
        server.GameWriter.Received(1).SendPrivateMessage(players[0], "Player not found");
    }

    [Fact]
    public async System.Threading.Tasks.Task AServerWithoutSeatPreferencesSaysSo_AndALeavingPlayerIsForgotten()
    {
        var (server, seats, players) = Setup();
        server.GameWriter.SetSeatPreference(players[1], Arg.Any<SeatPreference>()).Returns(false);

        seats.Handle(new PilotCommand { Message = From(players[1]) });
        server.GameWriter.Received(1).SendPrivateMessage(players[1], "This server can't choose seats");
        Assert.Equal(SeatPreference.None, seats.PreferenceOf(players[1]));

        seats.Handle(new PilotCommand { Message = From(players[0]) });
        await seats.HandleAsync(new PlayerLeftEvent(players[0], System.DateTimeOffset.UtcNow));
        Assert.Equal(SeatPreference.None, seats.PreferenceOf(players[0]));
    }

    private static Message From(Player player) => new() { Channel = ChatChannel.Global, Text = ".pilot", Type = MessageType.Player, Player = player };

    private static (IGameServer Server, SeatModule Seats, Player[] Players) Setup()
    {
        var server = Substitute.For<IGameServer>();
        server.GameWriter.SetSeatPreference(Arg.Any<Player>(), Arg.Any<SeatPreference>()).Returns(true);
        var (_, _, players) = Data.Setup2v2Players();
        return (server, new SeatModule(server, NullLogger<SeatModule>.Instance, new CancellationTokenSource()), players);
    }
}
