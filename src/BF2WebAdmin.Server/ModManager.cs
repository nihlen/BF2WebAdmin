using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.DAL;
using BF2WebAdmin.DAL.Repositories;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Logging;
using BF2WebAdmin.Server.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Client;

namespace BF2WebAdmin.Server
{
    public interface IModManager { }

    public class ModManager : IModManager
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<ModManager>();
        private static readonly Assembly CurrentAssembly = Assembly.GetEntryAssembly();

        private static readonly string CommandPrefix = ".";

        // Module types
        private static Type[] _moduleTypes;

        // Command types
        private static Type[] _commandTypes;

        // Parses different command aliases and parameters to their respective ICommand(s)
        private static Dictionary<string, List<Func<string[], ICommand>>> _commandParsers =
            new Dictionary<string, List<Func<string[], ICommand>>>();

        // The BF2 server
        private IGameServer _gameServer;

        // Module instances
        private Dictionary<Type, IModule> _modules =
            new Dictionary<Type, IModule>();

        // Invokes the handler module(s) with a command of the given Type
        private Dictionary<Type, List<Action<ICommand>>> _commandHandlers =
            new Dictionary<Type, List<Action<ICommand>>>();

        private IServiceProvider _services;

        static ModManager()
        {
            // Scan assembly for commands and modules to handle them
            ScanAssembly();
        }

        public ModManager(IGameServer gameServer)
        {
            _gameServer = gameServer;
            gameServer.ChatMessage += HandleChatMessage;

            ConfigureDependencies();
            CreateModules();
            CreateCommandHandlers();
        }

        private void ConfigureDependencies()
        {
            var serviceCollection = new ServiceCollection();

            // Raven
            serviceCollection.AddSingleton(DocumentStoreHolder.Store);

            // Services
            serviceCollection.AddSingleton(_gameServer);
            serviceCollection.AddSingleton<IScriptRepository, RavenScriptRepository>();

            // Modules
            foreach (var moduleType in _moduleTypes)
                serviceCollection.AddSingleton(moduleType);

            _services = serviceCollection.BuildServiceProvider();
        }

        private void CreateModules()
        {
            // Instantiate modules
            foreach (var moduleType in _moduleTypes)
            {
                try
                {
                    var moduleInstance = (IModule)_services.GetRequiredService(moduleType);
                    _modules.Add(moduleType, moduleInstance);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resolve module error", ex);
                    throw;
                }
            }
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
            var commandHandlers = GetCommandHandlers(CurrentAssembly); // Search in IModules instead of whole assembly?

            foreach (var commandHandler in commandHandlers)
            {
                var cmdType = commandHandler.CommandType;
                if (!_commandHandlers.ContainsKey(cmdType))
                    _commandHandlers.Add(cmdType, new List<Action<ICommand>>());

                _commandHandlers[cmdType].Add(command =>
                {
                    if (!_modules.ContainsKey(commandHandler.ModuleType))
                        throw new Exception($"No instance of {commandHandler.ModuleType} exists");

                    var handler = _modules[commandHandler.ModuleType] as IHandleCommand<TCommand>;
                    if (handler == null)
                        throw new Exception($"{commandHandler.ModuleType} is not a valid handler for {typeof(TCommand)}");

                    handler.Handle((TCommand)command);
                });
            }
        }

        private void HandleChatMessage(Message message)
        {
            if (message.Type != MessageType.Player)
                return;
            if (message.Text.StartsWith(CommandPrefix))
                HandleCommand(message);
        }

        private void HandleCommand(Message message)
        {
            var cmd = message.Text.Substring(CommandPrefix.Length);
            var parts = cmd.Split(' ');
            var alias = parts[0];
            var parameters = parts.Skip(1).ToArray();

            // Unknown command
            if (!_commandParsers.ContainsKey(alias))
                return;

            // Try to parse the message into different commands
            var parsers = _commandParsers[alias];
            foreach (var parser in parsers)
            {
                try
                {
                    // TODO: auth
                    var command = parser(parameters);
                    var commandType = command.GetType();

                    // Append the message if it's a BaseCommand
                    var baseCommand = command as BaseCommand;
                    if (baseCommand != null)
                        baseCommand.Message = message;

                    if (!_commandHandlers.ContainsKey(commandType))
                        throw new Exception($"No command handler registered for {commandType.Name}");

                    foreach (var handler in _commandHandlers[commandType])
                        handler(command);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("Handle exception", ex);
                }
            }
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
                var index = 0;
                foreach (var propName in attr.Parameters)
                {
                    if (index > args.Length)
                        throw new Exception($"Command does not have enough args (expecting {attr.Parameters.Length})");

                    var propInfo = commandType.GetProperty(propName);
                    if (propInfo == null)
                        throw new Exception($"No property {propName} found in {commandType}");

                    var value = Convert.ChangeType(args[index++], propInfo.PropertyType);
                    propInfo.SetValue(command, value);
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
                let interfaces = t.GetInterfaces().Where(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleCommand<>))
                from i in interfaces
                let args = i.GetGenericArguments()
                select new CommandModule
                {
                    CommandType = args[0],
                    ModuleType = t
                };
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
}
