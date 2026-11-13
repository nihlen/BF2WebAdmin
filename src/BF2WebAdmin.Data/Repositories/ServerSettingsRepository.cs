using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>Servers, modules and player auth levels on EF Core (SQLite, SQL Server, Postgres), one short context per call.</summary>
public class ServerSettingsRepository : IServerSettingsRepository
{
    private readonly IDbContextFactory<BF2Context> _contextFactory;

    public ServerSettingsRepository(IDbContextFactory<BF2Context> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<string>> GetServerGroupsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Servers
            .Select(s => s.ServerGroup)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<Server>> GetServersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Servers.ToListAsync();
    }

    public async Task<Server> GetServerAsync(string serverId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Servers.FindAsync(serverId);
    }

    public async Task SetServerAsync(Server server)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingServer = await context.Servers.FindAsync(server.ServerId);
        if (existingServer is not null)
        {
            existingServer.ServerGroup = server.ServerGroup;
            existingServer.QueryPort = server.QueryPort;
            existingServer.RconPort = server.RconPort;
            existingServer.RconPassword = server.RconPassword;
            existingServer.DiscordBotToken = server.DiscordBotToken;
            existingServer.DiscordAdminChannel = server.DiscordAdminChannel;
            existingServer.DiscordNotificationChannel = server.DiscordNotificationChannel;
            existingServer.DiscordMatchResultChannel = server.DiscordMatchResultChannel;
            existingServer.AdminKey = server.AdminKey;
            existingServer.IsTestServer = server.IsTestServer;
            context.Servers.Update(existingServer);
        }
        else
        {
            await context.Servers.AddAsync(server);
        }

        await context.SaveChangesAsync();
    }

    public async Task RemoveServerAsync(string serverId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingServer = await context.Servers.FindAsync(serverId);
        if (existingServer is null)
            return;

        context.Servers.Remove(existingServer);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<string>> GetModulesAsync(string serverGroup)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ServerModules
            .Where(sm => sm.ServerGroup == serverGroup && sm.IsEnabled)
            .Select(sm => sm.Module)
            .ToListAsync();
    }

    public async Task SetModulesAsync(string serverGroup, IEnumerable<string> moduleNames)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingModules = await context.ServerModules.Where(sm => sm.ServerGroup == serverGroup).ToListAsync();
        if (existingModules.Any())
        {
            context.ServerModules.RemoveRange(existingModules);
        }

        context.ServerModules.AddRange(moduleNames.Select(m => new ServerModule
        {
            ServerGroup = serverGroup,
            Module = m,
            IsEnabled = true
        }));

        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverGroup)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ServerPlayerAuths
            .Where(spa => spa.ServerGroup == serverGroup)
            .ToListAsync();
    }

    public async Task SetPlayerAuthAsync(string serverGroup, string playerHash, int authLevel)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingPlayerAuth = await context.ServerPlayerAuths.FindAsync(serverGroup, playerHash);
        if (existingPlayerAuth is not null)
        {
            existingPlayerAuth.AuthLevel = authLevel;
            context.ServerPlayerAuths.Update(existingPlayerAuth);
        }
        else
        {
            context.ServerPlayerAuths.Add(new()
            {
                ServerGroup = serverGroup,
                PlayerHash = playerHash,
                AuthLevel = authLevel
            });
        }

        await context.SaveChangesAsync();
    }
}
