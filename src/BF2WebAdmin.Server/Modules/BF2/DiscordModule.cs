using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Threading.Channels;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Services;
using BF2WebAdmin.Shared;
using Discord;
using Discord.WebSocket;
using Nihlen.Common.Telemetry;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

namespace BF2WebAdmin.Server.Modules.BF2;

// TODO: .need <players> command?
public class DiscordModule : BaseModule,
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
    IHandleCommandAsync<StartStreamCommand>,
    IHandleCommandAsync<StopStreamCommand>
{
    public const string DiscordBotHashGod = "DiscordBotHashGod";
    public const string DiscordBotHashSuperAdmin = "DiscordBotHashSuperAdmin";
    public const string DiscordBotHashAdmin = "DiscordBotHashAdmin";

    //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<DiscordModule>();

    private readonly IGameServer _game;
    private readonly IGameStreamService _gameStreamService;
    private readonly ServerInfo.DiscordBotConfig _config;
    private readonly IDictionary<int, bool> _newPlayers = new Dictionary<int, bool>();
    private readonly Channel<(ISocketMessageChannel Channel, string? Text, Embed? Embed)> _discordMessageChannel;
    private IEnumerable<SocketTextChannel> _adminChannels;
    private IEnumerable<SocketTextChannel> _notificationChannels;
    private IEnumerable<SocketTextChannel> _matchResultChannels;
    private DiscordSocketClient _discord;
    private string? _streamUrl;
    private string? _botName;

    // TODO: some different structure
    private static readonly IDictionary<string, string> WeaponNames = new Dictionary<string, string>
    {
        // Vehicles: https://github.com/chrisw1229/bf2-stats/blob/master/webapp/models/vehicles.py
        {"aav_tunguska_gun", "Tunguska"},
        {"aav_tunguska_sa19launcher", "Tunguska"},

        {"aav_type95guns", "Type 95"},
        {"aav_type95_qw2launcher", "Type 95"},

        {"ahe_ah1z_hydralauncher", "AH-1Z Hydra"},
        {"ahe_ah1z_cogunner_hellfirelaunchertv", "AH-1Z TV"},
        {"ahe_ah1z_gun", "AH-1Z MG"},

        {"ahe_z10_s8launcher", "Z-10 Hydra"},
        {"ahe_z10_hj8launcher_tv", "Z-10 TV"},
        {"ahe_z10_gun", "Z-10 MG"},

        {"ahe_havoc_s8launcher", "Mi-28 Hydra"},
        {"ahe_havoc_atakalauncher_tv", "Mi-28 Hydra"},
        {"ahe_havoc_gun", "Mi-28 Hydra"},

        {"air_a10_us_bomblauncher", "A-10"},

        {"air_f35b_autocannon", "F-35B"},
        {"air_f35b_sidewinderlauncher", "F-35B"},
        {"air_f35b_bomblauncher", "F-35B"},

        {"air_j10_cannon", "J-10"},
        {"air_j10_archerlauncher", "J-10"},

        {"air_su30mkk_30mmcannon", "Su-30MKK"},
        {"air_su30mkk_archerlauncher", "Su-30MKK"},
        {"air_su30mkk_kedgelauncher_laser", "Su-30MKK"},

        {"air_su39_canon", "Su-39"},

        {"apc_btr90__barrel", "BTR-90"},
        {"apc_btr90_hj8launcher", "BTR-90"},
        //{"firingport_ak", "BTR-90"}, // duplicate key

        {"apc_wz551_barrel", "WZ551"},
        {"apc_wz551_hj8launcher", "WZ551"},
        //{"firingport_ak", "WZ551"}, // duplicate key

        {"ars_d30_barrel", "D-30"},

        {"ats_hj8_launcher", "HJ-8"},

        {"ats_tow_launcher", "BGM-71"},

        //{"uslmg_m249saw_stationary", "RIB"}, // duplicate key

        {"che_wz11_canons", "WZ-11"},

        //{"chlmg_type95_stationary", "Bipod"}, // duplicate key

        //{"chhmg_type85", "Z-8"}, // duplicate key

        //{"hmg_m2hb", "M2"}, // duplicate key

        {"igla_djigit_launcher", "Igla"},

        //{"hmg_m2hb", "FAAV"}, // duplicate key
        //{"uslmg_m249saw_stationary", "FAAV"}, // duplicate key

        //{"chhmg_kord", "Paratrooper"},
        //{"rulmg_rpk74_stationary", "Paratrooper"},

        //{"chhmg_type85", "Nanjing"}, // duplicate key

        //{"chhmg_type85", "Paratrooper"}, // duplicate key
        //{"chlmg_type95_stationary", "Paratrooper"}, // duplicate key

        //{"chhmg_kord", "GAZ-3937"},

        //{"rulmg_rpk74_stationary", "Bipod"},

        {"ruair_mig29_30mmcannon", "MiG-29"},
        {"ruair_archerlauncher", "MiG-29"},
        {"ruair_mig29_bomblauncher_1", "MiG-29"},

        {"ruair_su34_30mmcannon", "Su-34"},
        {"ruair_su34_archerlauncher", "Su-34"},
        {"ruair_su34_250kgbomblauncher", "Su-34"},
        {"ruair_su34_kedgelaunchertv", "Su-34"},

        {"rutnk_t90_barrel", "T-90"},
        {"coaxial_mg_mec", "T-90"},
        //{"chhmg_kord", "T-90"},

        {"she_ec635_cannons", "EC635"},

        {"she_littlebird_miniguns", "MH-6"},

        {"tnk_type98_barrel", "Type 98"},
        {"coaxial_mg_china", "Type 98"},
        //{"chhmg_type85", "Type 98"}, // duplicate key

        //{"uslmg_m249saw_stationary", "Bipod"}, // duplicate key

        {"usaas_stinger_launcher", "FIM-92"},

        {"usaav_m6_barrel", "M6"},
        {"usaav_m6_stinger_launcher", "M6"},

        {"f18_autocannon", "F/A-18"},
        {"f18_sidewinderlauncher", "F/A-18"},
        {"usair_f18_bomblauncher", "F/A-18"},

        {"usair_f15_autocannon", "F-15"},
        {"usair_f15_sidewinderlauncher", "F-15"},
        {"usair_f15_mavericklauncherlaser", "F-15"},
        {"usair_f15_250kgbomblauncher", "F-15"},

        {"usapc_lav25_barrel", "LAV-25"},
        {"usapc_lav25_towlauncher", "LAV-25"},
        {"firingport_m16", "LAV-25"},

        {"usart_lw155_barrel", "M777"},

        //{"hmg_m2hb", "HMMWV"}, // duplicate key

        {"hmg_m134_gun", "UH-60"},

        {"ustnk_m1a2_barrel", "M1A2"},
        {"coaxial_browning", "M1A2"},
        //{"hmg_m2hb", "M1A2"}, // duplicate key

        {"eurofighter_autocannon", "Eurofighter"},
        {"eurofighter_missiles", "Eurofighter"},
        {"eurofighter_bomb_launcher", "Eurofighter"},

        {"fantan_autocannon", "Q-5"},
        {"xpak2_fantan_bomblauncher", "Q-5"},

        {"xpak2_tiger_missiles", "EC665"},
        {"tnk_c2_barrel", "C2"}
    };

    private const string HelpText = @"```# Show player scores
/score

# Use custom commands
.<command>

# Change to a specific map
!map:m <map> <gametype> <size> (e.g. !m daqing_2_v_2 gpm_cq 16)

# Kick a player from the server with a message
!kick:k <playerid> ""<reason>""

# Ban a player from the server with a message
!ban:b <playerid> ""<reason>""

# Ban a player for a specified timer period from the server with a message
!banby <playerid> <bannedby> <period> ""<reason>""

# Display the current banlist
!banlist

# Clear the current banlist
!clearbans

# Unban a player with an optional reason
!unban <address|cdkey> ""<reason>""

# List the players on the server
!list

# Print a list of players and their profileids
!profileids

# Execute a console command
!exec```";

    public DiscordModule(IGameServer server, IGameStreamService gameStreamService, ILogger<DiscordModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _game = server;
        _gameStreamService = gameStreamService;

        var discordBot = server.ServerInfo.DiscordBot;
        if (discordBot == null)
            return;

        _config = discordBot;

        _discordMessageChannel = Channel.CreateUnbounded<(ISocketMessageChannel Channel, string? Text, Embed? Embed)>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = true
        });

        if (_discordMessageChannel.Reader.CanCount)
        {
            var tagList = new TagList { { "serverid", GameServer.Id } };
            _ = Telemetry.Meter.CreateObservableGauge("bf2wa.discord.queue.count", () => new Measurement<int>(_discordMessageChannel.Reader.Count, tagList), description: "Length of the discord channel queue");
        }

        // TODO: Start async in an event callback - proper?
        RunBackgroundTask("Discord Bot", StartBotAsync);
    }

    private async Task StartBotAsync()
    {
        _discord = new DiscordSocketClient();

        SetupDiscordEvents();

        await _discord.LoginAsync(TokenType.Bot, _config.Token);
        await _discord.StartAsync();

        // Send all queued Discord messages
        await foreach (var (channel, text, embed) in _discordMessageChannel.Reader.ReadAllAsync(ModuleCancellationToken))
        {
            try
            {
                await channel.SendMessageAsync(text: text, embed: embed);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send Discord message {Message}", text);
            }
        }
    }

    private void SetupDiscordEvents()
    {
        _discord.Log += message =>
        {
            if (message.Exception != null)
            {
                if (message.Exception is GatewayReconnectException)
                {
                    // Ignore regular reconnects
                }
                else
                {
                    Logger.LogError(message.Exception, "DISCORD: {message}", message.Message);
                }
            }
            else if (message.Severity == LogSeverity.Warning)
            {
                Logger.LogWarning("DISCORD: {Message}", message.Message);
            }
            else
            {
                Logger.LogDebug("DISCORD: {Message}", message.Message);
            }

            return Task.CompletedTask;
        };

        _discord.Ready += async () =>
        {
            _adminChannels = _discord.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == _config.AdminChannel));
            _notificationChannels = _discord.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == _config.NotificationChannel));
            _matchResultChannels = _discord.Guilds.SelectMany(g => g.TextChannels.Where(c => c.Name == _config.MatchResultChannel));

            await UpdateActivityNameAsync();

            await SendTextMessageToChannelsAsync($"{(_game.SocketState == SocketState.Connected ? ":green_square:` Connected to" : ":red_square:` Disconnected from")} {_game.Name}`");
        };

        _discord.MessageReceived += async message =>
        {
            if (message.Author.IsBot)
                return;
            if (message.Channel.Name != _config.AdminChannel)
                return;

            if (message.Content.Length > 4 && message.Content[1..5] == "help")
            {
                _discordMessageChannel.Writer.TryWrite((message.Channel, HelpText, null));
            }
            else if (message.Content.StartsWith("!"))
            {
                var command = GetRconCommand(message.Content);
                var response = await SendRconCommandAsync(command);
                _discordMessageChannel.Writer.TryWrite((message.Channel, $"```{GetObfuscatedResponse(response)}```", null));
            }
            else if (message.Content.StartsWith("."))
            {
                await _game.ModManager.HandleFakeChatMessageAsync(new Message
                {
                    Channel = ChatChannel.Global,
                    Type = MessageType.Player,
                    Player = new Player
                    {
                        Index = -1,
                        Name = (message.Author as SocketGuildUser)?.Nickname ?? message.Author.Username,
                        Hash = message.Author.Id == 135810577292328960 ? DiscordBotHashGod : DiscordBotHashAdmin
                    },
                    Text = message.Content
                });
            }
            else if (message.Content.StartsWith("/"))
            {
                const int maxLength = 19;
                if (message.Content.ToLower() == "/score")
                {
                    var sb = new StringBuilder(100);
                    foreach (var team in _game.Teams.OrderByDescending(t => t.Id))
                    {
                        sb.Append("#  ");
                        sb.Append(team.Name?.PadRight(3));
                        sb.Append(string.Empty.PadRight(maxLength));
                        sb.Append("🏆".PadLeft(5));
                        sb.Append("🛠".PadLeft(5));
                        sb.Append("🎯".PadLeft(5));
                        sb.Append("💀".PadLeft(5));
                        sb.Append("🌐".PadLeft(5));
                        sb.AppendLine();

                        foreach (var player in _game.Players.OrderByDescending(p => p.Score.Total).Where(p => p.Team.Id == team.Id))
                        {
                            var name = player.DisplayName.Length <= maxLength ? player.DisplayName : player.DisplayName.Substring(0, maxLength);
                            sb.Append(player.Index.ToString().PadRight(3));
                            sb.Append((player.Country.Code ?? string.Empty).PadRight(3));
                            sb.Append(name.PadRight(maxLength));
                            sb.Append(player.Score.Total.ToString().PadLeft(5));
                            sb.Append(player.Score.Team.ToString().PadLeft(5));
                            sb.Append(player.Score.Kills.ToString().PadLeft(5));
                            sb.Append(player.Score.Deaths.ToString().PadLeft(5));
                            sb.Append(player.Score.Ping.ToString().PadLeft(5));
                            sb.AppendLine();
                        }

                        sb.AppendLine();
                    }

                    _discordMessageChannel.Writer.TryWrite((message.Channel, $"```{sb}```", null));
                }
            }
            else
            {
                var name = (message.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;
                _game.GameWriter.SendText($"[§C1001{name}§C1001] {message.Content}", false, false);
            }
        };
    }

    private static string GetWeaponName(string weapon)
    {
        var key = weapon?.ToLower() ?? string.Empty;
        return WeaponNames.ContainsKey(key) ? WeaponNames[key] : weapon;
    }

    private static string GetTeamFlag(string teamName)
    {
        switch (teamName)
        {
            case "US":
                return ":flag_us:";
            case "CH":
                return ":flag_cn:";
            case "MEC":
                return ":orange_square:";
            case "EU":
                return ":flag_eu:";
            default:
                return ":black_medium_square:";
        }
    }

    private async Task UpdateActivityNameAsync()
    {
        RunBackgroundTask("Update Discord bot activity name", async () =>
        {
            if (_game.SocketState == SocketState.Disconnected)
            {
                if (_discord.Status != UserStatus.DoNotDisturb)
                    await _discord.SetStatusAsync(UserStatus.DoNotDisturb);

                await _discord.SetActivityAsync(new Game("Disconnected"));
            }
            else
            {
                if (_discord.Status != UserStatus.Online)
                    await _discord.SetStatusAsync(UserStatus.Online);

                var name = $"{_game.Players.Count()}/{_game.MaxPlayers} - {_game.Map?.Name ?? "Unknown"}";
                await _discord.SetActivityAsync(_streamUrl is null ? new Game(name) : new StreamingGame(name, _streamUrl));
            }
        });
    }

    // Used to prevent FakeGameServer spam when fast forwarding
    public static bool IsEnabled = true;

    public async Task SendTextMessageToChannelsAsync(string text, bool debug = false)
    {
        // TODO: remove after testing
        if (!IsEnabled) return;

        // TODO: sanitize message, remove ` and other characters
        if (_adminChannels == null)
        {
            Logger.LogWarning("No admin channels found");
            return;
        }

        var channels = debug ? _adminChannels.Where(c => c.Guild.Name.ToLower().Contains("netsky")) : _adminChannels;

        foreach (var channel in channels)
        {
            _discordMessageChannel.Writer.TryWrite((channel, text, null));
        }
    }

    public async Task SendEmbedMessageToChannelsAsync(Embed embed, bool debug = false)
    {
        // TODO: remove after testing
        if (!IsEnabled) return;

        // TODO: sanitize message, remove ` and other characters
        if (_matchResultChannels == null)
        {
            Logger.LogWarning("No match result channels found");
            return;
        }

        var channels = debug ? _matchResultChannels.Where(c => c.Guild.Name.ToLower().Contains("netsky")) : _matchResultChannels;

        foreach (var channel in channels)
        {
            _discordMessageChannel.Writer.TryWrite((channel, null, embed));
        }
    }

    public async ValueTask HandleAsync(LeaveCommand command)
    {
        if (_adminChannels == null)
        {
            Logger.LogWarning("No admin channel channels found");
            return;
        }

        if (_game.Players.Count() < 4)
            return;

        if (command.Minutes < 1 || command.Minutes > 120)
        {
            _game.GameWriter.SendText($"{command.Minutes} minutes?! go touch grass ({command.Message.Player.DisplayName})");
            return;
        }

        var isCommandCooldown = DateTime.UtcNow - command.Message.Player.LastLeaveNotification < TimeSpan.FromMinutes(5);
        if (isCommandCooldown)
            return;

        command.Message.Player.LastLeaveNotification = DateTime.UtcNow;

        _game.GameWriter.SendText($"{command.Message.Player.DisplayName} is leaving in {command.Minutes} minutes - Discord notification sent");

        foreach (var channel in _notificationChannels)
        {
            _discordMessageChannel.Writer.TryWrite((channel, $"`{command.Message.Player.DisplayName}` is leaving in {command.Minutes} minutes", null));
        }
    }

    public static string Sanitize(string text)
    {
        if (text == null)
            return string.Empty;

        // Add zero width space to break @ # < : ? \u200B
        var noMarkdown = Format.Sanitize(text);
        var noMentions = noMarkdown.Replace("@", "@\u200B").Replace("#", "#\u200B").Replace("<", "<\u200B");
        return noMentions;
    }

    public async ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        await SendTextMessageToChannelsAsync($"{(e.SocketState == SocketState.Connected ? ":green_square:` Connected to" : ":red_square:` Disconnected from")} {_game.Name}`");
        await UpdateActivityNameAsync();
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        if (e.Message.Type == MessageType.Player)
        {
            var teamFlag = GetTeamFlag(e.Message.Player.Team.Name);
            await SendTextMessageToChannelsAsync($"{teamFlag} `<{e.Message.Channel}> {e.Message.Player.DisplayName}: {Sanitize(e.Message.Text)}`");
        }
        else
        {
            await SendTextMessageToChannelsAsync($":globe_with_meridians: `<Server> {e.Message.Text}`");
        }
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        await UpdateActivityNameAsync();
        await SendTextMessageToChannelsAsync($"`Map changed to {e.Map.Name}`");
    }

    public async ValueTask HandleAsync(PlayerJoinEvent e)
    {
        await UpdateActivityNameAsync();

        _newPlayers.TryAdd(e.Player.Index, true);
        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} is connecting`");
    }

    public async ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (!_newPlayers.ContainsKey(e.Player.Index))
            return;

        _newPlayers.Remove(e.Player.Index);
        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} joined (`:flag_{e.Player.Country?.Code?.ToLower()}:`{e.Player.Country.Code})`");
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        await UpdateActivityNameAsync();

        _newPlayers.Remove(e.Player.Index);
        await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} disconnected`");
    }

    public async ValueTask HandleAsync(PlayerKillEvent e)
    {
        if (_game.State == GameState.Playing && _game.Players.Count() >= 4)
        {
            var weaponName = GetWeaponName(e.Weapon);
            var weaponText = e.Attacker?.Team.Id == e.Victim?.Team.Id ? "TK: " + weaponName : weaponName;
            await SendTextMessageToChannelsAsync($"`{Sanitize(e.Attacker?.DisplayName)} [{weaponText}] {Sanitize(e.Victim?.DisplayName)}`");
        }
    }

    public async ValueTask HandleAsync(PlayerDeathEvent e)
    {
        if (_game.State == GameState.Playing && _game.Players.Count() >= 4 && !e.IsSuicide)
        {
            await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} died`", debug: true);
        }
    }

    public async ValueTask HandleAsync(GameStreamStartedEvent e)
    {
        _streamUrl = e.StreamUrl;
        _botName = e.BotName;

        _game.GameWriter.SendText($"Stream has started at {_streamUrl}");

        await UpdateActivityNameAsync();
    }

    public async ValueTask HandleAsync(GameStreamStoppedEvent e)
    {
        _streamUrl = null;
        _botName = null;

        _game.GameWriter.SendText("Stream has been stopped");

        await UpdateActivityNameAsync();
    }

    public async ValueTask HandleAsync(StartStreamCommand command)
    {
        await StartStreamAsync();

        _game.GameWriter.SendText("Requested stream bot");
    }

    public async ValueTask HandleAsync(StopStreamCommand command)
    {
        await StopStreamAsync();

        _game.GameWriter.SendText("Requested stream bot stop");
    }

    public async ValueTask HandleAsync(MatchStartEvent e)
    {
        await StartStreamAsync();
    }

    public async ValueTask HandleAsync(MatchEndEvent e)
    {
        await StopStreamAsync();
    }

    private async Task StartStreamAsync()
    {
        await _gameStreamService.StartGameStreamAsync(GameServer.IpAddress.ToString(), GameServer.GamePort, GameServer.QueryPort);
    }

    private async Task StopStreamAsync()
    {
        await _gameStreamService.StopGameStreamAsync(GameServer.IpAddress.ToString(), GameServer.GamePort);
    }
}