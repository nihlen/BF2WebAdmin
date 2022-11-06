using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

public interface IServerSettingsRepository
{
    Task<IEnumerable<string>> GetServerGroupsAsync();
    Task<IEnumerable<Server>> GetServersAsync();
    Task<Server> GetServerAsync(string serverId);
    Task SetServerAsync(Server server);
    Task RemoveServerAsync(string serverId);
    Task<IEnumerable<string>> GetModulesAsync(string serverGroup);
    Task SetModulesAsync(string serverGroup, IEnumerable<string> moduleNames);
    Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverGroup);
    Task SetPlayerAuthAsync(string serverGroup, string playerHash, int authLevel);
}
