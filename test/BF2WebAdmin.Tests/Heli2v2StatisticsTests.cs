using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Modules.Heli2v2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Match = BF2WebAdmin.Server.Modules.Heli2v2.Match;

namespace BF2WebAdmin.Tests;

/// <summary>Heli2v2's ready detection and its match and round summaries.</summary>
public class Heli2v2StatisticsTests
{
    [Theory]
    [InlineData("rdy", ReadyChat.Ready)]
    [InlineData("RDY", ReadyChat.Ready)]
    [InlineData("ready", ReadyChat.Ready)]
    [InlineData("go", ReadyChat.Ready)]
    [InlineData("gogo", ReadyChat.Ready)]
    [InlineData("rdy!", ReadyChat.Ready)]
    [InlineData("jrdy", ReadyChat.Ready)]
    [InlineData("rdy pls", ReadyChat.Ready)]
    [InlineData("ok go", ReadyChat.Ready)]
    [InlineData("pad", ReadyChat.Pad)]
    [InlineData("jpad", ReadyChat.Pad)]
    [InlineData("PAD", ReadyChat.Pad)]
    [InlineData("yes", ReadyChat.None)]
    [InlineData("gg", ReadyChat.None)]
    [InlineData("hello", ReadyChat.None)]
    [InlineData("good game", ReadyChat.None)]
    [InlineData("", ReadyChat.None)]
    public void ReadyMessage_Parse(string text, ReadyChat expected)
    {
        Assert.Equal(expected, ReadyMessage.Parse(text));
    }

    [Fact]
    public void TvShot_AddsTheDistanceAndTheTurns()
    {
        var (distance, angle) = MatchStatistics.TvShot(TvPath());

        Assert.Equal(20, distance, 3);
        Assert.Equal(90, angle, 3);
        Assert.Equal((0d, 0d), MatchStatistics.TvShot(null));
    }

    [Fact]
    public void RoundSummary_WinByTv()
    {
        var match = TwoRoundMatch(out var tvWin, out _);

        var text = MatchStatistics.RoundSummary(match, tvWin, "China");

        Assert.StartsWith("```md\nRound 2 won by China in 30 s", text);
        Assert.Contains("TV: est. 20m 90°", text);
        Assert.Contains("> 1+ vs 0 <", text);
    }

    [Fact]
    public void RoundSummary_Draw()
    {
        var match = TwoRoundMatch(out _, out var draw);

        var text = MatchStatistics.RoundSummary(match, draw, null);

        Assert.Contains("ended in a CLASSIC DRAW in 30 s", text);
        Assert.DoesNotContain("+", text);
    }

    [Fact]
    public void Build_ScoreDrawsAndTvCurve_NoEmbedForAShortMatch()
    {
        var match = TwoRoundMatch(out _, out _);

        var report = MatchStatistics.Build(match, new DateTimeOffset(Start.AddMinutes(5), TimeSpan.Zero), NullLogger.Instance);

        Assert.Contains("Score: 1 - 0 for <", report.Text);
        Assert.Contains("Draw rounds: 1", report.Text);
        Assert.Contains("Biggest TV curve kill: est. 20 m 90°", report.Text);
        Assert.Contains("Round duration: avg. 30 s / longest 30 s / shortest 30 s", report.Text);
        Assert.False(report.PostEmbed);
        Assert.Equal("dalian_2_v_2", Assert.Single(report.Embed.Fields, f => f.Name == "Map").Value);
    }

    internal static readonly DateTime Start = new(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc);

    private static List<RoundPosition> TvPath() =>
    [
        new() { Position = new Position(0, 150, 0), Rotation = new Rotation(0, 0, 0) },
        new() { Position = new Position(10, 150, 0), Rotation = new Rotation(30, 0, 0) },
        new() { Position = new Position(20, 150, 0), Rotation = new Rotation(90, 0, 0) },
    ];

    /// <summary>China (AB) wins round 1 with a TV missile from B; round 2 is a draw.</summary>
    internal static Match TwoRoundMatch(out Round tvWin, out Round draw)
    {
        var (_, _, players) = Data.Setup2v2Players();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamHashes = ["AB", "CD"],
            Map = "dalian_2_v_2",
            ServerName = "Test",
            MatchStart = Start,
            MatchEnd = Start.AddSeconds(100),
        };

        Round NewRound(int startSeconds)
        {
            var round = new Round { Id = Guid.NewGuid(), MatchId = match.Id, RoundStart = Start.AddSeconds(startSeconds), RoundEnd = Start.AddSeconds(startSeconds + 30) };
            foreach (var player in players)
                round.Players.Add(new RoundPlayer(player, player.Team.Id == 1 ? "AB" : "CD", round.Id));
            match.Rounds.Add(round);
            return round;
        }

        tvWin = NewRound(0);
        tvWin.WinningTeamId = 1;
        tvWin.WinningTeamHash = "AB";
        tvWin.LosingTeamHash = "CD";
        tvWin.Players[1].ProjectilePaths.Add(TvPath());
        foreach (var victim in new[] { tvWin.Players[2], tvWin.Players[3] })
        {
            victim.IsAlive = false;
            victim.KillerHash = "B";
            victim.KillerWeapon = "agm114_hellfire_tv";
        }

        draw = NewRound(60);
        draw.WinningTeamId = -1;
        return match;
    }
}
