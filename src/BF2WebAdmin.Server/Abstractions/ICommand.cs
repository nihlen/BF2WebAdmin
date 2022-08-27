using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions;

public interface ICommand
{
    Message Message{ get; }
}