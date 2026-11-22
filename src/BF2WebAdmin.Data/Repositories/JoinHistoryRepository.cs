using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>The player joins on EF Core: table JoinHistory on SQLite and SQL Server, player_join on Postgres.</summary>
public class JoinHistoryRepository : IJoinHistoryRepository
{
    private readonly IDbContextFactory<BF2Context> _contextFactory;

    public JoinHistoryRepository(IDbContextFactory<BF2Context> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InsertAsync(PlayerJoin join)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.JoinHistory.AddAsync(join);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PlayerJoin>> GetJoinsSinceAsync(long sinceId, int limit)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JoinHistory
            .AsNoTracking()
            .Where(j => j.Id > sinceId)
            .OrderBy(j => j.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PlayerJoin>> FindJoinsAsync(IReadOnlyCollection<string> serverIpAddresses, string query, int limit)
    {
        var ips = serverIpAddresses.Distinct().ToList();
        var text = query.Trim().ToLower();
        if (ips.Count == 0 || text.Length == 0)
            return [];

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JoinHistory
            .AsNoTracking()
            .Where(j => ips.Contains(j.ServerIpAddress) && j.Guid != null && j.Guid != "")
            .Where(j => j.Name.ToLower().Contains(text) || j.Guid.ToLower().StartsWith(text))
            .OrderByDescending(j => j.JoinDate)
            .Take(limit)
            .ToListAsync();
    }
}
