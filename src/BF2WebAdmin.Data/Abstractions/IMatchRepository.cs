using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

public interface IMatchRepository
{
    Task<Match> GetMatchAsync(Guid matchId);
    Task<IEnumerable<Match>> GetMatchesByNewestAsync(int offset, int numberOfRows);

    /// <summary>Inserts the match and gives it a <see cref="Match.ChangeSeq"/>.</summary>
    Task InsertMatchAsync(Match match);

    /// <summary>Sets the match's scores and end (nothing else) and gives it a new <see cref="Match.ChangeSeq"/>.</summary>
    Task UpdateMatchAsync(Match match);

    /// <summary>Inserts the round and its players and gives the round's match a new <see cref="Match.ChangeSeq"/>.</summary>
    Task InsertRoundAsync(MatchRound round);

    /// <summary>
    /// The export feed: up to <paramref name="limit"/> whole matches (rounds, players, paths) with a
    /// <see cref="Match.ChangeSeq"/> above <paramref name="since"/>, in ChangeSeq order.
    /// </summary>
    Task<IReadOnlyList<Match>> GetMatchesChangedSinceAsync(long since, int limit);

    /// <summary>Matches without an end, with rounds and players but without the paths.</summary>
    Task<IReadOnlyList<Match>> GetUnfinishedMatchesAsync();
}
