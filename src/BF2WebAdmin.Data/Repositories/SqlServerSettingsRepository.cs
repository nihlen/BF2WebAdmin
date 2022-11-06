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

public class SqlServerSettingsRepository : IServerSettingsRepository
{
    private readonly string _connectionString;
    protected IDbConnection NewConnection => new SqlConnection(_connectionString);

    public SqlServerSettingsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<string>> GetServerGroupsAsync()
    {
        using var connection = NewConnection;
        return await connection.QueryAsync<string>(@"SELECT DISTINCT [ServerGroup] FROM [dbo].[Server]");
    }

    public async Task<IEnumerable<Server>> GetServersAsync()
    {
        using var connection = NewConnection;
        return await connection.QueryAsync<Server>(
            @"SELECT [ServerId]
                ,[ServerGroup]
                ,[IpAddress]
                ,[GamePort]
                ,[QueryPort]
                ,[RconPort]
                ,[RconPassword]
                ,[DiscordBotToken]
                ,[DiscordAdminChannel]
                ,[DiscordNotificationChannel]
                ,[DiscordMatchResultChannel]
            FROM [dbo].[Server]"
        );
    }

    public async Task<Server> GetServerAsync(string serverId)
    {
        using var connection = NewConnection;
        return await connection.QueryFirstOrDefaultAsync<Server>(
            @"SELECT TOP 1 [ServerId]
                ,[ServerGroup]
                ,[IpAddress]
                ,[GamePort]
                ,[QueryPort]
                ,[RconPort]
                ,[RconPassword]
                ,[DiscordBotToken]
                ,[DiscordAdminChannel]
                ,[DiscordNotificationChannel]
                ,[DiscordMatchResultChannel]
            FROM [dbo].[Server]
            WHERE [ServerId] = @ServerId",
            new { ServerId = serverId }
        );
    }

    public async Task SetServerAsync(Server server)
    {
        using var connection = NewConnection;
        var existingServer = await GetServerAsync(server.ServerId);
        if (existingServer is null)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO [dbo].[Server]
	                ([ServerId]
	                ,[ServerGroup]
	                ,[IpAddress]
	                ,[GamePort]
	                ,[QueryPort]
	                ,[RconPort]
	                ,[RconPassword]
	                ,[DiscordBotToken]
	                ,[DiscordAdminChannel]
	                ,[DiscordNotificationChannel]
	                ,[DiscordMatchResultChannel])
                VALUES
	                (@ServerId
	                ,@ServerGroup
	                ,@IpAddress
	                ,@GamePort
	                ,@QueryPort
	                ,@RconPort
	                ,@RconPassword
	                ,@DiscordBotToken
	                ,@DiscordAdminChannel
	                ,@DiscordNotificationChannel
	                ,@DiscordMatchResultChannel)",
                new
                {
                    ServerId = server.ServerId, 
                    ServerGroup = server.ServerGroup,
                    IpAddress = server.IpAddress,
                    GamePort = server.GamePort,
                    QueryPort = server.QueryPort,
                    RconPort = server.RconPort,
                    RconPassword = server.RconPassword,
                    DiscordBotToken = server.DiscordBotToken,
                    DiscordAdminChannel = server.DiscordAdminChannel,
                    DiscordNotificationChannel = server.DiscordNotificationChannel,
                    DiscordMatchResultChannel = server.DiscordMatchResultChannel
                }
            );
        }
        else
        {
            await connection.ExecuteAsync(
                @"UPDATE [dbo].[Server]
                SET [ServerGroup] = @ServerGroup
	                ,[QueryPort] = @QueryPort
	                ,[RconPort] = @RconPort
	                ,[RconPassword] = @RconPassword
	                ,[DiscordBotToken] = @DiscordBotToken
	                ,[DiscordAdminChannel] = @DiscordAdminChannel
	                ,[DiscordNotificationChannel] = @DiscordNotificationChannel
	                ,[DiscordMatchResultChannel] = @DiscordMatchResultChannel
                WHERE [ServerId] = @ServerId",
                new
                {
                    ServerId = server.ServerId, 
                    ServerGroup = server.ServerGroup,
                    QueryPort = server.QueryPort,
                    RconPort = server.RconPort,
                    RconPassword = server.RconPassword,
                    DiscordBotToken = server.DiscordBotToken,
                    DiscordAdminChannel = server.DiscordAdminChannel,
                    DiscordNotificationChannel = server.DiscordNotificationChannel,
                    DiscordMatchResultChannel = server.DiscordMatchResultChannel
                }
            );
        }
    }

    public async Task RemoveServerAsync(string serverId)
    {
        using var connection = NewConnection;
        await connection.ExecuteAsync(
            @"DELETE FROM [dbo].[Server]
            WHERE [ServerId] = @ServerId",
            new { ServerId = serverId }
        );
    }

    public async Task<IEnumerable<string>> GetModulesAsync(string serverGroup)
    {
        using var connection = NewConnection;
        var serverModules = await connection.QueryAsync<ServerModule>(
            @"SELECT sm.[ServerGroup], sm.[Module], sm.[IsEnabled]
            FROM dbo.[ServerModule] sm
            WHERE sm.[ServerGroup] = @ServerGroup AND sm.[IsEnabled] = 1",
            new { ServerGroup = serverGroup }
        );

        return serverModules.Select(m => m.Module).ToList();
    }

    public async Task SetModulesAsync(string serverGroup, IEnumerable<string> moduleNames)
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        using var connection = NewConnection;
        
        await connection.ExecuteAsync(@"DELETE FROM [dbo].[ServerModule] WHERE [ServerGroup] = @ServerGroup", new { ServerGroup = serverGroup });
        await connection.InsertAsync(moduleNames.Select(m => new ServerModule { ServerGroup = serverGroup, Module = m, IsEnabled = true }));
        
        transaction.Complete();
    }

    public async Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverGroup)
    {
        using var connection = NewConnection;
        return await connection.QueryAsync<ServerPlayerAuth>(
            @"SELECT spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
            FROM dbo.[ServerPlayerAuth] spa
            WHERE spa.[ServerGroup] = @ServerGroup",
            new { ServerGroup = serverGroup }
        );
    }

    public async Task SetPlayerAuthAsync(string serverGroup, string playerHash, int authLevel)
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        using var connection = NewConnection;

        var existingPlayerAuth = await connection.QueryFirstOrDefaultAsync<ServerPlayerAuth>(
            @"SELECT TOP 1 spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
            FROM dbo.[ServerPlayerAuth] spa
            WHERE spa.[ServerGroup] = @ServerGroup AND spa.[PlayerHash] = @PlayerHash",
            new { ServerGroup = serverGroup, PlayerHash = playerHash }
        );

        if (existingPlayerAuth != null)
        {
            existingPlayerAuth.AuthLevel = authLevel;
            await connection.UpdateAsync(existingPlayerAuth);
        }
        else
        {
            await connection.InsertAsync(new ServerPlayerAuth
            {
                ServerGroup = serverGroup,
                PlayerHash = playerHash,
                AuthLevel = authLevel
            });
        }

        transaction.Complete();
    }
}