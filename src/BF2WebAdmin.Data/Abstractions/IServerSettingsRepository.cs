using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

public interface IServerSettingsRepository
{
    Task<Server> GetServerAsync(string serverId);
    Task<IEnumerable<string>> GetModsAsync(string serverId);
    Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverId);
    Task SetPlayerAuthAsync(string serverId, string playerHash, int authLevel);
}