using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server;

public class ModuleResolver : IModuleResolver
{
    private readonly ILogger<ModuleResolver> _logger;
    private static readonly Assembly CurrentAssembly = typeof(ModuleResolver).Assembly;

    private static readonly StringBuilder CommandDocumentation;

    public static IEnumerable<string> AllModuleNames => _moduleTypes.Select(t => t.Name);
    public IEnumerable<Type> ModuleTypes => _moduleTypes;
    private static IEnumerable<Type> _moduleTypes;

    private static IEnumerable<Type> _commandTypes;
    private static IEnumerable<Type> _eventTypes;

    // The handler interfaces of every type in the assembly: (command or event type, module type)
    private static (Type CommandType, Type ModuleType)[] _commandModules;
    private static (Type EventType, Type ModuleType)[] _eventModules;

    // The parsers of each alias: a chat line's alias and arguments to an ICommand
    public IReadOnlyDictionary<string, IReadOnlyList<CommandParser>> CommandParsers => _commandParsers;
    private static readonly Dictionary<string, IReadOnlyList<CommandParser>> _commandParsers = new();

    // Get auth level required for a command
    public IDictionary<Type, Auth> AuthLevels => _authLevels;
    private static IDictionary<Type, Auth> _authLevels = new Dictionary<Type, Auth>();

    // Invokes the handler module instance(s) with a command of the given Type. Built on first use, after the modules are created
    public IDictionary<Type, IList<Func<ICommand, ValueTask>>> CommandHandlers => GetHandlers().Commands;

    // Invokes the handler module instance(s) with an event of the given Type
    public IDictionary<Type, IList<Func<IEvent, ValueTask>>> EventHandlers => GetHandlers().Events;

    private readonly object _handlersLock = new();
    private (IDictionary<Type, IList<Func<ICommand, ValueTask>>> Commands, IDictionary<Type, IList<Func<IEvent, ValueTask>>> Events)? _handlers;

    // Module instances
    public IDictionary<Type, IModule> Modules { get; } = new Dictionary<Type, IModule>();

    // Every [Command] attribute, and the modules that handle each command type
    private static readonly List<(Type CommandType, CommandAttribute Attribute)> _commandAttributes = new();
    private static ILookup<Type, Type> _commandModuleTypes;

    /// <summary>The chat commands that a loaded module handles, in declaration order.</summary>
    public IReadOnlyList<CommandInfo> GetCommands() =>
        _commandAttributes
            .Select(c => (c.CommandType, c.Attribute, Modules: _commandModuleTypes[c.CommandType].Where(Modules.ContainsKey).Select(t => t.Name).ToArray()))
            .Where(c => c.Modules.Length > 0)
            .Select(c => new CommandInfo(
                c.Attribute.Aliases,
                c.Attribute.Parameters ?? [],
                c.Attribute.AuthLevel,
                c.Modules))
            .Where(c => c.Aliases.Count > 0)
            .ToList();

    /// <summary>Whether a loaded module handles commands of this type.</summary>
    public bool IsHandled(Type commandType) => _commandModuleTypes[commandType].Any(Modules.ContainsKey);

    static ModuleResolver()
    {
        CommandDocumentation = new StringBuilder();
        ScanAssembly();
#if DEBUG
        try
        {
            File.WriteAllText("commands.txt", CommandDocumentation.ToString());
        }
        catch (Exception)
        {
            // Ignore, just used for reference when building locally
        }
#endif
    }

    public ModuleResolver(ILogger<ModuleResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The command that a chat line's arguments stand for among the parsers of its alias, or null when none fits. Only one
    /// command runs per line: a parser whose parameter count matches the arguments beats one whose last parameter takes the
    /// rest of the line, then typed parameters beat text (".sw 3" is SwitchLater, ".sw bob" is Switch), then more parameters.
    /// </summary>
    public static ICommand? ParseCommand(IEnumerable<CommandParser> parsers, string[] args, out CommandParser? matched)
    {
        var candidates = parsers
            .Select(p => (Parser: p, Fit: p.Fit(args.Length)))
            .Where(c => c.Fit > 0)
            .OrderByDescending(c => c.Fit)
            .ThenByDescending(c => c.Parser.TypedParameterCount)
            .ThenByDescending(c => c.Parser.ParameterCount);

        foreach (var (parser, _) in candidates)
        {
            try
            {
                var command = parser.Parse(args);
                matched = parser;
                return command;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or CommandArgumentCountException)
            {
                // The arguments don't convert to this command's parameters; try the next one
            }
        }

        matched = null;
        return null;
    }

    private (IDictionary<Type, IList<Func<ICommand, ValueTask>>> Commands, IDictionary<Type, IList<Func<IEvent, ValueTask>>> Events) GetHandlers()
    {
        if (_handlers is { } handlers)
            return handlers;

        lock (_handlersLock)
        {
            if (_handlers is { } created)
                return created;

            var commands = new Dictionary<Type, IList<Func<ICommand, ValueTask>>>();
            foreach (var commandType in _commandTypes)
            {
                var create = CreateCommandHandlers<ICommand>;
                create.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(commandType).Invoke(this, [commands]);
            }

            var events = new Dictionary<Type, IList<Func<IEvent, ValueTask>>>();
            foreach (var eventType in _eventTypes)
            {
                var create = CreateEventHandlers<IEvent>;
                create.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(eventType).Invoke(this, [events]);
            }

            _handlers = (commands, events);
            return _handlers.Value;
        }
    }

    private void CreateCommandHandlers<TCommand>(IDictionary<Type, IList<Func<ICommand, ValueTask>>> handlers) where TCommand : ICommand
    {
        var commandType = typeof(TCommand);
        var list = new List<Func<ICommand, ValueTask>>();
        handlers[commandType] = list;

        foreach (var moduleType in _commandModules.Where(h => h.CommandType == commandType).Select(h => h.ModuleType).Distinct())
        {
            // Has an instance of this module been created?
            if (!Modules.TryGetValue(moduleType, out var module))
            {
                _logger.LogTrace("No instance of {ModuleType} exists", moduleType);
                continue;
            }

            if (module is IHandleCommand<TCommand> syncHandler)
            {
                list.Add(command =>
                {
                    var matchedCommand = (TCommand)command;
                    // TODO: add GameServer to command context so we can add the serverid to metrics
                    var startTime = DateTimeOffset.UtcNow;
                    var moduleName = syncHandler.GetType().Name;
                    using var activity = Telemetry.ActivitySource.StartActivity("SyncCommandHandler:" + moduleName);
                    activity?.SetTag("bf2wa.command-type", commandType.Name);
                    activity?.SetTag("bf2wa.module-type", moduleName);

                    try
                    {
                        syncHandler.Handle(matchedCommand);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Command handling failed for {Message} (sync)", matchedCommand.Message.Text);
                        activity?.SetStatus(ActivityStatusCode.Error, $"Command failed: {matchedCommand.Message.Text}");
                    }

                    AppDiagnostics.TrackCommand(commandType.Name, moduleName, startTime);
                    return ValueTask.CompletedTask;
                });
            }
            else if (module is IHandleCommandAsync<TCommand> asyncHandler)
            {
                list.Add(async command =>
                {
                    var matchedCommand = (TCommand)command;
                    var startTime = DateTimeOffset.UtcNow;
                    var moduleName = asyncHandler.GetType().Name;
                    using var activity = Telemetry.ActivitySource.StartActivity("AsyncCommandHandler:" + moduleName);
                    activity?.SetTag("bf2wa.command-type", commandType.Name);
                    activity?.SetTag("bf2wa.module-type", moduleName);

                    try
                    {
                        await asyncHandler.HandleAsync(matchedCommand);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Command handling failed for {Message} (async)", matchedCommand.Message.Text);
                        activity?.SetStatus(ActivityStatusCode.Error, $"Command failed: {matchedCommand.Message.Text} {ex.Message}");
                    }

                    AppDiagnostics.TrackCommand(commandType.Name, moduleName, startTime);
                });
            }
        }

        if (list.Count == 0)
            _logger.LogTrace("No command handlers registered for {CommandName}", commandType.Name);
    }

    private void CreateEventHandlers<TEvent>(IDictionary<Type, IList<Func<IEvent, ValueTask>>> handlers) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        var list = new List<Func<IEvent, ValueTask>>();
        handlers[eventType] = list;
        var traced = eventType != typeof(PlayerPositionEvent) && eventType != typeof(ProjectilePositionEvent);

        foreach (var moduleType in _eventModules.Where(h => h.EventType == eventType).Select(h => h.ModuleType).Distinct())
        {
            // Has an instance of this module been created? Some modules are optional
            if (!Modules.TryGetValue(moduleType, out var module))
            {
                _logger.LogTrace("No instance of {ModuleTypeName} exists for {EventTypeName}", moduleType.Name, eventType.Name);
                continue;
            }

            if (module is not IHandleEventAsync<TEvent> asyncHandler)
                continue;

            list.Add(async gameEvent =>
            {
                var matchedEvent = (TEvent)gameEvent;
                var startTime = DateTimeOffset.UtcNow;
                var moduleName = asyncHandler.GetType().Name;
                using var activity = traced ? Telemetry.ActivitySource.StartActivity("AsyncEventHandler:" + moduleName) : null;
                activity?.SetTag("bf2wa.event-type", eventType.Name);
                activity?.SetTag("bf2wa.module-type", moduleName);

                try
                {
                    await asyncHandler.HandleAsync(matchedEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Event handling failed for {Event} (async)", matchedEvent);
                    activity?.SetStatus(ActivityStatusCode.Error, $"Event failed: {eventType.Name}, {ex.Message}");
                }

                AppDiagnostics.TrackEvent(eventType.Name, moduleName, startTime);
            });
        }

        if (list.Count == 0)
            _logger.LogTrace("No event handlers registered for {EventName}", eventType.Name);
    }

    private static void ScanAssembly()
    {
        _moduleTypes = GetModules(CurrentAssembly).ToArray();
        _commandTypes = GetCommands(CurrentAssembly).ToArray();
        _eventTypes = GetEvents(CurrentAssembly).ToArray();
        _commandModules = GetCommandHandlers(CurrentAssembly).ToArray();
        _eventModules = GetEventHandlers(CurrentAssembly).ToArray();
        _commandModuleTypes = _commandModules.ToLookup(c => c.CommandType, c => c.ModuleType);

        var parsers = new Dictionary<string, List<CommandParser>>();
        foreach (var commandType in _commandTypes)
        {
            var attributes = AllAttributes<CommandAttribute>(commandType);
            foreach (var attr in attributes)
            {
                _commandAttributes.Add((commandType, attr));
                var parser = new CommandParser(commandType, attr, GetCommandParser(commandType, attr));
                foreach (var alias in attr.Aliases)
                {
                    if (!parsers.TryGetValue(alias, out var list))
                        parsers[alias] = list = new List<CommandParser>();
                    list.Add(parser);

                    // Add auth level required for command
                    _authLevels[commandType] = attr.AuthLevel;
                }

                var parameters = attr.Parameters != null ? $"<{string.Join("> <", attr.Parameters)}>" : string.Empty;
                CommandDocumentation.AppendLine($"{string.Join("|", attr.Aliases ?? Array.Empty<string>())} {parameters}\t{attr.AuthLevel}\t({commandType.Name})");
            }
        }

        foreach (var (alias, list) in parsers)
            _commandParsers[alias] = list;
    }

    private static Func<string[], ICommand> GetCommandParser(Type commandType, CommandAttribute attr)
    {
        return args =>
        {
            var command = (ICommand)Activator.CreateInstance(commandType);

            if (attr.Parameters == null && args.Length > 0)
                throw new CommandArgumentCountException("Command expects no args");

            if (attr.Parameters == null || !attr.Parameters.Any())
                return command;

            var index = 0;
            foreach (var propertyName in attr.Parameters)
            {
                if (index >= args.Length)
                    throw new CommandArgumentCountException($"Command does not have enough args (expecting {attr.Parameters.Length})");

                var propertyInfo = commandType.GetProperty(propertyName);
                if (propertyInfo == null)
                    throw new CommandParseException($"No property {propertyName} found in {commandType}");

                // Assign the rest of the string to the last parameter?
                var shouldCombineRemainingParameters = attr.Parameters.Length == (index + 1) && attr.CombineLast;
                var argumentValue = shouldCombineRemainingParameters ? string.Join(" ", args.Skip(index)) : args[index++];

                // Numbers as typed in chat ("0.5"), whatever the server's culture
                var convertedValue = Convert.ChangeType(argumentValue, propertyInfo.PropertyType, CultureInfo.InvariantCulture);
                propertyInfo.SetValue(command, convertedValue);
            }

            return command;
        };
    }

    private static IEnumerable<Type> GetModules(Assembly assembly)
    {
        return AllTypesWithInterface<IModule>(assembly);
    }

    private static IEnumerable<(Type CommandType, Type ModuleType)> GetCommandHandlers(Assembly assembly)
    {
        return
            from t in assembly.GetTypes()
            let interfaces = t.GetInterfaces().Where(IsCommandHandler)
            from i in interfaces
            select (i.GetGenericArguments()[0], t);
    }

    private static IEnumerable<(Type EventType, Type ModuleType)> GetEventHandlers(Assembly assembly)
    {
        return
            from t in assembly.GetTypes()
            let interfaces = t.GetInterfaces().Where(IsEventHandler)
            from i in interfaces
            select (i.GetGenericArguments()[0], t);
    }

    private static bool IsCommandHandler(Type type)
    {
        return type.GetTypeInfo().IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(IHandleCommand<>) ||
                type.GetGenericTypeDefinition() == typeof(IHandleCommandAsync<>));
    }

    private static bool IsEventHandler(Type type)
    {
        return type.GetTypeInfo().IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(IHandleEventAsync<>));
    }

    private static IEnumerable<Type> GetCommands(Assembly assembly)
    {
        return AllTypesWithInterface<ICommand>(assembly);
    }

    private static IEnumerable<Type> GetEvents(Assembly assembly)
    {
        return AllTypesWithInterface<IEvent>(assembly);
    }

    private static IEnumerable<Type> AllTypesWithInterface<T>(Assembly assembly)
    {
        return
            from t in assembly.GetTypes()
            where !t.GetTypeInfo().IsAbstract && t.GetInterfaces().Any(i => i == typeof(T))
            select t;
    }

    private static IEnumerable<TAttr> AllAttributes<TAttr>(Type type) where TAttr : Attribute
    {
        var typeInfo = type.GetTypeInfo();
        return typeInfo.GetCustomAttributes<TAttr>();
    }
}

/// <summary>One [Command] attribute of a command type: how a chat line's arguments become that command.</summary>
public sealed class CommandParser(Type commandType, CommandAttribute attribute, Func<string[], ICommand> parse)
{
    public Type CommandType { get; } = commandType;
    public CommandAttribute Attribute { get; } = attribute;
    public Func<string[], ICommand> Parse { get; } = parse;

    public int ParameterCount => Attribute.Parameters?.Length ?? 0;

    /// <summary>The parameters that are not text, such as numbers: a value that converts says more than one that fits any text.</summary>
    public int TypedParameterCount { get; } = (attribute.Parameters ?? [])
        .Count(p => commandType.GetProperty(p)?.PropertyType is { } type && type != typeof(string));

    /// <summary>2 when the arguments match the parameters one to one, 1 when the last parameter takes the rest of the line, 0 when they don't fit.</summary>
    public int Fit(int argumentCount) =>
        argumentCount == ParameterCount ? 2
        : argumentCount > ParameterCount && ParameterCount > 0 ? 1
        : 0;

    /// <summary>".alias &lt;Param&gt; ..." for a usage message.</summary>
    public string Usage(string alias) =>
        ParameterCount == 0 ? "." + alias : $".{alias} <{string.Join("> <", Attribute.Parameters!)}>";
}

public class CommandArgumentCountException : Exception
{
    public CommandArgumentCountException(string message) : base(message)
    {
    }
}

public class CommandParseException : Exception
{
    public CommandParseException(string message) : base(message)
    {
    }
}
