using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;

namespace BF2WebAdmin.Data.Repositories;

public class SqlMatchRepository : IMatchRepository
{
    private const string MatchColumns =
        @"[Id]
        ,[ServerId]
        ,[ServerName]
        ,[Map]
        ,[Type]
        ,[TeamAHash]
        ,[TeamAName]
        ,[TeamAScore]
        ,[TeamBHash]
        ,[TeamBName]
        ,[TeamBScore]
        ,[MatchStart]
        ,[MatchEnd]
        ,[ChangeSeq]";

    private const string RoundColumns =
        @"[Id]
        ,[MatchId]
        ,[WinningTeamId]
        ,CAST([PositionTrackerInterval] AS float) AS [PositionTrackerInterval]
        ,[RoundStart]
        ,[RoundEnd]";

    private const string PlayerColumns =
        @"[RoundId]
        ,[PlayerHash]
        ,[MatchId]
        ,[PlayerName]
        ,[TeamId]
        ,[SubVehicle]
        ,[SaidGo]
        ,[StartPosition]
        ,[DeathPosition]
        ,[DeathTime]
        ,[KillerHash]
        ,[KillerWeapon]
        ,[KillerPosition]";

    private const string PathColumns =
        @",[MovementPathJson]
        ,[ProjectilePathsJson]";

    /// <summary>The next export cursor value for a match, from sequence dbo.MatchChangeSeq (see Schema.sql).</summary>
    private const string BumpChangeSeqSql =
        @"UPDATE [dbo].[Match] SET [ChangeSeq] = NEXT VALUE FOR [dbo].[MatchChangeSeq] WHERE [Id] = @Id";

    private readonly string _connectionString;
    protected IDbConnection NewConnection => new SqlConnection(_connectionString);

    public SqlMatchRepository(string connectionString)
    {
        _connectionString = connectionString;

        // As BaseSqlRepository: singular table names, Position and Rotation as strings
        SqlMapperExtensions.TableNameMapper = type => type.Name;
        SqlMapper.AddTypeHandler(new PositionTypeHandler());
        SqlMapper.AddTypeHandler(new RotationTypeHandler());
    }

    public async Task<Match> GetMatchAsync(Guid id)
    {
        using var connection = NewConnection;
        var result = await connection.QueryFirstOrDefaultAsync<Match>(
            $"SELECT {MatchColumns} FROM [dbo].[Match] WHERE [Id] = @Id", new { Id = id });

        if (result == null)
            throw new ArgumentException($"Match {nameof(id)} {id} was not found");

        await LoadRoundsAsync(connection, new[] { result }, withPaths: true);
        return result;
    }

    public async Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows)
    {
        using var connection = NewConnection;
        return await connection.QueryAsync<Match>(
            $@"SELECT {MatchColumns}
                FROM [dbo].[Match]
                ORDER BY [MatchStart] DESC
                OFFSET @Offset ROWS
                FETCH NEXT @NumberOfRows ROWS ONLY",
            new { Offset = offset, NumberOfRows = numberOfRows }
        );
    }

    public async Task InsertMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            using var connection = NewConnection;
            match.ChangeSeq = null;
            await connection.InsertAsync(match);
            await connection.ExecuteAsync(BumpChangeSeqSql, new { match.Id });
            transaction.Complete();
        }
    }

    public async Task UpdateMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            using var connection = NewConnection;
            var updated = await connection.ExecuteAsync(
                @"UPDATE [dbo].[Match]
                    SET [TeamAScore] = @TeamAScore
                        ,[TeamBScore] = @TeamBScore
                        ,[MatchEnd] = @MatchEnd
                        ,[ChangeSeq] = NEXT VALUE FOR [dbo].[MatchChangeSeq]
                    WHERE [Id] = @Id",
                new { match.Id, match.TeamAScore, match.TeamBScore, match.MatchEnd });

            if (updated == 0)
                throw new ArgumentException($"Match {nameof(match.Id)} {match.Id} was not found for update");

            transaction.Complete();
        }
    }

    public async Task InsertRoundAsync(MatchRound round)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            using var connection = NewConnection;

            await connection.InsertAsync(round);

            if (round.MatchRoundPlayers != null && round.MatchRoundPlayers.Any())
            {
                await connection.InsertAsync(round.MatchRoundPlayers);
            }

            if (round.MatchId is { } matchId)
                await connection.ExecuteAsync(BumpChangeSeqSql, new { Id = matchId });

            transaction.Complete();
        }
    }

    public async Task<IReadOnlyList<Match>> GetMatchesChangedSinceAsync(long since, int limit)
    {
        using var connection = NewConnection;
        var matches = (await connection.QueryAsync<Match>(
            $@"SELECT TOP (@Limit) {MatchColumns}
                FROM [dbo].[Match]
                WHERE [ChangeSeq] > @Since
                ORDER BY [ChangeSeq]",
            new { Since = since, Limit = limit }
        )).ToList();

        await LoadRoundsAsync(connection, matches, withPaths: true);
        return matches;
    }

    public async Task<IReadOnlyList<Match>> GetUnfinishedMatchesAsync()
    {
        using var connection = NewConnection;
        var matches = (await connection.QueryAsync<Match>(
            $"SELECT {MatchColumns} FROM [dbo].[Match] WHERE [MatchEnd] IS NULL"
        )).ToList();

        await LoadRoundsAsync(connection, matches, withPaths: false);
        return matches;
    }

    private static async Task LoadRoundsAsync(IDbConnection connection, IReadOnlyCollection<Match> matches, bool withPaths)
    {
        if (matches.Count == 0)
            return;

        var matchIds = matches.Select(m => m.Id).ToArray();

        var rounds = (await connection.QueryAsync<MatchRound>(
            $"SELECT {RoundColumns} FROM [dbo].[MatchRound] WHERE [MatchId] IN @MatchIds ORDER BY [RoundStart]",
            new { MatchIds = matchIds }
        )).ToLookup(r => r.MatchId);

        var players = (await connection.QueryAsync<MatchRoundPlayer>(
            $"SELECT {PlayerColumns}{(withPaths ? PathColumns : "")} FROM [dbo].[MatchRoundPlayer] WHERE [MatchId] IN @MatchIds",
            new { MatchIds = matchIds }
        )).ToLookup(p => p.RoundId);

        foreach (var match in matches)
        {
            match.MatchRounds = rounds[match.Id].ToList();
            foreach (var round in match.MatchRounds)
            {
                round.MatchRoundPlayers = players[round.Id].ToList();
            }
        }
    }
}
