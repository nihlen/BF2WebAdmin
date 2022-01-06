using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using Discord;
using Discord.WebSocket;
using Serilog;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

namespace BF2WebAdmin.Server.Modules.BF2
{
    // TODO: /whois command using IP and guid matching from JoinHistory table
    // TODO: .need <players> command?
    public class DiscordModule : IModule,
        IHandleEventAsync<SocketStateChangedEvent>,
        IHandleEventAsync<ChatMessageEvent>,
        IHandleEventAsync<MapChangedEvent>,
        IHandleEventAsync<PlayerJoinEvent>,
        IHandleEventAsync<PlayerSpawnEvent>,
        IHandleEventAsync<PlayerLeftEvent>,
        IHandleEventAsync<PlayerKillEvent>,
        IHandleEventAsync<PlayerDeathEvent>,
        IHandleCommandAsync<LeaveCommand>
    {
        public const string DiscordBotHashGod = "DiscordBotHashGod";
        public const string DiscordBotHashSuperAdmin = "DiscordBotHashSuperAdmin";
        public const string DiscordBotHashAdmin = "DiscordBotHashAdmin";

        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<DiscordModule>();

        private readonly IGameServer _game;
        private readonly ServerInfo.DiscordBotConfig _config;
        private readonly IDictionary<int, bool> _newPlayers = new Dictionary<int, bool>();
        private readonly Channel<string> _discordMessageChannel;
        private IEnumerable<SocketTextChannel> _adminChannels;
        private IEnumerable<SocketTextChannel> _notificationChannels;
        private IEnumerable<SocketTextChannel> _matchResultChannels;
        private DiscordSocketClient _discord;

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
!map:m <map>

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

        private Task _botTask;

        public DiscordModule(IGameServer game)
        {
            _game = game;
            var discordBot = game.ServerInfo.DiscordBot;
            if (discordBot == null)
                return;

            _config = discordBot;

            // TODO: Start async in an event callback - proper?
            _botTask = Task.Run(StartBotAsync);
        }

        private async Task StartBotAsync()
        {
            try
            {
                _discord = new DiscordSocketClient();

                SetupDiscordEvents();
                SetupGameEvents();

                await _discord.LoginAsync(TokenType.Bot, _config.Token);
                await _discord.StartAsync();

                //await Task.Delay(-1);
            }
            catch (Exception e)
            {
                Log.Error(e, "Discord bot error");
            }
        }

        private async Task<string> SendRconCommandAsync(string command)
        {
            var rcon = new RconClient(_game.IpAddress, _game.ServerInfo.RconPort, _game.ServerInfo.RconPassword);
            return await rcon.SendAsync(command);
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
                        Log.Error(message.Exception, "DISCORD: {message}", message.Message);
                    }
                }
                else if (message.Severity == LogSeverity.Warning)
                {
                    Log.Warning("DISCORD: {message}", message.Message);
                }
                else
                {
                    Log.Debug("DISCORD: {message}", message.Message);
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
                    await message.Channel.SendMessageAsync(HelpText);
                }
                else if (message.Content.StartsWith("!"))
                {
                    var command = GetRconCommand(message.Content);
                    var response = await SendRconCommandAsync(command);
                    await message.Channel.SendMessageAsync($"```{GetObfuscatedResponse(response)}```");
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
                        Text = message.Content,
                        Time = DateTime.UtcNow
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
                                //sb.AppendLine($"{player.Index} {player.DisplayName} {player.Score.Total} {player.Score.Team} {player.Score.Kills} {player.Score.Deaths} {player.Score.Ping}");
                            }

                            sb.AppendLine();
                        }

                        await message.Channel.SendMessageAsync($"```{sb}```");
                    }
                }
                else
                {
                    var name = (message.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;
                    _game.GameWriter.SendText($"[§C1001{name}§C1001] {message.Content}", false, false);
                }
            };

            static string GetRconCommand(string text)
            {
                if (text.StartsWith("!m ")) return "map gpm_cq" + text[2..];
                if (text.StartsWith("!map ")) return "map gpm_cq" + text[4..];
                if (text.StartsWith("!w ")) return "warn" + text[2..];
                if (text.StartsWith("!k ")) return "kick" + text[2..];
                if (text.StartsWith("!b ")) return "ban" + text[2..];
                return text.TrimStart('!');
            }

            static string GetObfuscatedResponse(string text)
            {
                var result = Regex.Replace(
                    text,
                    @"((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
                    "[IP REMOVED]",
                    RegexOptions.Compiled
                );

                return result.Length > 1 ? result : "Empty response";
            }
        }

        private void SetupGameEvents()
        {
            // TODO: Events return Func<Task> instead of Action so they can be safely awaited? test with exceptions in some common event to test locally, had to comment out PlayerKill
            //_game.SocketStateChanged += async state =>
            //{
            //    await SendTextMessageToChannelsAsync($"{(state == SocketState.Connected ? ":green_square:` Connected to" : ":red_square:` Disconnected from")} {_game.Name}`");

            //    await UpdateActivityNameAsync();
            //};

            //_game.ChatMessage += async message =>
            //{
            //    if (message.Type == MessageType.Player)
            //    {
            //        var teamFlag = GetTeamFlag(message.Player.Team.Name);
            //        await SendTextMessageToChannelsAsync($"{teamFlag} `<{message.Channel}> {message.Player.DisplayName}: {Sanitize(message.Text)}`");
            //    }
            //    else
            //    {
            //        await SendTextMessageToChannelsAsync($":globe_with_meridians: `<Server> {message.Text}`");
            //    }
            //};

            //_game.MapChanged += async map =>
            //{
            //    await UpdateActivityNameAsync();

            //    await SendTextMessageToChannelsAsync($"`Map changed to {map.Name}`");
            //};

            //_game.PlayerJoin += async player =>
            //{
            //    using (Profiler.Start("DEBUG PlayerJoin UpdateActivityNameAsync"))
            //    {
            //        await UpdateActivityNameAsync();
            //    }

            //    using (Profiler.Start("DEBUG PlayerJoin SendTextMessageToChannelsAsync"))
            //    {
            //        _newPlayers.TryAdd(player.Index, true);
            //        await SendTextMessageToChannelsAsync($"`{Sanitize(player.DisplayName)} is connecting`");
            //    }
            //};

            //_game.PlayerSpawn += async (player, position, rotation) =>
            //{
            //    if (!_newPlayers.ContainsKey(player.Index))
            //        return;

            //    _newPlayers.Remove(player.Index);
            //    await SendTextMessageToChannelsAsync($"`{Sanitize(player.DisplayName)} joined (`:flag_{player.Country?.Code?.ToLower()}:`{player.Country.Code})`");
            //};

            //_game.PlayerLeft += async player =>
            //{
            //    await UpdateActivityNameAsync();

            //    _newPlayers.Remove(player.Index);
            //    await SendTextMessageToChannelsAsync($"`{Sanitize(player.DisplayName)} disconnected`");
            //};

            //_game.PlayerKill += async (attacker, attackerPosition, victim, victimPosition, weapon) =>
            //{
            //    if (_game.State == GameState.Playing && _game.Players.Count() >= 4)
            //    {
            //        var weaponName = GetWeaponName(weapon);
            //        var weaponText = attacker?.Team.Id == victim?.Team.Id ? "TK: " + weaponName : weaponName;
            //        await SendTextMessageToChannelsAsync($"`{Sanitize(attacker?.DisplayName)} [{weaponText}] {Sanitize(victim?.DisplayName)}`");
            //    }
            //};

            //_game.PlayerDeath += async (player, position, isSuicide) =>
            //{
            //    if (_game.State == GameState.Playing && _game.Players.Count() >= 4 && !isSuicide)
            //    {
            //        await SendTextMessageToChannelsAsync($"`{Sanitize(player.DisplayName)} died`", debug: true);
            //    }
            //};
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
            // TODO: use Threading.Channels and send in a different Task so we don't block here if we are rate limited - instead of Task.Run?
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_game.SocketState == SocketState.Disconnected)
                    {
                        if (_discord.Status != UserStatus.DoNotDisturb)
                            await _discord.SetStatusAsync(UserStatus.DoNotDisturb);

                        await _discord.SetActivityAsync(new Game("Disconnected"));
                        //await _discord.SetActivityAsync(new Game("Disconnected", ActivityType.Playing, ActivityProperties.Spectate, $"IP: {_game.Id} | Servers and maps: https://bf2.nihlen.net/servers"));
                    }
                    else
                    {
                        if (_discord.Status != UserStatus.Online)
                            await _discord.SetStatusAsync(UserStatus.Online);

                        var name = $"{_game.Players.Count()}/{_game.MaxPlayers} - {_game.Map?.Name ?? "Unknown"}";
                        await _discord.SetActivityAsync(new Game(name));
                        //await _discord.SetActivityAsync(new Game(name, ActivityType.Playing, ActivityProperties.Spectate, $"IP: {_game.Id} | Servers and maps: https://bf2.nihlen.net/servers"));
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error updating activity name");
                }
            });
        }

        public static bool IsEnabled = true;

        public async Task SendTextMessageToChannelsAsync(string text, bool debug = false)
        {
            // TODO: use Threading.Channels and send in a different Task so we don't block here if we are rate limited
            // TODO: remove after testing
            if (!IsEnabled) return;

            // TODO: sanitize message, remove ` and other characters
            if (_adminChannels == null)
            {
                Log.Warning("No admin channels found");
                return;
            }

            var channels = debug ? _adminChannels.Where(c => c.Guild.Name.ToLower().Contains("netsky")) : _adminChannels;

            var tasks = channels.Select(c => c.SendMessageAsync(text));
            await Task.WhenAll(tasks);
        }

        public async Task SendEmbedMessageToChannelsAsync(Embed embed, bool debug = false)
        {
            // TODO: use Threading.Channels and send in a different Task so we don't block here if we are rate limited
            // TODO: remove after testing
            if (!IsEnabled) return;

            // TODO: sanitize message, remove ` and other characters
            if (_matchResultChannels == null)
            {
                Log.Warning("No match result channels found");
                return;
            }

            var channels = debug ? _matchResultChannels.Where(c => c.Guild.Name.ToLower().Contains("netsky")) : _matchResultChannels;

            var tasks = channels.Select(c => c.SendMessageAsync(embed: embed));
            await Task.WhenAll(tasks);
        }

        public async ValueTask HandleAsync(LeaveCommand command)
        {
            // TODO: use Threading.Channels and send in a different Task so we don't block here if we are rate limited
            if (_adminChannels == null)
            {
                Log.Warning("No admin channel channels found");
                return;
            }

            if (_game.Players.Count() < 4)
                return;

            if (command.Minutes < 1 || command.Minutes > 120)
            {
                _game.GameWriter.SendText($"{command.Minutes} minutes?! go touch grass ({command.Message.Player.DisplayName})");
                return;
            }

            if (DateTime.UtcNow - command.Message.Player.LastLeaveNotification < TimeSpan.FromMinutes(5))
                return;

            command.Message.Player.LastLeaveNotification = DateTime.UtcNow;

            _game.GameWriter.SendText($"{command.Message.Player.DisplayName} is leaving in {command.Minutes} minutes - Discord notification sent");

            var tasks = _notificationChannels.Select(c => c.SendMessageAsync($"`{command.Message.Player.DisplayName}` is leaving in {command.Minutes} minutes"));
            await Task.WhenAll(tasks);
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
            using (Profiler.Start("DEBUG PlayerJoin UpdateActivityNameAsync"))
            {
                await UpdateActivityNameAsync();
            }

            using (Profiler.Start("DEBUG PlayerJoin SendTextMessageToChannelsAsync"))
            {
                _newPlayers.TryAdd(e.Player.Index, true);
                await SendTextMessageToChannelsAsync($"`{Sanitize(e.Player.DisplayName)} is connecting`");
            }
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
    }
}
