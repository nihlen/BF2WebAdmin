using System;
using System.Linq;
using BF2WebAdmin.DAL.Repositories;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Entities;

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