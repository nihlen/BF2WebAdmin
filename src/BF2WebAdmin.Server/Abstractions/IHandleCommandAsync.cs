namespace BF2WebAdmin.Server.Abstractions
{
    public interface IHandleCommandAsync<in TCommand> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command);
    }
}