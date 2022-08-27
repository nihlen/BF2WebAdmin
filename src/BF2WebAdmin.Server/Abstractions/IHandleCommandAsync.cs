namespace BF2WebAdmin.Server.Abstractions;

public interface IHandleCommandAsync<in TCommand> where TCommand : ICommand
{
    ValueTask HandleAsync(TCommand command);
}