namespace BF2WebAdmin.Server.Abstractions;

public interface IHandleCommand<in TCommand> where TCommand : ICommand
{
    void Handle(TCommand command);
}