using System.Net;
using System.Threading.Channels;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>Takes player joins for dbo.JoinHistory without waiting for the database.</summary>
public interface IJoinHistoryWriter
{
    /// <summary>Queues the row and returns at once; a full queue drops it.</summary>
    void Enqueue(PlayerJoin join);
}

/// <summary>
/// Writes dbo.JoinHistory, the table of player joins (country flags, aliases) that the export API serves.
/// <see cref="AdminStateApplier"/> queues a row on each admin channel PlayerConnect
/// of a human player; this service inserts them one at a time in the background, so the database never holds up the
/// apply loop. A failed insert only logs a warning.
/// </summary>
public sealed class JoinHistoryWriter : BackgroundService, IJoinHistoryWriter
{
    private const int Capacity = 1000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JoinHistoryWriter> _logger;
    private readonly Channel<PlayerJoin> _queue = Channel.CreateBounded<PlayerJoin>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public JoinHistoryWriter(IServiceScopeFactory scopeFactory, ILogger<JoinHistoryWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(PlayerJoin join)
    {
        if (!_queue.Writer.TryWrite(join))
            _logger.LogWarning("JoinHistory: queue full ({Capacity}), dropped the join of '{Name}'", Capacity, join.Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var join in _queue.Reader.ReadAllAsync(stoppingToken))
                await InsertAsync(join);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task InsertAsync(PlayerJoin join)
    {
        try
        {
            // The SQLite repository is scoped (DbContext); the SQL Server one is a singleton
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IJoinHistoryRepository>().InsertAsync(join);
            _logger.LogDebug("JoinHistory: '{Name}' {IpAddress} on {ServerIpAddress}", join.Name, join.IpAddress, join.ServerIpAddress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JoinHistory: could not insert the join of '{Name}' on {ServerIpAddress}", join.Name, join.ServerIpAddress);
        }
    }

    // ---------------------------------------------------------------- mapping

    /// <summary>
    /// The JoinHistory row for a PlayerConnect, or null when the player gets none: bots (AI or created by the bot
    /// channel), players without a usable IP (none, withheld, loopback or unspecified) and players without a CD-key hash
    /// (withheld, or a LAN or test server; the name keys of test servers are never stored).
    /// </summary>
    /// <param name="player">The player of the PlayerConnect event.</param>
    /// <param name="configuredServerIp">The game server's configured IP (Server.IpAddress).</param>
    /// <param name="resolvedServerIp">That address resolved, used when the configured one is a host name.</param>
    /// <param name="time">The time of the event; stored as UTC.</param>
    public static PlayerJoin? CreateJoin(AdminPlayerInfo player, string? configuredServerIp, IPAddress? resolvedServerIp, DateTimeOffset time)
    {
        if (player.IsAi || (player.Flags & AdminPlayerInfo.FlagBotChannel) != 0)
            return null;

        var hash = player.CdKeyHash?.Trim();
        if (string.IsNullOrEmpty(hash) || hash.StartsWith(Player.NameKeyPrefix, StringComparison.Ordinal) || hash.Length > PlayerJoin.GuidLength)
            return null;

        var ip = ParseIpAddress(player.IpAddress);
        if (ip == null || !IsRemote(ip))
            return null;

        var ipText = ip.ToString();
        if (ipText.Length > PlayerJoin.IpAddressLength)
            return null;

        return new PlayerJoin
        {
            Name = Truncate(player.Name.Trim(), PlayerJoin.NameLength),
            Guid = hash,
            IpAddress = ipText,
            ServerIpAddress = ServerIpAddress(configuredServerIp, resolvedServerIp),
            JoinDate = time.UtcDateTime,
        };
    }

    /// <summary>An address with or without a port ("1.2.3.4", "1.2.3.4:5678", "[::1]:5678"), IPv4-mapped IPv6 as IPv4; null if none.</summary>
    public static IPAddress? ParseIpAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !IPEndPoint.TryParse(text.Trim(), out var endPoint))
            return null;

        var address = endPoint.Address;
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool IsRemote(IPAddress ip) =>
        !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.None) &&
        !ip.Equals(IPAddress.IPv6Any) && !ip.Equals(IPAddress.IPv6None);

    /// <summary>The configured IP when it is an address (without a port); else the resolved one; else the configured text.</summary>
    public static string ServerIpAddress(string? configured, IPAddress? resolved)
    {
        if (ParseIpAddress(configured) is { } literal)
            return literal.ToString();

        if (resolved != null && !resolved.Equals(IPAddress.None) && !resolved.Equals(IPAddress.Any))
            return (resolved.IsIPv4MappedToIPv6 ? resolved.MapToIPv4() : resolved).ToString();

        return Truncate(configured?.Trim() ?? "", PlayerJoin.IpAddressLength);
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}
