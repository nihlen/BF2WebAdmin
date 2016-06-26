using System;
using System.Linq;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.Modules
{
    public class BF2Module : IModule,
        IHandleCommand<RankCommand>
    {
        private readonly IGameServer _gameServer;

        public BF2Module(IGameServer server)
        {
            _gameServer = server;
        }

        public void Handle(RankCommand command)
        {
            var validRank = Enum.IsDefined(typeof(Rank), command.Rank);
            if (!validRank)
                return;

            var player = _gameServer.Players.FirstOrDefault(p => p.Name.ToLower().Contains(command.Name));
            if (player == null)
                return;

            _gameServer.GameWriter.SendRank(player, (Rank)command.Rank, true);
        }
    }
}