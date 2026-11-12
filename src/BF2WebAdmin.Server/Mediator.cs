using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;

namespace BF2WebAdmin.Server;

public interface IMediator
{
    ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent;
    /// <summary>
    /// Starts the command's handlers on the thread pool and returns at once; the returned task completes when they all have.
    /// A handler's errors are logged, never thrown.
    /// </summary>
    Task HandleAsync<TCommand>(TCommand command) where TCommand : ICommand;
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
            await handler(gameEvent);
        }
        
        static bool TraceEventType(string? eventType) => eventType != nameof(PlayerPositionEvent) && eventType != nameof(ProjectilePositionEvent);
    }

    public Task HandleAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var commandType = command.GetType();
        using var activity = Telemetry.ActivitySource.StartActivity("PublishCommand:" + commandType.Name);

        if (!_moduleResolver.CommandHandlers.TryGetValue(commandType, out var handlers) || handlers.Count == 0)
        {
            _logger.LogDebug("No loaded module handles {CommandType}", commandType.Name);
            return Task.CompletedTask;
        }

        activity?.SetTag("bf2wa.command-handler-count", handlers.Count);

        // On the thread pool, not on the caller's thread (for a chat line the admin channel's apply loop): a command may run
        // for seconds. Errors are handled in the command handler wrapper
        var running = new Task[handlers.Count];
        for (var i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];
            running[i] = Task.Run(() => handler(command).AsTask());
        }

        return Task.WhenAll(running);
    }
}
