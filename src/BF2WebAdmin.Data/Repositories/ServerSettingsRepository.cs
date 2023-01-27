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

    public ServerSettingsRepository(BF2Context context)
    {
        _context = context;
    }

    public async Task<IEnumerable<string>> GetServerGroupsAsync()
    {
        return await _context.Servers
            .DistinctBy(s => s.ServerGroup)
            .Select(s => s.ServerGroup)
            .ToListAsync();
    }

    public async Task<IEnumerable<Server>> GetServersAsync()
    {
        return await _context.Servers.ToListAsync();
    }

    public async Task<Server> GetServerAsync(string serverId)
    {
        return await _context.Servers.FindAsync(serverId);
    }

    public async Task SetServerAsync(Server server)
    {
        var existingServer = await _context.Servers.FindAsync(server.ServerId);
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
            _context.Servers.Update(existingServer);
        }
        else
        {
            await _context.Servers.AddAsync(server);
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemoveServerAsync(string serverId)
    {
        var existingServer = await _context.Servers.FindAsync(serverId);
        if (existingServer is null)
            return;

        _context.Servers.Remove(existingServer);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<string>> GetModulesAsync(string serverGroup)
    {
        return await _context.ServerModules
            .Where(sm => sm.ServerGroup == serverGroup && sm.IsEnabled)
            .Select(sm => sm.Module)
            .ToListAsync();
    }

    public async Task SetModulesAsync(string serverGroup, IEnumerable<string> moduleNames)
    {
        var existingModules = await _context.ServerModules.Where(sm => sm.ServerGroup == serverGroup).ToListAsync();
        if (existingModules.Any())
        {
            _context.ServerModules.RemoveRange(existingModules);
        }

        _context.ServerModules.AddRange(moduleNames.Select(m => new ServerModule
        {
            ServerGroup = serverGroup,
            Module = m,
            IsEnabled = true
        }));

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverGroup)
    {
        return await _context.ServerPlayerAuths
            .Where(spa => spa.ServerGroup == serverGroup)
            .ToListAsync();
    }

    public async Task SetPlayerAuthAsync(string serverGroup, string playerHash, int authLevel)
    {
        var existingPlayerAuth = await _context.ServerPlayerAuths.FindAsync(serverGroup, playerHash);
        if (existingPlayerAuth is not null)
        {
            existingPlayerAuth.AuthLevel = authLevel;
            _context.ServerPlayerAuths.Update(existingPlayerAuth);
        }
        else
        {
            _context.ServerPlayerAuths.Add(new()
            {
                ServerGroup = serverGroup,
                PlayerHash = playerHash,
                AuthLevel = authLevel
            });
        }

        await _context.SaveChangesAsync();
    }
}
