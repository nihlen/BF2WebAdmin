using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server.Api;
using BF2WebAdmin.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Match = BF2WebAdmin.Data.Entities.Match;

namespace BF2WebAdmin.Tests;

/// <summary>The export feed (/api/export/v1): the match cursor, the stale-match closer and the export documents.</summary>
public class ExportTests
{
    internal const string HashA1 = "aaaa1111aaaa1111aaaa1111aaaa1111", HashA2 = "aaaa2222aaaa2222aaaa2222aaaa2222";
    internal const string HashB1 = "bbbb1111bbbb1111bbbb1111bbbb1111", HashB2 = "bbbb2222bbbb2222bbbb2222bbbb2222";
    internal static readonly DateTime Start = new(2026, 11, 8, 12, 0, 0);

    // ---------------------------------------------------------------- stale matches

    [Fact]
    public void AnIdleMatchIsClosedWithItsRoundScores()
    {
        var match = NewMatch(Start);
        match.MatchRounds.Add(NewRound(match.Id, Start.AddMinutes(1), winningTeam: 1));
        match.MatchRounds.Add(NewRound(match.Id, Start.AddMinutes(4), winningTeam: 2));
        match.MatchRounds.Add(NewRound(match.Id, Start.AddMinutes(7), winningTeam: 1));
        var lastRoundEnd = Start.AddMinutes(9);

        Assert.False(StaleMatchCloser.TryClose(match, lastRoundEnd.AddMinutes(59)));
        Assert.Null(match.MatchEnd);

        Assert.True(StaleMatchCloser.TryClose(match, lastRoundEnd.AddHours(1)));
        Assert.Equal(lastRoundEnd, match.MatchEnd);
        Assert.Equal(2, match.TeamAScore);
        Assert.Equal(1, match.TeamBScore);

        Assert.False(StaleMatchCloser.TryClose(match, lastRoundEnd.AddHours(5)));
    }

    [Fact]
    public void AMatchWithoutRoundsEndsAtItsStart()
    {
        var match = NewMatch(Start);
        Assert.True(StaleMatchCloser.TryClose(match, Start.AddHours(2)));
        Assert.Equal(Start, match.MatchEnd);
        Assert.Equal(0, match.TeamAScore);
        Assert.Equal(0, match.TeamBScore);
    }

    // ---------------------------------------------------------------- export documents

    [Fact]
    public void TheExportDecodesThePathsAndMarksTimesUtc()
    {
        var match = NewMatch(Start);
        match.ChangeSeq = 7;
        match.MatchRounds.Add(NewRound(match.Id, Start.AddMinutes(1), winningTeam: 2));
        var player = match.MatchRounds[0].MatchRoundPlayers.First(p => p.PlayerHash == HashA1);
        player.ProjectilePathsJson = "not base64";

        var export = ExportEndpoints.ToExport(match, NullLogger.Instance);

        Assert.Equal(7, export.ChangeSeq);
        Assert.Equal(DateTimeKind.Utc, export.MatchStart!.Value.Kind);
        var round = Assert.Single(export.Rounds);
        Assert.Equal(DateTimeKind.Utc, round.RoundEnd!.Value.Kind);
        Assert.Equal(new[] { HashA1, HashA2, HashB1, HashB2 }, round.Players.Select(p => p.PlayerHash));

        var exported = round.Players[0];
        Assert.Equal("100.000/50.000/-20.000", exported.StartPosition);
        Assert.Equal(new[] { "0|1.000/2.000/3.000|0.000/0.000/0.000|40", "500|1.500/2.000/3.000|0.000/0.000/0.000|41" },
            exported.MovementPath!.Value.EnumerateArray().Select(e => e.GetString()));
        Assert.Null(exported.ProjectilePaths);
        Assert.Equal(1, round.Players[1].ProjectilePaths!.Value.GetArrayLength());
    }

    // ---------------------------------------------------------------- SQLite (EF)

    [Fact]
    public async Task SqliteWritesGiveIncreasingChangeSeqs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(connection).Options;
        await using (var context = new BF2Context(options))
            await context.Database.MigrateAsync();

        var factory = new DelegateContextFactory(() => new BF2Context(options));
        IMatchRepository Repository() => new MatchRepository(factory);
        await AssertCursorBehaviourAsync(Repository);

        {
            var joins = new JoinHistoryRepository(factory);
            for (var i = 0; i < 3; i++)
                await joins.InsertAsync(new PlayerJoin { Name = $"p{i}", Guid = HashA1, IpAddress = "203.0.113.7", ServerIpAddress = "198.51.100.20", JoinDate = Start });
            var after = await joins.GetJoinsSinceAsync(1, 10);
            Assert.Equal(new[] { "p1", "p2" }, after.Select(j => j.Name));
            Assert.Single(await joins.GetJoinsSinceAsync(0, 1));
        }
    }

    [Fact]
    public async Task SqliteMigrationNumbersExistingMatchesInStartOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(connection).Options;
        await using (var context = new BF2Context(options))
        {
            await context.GetService<IMigrator>().MigrateAsync("20261108070933_JoinHistory");
            foreach (var (id, start) in new[] { ("c", Start.AddHours(2)), ("a", Start), ("b", Start.AddHours(1)) })
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO ""Matches"" (""Id"", ""TeamAScore"", ""TeamBScore"", ""MatchStart"") VALUES ({0}, 0, 0, {1})",
                    Guid.Parse(new string(id[0], 8) + "-0000-0000-0000-000000000000"), start);
            }

            await context.Database.MigrateAsync();
            var seqs = await context.Matches.OrderBy(m => m.MatchStart).Select(m => m.ChangeSeq).ToListAsync();
            Assert.Equal(new long?[] { 1, 2, 3 }, seqs);
        }
    }

    /// <summary>Insert, a round, the end: each write gives the match a higher ChangeSeq, and the feed returns it once, in order.</summary>
    internal static async Task AssertCursorBehaviourAsync(Func<IMatchRepository> repository, int existingMatches = 0)
    {
        var first = NewMatch(Start);
        var second = NewMatch(Start.AddMinutes(30));
        await repository().InsertMatchAsync(first);
        await repository().InsertMatchAsync(second);

        var page = await repository().GetMatchesChangedSinceAsync(0, 100);
        Assert.Equal(existingMatches + 2, page.Count);
        var cursor = page[existingMatches].ChangeSeq!.Value;
        Assert.Equal(first.Id, page[existingMatches].Id);
        Assert.Equal(second.Id, page[^1].Id);

        await repository().InsertRoundAsync(NewRound(first.Id, Start.AddMinutes(1), winningTeam: 1));
        await repository().InsertRoundAsync(NewRound(first.Id, Start.AddMinutes(4), winningTeam: 2));
        first.TeamAScore = 1;
        first.TeamBScore = 1;
        first.MatchEnd = Start.AddMinutes(6);
        await repository().UpdateMatchAsync(first);

        var changed = await repository().GetMatchesChangedSinceAsync(cursor, 100);
        Assert.Equal(new[] { second.Id, first.Id }, changed.Select(m => m.Id));
        Assert.True(changed[1].ChangeSeq > changed[0].ChangeSeq);

        var exported = changed[1];
        Assert.Equal(Start.AddMinutes(6), exported.MatchEnd);
        Assert.Equal(1, exported.TeamAScore);
        Assert.Equal(2, exported.MatchRounds.Count);
        Assert.Equal(Start.AddMinutes(1), exported.MatchRounds[0].RoundStart);
        Assert.Equal(0.5, exported.MatchRounds[0].PositionTrackerInterval, 5);
        var player = exported.MatchRounds[0].MatchRoundPlayers.Single(p => p.PlayerHash == HashA1);
        Assert.Equal(2, ExportEndpoints.DecodePath(player.MovementPathJson, first.Id, NullLogger.Instance)!.Value.GetArrayLength());
        Assert.Equal("100.000/50.000/-20.000", player.StartPosition.ToString());

        Assert.Empty(await repository().GetMatchesChangedSinceAsync(changed[1].ChangeSeq!.Value, 100));
        Assert.Single(await repository().GetMatchesChangedSinceAsync(cursor, 1));

        var unfinished = await repository().GetUnfinishedMatchesAsync();
        Assert.Equal(second.Id, Assert.Single(unfinished).Id);
    }

    internal static Match NewMatch(DateTime start) => new()
    {
        Id = Guid.NewGuid(),
        ServerId = "198.51.100.20:16567",
        ServerName = "Test 2v2",
        Map = "dalian_plant",
        Type = "HELI_2V2",
        TeamAHash = HashA1 + HashA2,
        TeamAName = "A",
        TeamBHash = HashB1 + HashB2,
        TeamBName = "B",
        MatchStart = start,
    };

    internal static MatchRound NewRound(Guid matchId, DateTime start, int winningTeam)
    {
        var round = new MatchRound
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            WinningTeamId = winningTeam,
            PositionTrackerInterval = 0.5,
            RoundStart = start,
            RoundEnd = start.AddMinutes(2),
        };

        foreach (var (hash, team) in new[] { (HashA1, 1), (HashA2, 1), (HashB1, 2), (HashB2, 2) })
        {
            round.MatchRoundPlayers.Add(new MatchRoundPlayer
            {
                RoundId = round.Id,
                MatchId = matchId,
                PlayerHash = hash,
                PlayerName = "player " + hash[..4],
                TeamId = team,
                SaidGo = true,
                StartPosition = new Position(100, 50, -20),
                MovementPathJson = Compression.CompressText("[\"0|1.000/2.000/3.000|0.000/0.000/0.000|40\",\"500|1.500/2.000/3.000|0.000/0.000/0.000|41\"]"),
                ProjectilePathsJson = Compression.CompressText("[[\"0|1.000/2.000/3.000|0.000/0.000/0.000|40\"]]"),
            });
        }

        return round;
    }
}
