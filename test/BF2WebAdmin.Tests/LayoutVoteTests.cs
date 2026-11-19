using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Layout votes: the rules, and LayoutVoteModule counting the module's ballots, ending early and announcing.</summary>
public class LayoutVoteTests
{
    [Theory]
    [InlineData(false, 0, 1)]
    [InlineData(false, 1, 1)]
    [InlineData(false, 2, 2)]
    [InlineData(false, 7, 2)]
    [InlineData(true, 4, 3)]
    public void RequiredYesVotes(bool matchRunning, int humans, int required) =>
        Assert.Equal(required, LayoutVoteRules.RequiredYes(matchRunning, humans));

    [Theory]
    // yes, no, undecided, required -> decision
    [InlineData(2, 0, 0, 2, LayoutVoteDecision.Passed)]
    [InlineData(2, 0, 1, 2, LayoutVoteDecision.Passed)]   // the last voter cannot make it a tie
    [InlineData(2, 1, 1, 2, LayoutVoteDecision.Open)]     // 2-2 would fail
    [InlineData(2, 2, 0, 2, LayoutVoteDecision.Failed)]   // a tie is no majority
    [InlineData(1, 0, 0, 2, LayoutVoteDecision.Failed)]   // nobody left to give the second yes
    [InlineData(1, 0, 1, 2, LayoutVoteDecision.Open)]
    [InlineData(0, 1, 1, 2, LayoutVoteDecision.Failed)]
    [InlineData(1, 0, 0, 1, LayoutVoteDecision.Passed)]   // one player alone
    [InlineData(2, 0, 2, 3, LayoutVoteDecision.Open)]     // a match: 3 of its players
    [InlineData(3, 1, 0, 3, LayoutVoteDecision.Passed)]
    [InlineData(2, 2, 0, 3, LayoutVoteDecision.Failed)]
    [InlineData(2, 3, 5, 2, LayoutVoteDecision.Open)]
    public void DecidesOnceTheResultIsCertain(int yes, int no, int undecided, int required, LayoutVoteDecision decision) =>
        Assert.Equal(decision, LayoutVoteRules.Decide(yes, no, undecided, required));

    [Theory]
    [InlineData(2, 0, 2, true)]
    [InlineData(2, 1, 2, true)]
    [InlineData(2, 2, 2, false)]
    [InlineData(1, 0, 2, false)]
    [InlineData(1, 0, 1, true)]
    [InlineData(3, 2, 3, true)]
    public void AtTheEnd(int yes, int no, int required, bool passes) =>
        Assert.Equal(passes, LayoutVoteRules.PassesAtEnd(yes, no, required));

    [Fact]
    public void AnyoneMayStartAVote_TheModuleComesWithTheMapBuilder()
    {
        var attr = typeof(LayoutVoteCommand).GetCustomAttribute<CommandAttribute>()!;
        Assert.Equal(("vote,votemap", BF2WebAdmin.Common.Auth.All), (string.Join(",", attr.Aliases), attr.AuthLevel));
        Assert.Contains(typeof(LayoutVoteModule), ModManager.ModuleTypesFor(["MapModule"]));
        Assert.DoesNotContain(typeof(LayoutVoteModule), ModManager.ModuleTypesFor([]));
    }

    [Fact]
    public async Task AVoteFromTheWebPassesEarly_TheModuleEndsItAndThePlayersAreTold()
    {
        var (server, module, players, time) = Setup();

        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "Web User"));
        // Every human on a team votes: an empty voter list, 2 yes needed with 4 players
        await server.GameWriter.Received(1).StartClientVoteAsync("Load layout 'Towers'?", LayoutVoteRules.VoteSeconds,
            Arg.Is<IReadOnlyList<Player>>(l => l.Count == 0));
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.Contains("Web User started a vote") && s.Contains("2 yes needed")));

        await module.HandleAsync(Ballot(players[0], VoteChoice.Yes));
        await module.HandleAsync(Ballot(players[1], VoteChoice.Yes));
        await server.GameWriter.DidNotReceive().CancelVoteAsync();
        await module.HandleAsync(Ballot(players[2], VoteChoice.Yes));   // 3 yes beat the last voter: certain

        await server.GameWriter.Received(1).CancelVoteAsync();
        server.GameWriter.Received(1).SendText("VOTE PASSED: load layout 'Towers' (yes 3, no 0)");

        // The module's result for the cancelled vote changes nothing
        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeCancelled, 3, 0, 4, DateTimeOffset.UtcNow));
        server.GameWriter.DidNotReceive().SendText(Arg.Is<string>(s => s.StartsWith("VOTE CANCELLED")));
    }

    [Fact]
    public async Task AtTheTimeTheModulesCountsDecide_AndAChangedOrLostBallotCounts()
    {
        var (server, module, players, _) = Setup();
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "Web User"));

        await module.HandleAsync(Ballot(players[0], VoteChoice.Yes));
        await module.HandleAsync(Ballot(players[1], VoteChoice.No));
        await module.HandleAsync(Ballot(players[1], VoteChoice.Yes));   // changed
        await module.HandleAsync(Ballot(players[1], VoteChoice.None));  // left
        await server.GameWriter.DidNotReceive().CancelVoteAsync();

        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeTimeUp, 1, 0, 3, DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SendText("VOTE FAILED: load layout 'Towers' (yes 1, no 0, 2 yes needed)");
    }

    [Fact]
    public async Task OneVoteAtATime_TwoMinutesBetweenAStartersVotes_TheRunningMapOnly()
    {
        var (server, module, players, time) = Setup();
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "Web User"));
        Assert.Equal("A vote is already running.", await StartAsync(module, Layout.Id, "web:2", "Other"));

        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeTimeUp, 0, 0, 4, DateTimeOffset.UtcNow));
        time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal("Wait 60 s before you start another vote.", await StartAsync(module, Layout.Id, "web:1", "Web User"));
        Assert.Null(await StartAsync(module, Layout.Id, "web:2", "Other"));

        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeTimeUp, 0, 0, 4, DateTimeOffset.UtcNow));
        time.Advance(TimeSpan.FromSeconds(61));
        var other = new MapMod { Id = Guid.NewGuid(), Name = "Elsewhere", Map = "kubra_dam" };
        Assert.Equal("The layout 'Elsewhere' is for kubra_dam; the server runs dalian_2_v_2.", await StartAsync(module, other.Id, "web:1", "Web User", other));
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "Web User"));
        Assert.Equal("No such layout.", await StartAsync(module, Guid.NewGuid(), "web:3", "Third", missing: true));
    }

    [Fact]
    public async Task NoVoteWithoutPlayersOrWithAnOlderModule_AndOnePlayerAloneDecides()
    {
        var (server, module, players, _) = Setup();
        server.GameWriter.CanStartClientVotes.Returns(false);
        Assert.Equal("This server cannot run layout votes (its module has no client votes).", await StartAsync(module, Layout.Id, "web:1", "W"));

        server.GameWriter.CanStartClientVotes.Returns(true);
        server.Players.Returns(Array.Empty<Player>());
        Assert.Equal("Nobody is on the server to vote.", await StartAsync(module, Layout.Id, "web:1", "W"));

        server.Players.Returns([players[0]]);
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "W"));
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.Contains("1 yes needed")));
        await module.HandleAsync(Ballot(players[0], VoteChoice.Yes));
        server.GameWriter.Received(1).SendText("VOTE PASSED: load layout 'Towers' (yes 1, no 0)");
    }

    [Fact]
    public async Task TheModuleRefusingIsReported()
    {
        var (server, module, _, _) = Setup();
        server.GameWriter.StartClientVoteAsync(default!, default, default!)
            .ThrowsAsyncForAnyArgs(new AdminCommandException("refused", AdminErrorCode.Refused));
        Assert.Equal("A vote is already running, or nobody can vote.", await StartAsync(module, Layout.Id, "web:1", "W"));

        // Not started: no cooldown
        server.GameWriter.StartClientVoteAsync(default!, default, default!).ReturnsForAnyArgs(RequestId);
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "W"));
    }

    [Fact]
    public async Task ACancelledVoteIsAnnounced_AMapChangeEndsItQuietly()
    {
        var (server, module, _, time) = Setup();
        Assert.Null(await StartAsync(module, Layout.Id, "web:1", "W"));
        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeCancelled, 0, 0, 4, DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SendText("VOTE CANCELLED: load layout 'Towers'");

        Assert.Null(await StartAsync(module, Layout.Id, "web:2", "W2"));
        await module.HandleAsync(new VoteResultEvent(RequestId, EvVoteResult.OutcomeUnloaded, 0, 0, 4, DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SendText(Arg.Is<string>(s => s.StartsWith("VOTE")));
    }

    private const uint RequestId = 77;
    private static readonly MapMod Layout = new() { Id = Guid.NewGuid(), Name = "Towers", Map = "dalian_2_v_2" };

    private static VoteBallotEvent Ballot(Player player, VoteChoice choice) => new(RequestId, player.Index, choice, DateTimeOffset.UtcNow);

    private async Task<string?> StartAsync(LayoutVoteModule module, Guid layoutId, string key, string name, MapMod? layout = null,
        bool missing = false)
    {
        _maps.GetAsync(layoutId).Returns(missing ? null! : layout ?? Layout);
        var result = new TaskCompletionSource<string?>();
        await module.HandleAsync(new LayoutVoteRequestEvent(layoutId, key, name, result, DateTimeOffset.UtcNow));
        return await result.Task;
    }

    private readonly IMapRepository _maps = Substitute.For<IMapRepository>();

    private (IGameServer Server, LayoutVoteModule Module, Player[] Players, FakeTimeProvider Time) Setup()
    {
        var server = Substitute.For<IGameServer>();
        var (_, _, players) = Data.Setup2v2Players();
        server.Players.Returns(players);
        server.Map.Returns(new Map { Name = "dalian_2_v_2" });
        server.GameWriter.CanStartClientVotes.Returns(true);
        server.GameWriter.StartClientVoteAsync(default!, default, default!).ReturnsForAnyArgs(RequestId);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var module = new LayoutVoteModule(server, _maps, time, NullLogger<LayoutVoteModule>.Instance, new CancellationTokenSource());
        return (server, module, players, time);
    }
}
