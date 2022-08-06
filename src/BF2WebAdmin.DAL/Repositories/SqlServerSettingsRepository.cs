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

namespace BF2WebAdmin.Data.Repositories
{
    public class SqlServerSettingsRepository : IServerSettingsRepository
    {
        private readonly string _connectionString;
        protected IDbConnection NewConnection => new SqlConnection(_connectionString);

        public SqlServerSettingsRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Server> GetServerAsync(string serverId)
        {
            using var connection = NewConnection;
            return await connection.QueryFirstOrDefaultAsync<Server>(
                @"SELECT TOP 1 s.[ServerId], s.[ServerGroup]
                    FROM dbo.[Server] s
                    WHERE s.[ServerId] = @ServerId",
                new { ServerId = serverId }
            );
        }

        public async Task<IEnumerable<string>> GetModsAsync(string serverId)
        {
            using var connection = NewConnection;
            var serverModules = await connection.QueryAsync<ServerModule>(
                @"SELECT sm.[ServerGroup], sm.[Module], sm.[IsEnabled]
                    FROM dbo.[ServerModule] sm
                    INNER JOIN dbo.[Server] s ON s.[ServerGroup] = sm.[ServerGroup]
                    WHERE s.[ServerId] = @ServerId AND sm.[IsEnabled] = 1",
                new { ServerId = serverId }
            );

            return serverModules.Select(m => m.Module).ToList();
        }

        public async Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverId)
        {
            using var connection = NewConnection;
            return await connection.QueryAsync<ServerPlayerAuth>(
                @"SELECT spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
                    FROM dbo.[ServerPlayerAuth] spa
                    INNER JOIN dbo.[Server] s ON s.[ServerGroup] = spa.[ServerGroup]
                    WHERE s.[ServerId] = @ServerId",
                new { ServerId = serverId }
            );
        }

        public async Task SetPlayerAuthAsync(string serverId, string playerHash, int authLevel)
        {
            using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            using var connection = NewConnection;

            var existingPlayerAuth = await connection.QueryFirstOrDefaultAsync<ServerPlayerAuth>(
                @"SELECT TOP 1 spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
                    FROM dbo.[ServerPlayerAuth] spa
                    INNER JOIN dbo.[Server] s WITH (NOLOCK) ON s.[ServerGroup] = spa.[ServerGroup]
                    WHERE s.[ServerId] = @ServerId AND spa.[PlayerHash] = @PlayerHash",
                new { ServerId = serverId, PlayerHash = playerHash }
            );

            if (existingPlayerAuth != null)
            {
                existingPlayerAuth.AuthLevel = authLevel;
                await connection.UpdateAsync(existingPlayerAuth);
            }
            else
            {
                var server = await connection.GetAsync<Server>(serverId);
                await connection.InsertAsync(new ServerPlayerAuth
                {
                    ServerGroup = server.ServerGroup,
                    PlayerHash = playerHash,
                    AuthLevel = authLevel
                });
            }

            transaction.Complete();
        }
    }
}
