using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>The player joins on EF Core: table JoinHistory on SQLite, player_join on Postgres.</summary>
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
}
