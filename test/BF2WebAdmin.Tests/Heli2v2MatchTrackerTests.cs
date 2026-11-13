using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Heli2v2's matches and rounds as they are played (<see cref="MatchTracker"/>).</summary>
public class Heli2v2MatchTrackerTests
{
    private readonly DateTimeOffset _start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);
    private readonly IGameServer _server = Substitute.For<IGameServer>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IMatchRepository _repository = Substitute.For<IMatchRepository>();
    private readonly MatchTracker _tracker;
    private readonly Player[] _players;

    public Heli2v2MatchTrackerTests()
    {
        _server.Map.Returns(new Map { Name = "dalian_2_v_2" });
        _server.Id.Returns("Test server");
        (_, _, _players) = Data.Setup2v2Players();
        _server.Players.Returns(_players);
        foreach (var player in _players)
            _server.GetPlayerByHash(player.Hash).Returns(player);

        var store = new MatchStore(_repository, Substitute.For<IReadOnlyPolicyRegistry<string>>(), NullLogger.Instance);
        _tracker = new MatchTracker(_server, store, Substitute.For<IDelayProvider>(), new FakeTimeProvider(_start), Substitute.For<ITaskRunner>(),
            () => _mediator, _ => Task.CompletedTask, NullLogger.Instance, CancellationToken.None);
    }

    private Message Chat(Player player, string text) => new() { Channel = ChatChannel.Global, Type = MessageType.Player, Text = text, Player = player };

    private async Task StartRoundAsync()
    {
        await _tracker.OnChatAsync(Chat(_players[1], "rdy"));
        await _tracker.OnChatAsync(Chat(_players[3], "go"));
    }

    [Fact]
    public async Task Pad_DropsThePendingRound()
    {
        await _tracker.OnChatAsync(Chat(_players[1], "rdy"));
        await _tracker.OnChatAsync(Chat(_players[0], "pad"));
        await _tracker.OnChatAsync(Chat(_players[3], "go"));

        await _mediator.DidNotReceiveWithAnyArgs().PublishAsync(default(RoundStartEvent)!);
        Assert.Null(_tracker.ActiveMatch);
    }

    [Fact]
    public async Task APlayerLeaving_EndsTheMatch_WhenTwoOfItsPlayersAreLeft()
    {
        await StartRoundAsync();
        var match = _tracker.ActiveMatch;
        Assert.NotNull(match);
        match.Rounds[0].RoundEnd = _start.UtcDateTime.AddSeconds(30); // Played; an active round would go with the player

        _server.Players.Returns(_players.Take(3).ToArray());
        await _tracker.OnPlayerLeftAsync(_players[3].Hash);
        await _mediator.DidNotReceiveWithAnyArgs().PublishAsync(default(MatchEndEvent)!);

        _server.Players.Returns(_players.Take(2).ToArray());
        await _tracker.OnPlayerLeftAsync(_players[2].Hash);
        await _mediator.Received(1).PublishAsync(Arg.Is<MatchEndEvent>(e => e.Match == match && e.Match.MatchEnd != null));
        Assert.Null(_tracker.ActiveMatch);
    }

    [Fact]
    public async Task TheSameMap_KeepsTheMatch_AnotherMapEndsIt()
    {
        await StartRoundAsync();
        var match = _tracker.ActiveMatch;

        Assert.False(await _tracker.OnMapChangedAsync("dalian_2_v_2", endMatch: true));
        Assert.Same(match, _tracker.ActiveMatch);

        Assert.True(await _tracker.OnMapChangedAsync("other_map", endMatch: true));
        await _mediator.Received(1).PublishAsync(Arg.Is<MatchEndEvent>(e => e.Match == match));
        Assert.Null(_tracker.ActiveMatch);
    }

    [Fact]
    public async Task APreviousMatchWithADraw_IsResumed()
    {
        var previousId = Guid.NewGuid();
        var previous = new BF2WebAdmin.Data.Entities.Match
        {
            Id = previousId,
            MatchStart = _start.UtcDateTime.AddMinutes(-20),
            Map = "dalian_2_v_2",
            ServerId = "Test server",
            TeamAHash = "AB",
            TeamBHash = "CD",
            MatchRounds = new List<MatchRound>
            {
                Round(-1, _start.UtcDateTime.AddMinutes(-5)),
                Round(_players[0].Team.Id, _start.UtcDateTime.AddMinutes(-2)),
            },
        };
        _repository.GetMatchesByNewestAsync(0, 1).Returns([previous]);
        _repository.GetMatchAsync(previous.Id).Returns(previous);

        await StartRoundAsync();

        await _mediator.DidNotReceiveWithAnyArgs().PublishAsync(default(MatchStartEvent)!);
        await _mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.MatchId == previous.Id));
        Assert.Equal(3, _tracker.ActiveMatch!.Rounds.Count);

        MatchRound Round(int winningTeamId, DateTime end) => new()
        {
            Id = previousId,
            MatchId = previousId,
            RoundStart = end.AddSeconds(-30),
            RoundEnd = end,
            WinningTeamId = winningTeamId,
            MatchRoundPlayers = _players.Select(p => new MatchRoundPlayer { PlayerHash = p.Hash, TeamId = p.Team.Id }).ToList(),
        };
    }
}
