using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

/// <summary>The player join table (dbo.JoinHistory on SQL Server), read by the BF2v2 site.</summary>
public interface IJoinHistoryRepository
{
    Task InsertAsync(PlayerJoin join);

    /// <summary>The export feed: up to <paramref name="limit"/> joins with an id above <paramref name="sinceId"/>, in id order.</summary>
    Task<IReadOnlyList<PlayerJoin>> GetJoinsSinceAsync(long sinceId, int limit);
}
