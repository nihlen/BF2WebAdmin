using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly BF2Context _context;

    public MatchRepository(BF2Context context)
    {
        _context = context;
    }

    public async Task<Match> GetMatchAsync(Guid id)
    {
        return await _context.Matches
            .Include(m => m.MatchRounds)
            .ThenInclude(mr => mr.MatchRoundPlayers)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows)
    {
        return await _context.Matches
            .OrderByDescending(m => m.MatchStart)
            .Skip(offset)
            .Take(numberOfRows)
            .ToListAsync();
    }

    public async Task InsertMatchAsync(Match match)
    {
        await _context.Matches.AddAsync(match);
    }

    public async Task UpdateMatchAsync(Match match)
    {
        _context.Matches.Update(match);
        await _context.SaveChangesAsync();
    }

    public async Task InsertRoundAsync(MatchRound round)
    {
        await _context.MatchRounds.AddAsync(round);
    }
}