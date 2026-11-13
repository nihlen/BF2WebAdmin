using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Services;
using BF2WebAdmin.Server.StreamBot;
using Discord;
using Microsoft.Extensions.Options;
using DiscordConfig = BF2WebAdmin.Server.Configuration.Models.DiscordConfig;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

namespace BF2WebAdmin.Server.Modules.DiscordBot;

/// <summary>
/// The server's Discord bot, when its settings have a token: the game in the admin channels (chat, joins and leaves,
/// kills, map changes, the connection), the bot's status, .leave and .admin to the channels, and the stream bot (.stream,
/// and with each 2v2 match). The bot is a <see cref="DiscordConnection"/>; what people write to it,
/// <see cref="DiscordCommandHandler"/>.
/// </summary>
// TODO: .need <players> command?
public class DiscordModule : BaseModule, IAsyncDisposable,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleEventAsync<ChatMessageEvent>,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<PlayerJoinEvent>,
    IHandleEventAsync<PlayerSpawnEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleEventAsync<PlayerDeathEvent>,
    IHandleEventAsync<MatchStartEvent>,
    IHandleEventAsync<MatchEndEvent>,
    IHandleEventAsync<GameStreamStartedEvent>,
    IHandleEventAsync<GameStreamStoppedEvent>,
    IHandleCommandAsync<LeaveCommand>,
    IHandleCommandAsync<CallAdminCommand>,
    IHandleCommandAsync<StartStreamCommand>,
    IHandleCommandAsync<StopStreamCommand>
{
    // The auth players of commands from Discord (ModManager adds them)
    public const string DiscordBotHashGod = "DiscordBotHashGod";
    public const string DiscordBotHashSuperAdmin = "DiscordBotHashSuperAdmin";
    public const string DiscordBotHashAdmin = "DiscordBotHashAdmin";

    private readonly IGameStreamService _gameStreamService;
    private readonly DiscordConnection? _connection;
    private readonly IDictionary<int, bool> _newPlayers = new Dictionary<int, bool>();

    public DiscordModule(IGameServer server, IGameStreamService gameStreamService, ITaskRunner taskRunner, IServerBanRepository banRepository, IOptions<DiscordConfig> discordConfig,
        ILogger<DiscordModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameStreamService = gameStreamService;

        if (string.IsNullOrWhiteSpace(server.ServerInfo.DiscordBot?.Token))
            return;

        var commands = new DiscordCommandHandler(server, new ServerAdmin(server, banRepository), discordConfig.Value);
        _connection = new DiscordConnection(server, taskRunner, discordConfig.Value, logger, ModuleCancellationToken,
            message => commands.HandleAsync(message, _connection!));
        _connection.Start();
    }

    /// <summary>Stops and disposes the bot, so a reloaded module (or a removed server) leaves no second bot running its commands.</summary>
    public ValueTask DisposeAsync() => _connection?.DisposeAsync() ?? ValueTask.CompletedTask;

    // Used to prevent FakeGameServer spam when fast forwarding
    public static bool IsEnabled = true;

    /// <summary>Player text without Discord markdown and mentions.</summary>
    public static string Sanitize(string text) => DiscordText.Sanitize(text);

    public Task SendTextMessageToChannelsAsync(string text, bool debug = false)
    {
        if (IsEnabled)
            _connection?.SendToAdminChannels(text, debug);
        return Task.CompletedTask;
    }

    public Task SendEmbedMessageToChannelsAsync(Embed embed, bool debug = false)
    {
        if (IsEnabled)
            _connection?.SendToMatchResultChannels(embed, debug);
        return Task.CompletedTask;
    }

    private void UpdateStatus() => _connection?.UpdateStatus();

    public async ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        await SendTextMessageToChannelsAsync($"{(e.SocketState == SocketState.Connected ? ":green_square:` Connected to" : ":red_square:` Disconnected from")} {GameServer.Name}`");
        UpdateStatus();
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        if (e.Message.Type == MessageType.Player)
        {
            var teamFlag = DiscordText.TeamFlag(e.Message.Player.Team.Name);
            await SendTextMessageToChannelsAsync($"{teamFlag} `<{e.Message.Channel}> {e.Message.Player.DisplayName}: {Sanitize(e.Message.Text)}`");
        }
        else
        {
            await SendTextMessageToChannelsAsync($":globe_with_meridians: `<Server> {e.Message.Text}`");
        }
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        UpdateStatus();
        await SendTextMessageToChannelsAsync($"`Map changed to {e.Map.Name}`");
    }

    public async ValueTask HandleAsync(PlayerJoinEvent e)
    {
        UpdateStatus();

        _newPlayers.TryAdd(e.Player.Index, true);
        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} is connecting`");
    }

    public async ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (!_newPlayers.Remove(e.Player.Index))
            return;

        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} joined (`:flag_{e.Player.Country?.Code?.ToLower()}:`{e.Player.Country.Code})`");
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        UpdateStatus();

        _newPlayers.Remove(e.Player.Index);
        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} disconnected`");
    }

    public async ValueTask HandleAsync(PlayerKillEvent e)
    {
        if (GameServer.State == GameState.Playing && GameServer.Players.Count() >= 4)
        {
            var weaponName = DiscordText.WeaponName(e.Weapon);
            var weaponText = e.Attacker?.Team.Id == e.Victim?.Team.Id ? "TK: " + weaponName : weaponName;
            await SendTextMessageToChannelsAsync($"`{Sanitize(e.Attacker?.DisplayName)} [{weaponText}] {Sanitize(e.Victim?.DisplayName)}`");
        }
    }

    public async ValueTask HandleAsync(PlayerDeathEvent e)
    {
        if (GameServer.State == GameState.Playing && GameServer.Players.Count() >= 4 && !e.IsSuicide)
        {
            await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} died`", debug: true);
        }
    }

    public ValueTask HandleAsync(LeaveCommand command)
    {
        if (_connection is not { HasAdminChannels: true })
        {
            Logger.LogWarning("No admin channel channels found");
            return ValueTask.CompletedTask;
        }

        if (GameServer.Players.Count() < 4)
            return ValueTask.CompletedTask;

        if (command.Minutes < 1 || command.Minutes > 120)
        {
            GameServer.GameWriter.SendText($"{command.Minutes} minutes?! go touch grass ({command.Message.Player.DisplayName})");
            return ValueTask.CompletedTask;
        }

        var isCommandCooldown = DateTime.UtcNow - command.Message.Player.LastLeaveNotification < TimeSpan.FromMinutes(5);
        if (isCommandCooldown)
            return ValueTask.CompletedTask;

        command.Message.Player.LastLeaveNotification = DateTime.UtcNow;

        GameServer.GameWriter.SendText($"{command.Message.Player.DisplayName} is leaving in {command.Minutes} minutes - Discord notification sent");
        _connection.SendToNotificationChannels($"`{command.Message.Player.DisplayName}` is leaving in {command.Minutes} minutes");
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(CallAdminCommand command)
    {
        var isCommandCooldown = DateTime.UtcNow - command.Message.Player.LastAdminCall < TimeSpan.FromMinutes(30);
        if (isCommandCooldown)
            return;

        command.Message.Player.LastAdminCall = DateTime.UtcNow;

        await SendTextMessageToChannelsAsync($"@everyone Admin requested by {Sanitize(command.Message.Player.Name)}: {Sanitize(command.Reason)}");
        GameServer.GameWriter.SendText($"{command.Message.Player.Name} called an admin");
    }

    public ValueTask HandleAsync(GameStreamStartedEvent e)
    {
        if (_connection != null)
            _connection.StreamUrl = e.StreamUrl;

        GameServer.GameWriter.SendText($"Stream has started at {e.StreamUrl}");
        UpdateStatus();
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GameStreamStoppedEvent e)
    {
        if (_connection != null)
            _connection.StreamUrl = null;

        GameServer.GameWriter.SendText("Stream has been stopped");
        UpdateStatus();
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(StartStreamCommand command)
    {
        var requested = await StartStreamAsync(matchId: null);
        GameServer.GameWriter.SendText(requested ? "Requested stream bot" : "No stream bot is connected");
    }

    public async ValueTask HandleAsync(StopStreamCommand command)
    {
        var requested = await StopStreamAsync(matchId: null);
        GameServer.GameWriter.SendText(requested ? "Requested stream bot stop" : "No stream bot is connected");
    }

    // Each 2v2 match is streamed when a bot is connected
    public async ValueTask HandleAsync(MatchStartEvent e) => await StartStreamAsync(e.Match.Id);

    public async ValueTask HandleAsync(MatchEndEvent e) => await StopStreamAsync(e.Match.Id);

    private Task<bool> StartStreamAsync(Guid? matchId) =>
        _gameStreamService.StartGameStreamAsync(GameServer.Id, GameServer.IpAddress.ToString(), GameServer.GamePort, GameServer.QueryPort, matchId);

    private Task<bool> StopStreamAsync(Guid? matchId) =>
        _gameStreamService.StopGameStreamAsync(GameServer.Id, GameServer.IpAddress.ToString(), GameServer.GamePort, matchId);
}
