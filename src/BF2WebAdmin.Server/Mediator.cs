using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server;

public interface IMediator
{
    /// <summary>Runs the event's handlers one after the other, on the module thread; completes when they have.</summary>
    ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent;

    /// <summary>
    /// Queues the command's handlers on the module thread and returns at once; the returned task completes when they all
    /// have. A handler's errors are logged, never thrown.
    /// </summary>
    Task HandleAsync<TCommand>(TCommand command) where TCommand : ICommand;
}

/// <summary>
/// Hands events and commands to the modules' handlers. With a <see cref="ModuleContext"/> (a server's modules) every
/// handler runs on its thread, so events, commands and the modules' own background work never run at the same time;
/// without one (tests) events run on the caller and commands on the thread pool.
/// </summary>
public class Mediator : IMediator
{
    private readonly IModuleResolver _moduleResolver;
    private readonly ILogger<Mediator> _logger;
    private readonly ModuleContext? _context;

    public Mediator(IModuleResolver moduleResolver, ILogger<Mediator> logger, ModuleContext? context = null)
    {
        _moduleResolver = moduleResolver;
        _logger = logger;
        _context = context;
    }

    public ValueTask PublishAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent
    {
        // A module publishing (RoundEndEvent) is on the module thread already and runs the handlers in place
        if (_context == null || _context.IsCurrent)
            return PublishHereAsync(gameEvent);

        return new ValueTask(_context.InvokeAsync(() => PublishHereAsync(gameEvent).AsTask()));
    }

    private async ValueTask PublishHereAsync<TEvent>(TEvent gameEvent) where TEvent : IEvent
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

        // Queued, not run on the caller's thread (for a chat line the admin channel's apply loop): a command may run for
        // seconds, and the apply loop goes on with the next event while it waits. Errors are handled in the command handler
        // wrapper
        var running = new Task[handlers.Count];
        for (var i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];
            running[i] = _context != null
                ? _context.InvokeAsync(() => handler(command).AsTask())
                : Task.Run(() => handler(command).AsTask());
        }

        return Task.WhenAll(running);
    }
}
