using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

public class ServerSettingsRepository : IServerSettingsRepository
{
    private readonly BF2Context _context;
    //private readonly string _connectionString;
    //protected IDbConnection NewConnection => new SqliteConnection(_connectionString);

    //public ServerSettingsRepository(string connectionString)
    //{
    //    _connectionString = connectionString;
    //}

    public ServerSettingsRepository(BF2Context context)
    {
        _context = context;
    }

    public async Task<Server> GetServerAsync(string serverId)
    {
        //using var connection = NewConnection;
        //return await connection.QueryFirstOrDefaultAsync<Server>(
        //    @"SELECT TOP 1 s.[ServerId], s.[ServerGroup]
        //        FROM dbo.[Server] s
        //        WHERE s.[ServerId] = @ServerId",
        //    new { ServerId = serverId }
        //);
        return await _context.Servers.FindAsync(serverId);
    }

    public async Task<IEnumerable<string>> GetModsAsync(string serverId)
    {
        //using var connection = NewConnection;
        //var serverModules = await connection.QueryAsync<ServerModule>(
        //    @"SELECT sm.[ServerGroup], sm.[Module], sm.[IsEnabled]
        //        FROM dbo.[ServerModule] sm
        //        INNER JOIN dbo.[Server] s ON s.[ServerGroup] = sm.[ServerGroup]
        //        WHERE s.[ServerId] = @ServerId AND sm.[IsEnabled] = 1",
        //    new { ServerId = serverId }
        //);

        //return serverModules.Select(m => m.Module).ToList();

        //return await _context.ServerModules
        //    .Join(
        //        _context.Servers,
        //        sm => sm.ServerGroup,
        //        s => s.ServerGroup,
        //        (sm, s) => new
        //        {
        //            ServerId = s.ServerId,
        //            ServerGroup = sm.ServerGroup,
        //            Module = sm.Module,
        //            IsEnabled = sm.IsEnabled
        //        }
        //    )
        //    .Where(x => x.ServerId == serverId && x.IsEnabled)
        //    .Select(x => x.Module)
        //    .ToListAsync();

        var server = await _context.Servers.FindAsync(serverId);
        if (server == null)
            return Array.Empty<string>();

        return await _context.ServerModules
            .Where(sm => sm.ServerGroup == server.ServerGroup && sm.IsEnabled)
            .Select(sm => sm.Module)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverId)
    {
        //using var connection = NewConnection;
        //return await connection.QueryAsync<ServerPlayerAuth>(
        //    @"SELECT spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
        //        FROM dbo.[ServerPlayerAuth] spa
        //        INNER JOIN dbo.[Server] s ON s.[ServerGroup] = spa.[ServerGroup]
        //        WHERE s.[ServerId] = @ServerId",
        //    new { ServerId = serverId }
        //);

        var server = await _context.Servers.FindAsync(serverId);
        if (server == null)
            return Array.Empty<ServerPlayerAuth>();

        return await _context.ServerPlayerAuths
            .Where(spa => spa.ServerGroup == server.ServerGroup)
            .ToListAsync();
    }

    public async Task SetPlayerAuthAsync(string serverId, string playerHash, int authLevel)
    {
        //using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        //using var connection = NewConnection;

        //var existingPlayerAuth = await connection.QueryFirstOrDefaultAsync<ServerPlayerAuth>(
        //    @"SELECT TOP 1 spa.[ServerGroup], spa.[PlayerHash], spa.[AuthLevel] 
        //        FROM dbo.[ServerPlayerAuth] spa
        //        INNER JOIN dbo.[Server] s WITH (NOLOCK) ON s.[ServerGroup] = spa.[ServerGroup]
        //        WHERE s.[ServerId] = @ServerId AND spa.[PlayerHash] = @PlayerHash",
        //    new { ServerId = serverId, PlayerHash = playerHash }
        //);

        //if (existingPlayerAuth != null)
        //{
        //    existingPlayerAuth.AuthLevel = authLevel;
        //    await connection.UpdateAsync(existingPlayerAuth);
        //}
        //else
        //{
        //    var server = await connection.GetAsync<Server>(serverId);
        //    await connection.InsertAsync(new ServerPlayerAuth
        //    {
        //        ServerGroup = server.ServerGroup,
        //        PlayerHash = playerHash,
        //        AuthLevel = authLevel
        //    });
        //}

        //transaction.Complete();

        var server = await _context.Servers.FindAsync(serverId);
        if (server == null)
            throw new Exception($"Invalid serverId {serverId}");

        var entity = new ServerPlayerAuth
        {
            ServerGroup = server.ServerGroup,
            PlayerHash = playerHash,
            AuthLevel = authLevel
        };
        _context.ServerPlayerAuths.Update(entity);
        await _context.SaveChangesAsync();
    }
}