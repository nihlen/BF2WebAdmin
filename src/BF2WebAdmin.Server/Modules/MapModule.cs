using BF2WebAdmin.DAL.Abstractions;
using BF2WebAdmin.Server.Abstractions;

namespace BF2WebAdmin.Server.Modules
{
    public class MapModule : IModule
    {
        private readonly IGameServer _gameServer;
        private readonly IScriptRepository _scriptRepository;

        public MapModule(IGameServer server, IScriptRepository scriptRepository)
        {
            _gameServer = server;
            _scriptRepository = scriptRepository;
        }
    }
}