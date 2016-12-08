using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.DAL;
using BF2WebAdmin.DAL.Abstractions;
using BF2WebAdmin.DAL.Repositories;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Logging;
using BF2WebAdmin.Server.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    public class ModManager : IModManager
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<ModManager>();

        private static IModuleResolver _moduleResolver;

        private const string CommandPrefix = ".";

        private IConfiguration Configuration { get; set; }

        private IGameServer _gameServer;

        private IServiceProvider _services;

        public ModManager(IGameServer gameServer) : this(gameServer, new ModuleResolver()) { }

        public ModManager(IGameServer gameServer, IModuleResolver moduleResolver)
        {
            _gameServer = gameServer;
            _moduleResolver = moduleResolver;

            gameServer.ChatMessage += HandleChatMessage;

            BuildConfiguration();
            ConfigureDependencies();
            CreateModules();
        }

        private void BuildConfiguration()
        {
            var profile = "debug"; // TODO: get current profile
            var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
            var builder = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{profile}.json", optional: false)
                .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsecrets.{profile}.json", optional: false);

            Configuration = builder.Build();
        }

        private void ConfigureDependencies()
        {
            var serviceCollection = new ServiceCollection();

            // Raven
            serviceCollection.AddSingleton(DocumentStoreHolder.Store);

            // Options
            serviceCollection.AddOptions();
            serviceCollection.Configure<TwitterConfig>(Configuration.GetSection("Twitter"));
            serviceCollection.Configure<MashapeConfig>(Configuration.GetSection("Mashape"));

            // Services
            serviceCollection.AddSingleton(_gameServer);
            serviceCollection.AddSingleton<IScriptRepository, RavenScriptRepository>();

            // Modules
            foreach (var moduleType in _moduleResolver.ModuleTypes)
                serviceCollection.AddSingleton(moduleType);

            _services = serviceCollection.BuildServiceProvider();
        }

        private void CreateModules()
        {
            // Instantiate modules
            foreach (var moduleType in _moduleResolver.ModuleTypes)
            {
                try
                {
                    var moduleInstance = (IModule)_services.GetRequiredService(moduleType);
                    _moduleResolver.Modules.Add(moduleType, moduleInstance);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resolve module error", ex);
                    throw;
                }
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
            // TODO: rate limit low users
            var cmd = message.Text.Substring(CommandPrefix.Length);
            var parts = cmd.Split(' ');
            var alias = parts[0];
            var parameters = parts.Skip(1).ToArray();

            // Unknown command
            if (!_moduleResolver.CommandParsers.ContainsKey(alias))
                return;

            // Try to parse the message into different commands
            var parsers = _moduleResolver.CommandParsers[alias];
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
                    else
                        Logger.LogWarning($"{commandType.Name} is not a {nameof(BaseCommand)}");

                    if (!_moduleResolver.CommandHandlers.ContainsKey(commandType))
                        throw new Exception($"No command handler registered for {commandType.Name}");

                    foreach (var handler in _moduleResolver.CommandHandlers[commandType])
                        handler(command);
                }
                catch (CommandArgumentCountException ex)
                {
                    Logger.LogDebug("Wrong argument count", ex);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Command handle exception", ex);
                }
            }
        }
    }
}
