using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions;

public abstract class BaseCommand : ICommand
{
    public Message Message { get; set; }
}