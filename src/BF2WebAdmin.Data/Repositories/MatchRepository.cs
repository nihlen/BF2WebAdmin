using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>Matches on EF Core (SQLite). The export cursor is the highest <see cref="Match.ChangeSeq"/> plus one, under <see cref="MatchChangeLock"/>.</summary>
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
            .AsNoTracking()
            .Include(m => m.MatchRounds)
            .ThenInclude(mr => mr.MatchRoundPlayers)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows)
    {
        return await _context.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.MatchStart)
            .Skip(offset)
            .Take(numberOfRows)
            .ToListAsync();
    }

    public async Task InsertMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        match.ChangeSeq = await NextChangeSeqAsync();
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    public async Task UpdateMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        var existing = await _context.Matches.FirstOrDefaultAsync(m => m.Id == match.Id)
            ?? throw new ArgumentException($"Match {nameof(match.Id)} {match.Id} was not found for update");

        existing.TeamAScore = match.TeamAScore;
        existing.TeamBScore = match.TeamBScore;
        existing.MatchEnd = match.MatchEnd;
        existing.ChangeSeq = await NextChangeSeqAsync();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    public async Task InsertRoundAsync(MatchRound round)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        _context.MatchRounds.Add(round);

        if (round.MatchId is { } matchId && await _context.Matches.FirstOrDefaultAsync(m => m.Id == matchId) is { } match)
            match.ChangeSeq = await NextChangeSeqAsync();

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<Match>> GetMatchesChangedSinceAsync(long since, int limit)
    {
        return await _context.Matches
            .AsNoTracking()
            .Where(m => m.ChangeSeq > since)
            .OrderBy(m => m.ChangeSeq)
            .Take(limit)
            .Include(m => m.MatchRounds.OrderBy(r => r.RoundStart))
            .ThenInclude(r => r.MatchRoundPlayers)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Match>> GetUnfinishedMatchesAsync()
    {
        // The paths are only needed by the export; no projection here, the SQLite database is a small local one
        return await _context.Matches
            .AsNoTracking()
            .Where(m => m.MatchEnd == null)
            .Include(m => m.MatchRounds.OrderBy(r => r.RoundStart))
            .ThenInclude(r => r.MatchRoundPlayers)
            .AsSplitQuery()
            .ToListAsync();
    }

    private async Task<long> NextChangeSeqAsync() =>
        (await _context.Matches.MaxAsync(m => m.ChangeSeq) ?? 0) + 1;
}
