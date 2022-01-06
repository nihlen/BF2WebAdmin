using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server;

public interface IMediator
{
    ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent;
    ValueTask HandleAsync<TCommand>(TCommand command) where TCommand : ICommand;
}

public class Mediator : IMediator
{
    private readonly IModuleResolver _moduleResolver;

    public Mediator(IModuleResolver moduleResolver)
    {
        _moduleResolver = moduleResolver;
    }

    public async ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent
    {
        // TODO: make an alternate version that uses Channel<IEvent> and see if it causes less delays
        var eventType = gameEvent.GetType();
        if (!_moduleResolver.EventHandlers.TryGetValue(eventType, out var handlers))
        {
            Log.Warning("No handler found for event type {EventType}", eventType);
            return;
        }

        foreach (var handler in handlers)
        {
            // Errors are handled in the event handler wrapper (?)
            //_ = Task.Run(async () => await handler(gameEvent));
            await handler(gameEvent);
        }
    }

    public async ValueTask HandleAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        // TODO: make an alternate version that uses Channel<ICommand> and see if it causes less delays
        var commandType = command.GetType();
        if (!_moduleResolver.CommandHandlers.TryGetValue(commandType, out var handlers))
            throw new Exception($"No command handler registered for {commandType.Name}");

        // TODO: properly hand async commands - always return Func<Task> and await Task.WhenAll?
        foreach (var handler in handlers)
        {
            // Errors are handled in the command handler wrapper
            _ = Task.Run(async () => await handler(command));
            //handledCount++;
        }
    }
}
