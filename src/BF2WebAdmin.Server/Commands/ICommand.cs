using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server.Commands
{
    public interface ICommand
    {
        Message Message{ get; }
    }
}