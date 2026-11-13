using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Configuration.Models;
using BF2WebAdmin.Server.Logging;
using BF2WebAdmin.Server.Services;
using BF2WebAdmin.Server.StreamBot;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.Registry;

namespace BF2WebAdmin.Server;

/// <summary>
/// The game modules' services, registered once in the host (<see cref="AddGameModules"/>). Each server's
/// <see cref="ModManager"/> creates its modules in a scope of the host's provider: the modules and the server's own
/// services (<see cref="ModuleScope"/>: the game server, its mediator, its module thread, its token) are scoped, the rest
/// (settings, repositories, retry policies, the Discord webhooks) is shared by all servers. Disposing the scope disposes
/// that server's modules.
/// </summary>
public static class ModuleServices
{
    public static IServiceCollection AddGameModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();
        services.Configure<DiscordConfig>(configuration.GetSection("Discord"));
        services.Configure<LogConfig>(configuration.GetSection("ServerSettings"));

        // Shared by all servers
        services.AddSingleton<ICountryResolver>(_ => new CountryResolver(configuration["Geoip:DatabasePath"]));
        services.AddSingleton<IReadOnlyPolicyRegistry<string>>(sp => ModManager.CreatePolicyRegistry(sp.GetRequiredService<ILogger<ModManager>>()));
        services.AddSingleton<StreamBotService>();
        services.AddSingleton<IGameStreamService>(sp => sp.GetRequiredService<StreamBotService>());
        services.AddSingleton<IDelayProvider, DelayProvider>();
        services.TryAddSingleton(TimeProvider.System);

        var discord = configuration.GetSection("Discord").Get<DiscordConfig>();
        if (!string.IsNullOrWhiteSpace(discord?.Webhooks?.FirstOrDefault()?.WebhookId))
            services.AddSingleton<IChatLogger, DiscordChatLogger>();
        else
            services.AddSingleton<IChatLogger, FakeChatLogger>();

        // One per server. The scope disposes what these factories return: only the token source is disposable, and
        // ModManager disposes it anyway
        services.AddScoped<ModuleScope>();
        services.AddScoped(sp => sp.GetRequiredService<ModuleScope>().GameServer);
        services.AddScoped(sp => sp.GetRequiredService<ModuleScope>().Mediator);
        services.AddScoped(sp => sp.GetRequiredService<ModuleScope>().TaskRunner);
        services.AddScoped(sp => sp.GetRequiredService<ModuleScope>().Cancellation);

        // Every module type; a server creates only those its server group enables
        foreach (var moduleType in ModuleResolver.AllModuleTypes)
            services.AddScoped(moduleType);

        return services;
    }
}

/// <summary>The services of one server's modules, set by its <see cref="ModManager"/> before it creates the modules.</summary>
public sealed class ModuleScope
{
    private IGameServer? _gameServer;
    private IMediator? _mediator;
    private ITaskRunner? _taskRunner;
    private CancellationTokenSource? _cancellation;

    public IGameServer GameServer => _gameServer ?? throw NotInitialized();
    public IMediator Mediator => _mediator ?? throw NotInitialized();
    public ITaskRunner TaskRunner => _taskRunner ?? throw NotInitialized();
    public CancellationTokenSource Cancellation => _cancellation ?? throw NotInitialized();

    public void Initialize(IGameServer gameServer, IMediator mediator, ITaskRunner taskRunner, CancellationTokenSource cancellation)
    {
        if (_gameServer != null)
            throw new InvalidOperationException("The module scope is initialized already");

        _gameServer = gameServer;
        _mediator = mediator;
        _taskRunner = taskRunner;
        _cancellation = cancellation;
    }

    private static InvalidOperationException NotInitialized() =>
        new("Modules are created in a server's scope (ModManager), not from the host's provider");
}
