using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>The JoinHistory table on SQLite.</summary>
public class JoinHistoryRepository : IJoinHistoryRepository
{
    private readonly BF2Context _context;

    public JoinHistoryRepository(BF2Context context)
    {
        _context = context;
    }

    public async Task InsertAsync(PlayerJoin join)
    {
        await _context.JoinHistory.AddAsync(join);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PlayerJoin>> GetJoinsSinceAsync(long sinceId, int limit)
    {
        return await _context.JoinHistory
            .AsNoTracking()
            .Where(j => j.Id > sinceId)
            .OrderBy(j => j.Id)
            .Take(limit)
            .ToListAsync();
    }
}
