using System.Reflection;
using System.Text;
using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;
using Serilog;

namespace BF2WebAdmin.Server
{
    public class ModuleResolver : IModuleResolver
    {
        private static readonly Assembly CurrentAssembly = Assembly.GetEntryAssembly();

        private static readonly StringBuilder CommandDocumentation;

        public IEnumerable<Type> ModuleTypes => _moduleTypes;
        private static IEnumerable<Type> _moduleTypes;

        private static IEnumerable<Type> _commandTypes;
        private static IEnumerable<Type> _eventTypes;

        // Parses different command aliases and parameters to their respective ICommand(s)
        public IDictionary<string, IList<Func<string[], ICommand>>> CommandParsers => _commandParsers;
        private static IDictionary<string, IList<Func<string[], ICommand>>> _commandParsers =
            new Dictionary<string, IList<Func<string[], ICommand>>>();

        // Get auth level required for a command
        public IDictionary<Type, Auth> AuthLevels => _authLevels;
        private static IDictionary<Type, Auth> _authLevels = new Dictionary<Type, Auth>();

        // Invokes the handler module instance(s) with a command of the given Type
        public IDictionary<Type, IList<Func<ICommand, ValueTask>>> CommandHandlers => GetCommandHandlers();
        private IDictionary<Type, IList<Func<ICommand, ValueTask>>> _commandHandlers;

        // Invokes the handler module instance(s) with a command of the given Type
        public IDictionary<Type, IList<Func<IEvent, ValueTask>>> EventHandlers => GetEventHandlers();
        private IDictionary<Type, IList<Func<IEvent, ValueTask>>> _eventHandlers;

        // Module instances
        public IDictionary<Type, IModule> Modules { get; } = new Dictionary<Type, IModule>();

        static ModuleResolver()
        {
            CommandDocumentation = new StringBuilder();
            ScanAssembly();
            File.WriteAllText("commands.txt", CommandDocumentation.ToString());
        }

        private IDictionary<Type, IList<Func<ICommand, ValueTask>>> GetCommandHandlers()
        {
            if (_commandHandlers == null)
            {
                _commandHandlers = new Dictionary<Type, IList<Func<ICommand, ValueTask>>>();
                CreateCommandHandlers();
            }

            return _commandHandlers;
        }

        private void CreateCommandHandlers()
        {
            foreach (var commandType in _commandTypes)
            {
                Action create = CreateCommandHandler<ICommand>;
                var method = create.GetMethodInfo().GetGenericMethodDefinition();
                var generic = method.MakeGenericMethod(commandType);
                generic.Invoke(this, null);
            }
        }

        private void CreateCommandHandler<TCommand>() where TCommand : ICommand
        {
            // TODO: Search in IModules instead of whole assembly?
            var assemblyCommandHandlers = GetCommandHandlers(CurrentAssembly);

            var handlerCount = 0;
            foreach (var commandHandler in assemblyCommandHandlers)
            {
                var commandType = commandHandler.CommandType;
                if (!CommandHandlers.ContainsKey(commandType))
                    CommandHandlers.Add(commandType, new List<Func<ICommand, ValueTask>>());

                // Has an instance of this module been created?
                if (!Modules.ContainsKey(commandHandler.ModuleType))
                {
                    // throw new NullReferenceException($"No instance of {commandHandler.ModuleType} exists");
                    Log.Verbose("No instance of {ModuleType} exists", commandHandler.ModuleType);
                    continue;
                }

                // Check if this module is a valid synchronous handler for TCommand
                if (Modules[commandHandler.ModuleType] is IHandleCommand<TCommand> syncHandler)
                {
                    CommandHandlers[commandType].Add(command =>
                    {
                        // Check command type since alias can map to different commands depending on parameters
                        if (command is TCommand matchedCommand)
                        {
                            try
                            {
                                syncHandler.Handle(matchedCommand);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Command handling failed for {message} (sync)", matchedCommand.Message);
                            }
                        }

                        return ValueTask.CompletedTask;
                    });

                    handlerCount++;
                    continue;
                }

                // Ok, well maybe it's async
                if (Modules[commandHandler.ModuleType] is IHandleCommandAsync<TCommand> asyncHandler)
                {
                    CommandHandlers[commandType].Add(async command =>
                    {
                        // Check command type since alias can map to different commands depending on parameters
                        if (command is TCommand matchedCommand)
                        {
                            try
                            {
                                await asyncHandler.HandleAsync(matchedCommand);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Command handling failed for {message} (async)", matchedCommand.Message);
                            }
                        }
                    });

                    handlerCount++;
                    continue;
                }
            }

            if (handlerCount == 0)
                Log.Verbose("No command handlers registered for {commandName}", typeof(TCommand).Name);
        }

        private IDictionary<Type, IList<Func<IEvent, ValueTask>>> GetEventHandlers()
        {
            if (_eventHandlers == null)
            {
                _eventHandlers = new Dictionary<Type, IList<Func<IEvent, ValueTask>>>();
                CreateEventHandlers();
            }

            return _eventHandlers;
        }

        private void CreateEventHandlers()
        {
            foreach (var eventType in _eventTypes)
            {
                Action create = CreateEventHandler<IEvent>;
                var method = create.GetMethodInfo().GetGenericMethodDefinition();
                var generic = method.MakeGenericMethod(eventType);
                generic.Invoke(this, null);
            }
        }

        private void CreateEventHandler<TEvent>() where TEvent : IEvent
        {
            // TODO: Search in IModules instead of whole assembly?
            var assemblyEventHandlers = GetEventHandlers(CurrentAssembly);

            var handlerCount = 0;
            foreach (var eventHandler in assemblyEventHandlers)
            {
                var eventType = eventHandler.EventType;
                if (!EventHandlers.ContainsKey(eventType))
                    EventHandlers.Add(eventType, new List<Func<IEvent, ValueTask>>());

                // Has an instance of this module been created?
                if (!Modules.ContainsKey(eventHandler.ModuleType))
                {
                    // Some modules are optional
                    //throw new NullReferenceException($"No instance of {eventHandler.ModuleType} exists");
                    Log.Verbose("No instance of {ModuleTypeName} exists for {EventTypeName}", eventHandler.ModuleType.Name, eventHandler.EventType.Name);
                    continue;
                }

                // Ok, well maybe it's async
                if (Modules[eventHandler.ModuleType] is IHandleEventAsync<TEvent> asyncHandler)
                {
                    EventHandlers[eventType].Add(async gameEvent =>
                    {
                        // Check command type since alias can map to different commands depending on parameters
                        if (gameEvent is TEvent matchedEvent)
                        {
                            try
                            {
                                await asyncHandler.HandleAsync(matchedEvent);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Event handling failed for {Event} (async)", matchedEvent);
                            }
                        }
                    });

                    handlerCount++;
                    continue;
                }
            }

            if (handlerCount == 0)
                Log.Verbose("No event handlers registered for {EventName}", typeof(TEvent).Name);
        }

        private static void ScanAssembly()
        {
            _moduleTypes = GetModules(CurrentAssembly).ToArray();
            _commandTypes = GetCommands(CurrentAssembly).ToArray();
            _eventTypes = GetEvents(CurrentAssembly).ToArray();

            foreach (var commandType in _commandTypes)
            {
                var attributes = AllAttributes<CommandAttribute>(commandType);
                foreach (var attr in attributes)
                {
                    foreach (var alias in attr.Aliases)
                    {
                        if (!_commandParsers.ContainsKey(alias))
                            _commandParsers.Add(alias, new List<Func<string[], ICommand>>());

                        // Add a parser function for this alias
                        _commandParsers[alias].Add(GetCommandParser(commandType, attr));

                        // Add auth level required for command
                        _authLevels[commandType] = attr.AuthLevel;
                    }

                    var parameters = attr.Parameters != null ? $"<{string.Join("> <", attr.Parameters)}>" : string.Empty;
                    CommandDocumentation.AppendLine($"{string.Join("|", attr.Aliases ?? Array.Empty<string>())} {parameters}\t{attr.AuthLevel}\t({commandType.Name})");
                }
            }
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
                    var argumentValue = shouldCombineRemainingParameters ?
                        string.Join(" ", args.Skip(index)) : args[index++];

                    var convertedValue = Convert.ChangeType(argumentValue, propertyInfo.PropertyType);
                    propertyInfo.SetValue(command, convertedValue);
                }

                return command;
            };
        }

        private static IEnumerable<Type> GetModules(Assembly assembly)
        {
            return AllTypesWithInterface<IModule>(assembly);
        }

        private static IEnumerable<CommandModule> GetCommandHandlers(Assembly assembly)
        {
            return
                from t in assembly.GetTypes()
                let interfaces = t.GetInterfaces().Where(IsCommandHandler)
                from i in interfaces
                let args = i.GetGenericArguments()
                select new CommandModule
                {
                    CommandType = args[0],
                    ModuleType = t
                };
        }

        private static IEnumerable<EventModule> GetEventHandlers(Assembly assembly)
        {
            return
                from t in assembly.GetTypes()
                let interfaces = t.GetInterfaces().Where(IsEventHandler)
                from i in interfaces
                let args = i.GetGenericArguments()
                select new EventModule
                {
                    EventType = args[0],
                    ModuleType = t
                };
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

        private class CommandModule
        {
            public Type CommandType { get; set; }
            public Type ModuleType { get; set; }
        }

        private class EventModule
        {
            public Type EventType { get; set; }
            public Type ModuleType { get; set; }
        }
    }

    public class CommandArgumentCountException : Exception
    {
        public CommandArgumentCountException(string message) : base(message) { }
    }

    public class CommandParseException : Exception
    {
        public CommandParseException(string message) : base(message) { }
    }
}
