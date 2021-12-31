using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Dapper;
using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Repositories
{
    public class MatchRepository : IMatchRepository
    {
        private readonly string _connectionString;
        protected IDbConnection NewConnection => new SqlConnection(_connectionString);

        public MatchRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Match> GetMatchAsync(Guid id)
        {
            using var connection = NewConnection;
            var result = await connection.GetAsync<Match>(id);

            if (result == null)
                throw new ArgumentException($"Match {nameof(id)} {id} was not found");

            result.Rounds = await connection.QueryAsync<MatchRound>(
                @"SELECT [Id]
                    ,[MatchId]
                    ,[WinningTeamId]
                    ,[RoundStart]
                    ,[RoundEnd]
                FROM [dbo].[MatchRound]
                WHERE [MatchId] = @MatchId",
                new { MatchId = id }
            );

            var roundPlayers = (await connection.QueryAsync<MatchRoundPlayer>(
                @"SELECT [RoundId]
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
                    ,[KillerPosition]
                    ,[MovementPathJson]
                    ,[ProjectilePathsJson]
                FROM [dbo].[MatchRoundPlayer]
                WHERE [MatchId] = @MatchId",
                new { MatchId = id }
            )).ToLookup(p => p.RoundId);

            foreach (var round in result.Rounds)
            {
                round.Players = roundPlayers[round.Id];
            }

            return result;
        }

        public async Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows)
        {
            using var connection = NewConnection;
            return await connection.QueryAsync<Match>(
                @"SELECT [Id]
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
                FROM [dbo].[Match]
                ORDER BY [MatchStart] DESC
                OFFSET @Offset ROWS 
                FETCH NEXT @NumberOfRows ROWS ONLY",
                new { Offset = offset, NumberOfRows = numberOfRows }
            );
        }

        public async Task InsertMatchAsync(Match match)
        {
            using var connection = NewConnection;
            await connection.InsertAsync(match);
        }

        public async Task UpdateMatchAsync(Match match)
        {
            using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            using var connection = NewConnection;

            var existingMatch = await connection.GetAsync<Match>(match.Id);

            if (existingMatch == null)
                throw new ArgumentException($"Match {nameof(match.Id)} {match.Id} was not found for update");

            existingMatch.TeamAScore = match.TeamAScore;
            existingMatch.TeamBScore = match.TeamBScore;
            existingMatch.MatchEnd = match.MatchEnd;

            await connection.UpdateAsync(existingMatch);

            transaction.Complete();
        }

        public async Task InsertRoundAsync(MatchRound round)
        {
            using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            using var connection = NewConnection;

            await connection.InsertAsync(round);

            if (round.Players != null && round.Players.Any())
            {
                await connection.InsertAsync(round.Players);
            }

            transaction.Complete();
        }
    }
}
