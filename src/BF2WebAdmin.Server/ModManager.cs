using System.Net;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Configuration.Models;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Logging;
using BF2WebAdmin.Server.Modules.BF2;
using BF2WebAdmin.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;

namespace BF2WebAdmin.Server;

// TODO: Move commands into new module folders? (like Features/)
public partial class ModManager : IModManager
{
    private static IReadOnlyPolicyRegistry<string>? PolicyRegistry;
    public static readonly string[] DefaultModuleNames = { nameof(BF2Module), nameof(LogModule), nameof(WebModule) };

    /// <summary>Chat commands start with "." or "!" ("!" was ModManager's in-game admin prefix, which the servers no longer run).</summary>
    public static bool IsCommandText(string text) => text.Length > 1 && text[0] is '.' or '!';

    private readonly IModuleResolver _moduleResolver;
    private readonly IGameServer _gameServer;
    private readonly IServiceProvider _globalServices;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private IConfiguration Configuration { get; set; }
    private IServiceProvider _services;
    private int _disposed;
    private readonly ILogger<ModManager> _logger;

    public ILookup<string, ServerPlayerAuth> AuthPlayers { get; private set; }

    public Data.Entities.Server ServerSettings { get; private set; }

    public IMediator Mediator { get; private set; }

    private ModManager(IGameServer gameServer, IServiceProvider globalServices, CancellationToken cancellationToken)
    {
        _gameServer = gameServer;
        _globalServices = globalServices;
        _logger = globalServices.GetRequiredService<ILogger<ModManager>>();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _moduleResolver = new ModuleResolver(globalServices.GetRequiredService<ILogger<ModuleResolver>>());
        Mediator = new Mediator(_moduleResolver, globalServices.GetRequiredService<ILogger<Mediator>>());
        PolicyRegistry = CreatePolicyRegistry(globalServices.GetRequiredService<ILogger<ModManager>>());
        
        BuildConfiguration();
        ConfigureDependencies();
    }

    public static async Task<ModManager> CreateAsync(IGameServer gameServer, IServiceProvider globalServices, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var modManager = new ModManager(gameServer, globalServices, cancellationToken);
        
        activity?.AddEvent(new ("Created Modmanager"));

        // One after the other: EF Core queries can't run on the same DbContext in parallel
        await PolicyRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyAsync).ExecuteAsync(async () =>
        {
            await modManager.GetServerSettingsAsync();
            await modManager.CreateModulesAsync();
            await modManager.GetAuthPlayersAsync();
        });

        return modManager;
    }

    private static IReadOnlyPolicyRegistry<string> CreatePolicyRegistry(ILogger<ModManager> logger)
    {
        if (PolicyRegistry is not null)
            return PolicyRegistry;
        
        // Exponential back-off plus some jitter
        var jitterer = new Random();

        var retryPolicyAsync = Policy
            .Handle<SqlException>()
            .WaitAndRetryAsync(
                retryCount: 6,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 100)),
                onRetry: (exception, timespan, context) => logger.LogWarning("Failed attempt #{Attempt}. Retrying in {TimeSpan}.\n{ExceptionMessage} - {ExceptionStacktrace}", timespan, context, exception.Message, exception.StackTrace)
            );

        var retryPolicyLongAsync = Policy
            .Handle<SqlException>()
            .WaitAndRetryAsync(
                retryCount: 10, // 2 + 4 + ... + 1024 s, ~34 min
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 100)),
                onRetry: (exception, retryAttempt, timespan) => logger.LogWarning("Failed attempt #{Attempt}. Retrying in {TimeSpan}.\n{ExceptionMessage} - {ExceptionStacktrace}", retryAttempt, timespan, exception.Message, exception.StackTrace)
            );

        var httpStatusCodesWorthRetrying = new[] {
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout // 504
        };
        var retryPolicyHttpAsync = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: 6,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 100)),
                onRetry: (exception, retryAttempt, timespan) => logger.LogWarning("Failed attempt #{Attempt}. Retrying in {TimeSpan}.\n{ExceptionMessage} - {ExceptionStacktrace}", retryAttempt, timespan, exception.Exception.Message, exception.Exception.StackTrace)
            );

        var retryPolicy = Policy
            .Handle<SqlException>()
            .WaitAndRetry(
                retryCount: 6,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 100)),
                onRetry: (exception, retryAttempt, timespan) => logger.LogWarning("Failed attempt #{Attempts}. Retrying in {TimeSpan}.\n{ExceptionMessage} - {ExceptionStacktrace}", retryAttempt, timespan, exception.Message, exception.StackTrace)
            );

        return new PolicyRegistry
        {
            { PolicyNames.RetryPolicy, retryPolicy },
            { PolicyNames.RetryPolicyAsync, retryPolicyAsync },
            { PolicyNames.RetryPolicyLongAsync, retryPolicyLongAsync },
            { PolicyNames.RetryPolicyHttpAsync, retryPolicyHttpAsync }
        };
    }
    
    public T? GetModule<T>() where T : IModule
    {
        return (T?)(_moduleResolver.Modules.TryGetValue(typeof(T), out var result) ? result : null);
    }

    public T GetGlobalService<T>() where T : notnull
    {
        return _globalServices.GetRequiredService<T>();
    }

    private void BuildConfiguration()
    {
        var profile = ProjectContext.GetEnvironmentName();
        var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
        var builder = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{profile}.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsecrets.{profile}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        Configuration = builder.Build();
    }

    private void ConfigureDependencies()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(builder => builder.AddProvider(_globalServices.GetRequiredService<ILoggerProvider>()));

        // Options
        var geoipConfig = Configuration.GetSection("Geoip");
        serviceCollection.AddOptions();
        serviceCollection.Configure<TwitterConfig>(Configuration.GetSection("Twitter"));
        serviceCollection.Configure<MashapeConfig>(Configuration.GetSection("Mashape"));
        serviceCollection.Configure<DiscordConfig>(Configuration.GetSection("Discord"));
        serviceCollection.Configure<LogConfig>(Configuration.GetSection("ServerSettings"));
        serviceCollection.Configure<GeoipConfig>(geoipConfig);

        // Services
        serviceCollection.AddSingleton(_gameServer);
        serviceCollection.AddSingleton(_cancellationTokenSource);
        serviceCollection.AddSingleton<ICountryResolver>(c => new CountryResolver(geoipConfig["DatabasePath"]));
        serviceCollection.AddSingleton<IReadOnlyPolicyRegistry<string>>(PolicyRegistry);
        serviceCollection.AddSingleton<IMediator>(c => Mediator);
        // The host's services as instances: the container disposes what its factories create, never an instance it was given
        serviceCollection.AddSingleton(_globalServices.GetRequiredService<IHubContext<ServerHub, IServerHubClient>>());
        if (_globalServices.GetService<MassTransit.IBus>() is { } bus)
            serviceCollection.AddSingleton(bus);
        else
            serviceCollection.AddSingleton<MassTransit.IBus>(_ => null!);
        serviceCollection.AddSingleton<IGameStreamService, RabbitMqGameStreamService>();
        serviceCollection.AddSingleton<ILogger>(_ => _logger);
        serviceCollection.AddSingleton<ITaskRunner, TaskRunner>();
        serviceCollection.AddSingleton<IDelayProvider, DelayProvider>();
        serviceCollection.AddSingleton(TimeProvider.System);
            
        var discordSettings = Configuration.GetSection("Discord").Get<DiscordConfig>();
        if (!string.IsNullOrWhiteSpace(discordSettings?.Webhooks?.FirstOrDefault()?.WebhookId))
        {
            serviceCollection.AddSingleton<IChatLogger, DiscordChatLogger>();
        }
        else
        {
            serviceCollection.AddSingleton<IChatLogger, FakeChatLogger>();
        }

        // The same database as Program.cs. The modules resolve the repositories once from this root provider; the EF ones
        // use a short context per call, so that is safe
        DatabaseSetup.FromConfiguration(Configuration).AddRepositories(serviceCollection, maps: true, joins: false);

        // Modules
        foreach (var moduleType in _moduleResolver.ModuleTypes)
        {
            serviceCollection.AddSingleton(moduleType);
        }

        _services = serviceCollection.BuildServiceProvider();
    }

    private async Task CreateModulesAsync()
    {
        var serverSettingsRepository = _services.GetRequiredService<IServerSettingsRepository>();
        var enabledModuleNames = await serverSettingsRepository.GetModulesAsync(ServerSettings.ServerGroup);
        var enabledModuleTypes = _moduleResolver.ModuleTypes.Where(t => DefaultModuleNames.Contains(t.Name) || enabledModuleNames.Contains(t.Name));

        // Instantiate modules
        foreach (var moduleType in enabledModuleTypes)
        {
            try
            {
                var moduleInstance = (IModule)_services.GetRequiredService(moduleType);
                _moduleResolver.Modules.Add(moduleType, moduleInstance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resolve module error");
                throw;
            }
        }
    }

    private async Task GetServerSettingsAsync()
    {
        var serverSettingsRepository = _services.GetRequiredService<IServerSettingsRepository>();
        ServerSettings = await serverSettingsRepository.GetServerAsync(_gameServer.Id) ?? new Data.Entities.Server
        {
            ServerId = _gameServer.Id,
            ServerGroup = "default"
        };
    }

    public async Task GetAuthPlayersAsync()
    {
        var serverSettingsRepository = _services.GetService<IServerSettingsRepository>();
        var authPlayers = (await serverSettingsRepository.GetPlayerAuthAsync(ServerSettings.ServerGroup)).ToList();

            
        // Add dummy admins for each server group so we can send commands from Discord
        foreach (var group in authPlayers.GroupBy(p => p.ServerGroup).Select(g => g.Key))
        {
            authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.God, PlayerHash = DiscordModule.DiscordBotHashGod, ServerGroup = group });
            authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.SuperAdmin, PlayerHash = DiscordModule.DiscordBotHashSuperAdmin, ServerGroup = group });
            authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.Admin, PlayerHash = DiscordModule.DiscordBotHashAdmin, ServerGroup = group });
            authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.God, PlayerHash = WebModule.WebAdminHashGod, ServerGroup = group });
        }
            
        // Add dummy admins without groups
        authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.God, PlayerHash = DiscordModule.DiscordBotHashGod });
        authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.SuperAdmin, PlayerHash = DiscordModule.DiscordBotHashSuperAdmin });
        authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.Admin, PlayerHash = DiscordModule.DiscordBotHashAdmin });
        authPlayers.Add(new ServerPlayerAuth { AuthLevel = (int)Auth.God, PlayerHash = WebModule.WebAdminHashGod });

        AuthPlayers = authPlayers.ToLookup(p => p.PlayerHash);
    }

    public async Task HandleFakeChatMessageAsync(Message message, TimeSpan? wait = null)
    {
        var handled = RunChatCommand(message);
        if (wait is not { } timeout)
            return;

        try
        {
            await handled.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            // A long command (a freeze, a playback) goes on; the caller gets what it said so far
        }
    }

    public ValueTask HandleChatMessageAsync(Message message)
    {
        // The handlers run in the background: the admin channel's apply loop does not wait for a command
        _ = RunChatCommand(message);
        return ValueTask.CompletedTask;
    }

    public IEnumerable<string> GetModules()
    {
        return _moduleResolver.Modules.Keys.Select(k => k.Name).ToList();
    }

    public IReadOnlyList<CommandInfo> GetCommands() => _moduleResolver.GetCommands();

    public bool IsCommand(string alias) => LoadedParsers(alias.ToLowerInvariant()).Count > 0;

    /// <summary>The parsers of an alias whose command a loaded module handles.</summary>
    private IReadOnlyList<CommandParser> LoadedParsers(string alias) =>
        _moduleResolver.CommandParsers.TryGetValue(alias, out var parsers)
            ? parsers.Where(p => _moduleResolver.IsHandled(p.CommandType)).ToList()
            : [];

    /// <summary>
    /// Runs a chat line that is a command: one command (see <see cref="ModuleResolver.ParseCommand"/>), if the player's
    /// auth level allows it. Returns a task that completes when its handlers have; it never fails.
    /// </summary>
    private Task RunChatCommand(Message message)
    {
        if (message.Type != MessageType.Player || message.Player == null)
            return Task.CompletedTask;

        // Cleanup *DEAD*
        message.Text = DeadPrefix().Replace(message.Text, string.Empty);
        if (!IsCommandText(message.Text))
            return Task.CompletedTask;

        // TODO: rate limit low users
        var parts = message.Text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Task.CompletedTask;

        var alias = parts[0].ToLowerInvariant();
        var parameters = parts[1..];
        var parsers = LoadedParsers(alias);
        if (parsers.Count == 0)
            return Task.CompletedTask;

        var playerAuthLevel = AuthPlayers[message.Player.Hash].FirstOrDefault()?.AuthLevel ?? (int)Auth.All;
        var command = ModuleResolver.ParseCommand(parsers, parameters, out var parser);
        if (command == null || parser == null)
        {
            // Tell the player how the command goes, if it may use it
            var usage = parsers.Where(p => playerAuthLevel >= (int)p.Attribute.AuthLevel).Select(p => p.Usage(alias)).Distinct().ToList();
            if (usage.Count > 0)
                _gameServer.GameWriter.SendPrivateMessage(message.Player, "Usage: " + string.Join(" or ", usage));
            return Task.CompletedTask;
        }

        if (playerAuthLevel < (int)parser.Attribute.AuthLevel)
        {
            _gameServer.GameWriter.SendPrivateMessage(message.Player, "Insufficient permissions");
            return Task.CompletedTask;
        }

        if (command is BaseCommand baseCommand)
            baseCommand.Message = message;
        else
            _logger.LogWarning("{CommandName} is not a {BaseCommand}", command.GetType().Name, nameof(BaseCommand));

        try
        {
            return Mediator.HandleAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {Command} failed: {ExceptionMessage}", command.GetType().Name, ex.Message);
            return Task.CompletedTask;
        }
    }

    [GeneratedRegex(@"^\*[^\*]+\*")]
    private static partial Regex DeadPrefix();

    /// <summary>
    /// Cancels the modules' token and disposes the modules' service provider, so modules that hold connections, files or
    /// background loops (Discord, the log files) release them. The services forwarded from the host are not disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await _cancellationTokenSource.CancelAsync();
        try
        {
            if (_services is IAsyncDisposable services)
                await services.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disposing the modules of {ServerId} failed", _gameServer.Id);
        }

        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}