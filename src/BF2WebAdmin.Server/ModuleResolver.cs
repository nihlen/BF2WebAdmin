using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    public class ModuleResolver : IModuleResolver
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<ModManager>();

        private static readonly Assembly CurrentAssembly = Assembly.GetEntryAssembly();

        public IEnumerable<Type> ModuleTypes => _moduleTypes;
        private static IEnumerable<Type> _moduleTypes;

        private static IEnumerable<Type> _commandTypes;

        // Parses different command aliases and parameters to their respective ICommand(s)
        public IDictionary<string, IList<Func<string[], ICommand>>> CommandParsers => _commandParsers;
        private static IDictionary<string, IList<Func<string[], ICommand>>> _commandParsers =
            new Dictionary<string, IList<Func<string[], ICommand>>>();

        // Invokes the handler module instance(s) with a command of the given Type
        public IDictionary<Type, IList<Action<ICommand>>> CommandHandlers => GetCommandHandlers();
        private IDictionary<Type, IList<Action<ICommand>>> _commandHandlers;

        // Module instances
        public IDictionary<Type, IModule> Modules { get; } = new Dictionary<Type, IModule>();

        static ModuleResolver()
        {
            ScanAssembly();
        }

        private IDictionary<Type, IList<Action<ICommand>>> GetCommandHandlers()
        {
            if (_commandHandlers == null)
            {
                _commandHandlers = new Dictionary<Type, IList<Action<ICommand>>>();
                CreateCommandHandlers();
            }

            return _commandHandlers;
        }

        private void CreateCommandHandlers()
        {
            foreach (var cmdType in _commandTypes)
            {
                Action create = CreateCommandHandler<ICommand>;
                var method = create.GetMethodInfo().GetGenericMethodDefinition();
                var generic = method.MakeGenericMethod(cmdType);
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
                    CommandHandlers.Add(commandType, new List<Action<ICommand>>());

                // Has an instance been created of this module?
                if (!Modules.ContainsKey(commandHandler.ModuleType))
                    throw new NullReferenceException($"No instance of {commandHandler.ModuleType} exists");

                // Check if this module is a valid synchrounous handler for TCommand
                var syncHandler = Modules[commandHandler.ModuleType] as IHandleCommand<TCommand>;
                if (syncHandler != null)
                {
                    CommandHandlers[commandType].Add(command =>
                    {
                        // Check command type since alias can map to different commands depending on parameters
                        if (command is TCommand)
                            syncHandler.Handle((TCommand)command);
                    });
                    handlerCount++;
                    continue;
                }

                // Ok, well maybe it's async
                var asyncHandler = Modules[commandHandler.ModuleType] as IHandleCommandAsync<TCommand>;
                if (asyncHandler != null)
                {
                    CommandHandlers[commandType].Add(async command =>
                    {
                        // Check command type since alias can map to different commands depending on parameters
                        if (command is TCommand)
                            await asyncHandler.HandleAsync((TCommand)command);
                    });
                    handlerCount++;
                }
            }

            if (handlerCount == 0)
                Logger.LogWarning($"No command handlers registered for {typeof(TCommand).Name}");
        }

        private static void ScanAssembly()
        {
            _moduleTypes = GetModules(CurrentAssembly).ToArray();
            _commandTypes = GetCommands(CurrentAssembly).ToArray();

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
                    }
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
                    var argumentValue = attr.Parameters.Length == (index + 1) && attr.CombineLast ?
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

        private static bool IsCommandHandler(Type type)
        {
            return type.GetTypeInfo().IsGenericType &&
                (type.GetGenericTypeDefinition() == typeof(IHandleCommand<>) ||
                type.GetGenericTypeDefinition() == typeof(IHandleCommandAsync<>));
        }

        private static IEnumerable<Type> GetCommands(Assembly assembly)
        {
            return AllTypesWithInterface<ICommand>(assembly);
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