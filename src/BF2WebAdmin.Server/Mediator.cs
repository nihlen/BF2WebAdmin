using BF2WebAdmin.Server.Abstractions;
using Nihlen.Common.Telemetry;

namespace BF2WebAdmin.Server;

public interface IMediator
{
    ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent;
    ValueTask HandleAsync<TCommand>(TCommand command) where TCommand : ICommand;
}

public class Mediator : IMediator
{
    private readonly IModuleResolver _moduleResolver;
    private readonly ILogger<Mediator> _logger;

    public Mediator(IModuleResolver moduleResolver, ILogger<Mediator> logger)
    {
        _moduleResolver = moduleResolver;
        _logger = logger;
    }

    public async ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent
    {
        var eventType = gameEvent.GetType();
        using var activity = TraceEventType(eventType.Name) ? Telemetry.ActivitySource.StartActivity("PublishEvent:" + eventType.Name) : null;

        // TODO: make an alternate version that uses Channel<IEvent> and see if it causes less delays
        if (!_moduleResolver.EventHandlers.TryGetValue(eventType, out var handlers))
        {
            _logger.LogTrace("No handler found for event type {EventType}", eventType);
            return;
        }
        
        activity?.SetTag("bf2wa.event-handler-count", handlers.Count);

        foreach (var handler in handlers)
        {
            // Errors are handled in the event handler wrapper (?)
            //_ = Task.Run(async () => await handler(gameEvent));
            await handler(gameEvent);
        }
        
        static bool TraceEventType(string? eventType) => eventType != nameof(PlayerPositionEvent) && eventType != nameof(ProjectilePositionEvent);
    }

    public async ValueTask HandleAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var commandType = command.GetType();
        using var activity = Telemetry.ActivitySource.StartActivity("PublishCommand:" + commandType.Name);

        // TODO: make an alternate version that uses Channel<ICommand> and see if it causes less delays
        if (!_moduleResolver.CommandHandlers.TryGetValue(commandType, out var handlers))
            throw new Exception($"No command handler registered for {commandType.Name}");

        activity?.SetTag("bf2wa.command-handler-count", handlers.Count);

        // TODO: properly handle async commands - always return Func<Task> and await Task.WhenAll?
        foreach (var handler in handlers)
        {
            // Errors are handled in the command handler wrapper
            _ = Task.Run(async () => await handler(command));
            //handledCount++;
        }
    }
}
