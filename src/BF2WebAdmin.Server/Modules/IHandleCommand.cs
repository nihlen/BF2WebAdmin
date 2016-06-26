using BF2WebAdmin.Server.Commands;

namespace BF2WebAdmin.Server.Modules
{
    public interface IHandleCommand<in TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }
}