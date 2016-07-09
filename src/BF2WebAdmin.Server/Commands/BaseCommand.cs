using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.Commands
{
    public abstract class BaseCommand : ICommand
    {
        public Message Message { get; set; }
    }
}