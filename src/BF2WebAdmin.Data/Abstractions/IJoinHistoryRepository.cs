using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

/// <summary>The player join table (dbo.JoinHistory on SQL Server), read by the export API.</summary>
public interface IJoinHistoryRepository
{
    Task InsertAsync(PlayerJoin join);

    /// <summary>The export feed: up to <paramref name="limit"/> joins with an id above <paramref name="sinceId"/>, in id order.</summary>
    Task<IReadOnlyList<PlayerJoin>> GetJoinsSinceAsync(long sinceId, int limit);

    /// <summary>
    /// The newest joins, up to <paramref name="limit"/>, on the game servers at <paramref name="serverIpAddresses"/>
    /// (Server.IpAddress) of a player whose name contains <paramref name="query"/> (ignoring case) or whose CD-key hash
    /// starts with it; joins without a hash are left out. Newest first.
    /// </summary>
    Task<IReadOnlyList<PlayerJoin>> FindJoinsAsync(IReadOnlyCollection<string> serverIpAddresses, string query, int limit);
}
