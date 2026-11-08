using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>The JoinHistory table on SQLite (local development; the BF2v2 site reads SQL Server only).</summary>
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
}
