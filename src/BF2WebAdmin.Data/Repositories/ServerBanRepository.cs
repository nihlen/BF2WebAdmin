using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>The ban descriptions on EF Core: table ServerBan on SQLite and SQL Server, server_ban on Postgres.</summary>
public class ServerBanRepository : IServerBanRepository
{
    private readonly IDbContextFactory<BF2Context> _contextFactory;

    public ServerBanRepository(IDbContextFactory<BF2Context> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<ServerBan>> GetBansAsync(string serverId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ServerBans.AsNoTracking().Where(b => b.ServerId == serverId).ToListAsync();
    }

    public async Task SetBanAsync(ServerBan ban)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.ServerBans.FirstOrDefaultAsync(b => b.ServerId == ban.ServerId && b.Identifier == ban.Identifier);
        if (existing == null)
        {
            context.ServerBans.Add(ban);
        }
        else
        {
            existing.ByCdKey = ban.ByCdKey;
            existing.Name = ban.Name;
            existing.BannedBy = ban.BannedBy;
            existing.Reason = ban.Reason;
            existing.BannedAt = ban.BannedAt;
        }

        await context.SaveChangesAsync();
    }

    public async Task RemoveBanAsync(string serverId, string identifier)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.ServerBans.Where(b => b.ServerId == serverId && b.Identifier == identifier).ExecuteDeleteAsync();
    }

    public async Task RemoveBansAsync(string serverId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.ServerBans.Where(b => b.ServerId == serverId).ExecuteDeleteAsync();
    }
}
