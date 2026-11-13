using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Logging;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// Every server's base module: players' countries, the join message in game, joins, leaves and the connection state on
/// the Discord webhooks, and the info commands (.time, .pos, .ping). The commands for admins come with it:
/// <see cref="AdminModule"/>, <see cref="ServerControlModule"/> and <see cref="FunModule"/>.
/// </summary>
public class BF2Module : BaseModule,
    IHandleEventAsync<PlayerJoinEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerSpawnEvent>,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleCommand<TimeCommand>,
    IHandleCommand<PositionCommand>,
    IHandleCommand<PositionSelfCommand>,
    IHandleCommand<PingCommand>,
    IHandleCommand<PingSelfCommand>
{
    private readonly ICountryResolver _countryResolver;
    private readonly IChatLogger _chatLogger;

    // Players who joined and have not spawned yet: the join message in game waits for the spawn
    private readonly IDictionary<int, bool> _newPlayers = new Dictionary<int, bool>();

    public BF2Module(IGameServer server, ICountryResolver countryResolver, IChatLogger chatLogger, ILogger<BF2Module> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _countryResolver = countryResolver;
        _chatLogger = chatLogger;
    }

    // No join messages for the players already there when the server (re)connects
    private bool IsReconnecting => DateTime.UtcNow - GameServer.StartTime <= TimeSpan.FromMinutes(1);

    public async ValueTask HandleAsync(PlayerJoinEvent e)
    {
        _newPlayers.TryAdd(e.Player.Index, true);
        var countryResponse = _countryResolver.GetCountryResponse(e.Player.IpAddress.ToString());
        e.Player.Country.Code = countryResponse?.Country?.IsoCode;
        e.Player.Country.Name = countryResponse?.Country?.Name;

        if (!IsReconnecting)
        {
            await _chatLogger.SendAsync(
                $"`{e.Player.DisplayName} ({e.Player.Country.Code}) joined {GameServer.Name} ({GameServer.Players.Count()}/{GameServer.MaxPlayers})`",
                GameServer.ModManager.ServerSettings?.ServerGroup,
                "player"
            );
        }
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _newPlayers.Remove(e.Player.Index);
        await _chatLogger.SendAsync(
            $"`{e.Player.DisplayName} left {GameServer.Name} ({GameServer.Players.Count()}/{GameServer.MaxPlayers})`",
            GameServer.ModManager.ServerSettings?.ServerGroup,
            "player"
        );
    }

    public ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (!_newPlayers.Remove(e.Player.Index))
            return ValueTask.CompletedTask;

        // TODO: send join message to all servers in group?
        if (!IsReconnecting)
            GameServer.GameWriter.SendText($"{e.Player.DisplayName} ({e.Player.Country.Code}) joined");

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        var icon = e.SocketState == SocketState.Connected ? ":green_square:" : ":red_square:";
        await _chatLogger.SendAsync($"{icon} `{GameServer.Name} => {e.SocketState} ({GameServer.Id})`", GameServer.ModManager.ServerSettings?.ServerGroup, "status");
    }

    public void Handle(TimeCommand command)
    {
        var offsetHours = TimeZoneInfo.Local.BaseUtcOffset.Hours;
        var timeZone = offsetHours == 0 ? "UTC" : offsetHours > 0 ? $"UTC +{offsetHours}" : $"UTC -{offsetHours}";
        GameServer.GameWriter.SendText($"Time: {DateTime.Now:HH:mm} ({timeZone})");
    }

    public void Handle(PositionCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendText($"Position: {player.Position}, Rotation: {player.Rotation}");
    }

    public void Handle(PositionSelfCommand command)
    {
        var player = command.Message.Player;
        GameServer.GameWriter.SendText($"Position: {player.Position}, Rotation: {player.Rotation}");
    }

    public void Handle(PingCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        PrintPingDeviation(player);
    }

    public void Handle(PingSelfCommand command)
    {
        PrintPingDeviation(command.Message.Player);
    }

    private void PrintPingDeviation(Player player)
    {
        var count = player.PingHistory.Sum(entry => entry.Value);

        // Average
        var averagePing = player.PingHistory.Sum(entry => entry.Key * entry.Value) / count;

        // Median
        var medianPing = player.Score.Ping;
        var temp = 0;
        foreach (var entry in player.PingHistory)
        {
            temp += entry.Value;
            if (temp >= count)
            {
                medianPing = entry.Key;
                break;
            }
        }

        GameServer.GameWriter.SendText($"{player.DisplayName} ping: avg {averagePing}, median {medianPing}, jitter +/- ?");
    }
}
