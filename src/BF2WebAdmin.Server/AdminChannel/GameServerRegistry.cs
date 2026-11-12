using System.Collections.Concurrent;
using System.Net;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Communication;
using Microsoft.Extensions.Caching.Memory;

namespace BF2WebAdmin.Server.AdminChannel;

public interface IGameServerRegistry
{
    IGameServer? GetGameServer(string serverId);
    IEnumerable<IGameServer> GetGameServers(string? serverGroup = null);
    Task<IEnumerable<ServerInfo>> GetDisconnectedServersAsync();
    ValueTask<ServerInfo?> FindServerInfoAsync(string serverId);
    Task AddOrUpdateServerAsync(Data.Entities.Server server);
    Task RemoveServerAsync(string serverId);
}

/// <summary>
/// The configured game servers (the Server table, plus ServerSettings:GameServers) and the
/// <see cref="GameServer"/> of every server that has connected. A server is identified by its
/// configured id (Server.ServerId), which is the module's adminServerId; its source address does not matter.
/// </summary>
public class GameServerRegistry : IGameServerRegistry
{
    private const string CacheKey = nameof(GameServerRegistry) + "." + nameof(GetServerInfoAsync);

    private readonly IEnumerable<ServerInfo>? _serverInfoFromConfig;
    private readonly IServiceProvider _globalServices;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameServerRegistry> _logger;
    private readonly ConcurrentDictionary<string, GameServer> _servers = new();
    private readonly SemaphoreSlim _createLock = new(1);
    private readonly CancellationTokenSource _cts = new();

    public GameServerRegistry(IEnumerable<ServerInfo>? serverInfoFromConfig, IServiceProvider globalServices)
    {
        _serverInfoFromConfig = serverInfoFromConfig;
        _globalServices = globalServices;
        _cache = globalServices.GetRequiredService<IMemoryCache>();
        _logger = globalServices.GetRequiredService<ILogger<GameServerRegistry>>();

        // TODO: Move to BF2WebAdminMeter.cs? How to access servers?
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa.server.connected.count", () => _servers.Values.Count(s => s.SocketState == SocketState.Connected), description: "Connected game servers");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa.player.count", () => _servers.Values.Sum(s => s.Players.Count()), description: "Players on all servers");
    }

    public IGameServer? GetGameServer(string serverId) => _servers.TryGetValue(serverId, out var server) ? server : null;

    public IEnumerable<IGameServer> GetGameServers(string? serverGroup = null) =>
        _servers.Values.Where(s => serverGroup is null || s.ModManager?.ServerSettings?.ServerGroup == serverGroup).ToList();

    public async Task<IEnumerable<ServerInfo>> GetDisconnectedServersAsync() =>
        (await GetServerInfoAsync()).Where(si => !_servers.ContainsKey(si.ServerId));

    public async ValueTask<IEnumerable<ServerInfo>> GetServerInfoAsync(bool refresh = false)
    {
        if (refresh)
            _cache.Remove(CacheKey);

        return await _cache.GetOrCreateAsync<IEnumerable<ServerInfo>>(CacheKey, async entry =>
        {
            using var serviceScope = _globalServices.CreateScope();
            var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();

            // Combine server info from config and DB
            entry.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5);

            var result = new List<ServerInfo>();
            var servers = await serverRepository.GetServersAsync();
            result.AddRange(servers.Select(s => new ServerInfo
            {
                ServerId = s.ServerId,
                IpAddress = s.IpAddress,
                GamePort = s.GamePort,
                QueryPort = s.QueryPort,
                ServerGroup = s.ServerGroup,
                AdminKey = s.AdminKey,
                IsTestServer = s.IsTestServer,
                DiscordBot = new ServerInfo.DiscordBotConfig
                {
                    Token = s.DiscordBotToken,
                    AdminChannel = s.DiscordAdminChannel,
                    NotificationChannel = s.DiscordNotificationChannel,
                    MatchResultChannel = s.DiscordMatchResultChannel
                }
            }));

            // Prioritize servers from the DB and fill in non-duplicates from app settings
            if (_serverInfoFromConfig?.Any() ?? false)
            {
                result.AddRange(_serverInfoFromConfig.Where(s => result.All(r => r.ServerId != s.ServerId)));
            }

            return result;
        }) ?? [];
    }

    /// <summary>The raw admin key of a configured server, or null (unknown id, no key or a malformed key).</summary>
    public async ValueTask<byte[]?> LookupKeyAsync(string serverId)
    {
        var serverInfo = await FindServerInfoAsync(serverId);
        if (serverInfo == null)
        {
            _logger.LogWarning("Admin channel: unknown server id '{ServerId}'", serverId);
            return null;
        }

        var key = AdminHandshake.ParseKey(serverInfo.AdminKey);
        if (key == null)
            _logger.LogWarning("Admin channel: server '{ServerId}' has no valid AdminKey (32-128 hex digits)", serverId);

        return key;
    }

    /// <summary>A configured server (the Server table or ServerSettings:GameServers), or null.</summary>
    public async ValueTask<ServerInfo?> FindServerInfoAsync(string serverId)
    {
        // A server added to the DB by hand is found without waiting for the cache to expire
        return (await GetServerInfoAsync()).FirstOrDefault(s => s.ServerId == serverId)
               ?? (await GetServerInfoAsync(refresh: true)).FirstOrDefault(s => s.ServerId == serverId);
    }

    /// <summary>Called after a link authenticates: the server's <see cref="GameServer"/>, created on its first link.</summary>
    public async Task<GameServer> GetOrCreateServerAsync(string serverId)
    {
        var serverInfo = await FindServerInfoAsync(serverId) ?? throw new InvalidOperationException($"Server {serverId} is not configured");
        await _createLock.WaitAsync();
        try
        {
            if (_servers.TryGetValue(serverId, out var existing))
            {
                existing.UpdateServerInfo(serverInfo);
                return existing;
            }

            var ipAddress = IPAddress.None;
            try
            {
                if (!string.IsNullOrWhiteSpace(serverInfo.IpAddress))
                    ipAddress = await serverInfo.IpAddress.GetIpAddressAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve {IpAddress} for {ServerId}", serverInfo.IpAddress, serverId);
            }

            _logger.LogInformation("Created new GameServer instance {ServerId}", serverId);
            var server = new GameServer(serverInfo, ipAddress, _globalServices, _cts.Token);
            _servers[serverId] = server;
            return server;
        }
        finally
        {
            _createLock.Release();
        }
    }

    public async Task AddOrUpdateServerAsync(Data.Entities.Server server)
    {
        using var serviceScope = _globalServices.CreateScope();
        var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
        await serverRepository.SetServerAsync(server);
        await HandleServerUpdateAsync(server.ServerId, ServerUpdateType.AddOrUpdate);
    }

    public async Task RemoveServerAsync(string serverId)
    {
        using var serviceScope = _globalServices.CreateScope();
        var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
        await serverRepository.RemoveServerAsync(serverId);
        await HandleServerUpdateAsync(serverId, ServerUpdateType.Remove);
    }

    private async Task HandleServerUpdateAsync(string serverId, ServerUpdateType type)
    {
        _logger.LogDebug("Handling server {UpdateType} for {ServerId}", type, serverId);

        // Clear cached server infos
        _cache.Remove(CacheKey);

        // A changed key or a removed server ends the current link; the module reconnects (and authenticates again)
        if (_servers.TryRemove(serverId, out var server))
        {
            server.Session?.Close($"server settings {type}");
            if (server.ModManager != null)
                await server.ModManager.DisposeAsync();
            server.Retire();
        }
    }
}

public enum ServerUpdateType
{
    AddOrUpdate,
    Remove
}
