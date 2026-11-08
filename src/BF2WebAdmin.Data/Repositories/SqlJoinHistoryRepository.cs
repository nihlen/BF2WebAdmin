using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>dbo.JoinHistory on SQL Server, with the BF2v2 site's insert (IsLikelySpoof takes the column default).</summary>
public class SqlJoinHistoryRepository : IJoinHistoryRepository
{
    public const string InsertSql =
        @"INSERT INTO [dbo].[JoinHistory]
            ([Name]
            ,[Guid]
            ,[IpAddress]
            ,[ServerIpAddress]
            ,[JoinDate])
        VALUES
            (@Name
            ,@Guid
            ,@IpAddress
            ,@ServerIpAddress
            ,@JoinDate)";

    private readonly string _connectionString;

    public SqlJoinHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InsertAsync(PlayerJoin join)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(InsertSql, new
        {
            Name = new DbString { Value = join.Name, IsAnsi = false, Length = PlayerJoin.NameLength },
            Guid = new DbString { Value = join.Guid, IsAnsi = false, Length = PlayerJoin.GuidLength },
            IpAddress = new DbString { Value = join.IpAddress, IsAnsi = false, Length = PlayerJoin.IpAddressLength },
            ServerIpAddress = new DbString { Value = join.ServerIpAddress, IsAnsi = false, Length = PlayerJoin.IpAddressLength },
            join.JoinDate,
        });
    }
}
