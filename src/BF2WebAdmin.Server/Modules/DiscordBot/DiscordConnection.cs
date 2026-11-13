using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Communication;
using Discord;
using Discord.WebSocket;
using DiscordConfig = BF2WebAdmin.Server.Configuration.Models.DiscordConfig;

namespace BF2WebAdmin.Server.Modules.DiscordBot;

/// <summary>
/// A server's Discord bot (its token is in the server's settings): the client, its channels (admin, notification, match
/// results; by name in every guild it is in), its status (players and map, or the stream) and one queue for everything it
/// sends, so a slow Discord never holds up the game. The client and the sender run on the thread pool, for the module's
/// life; what people write goes to <c>onMessage</c>, on Discord's threads.
/// </summary>
public sealed class DiscordConnection : IAsyncDisposable
{
    private readonly IGameServer _game;
    private readonly ITaskRunner _taskRunner;
    private readonly DiscordConfig _discordConfig;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly Func<SocketMessage, Task> _onMessage;

    // Written from the module thread and Discord's threads; the sender must not run on the writer's thread
    private readonly Channel<(ISocketMessageChannel Channel, string? Text, Embed? Embed)> _queue =
        Channel.CreateUnbounded<(ISocketMessageChannel Channel, string? Text, Embed? Embed)>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false
        });

    private DiscordSocketClient? _discord;
    private IEnumerable<SocketTextChannel>? _adminChannels;
    private IEnumerable<SocketTextChannel>? _notificationChannels;
    private IEnumerable<SocketTextChannel>? _matchResultChannels;
    private volatile bool _disposed;

    public DiscordConnection(IGameServer game, ITaskRunner taskRunner, DiscordConfig discordConfig, ILogger logger, CancellationToken cancellationToken,
        Func<SocketMessage, Task> onMessage)
    {
        _game = game;
        _taskRunner = taskRunner;
        _discordConfig = discordConfig;
        _logger = logger;
        _cancellationToken = cancellationToken;
        _onMessage = onMessage;

        // The meter is static: the gauge holds the connection weakly and reports nothing once it is disposed
        var tagList = new TagList { { "serverid", game.Id } };
        var connection = new WeakReference<DiscordConnection>(this);
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa.discord.queue.count",
            () => connection.TryGetTarget(out var c) && !c._disposed ? [new Measurement<int>(c._queue.Reader.Count, tagList)] : Array.Empty<Measurement<int>>(),
            description: "Length of the discord channel queue");
    }

    private ServerInfo.DiscordBotConfig Config => _game.ServerInfo.DiscordBot;

    /// <summary>The stream the bot's status links to while the stream bot plays.</summary>
    public string? StreamUrl { get; set; }

    /// <summary>Starts the bot in the background: on the thread pool, not the module thread.</summary>
    public void Start() => _taskRunner.RunBackgroundTask("Discord Bot", () => Task.Run(RunAsync), _cancellationToken);

    private async Task RunAsync()
    {
        _discord = new DiscordSocketClient();
        _discord.Log += LogAsync;
        _discord.Ready += OnReadyAsync;
        _discord.MessageReceived += message => message.Author.IsBot || message.Channel.Name != Config.AdminChannel ? Task.CompletedTask : _onMessage(message);

        await _discord.LoginAsync(TokenType.Bot, Config.Token);
        await _discord.StartAsync();

        if (_disposed)
        {
            // Disposed while logging in
            await _discord.StopAsync();
            return;
        }

        // Send all queued Discord messages
        await foreach (var (channel, text, embed) in _queue.Reader.ReadAllAsync(_cancellationToken))
        {
            try
            {
                await channel.SendMessageAsync(text: text, embed: embed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Discord message {Message}", text);
            }
        }
    }

    private Task LogAsync(LogMessage message)
    {
        if (message.Exception != null)
        {
            // Not the regular reconnects
            if (message.Exception is not GatewayReconnectException)
                _logger.LogError(message.Exception, "DISCORD: {message}", message.Message);
        }
        else if (message.Severity == LogSeverity.Warning)
        {
            _logger.LogWarning("DISCORD: {Message}", message.Message);
        }
        else
        {
            _logger.LogDebug("DISCORD: {Message}", message.Message);
        }

        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        _adminChannels = _discord!.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == Config.AdminChannel));
        _notificationChannels = _discord.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == Config.NotificationChannel));
        _matchResultChannels = _discord.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == Config.MatchResultChannel));

        UpdateStatus();
        SendToAdminChannels($"{(_game.SocketState == SocketState.Connected ? ":green_square:` Connected to" : ":red_square:` Disconnected from")} {_game.Name}`");
        return Task.CompletedTask;
    }

    /// <summary>The bot's status: "Disconnected", or the players and the map (with the stream while there is one).</summary>
    public void UpdateStatus()
    {
        if (_discord is not { } discord)
            return;

        _taskRunner.RunBackgroundTask("Update Discord bot activity name", async () =>
        {
            if (_game.SocketState == SocketState.Disconnected)
            {
                if (discord.Status != UserStatus.DoNotDisturb)
                    await discord.SetStatusAsync(UserStatus.DoNotDisturb);

                await discord.SetActivityAsync(new Game("Disconnected"));
            }
            else
            {
                if (discord.Status != UserStatus.Online)
                    await discord.SetStatusAsync(UserStatus.Online);

                var name = $"{_game.Players.Count()}/{_game.MaxPlayers} - {_game.Map?.Name ?? "Unknown"}";
                await discord.SetActivityAsync(StreamUrl is null ? new Game(name) : new StreamingGame(name, StreamUrl));
            }
        }, _cancellationToken);
    }

    /// <summary>Queues a message for the admin channels; <paramref name="debug"/>: only those of the debug guild.</summary>
    public void SendToAdminChannels(string text, bool debug = false)
    {
        if (_adminChannels == null)
        {
            _logger.LogWarning("No admin channels found");
            return;
        }

        foreach (var channel in debug ? _adminChannels.Where(IsDebugChannel) : _adminChannels)
            _queue.Writer.TryWrite((channel, text, null));
    }

    public void SendToNotificationChannels(string text)
    {
        foreach (var channel in _notificationChannels ?? [])
            _queue.Writer.TryWrite((channel, text, null));
    }

    public void SendToMatchResultChannels(Embed embed, bool debug = false)
    {
        if (_matchResultChannels == null)
        {
            _logger.LogWarning("No match result channels found");
            return;
        }

        foreach (var channel in debug ? _matchResultChannels.Where(IsDebugChannel) : _matchResultChannels)
            _queue.Writer.TryWrite((channel, null, embed));
    }

    /// <summary>Queues an answer to the channel a message came from.</summary>
    public void Reply(ISocketMessageChannel channel, string text) => _queue.Writer.TryWrite((channel, text, null));

    public bool HasAdminChannels => _adminChannels != null;

    private bool IsDebugChannel(SocketTextChannel channel) =>
        channel.Guild.Name.Contains(_discordConfig.DebugGuildName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Stops and disposes the bot, so a reloaded module (or a removed server) leaves no second bot running its commands.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queue.Writer.TryComplete();
        if (_discord is not { } discord)
            return;

        try
        {
            await discord.StopAsync();
            await discord.LogoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping the Discord bot of {ServerId} failed", _game.Id);
        }

        discord.Dispose();
    }
}
