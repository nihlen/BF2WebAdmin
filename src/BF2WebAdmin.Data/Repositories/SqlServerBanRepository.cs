using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>dbo.ServerBan on SQL Server (Schema.sql, <see cref="SqlServerSchemaUpdates"/>).</summary>
public class SqlServerBanRepository : IServerBanRepository
{
    private readonly string _connectionString;

    public SqlServerBanRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<ServerBan>> GetBansAsync(string serverId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<ServerBan>(
            @"SELECT [ServerId], [Identifier], [ByCdKey], [Name], [BannedBy], [Reason], [BannedAt]
            FROM [dbo].[ServerBan]
            WHERE [ServerId] = @ServerId",
            new { ServerId = serverId })).ToList();
    }

    public async Task SetBanAsync(ServerBan ban)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            @"UPDATE [dbo].[ServerBan]
                SET [ByCdKey] = @ByCdKey, [Name] = @Name, [BannedBy] = @BannedBy, [Reason] = @Reason, [BannedAt] = @BannedAt
                WHERE [ServerId] = @ServerId AND [Identifier] = @Identifier;
            IF @@ROWCOUNT = 0
                INSERT INTO [dbo].[ServerBan] ([ServerId], [Identifier], [ByCdKey], [Name], [BannedBy], [Reason], [BannedAt])
                VALUES (@ServerId, @Identifier, @ByCdKey, @Name, @BannedBy, @Reason, @BannedAt);",
            new
            {
                ServerId = Text(ban.ServerId, ServerBan.ServerIdLength),
                Identifier = Text(ban.Identifier, ServerBan.IdentifierLength),
                ban.ByCdKey,
                Name = Text(ban.Name, ServerBan.NameLength),
                BannedBy = Text(ban.BannedBy, ServerBan.BannedByLength),
                Reason = Text(ban.Reason, ServerBan.ReasonLength),
                ban.BannedAt,
            });
    }

    public async Task RemoveBanAsync(string serverId, string identifier)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM [dbo].[ServerBan] WHERE [ServerId] = @ServerId AND [Identifier] = @Identifier",
            new { ServerId = serverId, Identifier = identifier });
    }

    public async Task RemoveBansAsync(string serverId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM [dbo].[ServerBan] WHERE [ServerId] = @ServerId", new { ServerId = serverId });
    }

    private static DbString Text(string value, int length) => new() { Value = value, IsAnsi = false, Length = length };
}
