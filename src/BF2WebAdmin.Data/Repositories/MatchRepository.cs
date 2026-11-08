using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>
/// Matches on EF Core: SQLite (<see cref="BF2Context"/>) and Postgres (<see cref="PostgresBF2Context"/>). The export
/// cursor is taken under <see cref="MatchChangeLock"/>: on SQLite the highest <see cref="Match.ChangeSeq"/> plus one, on
/// Postgres the next value of sequence match_change_seq. On Postgres the paths live in match_round_player_path and are
/// read only where a whole match is asked for; players have no MatchId column there, so it is filled from the round.
/// </summary>
public class MatchRepository : IMatchRepository
{
    private readonly IDbContextFactory<BF2Context> _contextFactory;

    public MatchRepository(IDbContextFactory<BF2Context> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Match> GetMatchAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var match = await context.Matches
            .AsNoTracking()
            .Include(m => m.MatchRounds)
            .ThenInclude(mr => mr.MatchRoundPlayers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (match != null)
            await CompleteAsync(context, new[] { match }, withPaths: true);

        return match;
    }

    public async Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.MatchStart)
            .Skip(offset)
            .Take(numberOfRows)
            .ToListAsync();
    }

    public async Task InsertMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        await using var context = await _contextFactory.CreateDbContextAsync();
        match.ChangeSeq = await NextChangeSeqAsync(context);
        context.Matches.Add(match);
        await context.SaveChangesAsync();
    }

    public async Task UpdateMatchAsync(Match match)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Matches.FirstOrDefaultAsync(m => m.Id == match.Id)
            ?? throw new ArgumentException($"Match {nameof(match.Id)} {match.Id} was not found for update");

        existing.TeamAScore = match.TeamAScore;
        existing.TeamBScore = match.TeamBScore;
        existing.MatchEnd = match.MatchEnd;
        existing.ChangeSeq = await NextChangeSeqAsync(context);
        await context.SaveChangesAsync();
    }

    public async Task InsertRoundAsync(MatchRound round)
    {
        using var _ = await MatchChangeLock.AcquireAsync();
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.MatchRounds.Add(round);

        if (context is PostgresBF2Context postgres && round.MatchRoundPlayers != null)
        {
            foreach (var path in round.MatchRoundPlayers.Select(MatchRoundPlayerPath.From).Where(p => p != null))
                postgres.MatchRoundPlayerPaths.Add(path);
        }

        if (round.MatchId is { } matchId && await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId) is { } match)
            match.ChangeSeq = await NextChangeSeqAsync(context);

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Match>> GetMatchesChangedSinceAsync(long since, int limit)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var matches = await context.Matches
            .AsNoTracking()
            .Where(m => m.ChangeSeq > since)
            .OrderBy(m => m.ChangeSeq)
            .Take(limit)
            .Include(m => m.MatchRounds.OrderBy(r => r.RoundStart))
            .ThenInclude(r => r.MatchRoundPlayers)
            .AsSplitQuery()
            .ToListAsync();

        await CompleteAsync(context, matches, withPaths: true);
        return matches;
    }

    public async Task<IReadOnlyList<Match>> GetUnfinishedMatchesAsync()
    {
        // Postgres leaves the paths out; SQLite reads them with the players (a small local database)
        await using var context = await _contextFactory.CreateDbContextAsync();
        var matches = await context.Matches
            .AsNoTracking()
            .Where(m => m.MatchEnd == null)
            .Include(m => m.MatchRounds.OrderBy(r => r.RoundStart))
            .ThenInclude(r => r.MatchRoundPlayers)
            .AsSplitQuery()
            .ToListAsync();

        await CompleteAsync(context, matches, withPaths: false);
        return matches;
    }

    /// <summary>Postgres: the players' MatchId from their round, and the paths from match_round_player_path.</summary>
    private static async Task CompleteAsync(BF2Context context, IReadOnlyCollection<Match> matches, bool withPaths)
    {
        if (context is not PostgresBF2Context postgres || matches.Count == 0)
            return;

        var players = new Dictionary<(Guid, string), MatchRoundPlayer>();
        foreach (var round in matches.SelectMany(m => m.MatchRounds))
        {
            foreach (var player in round.MatchRoundPlayers)
            {
                player.MatchId = round.MatchId ?? Guid.Empty;
                players[(player.RoundId, player.PlayerHash)] = player;
            }
        }

        if (!withPaths || players.Count == 0)
            return;

        var roundIds = matches.SelectMany(m => m.MatchRounds).Select(r => r.Id).ToList();
        var paths = await postgres.MatchRoundPlayerPaths
            .AsNoTracking()
            .Where(p => roundIds.Contains(p.RoundId))
            .ToListAsync();

        foreach (var path in paths)
        {
            if (players.TryGetValue((path.RoundId, path.PlayerHash), out var player))
                path.CopyTo(player);
        }
    }

    private static async Task<long> NextChangeSeqAsync(BF2Context context)
    {
        if (context is PostgresBF2Context)
        {
            return await context.Database
                .SqlQueryRaw<long>($"SELECT nextval('{PostgresBF2Context.ChangeSeqSequence}') AS \"Value\"")
                .SingleAsync();
        }

        return (await context.Matches.MaxAsync(m => m.ChangeSeq) ?? 0) + 1;
    }
}
