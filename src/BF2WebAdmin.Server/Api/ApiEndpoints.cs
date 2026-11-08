using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Controllers;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Modules.BF2;
using BF2WebAdmin.Shared;
using BF2WebAdmin.Shared.Communication.DTOs;
using Mapster;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace BF2WebAdmin.Server.Api;

public record LoginRequest(string Username, string Password);
public record UserDto(string Name);
public record CommandRequest(string Command);
public record ChatRequest(string Message);
public record RconResponse(string Request, string Response);
public record ChatResponse(string? Response);
/// <summary>What a "." command said in game while it ran; empty if it said nothing.</summary>
public record CommandResponse(string Command, IReadOnlyList<string> Output);
/// <summary>The answer of a server action, for the notification.</summary>
public record ActionResponse(string Message);
public record MapRequest(string Name, string GameMode, int Size);
/// <summary>A chat command of a loaded module. Parameters are the names in the [Command] pattern.</summary>
public record CommandInfoDto(IReadOnlyList<string> Aliases, IReadOnlyList<string> Parameters, int AuthLevel, IReadOnlyList<string> Modules);
/// <summary>A player's auth level in a server group. Name and ServerId are set while the player is on a server of the group.</summary>
public record PlayerAuthDto(string PlayerHash, int AuthLevel, string? Name, string? ServerId);
public record SetPlayerAuthRequest(string PlayerHash, int AuthLevel);

/// <summary>
/// The web API used by the web UI (src/BF2WebAdmin.Web). Live updates go through <see cref="ServerHub"/>.
/// Server ids are "ip:port" and are URL-encoded in paths.
/// </summary>
public static class ApiEndpoints
{
    public const string AdminPolicy = "Admin";

    public static void MapApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        // As a Delegate: a method that only takes an HttpContext would bind as a RequestDelegate and ignore its result
        auth.MapPost("/logout", (Delegate)LogoutAsync);
        auth.MapGet("/me", (ClaimsPrincipal user) => new UserDto(user.Identity?.Name ?? "")).RequireAuthorization(AdminPolicy);

        var servers = api.MapGroup("/servers").WithTags("Servers").RequireAuthorization(AdminPolicy);
        servers.MapGet("/", GetServersAsync);
        servers.MapPost("/", CreateServerAsync);
        servers.MapPost("/test", TestServerAsync);
        servers.MapGet("/{serverId}", GetServerAsync);
        servers.MapDelete("/{serverId}", RemoveServerAsync);
        servers.MapGet("/{serverId}/settings", GetServerSettingsAsync);
        servers.MapPut("/{serverId}/settings", UpdateServerSettingsAsync);
        servers.MapPost("/{serverId}/chat", SendChatAsync);
        servers.MapPost("/{serverId}/rcon", SendRconAsync);
        servers.MapPost("/{serverId}/commands", SendCustomCommandAsync);
        servers.MapGet("/{serverId}/commands", GetCommands);
        servers.MapGet("/{serverId}/log", (string serverId, CommandLog log) => log.Get(serverId));
        servers.MapPost("/{serverId}/map", ChangeMapAsync);
        servers.MapPost("/{serverId}/map/restart", RestartMapAsync);
        servers.MapPost("/{serverId}/map/next", NextMapAsync);
        servers.MapPost("/{serverId}/pause", TogglePauseAsync);
        servers.MapGet("/{serverId}/bans", GetBansAsync);
        servers.MapGet("/{serverId}/missiles", GetMissiles);
        servers.MapGet("/{serverId}/missiles/{key:int}", GetMissile);

        var modules = api.MapGroup("/").WithTags("Modules").RequireAuthorization(AdminPolicy);
        modules.MapGet("/modules", () => ModuleResolver.AllModuleNames.Order().ToArray());
        modules.MapGet("/server-groups/{serverGroup}/modules", GetServerGroupModulesAsync);
        modules.MapPut("/server-groups/{serverGroup}/modules", SetServerGroupModulesAsync);
        modules.MapPost("/server-groups/{serverGroup}/modules/reload", ReloadServerGroupModulesAsync);

        var playerAuth = api.MapGroup("/server-groups/{serverGroup}/auth").WithTags("Player auth").RequireAuthorization(AdminPolicy);
        playerAuth.MapGet("/", GetPlayerAuthAsync);
        playerAuth.MapPut("/", SetPlayerAuthAsync);
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> LoginAsync(LoginRequest request, HttpContext context, IOptions<AuthSettings> authSettings)
    {
        var user = authSettings.Value.Admins.FirstOrDefault(a => a.Username == request.Username);
        if (user is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.Password), Encoding.UTF8.GetBytes(request.Password)))
            return TypedResults.Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Username),
            new(ClaimTypes.Role, "Administrator"),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    /// <summary>Connected servers and the configured servers that have not connected.</summary>
    private static async Task<List<ServerDto>> GetServersAsync(IGameServerRegistry registry)
    {
        var servers = registry.GetGameServers().Select(s => (ServerDto)s.ToServerUpdate()).ToList();
        servers.AddRange((await registry.GetDisconnectedServersAsync()).Select(s => s.ToServerUpdate()));
        return servers;
    }

    private static async Task<Results<Ok<ServerSnapshotDto>, NotFound>> GetServerAsync(string serverId, IGameServerRegistry registry)
    {
        var server = registry.GetGameServer(serverId);
        if (server is not null)
            return TypedResults.Ok(server.ToSnapshot());

        var serverInfo = (await registry.GetDisconnectedServersAsync()).FirstOrDefault(s => s.ServerId == serverId);
        return serverInfo is null ? TypedResults.NotFound() : TypedResults.Ok(serverInfo.ToSnapshot());
    }

    /// <summary>The last traced TV missiles of a connected server, newest first (the 3D viewer's list).</summary>
    private static Results<Ok<IReadOnlyList<MissileSummaryDto>>, NotFound> GetMissiles(string serverId, IGameServerRegistry registry) =>
        registry.GetGameServer(serverId) is GameServer server ? TypedResults.Ok(server.Viewer.Missiles) : TypedResults.NotFound();

    /// <summary>A traced missile's whole flight: per tick the missile and every target where it is and where it was tested.</summary>
    private static Results<Ok<MissileReplayDto>, NotFound> GetMissile(string serverId, int key, IGameServerRegistry registry) =>
        registry.GetGameServer(serverId) is GameServer server && server.Viewer.GetMissile(key) is { } missile
            ? TypedResults.Ok(missile)
            : TypedResults.NotFound();

    private static async Task<ServerDataDto> GetServerSettingsAsync(string serverId, IServerSettingsRepository repository, IGameServerRegistry registry)
    {
        var server = await repository.GetServerAsync(serverId);
        if (server is not null)
            return server.Adapt<ServerDataDto>();

        // A server from ServerSettings:GameServers (such as the AppHost's openbf2) has no row. Saving the form adds one,
        // which then wins over the configured entry
        var serverInfo = await registry.FindServerInfoAsync(serverId);
        if (serverInfo is null)
            return new ServerDataDto { ServerId = serverId, ServerGroup = "default" };

        return new ServerDataDto
        {
            ServerId = serverInfo.ServerId,
            ServerGroup = serverInfo.ServerGroup,
            IpAddress = serverInfo.IpAddress,
            GamePort = serverInfo.GamePort,
            QueryPort = serverInfo.QueryPort,
            RconPort = serverInfo.RconPort,
            RconPassword = serverInfo.RconPassword,
            DiscordBotToken = serverInfo.DiscordBot?.Token ?? "",
            DiscordAdminChannel = serverInfo.DiscordBot?.AdminChannel ?? "",
            DiscordNotificationChannel = serverInfo.DiscordBot?.NotificationChannel ?? "",
            DiscordMatchResultChannel = serverInfo.DiscordBot?.MatchResultChannel ?? "",
            AdminKey = serverInfo.AdminKey,
            IsTestServer = serverInfo.IsTestServer
        };
    }

    private static async Task<NoContent> CreateServerAsync(ServerDataDto server, IGameServerRegistry registry, IHubContext<ServerHub, IServerHubClient> hub)
    {
        await registry.AddOrUpdateServerAsync(server.Adapt<Data.Entities.Server>());
        await BroadcastServerAsync(server.ServerId, registry, hub);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, BadRequest<string>>> UpdateServerSettingsAsync(string serverId, ServerDataDto server, IGameServerRegistry registry,
        IHubContext<ServerHub, IServerHubClient> hub)
    {
        if (server.ServerId != serverId)
            return TypedResults.BadRequest("The server id in the path and the body differ");

        await registry.AddOrUpdateServerAsync(server.Adapt<Data.Entities.Server>());
        await BroadcastServerAsync(serverId, registry, hub);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveServerAsync(string serverId, IGameServerRegistry registry, IHubContext<ServerHub, IServerHubClient> hub)
    {
        await registry.RemoveServerAsync(serverId);
        await hub.Clients.All.ServerRemoveEvent(new Shared.Communication.Events.ServerRemoveEvent { ServerId = serverId });
        return TypedResults.NoContent();
    }

    private static async Task BroadcastServerAsync(string serverId, IGameServerRegistry registry, IHubContext<ServerHub, IServerHubClient> hub)
    {
        var server = registry.GetGameServer(serverId)?.ToServerUpdate()
                     ?? (await registry.GetDisconnectedServersAsync()).FirstOrDefault(s => s.ServerId == serverId)?.ToServerUpdate();

        if (server is not null)
            await hub.Clients.All.ServerUpdateEvent(server);
    }

    private static async Task<TestServerResult[]> TestServerAsync(ServerDataDto server, ILogger<RconClient> logger)
    {
        TestServerResult rcon;
        TestServerResult gamespy;

        try
        {
            using var rconClient = new RconClient(IPAddress.Parse(server.IpAddress), server.RconPort, server.RconPassword, logger);
            var rconResponse = await rconClient.SendAsync("iga listAdmins");
            rcon = string.IsNullOrWhiteSpace(rconResponse)
                ? new TestServerResult("RCON", false, $"No answer from {server.IpAddress}:{server.RconPort}")
                : new TestServerResult("RCON", true);
        }
        catch (Exception e)
        {
            rcon = new TestServerResult("RCON", false, e.Message);
        }

        try
        {
            var gamespyResponse = await new Gamespy3Service().QueryServerAsync(IPAddress.Parse(server.IpAddress), server.QueryPort);
            gamespy = string.IsNullOrWhiteSpace(gamespyResponse.Name)
                ? new TestServerResult("Gamespy query", false, $"No server name from {server.IpAddress}:{server.QueryPort}")
                : new TestServerResult("Gamespy query", true, gamespyResponse.Name);
        }
        catch (Exception e)
        {
            gamespy = new TestServerResult("Gamespy query", false, e.Message);
        }

        return [rcon, gamespy];
    }

    private static async Task<Results<Ok<ChatResponse>, NotFound>> SendChatAsync(string serverId, ChatRequest request, ClaimsPrincipal user, IGameServerRegistry registry,
        CommandLog log)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Message);
        var response = await module.HandleAdminChatAsync(GetUserId(user), request.Message);
        var kind = request.Message.StartsWith('.') ? CommandKind.Command : request.Message.StartsWith('!') ? CommandKind.Rcon : CommandKind.Chat;
        await log.AddAsync(serverId, GetUserId(user), kind, request.Message, response, true);
        return TypedResults.Ok(new ChatResponse(response));
    }

    private static async Task<Results<Ok<RconResponse>, NotFound>> SendRconAsync(string serverId, CommandRequest request, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Command);
        var response = await module.HandleRconCommandAsync(request.Command);
        await log.AddAsync(serverId, GetUserId(user), CommandKind.Rcon, request.Command, response, true);
        return TypedResults.Ok(new RconResponse(request.Command, response));
    }

    private static async Task<Results<Ok<CommandResponse>, NotFound>> SendCustomCommandAsync(string serverId, CommandRequest request, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Command);
        var output = await module.HandleCustomCommandAsync(GetUserId(user), request.Command);
        await log.AddAsync(serverId, GetUserId(user), CommandKind.Command, request.Command, output.Count > 0 ? string.Join('\n', output) : null, true);
        return TypedResults.Ok(new CommandResponse(request.Command, output));
    }

    /// <summary>The chat commands of the server's loaded modules, for the command panel and the chat box's completion.</summary>
    private static Results<Ok<CommandInfoDto[]>, NotFound> GetCommands(string serverId, IGameServerRegistry registry)
    {
        if (registry.GetGameServer(serverId) is not { } server)
            return TypedResults.NotFound();

        return TypedResults.Ok(server.ModManager.GetCommands()
            .Select(c => new CommandInfoDto(c.Aliases, c.Parameters, (int)c.AuthLevel, c.Modules))
            .ToArray());
    }

    /// <summary>Run a map of the rotation now (the admin channel's typed Map command, by name, mode and size).</summary>
    private static Task<Results<Ok<ActionResponse>, BadRequest<string>, NotFound>> ChangeMapAsync(string serverId, MapRequest request, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log) =>
        RunActionAsync(serverId, user, registry, log, $"Change map to {request.Name} ({request.GameMode}, {request.Size})", async server =>
        {
            await server.GameWriter.RunMapAsync(request.Name, request.GameMode, request.Size);
            return $"Loading {request.Name}";
        });

    private static Task<Results<Ok<ActionResponse>, BadRequest<string>, NotFound>> RestartMapAsync(string serverId, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log) =>
        RunActionAsync(serverId, user, registry, log, "Restart map", async server =>
        {
            await server.GameWriter.RestartMapAsync();
            return "Restarting the round";
        });

    private static Task<Results<Ok<ActionResponse>, BadRequest<string>, NotFound>> NextMapAsync(string serverId, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log) =>
        RunActionAsync(serverId, user, registry, log, "Run next map", async server =>
        {
            await server.GameWriter.RunNextMapAsync();
            return "Loading the next map";
        });

    private static Task<Results<Ok<ActionResponse>, BadRequest<string>, NotFound>> TogglePauseAsync(string serverId, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log) =>
        RunActionAsync(serverId, user, registry, log, "Toggle pause", async server =>
        {
            var output = await server.GameWriter.ConsoleAsync("gameLogic.togglePause");
            return output.Count > 0 ? string.Join('\n', output) : "Pause toggled";
        });

    /// <summary>Runs a typed admin channel command. A refusal or a closed link is a 400 with the reason, which the web UI shows.</summary>
    private static async Task<Results<Ok<ActionResponse>, BadRequest<string>, NotFound>> RunActionAsync(string serverId, ClaimsPrincipal user,
        IGameServerRegistry registry, CommandLog log, string description, Func<IGameServer, Task<string>> action)
    {
        if (registry.GetGameServer(serverId) is not { } server)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", description);
        try
        {
            var message = await action(server);
            await log.AddAsync(serverId, GetUserId(user), CommandKind.Action, description, message, true);
            return TypedResults.Ok(new ActionResponse(message));
        }
        catch (AdminCommandException e)
        {
            var message = e.Code switch
            {
                AdminErrorCode.Unavailable => "The server refused: the map is not in its rotation",
                AdminErrorCode.Disabled => "The server refused: disabled in its serverext.cfg",
                AdminErrorCode.Refused => $"The server refused: {e.Message}",
                _ => e.Message,
            };
            await log.AddAsync(serverId, GetUserId(user), CommandKind.Action, description, message, false);
            return TypedResults.BadRequest(message);
        }
    }

    /// <summary>The server's ban list (BF2CC "iga" banlist, an XML document).</summary>
    private static async Task<Results<Ok<List<Ban>>, NotFound>> GetBansAsync(string serverId, IGameServerRegistry registry)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        var response = await module.HandleRconCommandAsync("!banlist");
        try
        {
            using var reader = new StringReader(response);
            var banlist = new XmlSerializer(typeof(Banlist)).Deserialize(reader) as Banlist;
            return TypedResults.Ok(banlist?.Bans ?? []);
        }
        catch (InvalidOperationException)
        {
            // Not a ban list, such as "Empty response"
            return TypedResults.Ok(new List<Ban>());
        }
    }

    private static async Task<string[]> GetServerGroupModulesAsync(string serverGroup, IServerSettingsRepository repository)
    {
        var modules = await repository.GetModulesAsync(serverGroup);
        return modules.Concat(ModManager.DefaultModuleNames).Distinct().Order().ToArray();
    }

    private static async Task<Results<NoContent, BadRequest<string>>> SetServerGroupModulesAsync(string serverGroup, string[] moduleNames, IServerSettingsRepository repository)
    {
        if (serverGroup == "default")
            return TypedResults.BadRequest("The default server group cannot be changed");

        await repository.SetModulesAsync(serverGroup, moduleNames);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ReloadServerGroupModulesAsync(string serverGroup, IGameServerRegistry registry)
    {
        foreach (var gameServer in registry.GetGameServers(serverGroup))
        {
            // TODO: disable whatever the old modmanager is doing, dispose and finish all long-running tasks
            await gameServer.CreateModManagerAsync(true);
        }

        return TypedResults.NoContent();
    }

    /// <summary>The players with an auth level other than the default (All) in the group, highest first.</summary>
    private static async Task<PlayerAuthDto[]> GetPlayerAuthAsync(string serverGroup, IServerSettingsRepository repository, IGameServerRegistry registry)
    {
        var online = OnlinePlayers(serverGroup, registry);
        return (await repository.GetPlayerAuthAsync(serverGroup))
            .Where(a => a.AuthLevel != (int)Common.Auth.All)
            .Select(a => online.TryGetValue(a.PlayerHash, out var p)
                ? new PlayerAuthDto(a.PlayerHash, a.AuthLevel, p.Name, p.ServerId)
                : new PlayerAuthDto(a.PlayerHash, a.AuthLevel, NameFromKey(a.PlayerHash), null))
            .OrderByDescending(a => a.AuthLevel)
            .ThenBy(a => a.Name ?? a.PlayerHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly int[] AuthLevels = Enum.GetValues<Common.Auth>().Where(a => a != Common.Auth.None).Select(a => (int)a).ToArray();

    // ServerPlayerAuth.PlayerHash is nvarchar(50)
    private const int MaxPlayerHashLength = 50;

    /// <summary>
    /// Set a player's auth level in the group, by CD-key hash (or the "name:" key of a test server). The default level,
    /// All (10), is how an entry is removed: the row stays, with the level a player without a row has. The group's
    /// servers read the new levels at once.
    /// </summary>
    private static async Task<Results<NoContent, BadRequest<string>>> SetPlayerAuthAsync(string serverGroup, SetPlayerAuthRequest request, ClaimsPrincipal user,
        IServerSettingsRepository repository, IGameServerRegistry registry, CommandLog log)
    {
        var hash = request.PlayerHash?.Trim() ?? "";
        if (hash.Length == 0 || hash.Length > MaxPlayerHashLength)
            return TypedResults.BadRequest($"The player hash must be 1-{MaxPlayerHashLength} characters");
        if (!AuthLevels.Contains(request.AuthLevel))
            return TypedResults.BadRequest($"Unknown auth level {request.AuthLevel}");
        if (hash == WebModule.WebAdminHashGod || hash.StartsWith("DiscordBotHash", StringComparison.Ordinal))
            return TypedResults.BadRequest("That hash belongs to WebAdmin itself");

        await repository.SetPlayerAuthAsync(serverGroup, hash, request.AuthLevel);

        var name = OnlinePlayers(serverGroup, registry).TryGetValue(hash, out var p) ? p.Name : NameFromKey(hash) ?? hash;
        foreach (var server in registry.GetGameServers(serverGroup))
        {
            await server.ModManager.GetAuthPlayersAsync();
            await log.AddAsync(server.Id, GetUserId(user), CommandKind.Action,
                $"Set the auth level of {name} to {(Common.Auth)request.AuthLevel} ({request.AuthLevel})", null, true);
        }

        return TypedResults.NoContent();
    }

    private static Dictionary<string, (string Name, string ServerId)> OnlinePlayers(string serverGroup, IGameServerRegistry registry)
    {
        var players = new Dictionary<string, (string Name, string ServerId)>();
        foreach (var server in registry.GetGameServers(serverGroup))
        foreach (var player in server.Players.Where(p => !string.IsNullOrEmpty(p.Hash)))
            players.TryAdd(player.Hash, (player.Name, server.Id));
        return players;
    }

    /// <summary>A test server keys players without a CD-key hash by name ("name:" + name).</summary>
    private static string? NameFromKey(string playerHash) => playerHash.StartsWith("name:", StringComparison.Ordinal) ? playerHash[5..] : null;

    private static WebModule? GetWebModule(IGameServerRegistry registry, string serverId) =>
        registry.GetGameServer(serverId)?.ModManager.GetModule<WebModule>();

    private static string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
