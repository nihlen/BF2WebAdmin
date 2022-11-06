using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Repositories;

public class FakeServerSettingsRepository : IServerSettingsRepository
{
    public Task<IEnumerable<string>> GetServerGroupsAsync()
    {
        throw new System.NotImplementedException();
    }

    public async Task<IEnumerable<Server>> GetServersAsync()
    {
        throw new System.NotImplementedException();
    }

    public Task<Server> GetServerAsync(string serverId)
    {
        return Task.FromResult(new Server { ServerId = serverId, ServerGroup = serverId });
    }

    public Task SetServerAsync(Server server)
    {
        return Task.CompletedTask;
    }

    public Task RemoveServerAsync(string serverId)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetModulesAsync(string serverGroup)
    {
        return Task.FromResult((IEnumerable<string>)new[]
        {
            "BF2Module",
            "Heli2v2Module",
            "MapModule",
            "WebModule",
            "DictionaryModule",
            "QuoteModule",
            "TwitterModule",
            "LogModule",
        });
    }

    public Task SetModulesAsync(string serverGroup, IEnumerable<string> moduleNames)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ServerPlayerAuth>> GetPlayerAuthAsync(string serverGroup)
    {
        return Task.FromResult((IEnumerable<ServerPlayerAuth>)new[]
        {
            new ServerPlayerAuth
            {
                PlayerHash = "d975d59a9b32e9f105a15667a18e93d7",
                AuthLevel = 100
            },
        });
    }

    public Task SetPlayerAuthAsync(string serverGroup, string playerHash, int authLevel)
    {
        return Task.CompletedTask;
    }
}