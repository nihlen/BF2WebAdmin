using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

/// <summary>The descriptions of the bans on the game servers (<see cref="ServerBan"/>); the bans themselves are the engine's.</summary>
public interface IServerBanRepository
{
    Task<IReadOnlyList<ServerBan>> GetBansAsync(string serverId);

    /// <summary>Adds the ban, or replaces the one of the same server and identifier.</summary>
    Task SetBanAsync(ServerBan ban);

    Task RemoveBanAsync(string serverId, string identifier);

    Task RemoveBansAsync(string serverId);
}
