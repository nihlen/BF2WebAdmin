using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Controllers;
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
        servers.MapGet("/{serverId}/bans", GetBansAsync);

        var modules = api.MapGroup("/").WithTags("Modules").RequireAuthorization(AdminPolicy);
        modules.MapGet("/modules", () => ModuleResolver.AllModuleNames.Order().ToArray());
        modules.MapGet("/server-groups/{serverGroup}/modules", GetServerGroupModulesAsync);
        modules.MapPut("/server-groups/{serverGroup}/modules", SetServerGroupModulesAsync);
        modules.MapPost("/server-groups/{serverGroup}/modules/reload", ReloadServerGroupModulesAsync);
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

    private static async Task<ServerDataDto> GetServerSettingsAsync(string serverId, IServerSettingsRepository repository)
    {
        var server = await repository.GetServerAsync(serverId) ?? new Data.Entities.Server
        {
            ServerId = serverId,
            ServerGroup = "default"
        };

        return server.Adapt<ServerDataDto>();
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
        bool validRcon;
        bool validGamespy;

        try
        {
            using var rconClient = new RconClient(IPAddress.Parse(server.IpAddress), server.RconPort, server.RconPassword, logger);
            var rconResponse = await rconClient.SendAsync("iga listAdmins");
            validRcon = !string.IsNullOrWhiteSpace(rconResponse);
        }
        catch (Exception)
        {
            validRcon = false;
        }

        try
        {
            var gamespyResponse = await new Gamespy3Service().QueryServerAsync(IPAddress.Parse(server.IpAddress), server.QueryPort);
            validGamespy = !string.IsNullOrWhiteSpace(gamespyResponse.Name);
        }
        catch (Exception)
        {
            validGamespy = false;
        }

        return
        [
            new TestServerResult("RCON", validRcon),
            new TestServerResult("Gamespy query", validGamespy)
        ];
    }

    private static async Task<Results<Ok<ChatResponse>, NotFound>> SendChatAsync(string serverId, ChatRequest request, ClaimsPrincipal user, IGameServerRegistry registry)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Message);
        return TypedResults.Ok(new ChatResponse(await module.HandleAdminChatAsync(GetUserId(user), request.Message)));
    }

    private static async Task<Results<Ok<RconResponse>, NotFound>> SendRconAsync(string serverId, CommandRequest request, IGameServerRegistry registry)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Command);
        return TypedResults.Ok(new RconResponse(request.Command, await module.HandleRconCommandAsync(request.Command)));
    }

    private static async Task<Results<NoContent, NotFound>> SendCustomCommandAsync(string serverId, CommandRequest request, ClaimsPrincipal user, IGameServerRegistry registry)
    {
        if (GetWebModule(registry, serverId) is not { } module)
            return TypedResults.NotFound();

        Activity.Current?.SetTag("bf2wa.server-id", serverId);
        Activity.Current?.SetTag("bf2wa.custom-command", request.Command);
        await module.HandleCustomCommandAsync(GetUserId(user), request.Command);
        return TypedResults.NoContent();
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

    private static WebModule? GetWebModule(IGameServerRegistry registry, string serverId) =>
        registry.GetGameServer(serverId)?.ModManager.GetModule<WebModule>();

    private static string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
