using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Abstractions
{
    public abstract class BaseModule : IModule
    {
        protected readonly IGameServer GameServer;
        protected IMediator Mediator => GameServer.ModManager.Mediator;

        protected BaseModule(IGameServer gameServer)
        {
            GameServer = gameServer;
        }

        protected virtual void SpawnObject(string template, Position position, Rotation rotation, bool showMessage = false, int count = 0)
        {
            var replacements = new Dictionary<string, string>
            {
                {"{TEMPLATE}", template},
                {"{POSITION}", position.ToString()},
                {"{ROTATION}", rotation.ToString()}
            };

            var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
            GameServer.GameWriter.SendRcon(script.ToArray());

            if (showMessage)
                GameServer.GameWriter.SendText($"{count} Spawned '{template}' at {position}");
        }

        protected virtual void KillPlayer(Player player)
        {
            GameServer.GameWriter.SendHealth(player, 1);
        }
    }
}