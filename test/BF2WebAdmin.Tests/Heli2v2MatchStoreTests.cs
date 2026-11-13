using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly;
using Polly.Registry;
using Xunit;
using MatchEntity = BF2WebAdmin.Data.Entities.Match;

namespace BF2WebAdmin.Tests;

/// <summary>Heli2v2's matches in the database (<see cref="MatchStore"/>).</summary>
public class Heli2v2MatchStoreTests
{
    [Fact]
    public void ToEntityAndBack_KeepsTheScoreAndTheDraw()
    {
        var match = Heli2v2StatisticsTests.TwoRoundMatch(out var tvWin, out var draw);
        var players = tvWin.Players.Select(p => p.Player).ToDictionary(p => p.Hash);

        var entity = MatchStore.ToEntity(match, mapRounds: true);
        Assert.Equal(("AB", "CD", 1, 0), (entity.TeamAHash, entity.TeamBHash, entity.TeamAScore, entity.TeamBScore));
        Assert.Equal(2, entity.MatchRounds.Count);

        var resumed = MatchStore.FromEntity(entity, hash => players.GetValueOrDefault(hash));
        Assert.Equal(match.Id, resumed.Id);
        Assert.Equal(2, resumed.Rounds.Count);
        Assert.Equal(("AB", "CD"), (resumed.Rounds[0].WinningTeamHash, resumed.Rounds[0].LosingTeamHash));
        // A draw round has no winner; it made resuming fail
        Assert.Equal((-1, null, null), (resumed.Rounds[1].WinningTeamId, resumed.Rounds[1].WinningTeamHash, resumed.Rounds[1].LosingTeamHash));
        Assert.All(resumed.Rounds[1].Players, p => Assert.Contains(p.TeamHash, new[] { "AB", "CD" }));
    }

    [Fact]
    public void Paths_AreStoredAsTheSameJsonAsBefore()
    {
        // The text Newtonsoft.Json wrote: the export API and older rows have it
        var match = Heli2v2StatisticsTests.TwoRoundMatch(out var tvWin, out _);
        var player = tvWin.Players[0];
        player.MovementPath = new List<RoundPosition>
        {
            new() { Timestamp = 12, Position = new Common.Entities.Game.Position(1, -2.5, 3), Rotation = new Common.Entities.Game.Rotation(90, 0, -1), Ping = 30 },
        };
        player.ProjectilePaths = new List<IList<RoundPosition>> { new List<RoundPosition> { new() { Timestamp = 13, Position = new Common.Entities.Game.Position(4, 5, 6), Rotation = new Common.Entities.Game.Rotation(0, 0, 0) } }, new List<RoundPosition>() };

        var stored = MatchStore.ToEntity(tvWin).MatchRoundPlayers.Single(p => p.PlayerHash == player.Player.Hash);
        Assert.Equal(@"[""12|1.000/-2.500/3.000|90.000/0.000/-1.000|30""]", Common.Compression.DecompressText(stored.MovementPathJson));
        Assert.Equal(@"[[""13|4.000/5.000/6.000|0.000/0.000/0.000|0""],[]]", Common.Compression.DecompressText(stored.ProjectilePathsJson));
    }

    [Fact]
    public async Task Writes_RunInTheOrderTheyWereQueued()
    {
        var match = Heli2v2StatisticsTests.TwoRoundMatch(out var tvWin, out var draw);
        var repository = Substitute.For<IMatchRepository>();
        var release = new TaskCompletionSource();
        repository.InsertMatchAsync(Arg.Any<MatchEntity>()).Returns(release.Task);
        var policies = new PolicyRegistry { { PolicyNames.RetryPolicyLongAsync, Policy.NoOpAsync() } };
        var store = new MatchStore(repository, policies, NullLogger.Instance);

        _ = store.InsertMatch(match);
        _ = store.InsertRound(tvWin);
        var last = store.UpdateMatch(match);

        // The round waits for its match
        await Task.Delay(50);
        await repository.DidNotReceiveWithAnyArgs().InsertRoundAsync(default!);

        release.SetResult();
        await last.WaitAsync(TimeSpan.FromSeconds(5));
        Received.InOrder(() =>
        {
            repository.InsertMatchAsync(Arg.Is<MatchEntity>(m => m.Id == match.Id));
            repository.InsertRoundAsync(Arg.Is<MatchRound>(r => r.Id == tvWin.Id));
            repository.UpdateMatchAsync(Arg.Is<MatchEntity>(m => m.Id == match.Id));
        });
    }
}
