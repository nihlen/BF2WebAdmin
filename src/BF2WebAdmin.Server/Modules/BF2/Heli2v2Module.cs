using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using Discord;
using Humanizer;
using Humanizer.Localisation;
using Newtonsoft.Json;
using Polly;
using Polly.Registry;
using Serilog;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

// TODO: Remove async voids after testing 2v2 counting
// TODO: Handle "pad" (reset depending on how long after go) and draws
// TODO: .load sandrosbunker
// TODO: .send awsm (freeze in front of SEND NUDES objects)
namespace BF2WebAdmin.Server.Modules.BF2
{
    public class Heli2v2Module : BaseModule, // TODO: re-add
        IHandleCommand<SwitchCommand>,
        IHandleCommand<SwitchAllCommand>,
        IHandleCommand<SwitchLaterCommand>,
        IHandleCommand<HeliMgCommand>,
        IHandleCommand<NoclipCommand>,
        IHandleCommand<StalkCommand>,
        IHandleCommand<StopCommand>,
        IHandleCommand<Toggle2v2Command>,
        IHandleCommand<ToggleTvLogCommand>,
        IHandleCommand<RecordCommand>,
        IHandleCommandAsync<PlaybackCommand>,
        IHandleCommandAsync<LoopCommand>,
        IHandleCommandAsync<PadCommand>,
        IHandleCommandAsync<AutoPadCommand>,
        IHandleCommandAsync<AutoPadAllCommand>,
        IHandleCommandAsync<NasaCommand>,
        IHandleCommandAsync<GetTvMissileValuesCommand>,
        IHandleCommandAsync<SetTvMissileValueCommand>,
        IHandleCommandAsync<SetTvMissileTypeCommand>
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<Heli2v2Module>();
        private readonly IGameServer _gameServer;
        private readonly IMatchRepository _matchRepository;
        private readonly IReadOnlyPolicyRegistry<string> _polichRegistry;
        private const int DefaultDelay = 25; // 1000/35 fps ~ 28.5
        private const int AltitudeOffset = 140; // Dalian?
        private const double DefaultPositionTrackerInterval = 0.25; // Dalian?

        private int _stopCounter;

        private int _switchCounter;
        private int _switchRounds;

        private int _antiNasaAltitude;
        private bool _antiNasaActive;

        private bool _roundsActive;
        private IList<Match> _matches;
        //private IList<Round> _rounds;

        //private event Action<RoundStartEvent> RoundStarted;
        //private event Action<RoundEndEvent> RoundEnded;

        public event Func<MatchStartEvent, Task> MatchStarted
        {
            add => _matchStartEvent.Add(value);
            remove => _matchStartEvent.Remove(value);
        }
        private readonly AsyncEvent<Func<MatchStartEvent, Task>> _matchStartEvent = new AsyncEvent<Func<MatchStartEvent, Task>>();

        public event Func<MatchEndEvent, Task> MatchEnded
        {
            add => _matchEndEvent.Add(value);
            remove => _matchEndEvent.Remove(value);
        }
        private readonly AsyncEvent<Func<MatchEndEvent, Task>> _matchEndEvent = new AsyncEvent<Func<MatchEndEvent, Task>>();

        public event Func<RoundStartEvent, Task> RoundStarted
        {
            add => _roundStartEvent.Add(value);
            remove => _roundStartEvent.Remove(value);
        }
        private readonly AsyncEvent<Func<RoundStartEvent, Task>> _roundStartEvent = new AsyncEvent<Func<RoundStartEvent, Task>>();

        public event Func<RoundEndEvent, Task> RoundEnded
        {
            add => _roundEndEvent.Add(value);
            remove => _roundEndEvent.Remove(value);
        }
        private readonly AsyncEvent<Func<RoundEndEvent, Task>> _roundEndEvent = new AsyncEvent<Func<RoundEndEvent, Task>>();

        private Player _stalker;
        private Player _stalked;

        public Heli2v2Module(IGameServer server, IMatchRepository matchRepository, IReadOnlyPolicyRegistry<string> polichRegistry) : base(server)
        {
            _gameServer = server;
            _matchRepository = matchRepository;
            _polichRegistry = polichRegistry;
            ClearMatches();
            SetupRoundEvents();

            _gameServer.MapChanged += OnMapChangedAsync;
            //var test = MapStatsRenderer.GetMapMovementPathImage("dalian_2_v_2", null, null);
        }

        private void ClearMatches()
        {
            _switchCounter = 0;
            _stopCounter = 0;
            _antiNasaAltitude = 0;
            _antiNasaActive = false;
            _matches = new List<Match>();
            //_rounds = new List<Round>();
        }

        private Task SendDiscordMessageAsync(string text)
        {
            var discordModule = _gameServer.ModManager?.GetModule<DiscordModule>();
            if (discordModule != null)
            {
                return discordModule.SendTextMessageToChannelsAsync(text, debug: true);
            }

            return Task.CompletedTask;
        }

        private Task SendDiscordEmbedAsync(Embed embed)
        {
            var discordModule = _gameServer.ModManager?.GetModule<DiscordModule>();
            if (discordModule != null)
            {
                return discordModule.SendEmbedMessageToChannelsAsync(embed);
            }

            return Task.CompletedTask;
        }

        private Task OnMapChangedAsync(Map map)
        {
            _gameServer.GameWriter.SendRcon(RconScript.InitServer);
            _gameServer.GameWriter.SendTimerInterval(DefaultPositionTrackerInterval);
            SetDefaultTvMissile();
            ClearMatches();
            return Task.CompletedTask;
        }

        private async void SetupRoundEvents()
        {
            // Server events
            _gameServer.ChatMessage += OnChatMessageAsync;
            _gameServer.PlayerKill += OnPlayerKillAsync;
            _gameServer.PlayerDeath += OnPlayerDeathAsync;
            _gameServer.PlayerTeam += OnPlayerTeamAsync;
            _gameServer.PlayerLeft += OnPlayerLeftAsync;
            _gameServer.PlayerPosition += OnPlayerPositionAsync;
            _gameServer.ProjectilePosition += OnProjectilePositionAsync;
            _gameServer.MapChanged += OnMapChanged;

            // Custom events
            MatchStarted += OnMatchStartedAsync;
            MatchEnded += OnMatchEndedAsync;
            RoundStarted += OnRoundStartedAsync;
            RoundEnded += OnRoundEndedAsync;

            _roundsActive = true;
            await SendDiscordMessageAsync("2v2 mode enabled");
            //_gameServer.GameWriter.SendText("2v2 mode enabled");
        }

        private async Task OnMapChanged(Map map)
        {
            var match = GetActiveMatch();
            if (match != null && match.Map != map.Name)
            {
                match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                await _matchEndEvent.InvokeAsync(new MatchEndEvent { Match = match });
            }
        }

        private async Task OnMatchStartedAsync(MatchStartEvent e)
        {
            Log.Information("Saving new match {MatchId}", e.Match.Id);

            await _polichRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
                await _matchRepository.InsertMatchAsync(ToMatchEntity(e.Match))
            );
            await SendDiscordMessageAsync("New 2v2 match started");
            _gameServer.GameWriter.SendText("New 2v2 match started");
        }

        private string Sanitize(string text)
        {
            return DiscordModule.Sanitize(text);
        }

        private async Task OnMatchEndedAsync(MatchEndEvent e)
        {
            Log.Information("Saving match end results for {MatchId}", e.Match.Id);

            await _polichRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
                await _matchRepository.UpdateMatchAsync(ToMatchEntity(e.Match))
            );

            var duration = e.Match.MatchEnd!.Value - e.Match.MatchStart!.Value;
            _gameServer.GameWriter.SendText($"2v2 match ended after {duration.Humanize()}.");

            try
            {
                await GetMatchStatisticsAsync(e.Match);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get match statistics");
            }
        }

        public class TeamStats
        {
            public string TeamHash { get; set; }
            public int Wins { get; set; }
        }

        public class PlayerStats
        {
            public string PlayerHash { get; set; }
            public int PilotCount { get; set; }
            public int GunnerCount { get; set; }
            public int TeamUsCount { get; set; }
            public int TeamChCount { get; set; }
            public int Ah1zCount { get; set; }
            public int Z10Count { get; set; }
            public int TvWinCount { get; set; }
            public int HydraWinCount { get; set; }
            public int MgWinCount { get; set; }
            public int EnemyCrashWinCount { get; set; }
            public int RdyStartCount { get; set; }
            public int GoStartCount { get; set; }
            public double TotalDistanceFromPad { get; set; }
            public double TotalAverageAltitude { get; set; }
            public double TotalAverageSpeed { get; set; }
        }

        // Dalian US pad ground: 255.100/148.400/-258.700
        // Dalian CH pad ground: -218.000/153.900/159.000
        private Position _dalianUsPad = Position.Parse("255.100/148.400/-258.700");
        private Position _dalianChPad = Position.Parse("-218.000/153.900/159.000");

        private async Task GetMatchStatisticsAsync(Match match)
        {
            var builder = new EmbedBuilder();
            builder.WithColor(Color.Blue);

            var duration = (match.MatchEnd ?? DateTime.UtcNow) - match.MatchStart!.Value;

            var draws = 0;
            var roundTotalDuration = 0d;
            var biggestTvCurveKill = ((RoundPlayer)null, 0d, 0d);
            var longestHydraKill = ((RoundPlayer)null, 0d);
            var longestRoundDuration = 0d;
            var shortestRoundDuration = double.MaxValue;
            var teamStats = match.TeamHashes.ToDictionary(h => h, h => new TeamStats { TeamHash = h });
            var playerStats = match.Rounds.First().Players.ToDictionary(player => player.Player.Hash, player => new PlayerStats { PlayerHash = player.Player.Hash });

            foreach (var round in match.Rounds.Where(r => !r.IsActive))
            {
                var roundDuration = ((round.RoundEnd ?? DateTime.UtcNow) - round.RoundStart!.Value).TotalSeconds;
                roundTotalDuration += roundDuration;

                if (roundDuration > longestRoundDuration) longestRoundDuration = roundDuration;
                if (roundDuration < shortestRoundDuration) shortestRoundDuration = roundDuration;

                if (round.WinningTeamId == -1)
                {
                    draws++;
                }
                else
                {
                    teamStats[round.WinningTeamHash].Wins++;

                    foreach (var player in round.Players)
                    {
                        if (player.SubVehicle == null)
                        {
                            Log.Warning("player.SubVehicle is null in {StatisticsMethod} for {PlayerName}", nameof(GetMatchStatisticsAsync), player?.Player?.Name);
                        }

                        if (player.SubVehicle != null)
                        {
                            if (player.SubVehicle.ToLower().EndsWith("cogunner")) playerStats[player.Player.Hash].GunnerCount++;
                            else playerStats[player.Player.Hash].PilotCount++;
                        }

                        if (match.Map == "dalian_2_v_2")
                        {
                            playerStats[player.Player.Hash].TotalDistanceFromPad += player.StartPosition?.Distance(player.TeamId == 2 ? _dalianUsPad : _dalianChPad) ?? 0;
                        }

                        if (player.MovementPath != null && player.MovementPath.Any())
                        {
                            var previousPosition = player.MovementPath.First().Position;
                            double totalAltitude = 0d, totalDistanceM = 0d;
                            foreach (var roundPosition in player.MovementPath)
                            {
                                totalAltitude += roundPosition.Position.Height - AltitudeOffset;
                                totalDistanceM += previousPosition.Distance(roundPosition.Position);
                                previousPosition = roundPosition.Position;
                            }

                            playerStats[player.Player.Hash].TotalAverageAltitude += totalAltitude / player.MovementPath.Count;
                            var totalDistanceKm = totalDistanceM / 1000;
                            var averageSpeedKmph = totalDistanceKm / (round.RoundEnd.Value - round.RoundStart.Value).TotalHours;
                            playerStats[player.Player.Hash].TotalAverageSpeed += averageSpeedKmph;
                        }

                        if (player.SaidGo) playerStats[player.Player.Hash].GoStartCount++;
                        else playerStats[player.Player.Hash].RdyStartCount++;

                        if (player.TeamId == 2) playerStats[player.Player.Hash].TeamUsCount++;
                        else playerStats[player.Player.Hash].TeamChCount++;

                        if (player.SubVehicle != null)
                        {
                            if (player.SubVehicle.ToLower().Contains("ah1z")) playerStats[player.Player.Hash].Ah1zCount++;
                            else playerStats[player.Player.Hash].Z10Count++;
                        }

                        if (player.TeamHash != round.WinningTeamHash)
                            continue;

                        var playerVictims = round.Players.Where(p => p.KillerHash == player.Player.Hash).ToList();
                        if (playerVictims.Any(p => p.KillerWeapon?.ToLower().EndsWith("tv") ?? false))
                        {
                            playerStats[player.Player.Hash].TvWinCount++;
                            var tvStats = CalculateTvStats(player.LastProjectilePath);
                            if (tvStats.angle > biggestTvCurveKill.Item3) biggestTvCurveKill = (player, tvStats.distance, tvStats.angle);
                        }

                        if (playerVictims.Any(p => p.KillerWeapon?.ToLower().EndsWith("gun") ?? false))
                        {
                            playerStats[player.Player.Hash].MgWinCount++;
                        }

                        var hydraVictim = playerVictims.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("launcher") ?? false);
                        if (hydraVictim != null)
                        {
                            playerStats[player.Player.Hash].HydraWinCount++;
                            var distance = hydraVictim.KillerPosition.Distance(round.Players.First(p => p.TeamHash != player.TeamHash).DeathPosition);
                            if (distance > longestHydraKill.Item2) longestHydraKill = (player, distance);
                        }

                        if (round.Players.Where(p => p.TeamHash == round.LosingTeamHash).All(p => p.KillerWeapon == null)) playerStats[player.Player.Hash].EnemyCrashWinCount++;
                    }
                }
            }

            var numRounds = match.Rounds.Count;
            var mostMgWins = playerStats.Values.OrderByDescending(p => p.MgWinCount).First();
            var mostMgWinsPlayer = match.Rounds.First().Players.First(p => p.Player.Hash == mostMgWins.PlayerHash);

            var sb = new StringBuilder();

            sb.AppendLine("```md");
            sb.AppendLine($"# 2v2 ended after {((match.MatchEnd ?? DateTime.UtcNow) - match.MatchStart.Value).Humanize()} on {match.ServerName} #\n");

            builder.WithTitle("View match statistics");
            builder.Url = $"https://bf2.nihlen.net/matches/{match.Id.ToString().ToLower()}";

            builder.WithDescription($"2v2 ended after {((match.MatchEnd ?? DateTime.UtcNow) - match.MatchStart.Value).Humanize(2, minUnit: TimeUnit.Minute)} on {match.ServerName}\n\u200B");
            //builder.WithDescription($"2v2 ended after {((match.MatchEnd ?? DateTime.UtcNow) - match.MatchStart.Value).Humanize(2, minUnit: TimeUnit.Minute)} on {match.ServerName}\n```md\n# Test #\n```\n");

            if (teamStats.Values.First().Wins == teamStats.Values.Last().Wins)
            {
                sb.AppendLine($"Score: tied {teamStats.Values.First().Wins} - {teamStats.Values.Last().Wins}\n");
            }
            else if (teamStats.Values.First().Wins > teamStats.Values.Last().Wins)
            {
                var players = match.Rounds[0].Players.Where(p => p.TeamHash == teamStats.Values.First().TeamHash);
                var playerNames = string.Join("> <", players.Select(p => Sanitize(p.Player.ShortName)));
                sb.AppendLine($"Score: {teamStats.Values.First().Wins} - {teamStats.Values.Last().Wins} for <{playerNames}>\n");
            }
            else
            {
                var players = match.Rounds[0].Players.Where(p => p.TeamHash == teamStats.Values.Last().TeamHash);
                var playerNames = string.Join("> <", players.Select(p => Sanitize(p.Player.ShortName)));
                sb.AppendLine($"Score: {teamStats.Values.Last().Wins} - {teamStats.Values.First().Wins} for <{playerNames}>\n");
            }

            //var hasDivider = false;
            //var teamLines = new List<(string, string)>();

            var maxWins = teamStats.Max(s => s.Value.Wins);

            foreach (var (_, stats) in teamStats.OrderByDescending(s => s.Value.Wins))
            {
                var players = match.Rounds[0].Players.Where(p => p.TeamHash == stats.TeamHash).ToList();
                var playerTeamStats = playerStats.Values.Where(s => players.Any(p => p.Player.Hash == s.PlayerHash)).ToList();
                var firstPlayer = playerTeamStats.First(s => players.Any(p => p.Player.Hash == s.PlayerHash));
                var playerNames = string.Join(" and ", players.Select(p => Sanitize(p.Player.ShortName)));
                sb.AppendLine($"# {playerNames} #");

                var playerNamesEmbed = string.Join("  +  ", players.Select(p => $":flag_{p.Player.Country?.Code?.ToLower() ?? "md"}: {Sanitize(p.Player.ShortName)}"));
                builder.AddField(
                    $"`{stats.Wins,3} ` \u200B \u200B \u200B {playerNamesEmbed} {(stats.Wins == maxWins ? "\u200B :trophy:" : string.Empty)}",
                    //$"\u200B \u200B \u200B \u200B \u200B \u200B TV: \u200B {playerTeamStats.Sum(p => p.TvWinCount)} \u200B • \u200B Hydra: \u200B {playerTeamStats.Sum(p => p.HydraWinCount)} \u200B • \u200B MG: \u200B {playerTeamStats.Sum(p => p.MgWinCount)} \u200B • \u200B Enemy crashes: \u200B {firstPlayer.EnemyCrashWinCount}"
                    "\u200B"
                );

                //var playerNamesEmbed = string.Join("   ", players.Select(p => $":flag_{p.Player.Country?.Code?.ToLower() ?? "md"}: {Sanitize(p.Player.ShortName)}"));
                //builder.AddField($"{playerNamesEmbed}:", "\u200B", true);
                //builder.AddField($"{stats.Wins}", "\u200B", true);
                //builder.AddField("\u200B", "\u200B", true);

                //teamLines.Add(($"**{playerNamesEmbed}:**", $"**{stats.Wins}**"));

                //if (!hasDivider)
                //{
                //    builder.AddField("\u200B", "\u200B");
                //    //builder.AddField("vs", "-", true);
                //    hasDivider = true;
                //}

                var pilot = playerTeamStats.OrderByDescending(p => p.PilotCount).First();
                var pilotPlayer = players.First(p => p.Player.Hash == pilot.PlayerHash);
                sb.AppendLine($"Pilot: {Sanitize(pilotPlayer.Player.ShortName)} {FormatPercent(pilot.PilotCount, numRounds)}");

                var gunner = playerTeamStats.OrderByDescending(p => p.GunnerCount).First();
                var gunnerPlayer = players.First(p => p.Player.Hash == gunner.PlayerHash);
                sb.AppendLine($"Gunner: {Sanitize(gunnerPlayer.Player.ShortName)} {FormatPercent(gunner.GunnerCount, numRounds)}");

                sb.AppendLine($"Team: US {FormatPercent(firstPlayer.TeamUsCount, numRounds)} / CH {FormatPercent(firstPlayer.TeamChCount, numRounds)}");
                sb.AppendLine($"Vehicle: AH-1Z {FormatPercent(firstPlayer.Ah1zCount, numRounds)} / Z-10 {FormatPercent(firstPlayer.Z10Count, numRounds)}");
                sb.AppendLine($"Start: RDY {FormatPercent(firstPlayer.RdyStartCount, numRounds)} / GO {FormatPercent(firstPlayer.GoStartCount, numRounds)}");

                sb.Append("Wins: ");
                sb.Append($"TV {FormatPercent(playerTeamStats.Sum(p => p.TvWinCount), stats.Wins)} / ");
                sb.Append($"Hydra {FormatPercent(playerTeamStats.Sum(p => p.HydraWinCount), stats.Wins)} / ");
                sb.Append($"MG {FormatPercent(playerTeamStats.Sum(p => p.MgWinCount), stats.Wins)} / ");
                sb.AppendLine($"Enemy crash {FormatPercent(firstPlayer.EnemyCrashWinCount, stats.Wins)}");

                sb.AppendLine($"Pad: avg. start distance {firstPlayer.TotalDistanceFromPad / numRounds:##} m");
                sb.AppendLine($"Altitude: avg. per round {firstPlayer.TotalAverageAltitude / numRounds:##} m");
                sb.AppendLine($"Speed: avg. per round {firstPlayer.TotalAverageSpeed / numRounds:##} km/h\n");
            }

            //builder.AddField(teamLines[0].Item1, teamLines[1].Item1, true);
            //builder.AddField(teamLines[0].Item2, teamLines[1].Item2, true);

            builder.AddField("Map", match.Map);

            if (biggestTvCurveKill.Item1 != null)
            {
                sb.AppendLine($"Biggest TV curve kill: est. {biggestTvCurveKill.Item2:##} m {biggestTvCurveKill.Item3:##}° by {Sanitize(biggestTvCurveKill.Item1?.Player.ShortName ?? "?")}");
                builder.AddField("Biggest TV curve kill", $"Est. {biggestTvCurveKill.Item2:##} m {biggestTvCurveKill.Item3:##}° by {Sanitize(biggestTvCurveKill.Item1?.Player.ShortName ?? "?")}");
            }

            if (longestHydraKill.Item1 != null)
            {
                sb.AppendLine($"Longest hydra kill: est. {longestHydraKill.Item2:##} m by {Sanitize(longestHydraKill.Item1?.Player.ShortName ?? "?")}");
                builder.AddField("Longest hydra kill", $"Est. {longestHydraKill.Item2:##} m by {Sanitize(longestHydraKill.Item1?.Player.ShortName ?? "?")}");
            }

            if (mostMgWins.MgWinCount > 0)
            {
                sb.AppendLine($"Most MG wins: {mostMgWins.MgWinCount} by {Sanitize(mostMgWinsPlayer?.Player.ShortName ?? "?")}");
                builder.AddField("Most MG wins", $"{mostMgWins.MgWinCount} by {Sanitize(mostMgWinsPlayer?.Player.ShortName ?? "?")}");
            }

            sb.AppendLine($"Round duration: avg. {roundTotalDuration / numRounds:##} s / longest {longestRoundDuration:##} s / shortest {shortestRoundDuration:##} s");
            sb.AppendLine($"Draw rounds: {draws}");
            sb.Append("```");

            //await SendDiscordMessageAsync($"2v2 match ended after {duration.Humanize()}.");
            await SendDiscordMessageAsync(sb.ToString());

            if (duration > TimeSpan.FromMinutes(10) && match.Rounds.Count > 10)
            {
                Log.Information("Sending match results for {MatchId}", match.Id);
                await SendDiscordEmbedAsync(builder.Build());
            }
            else
            {
                Log.Information("Not sending match results for {MatchId} (duration: {Duration}, rounds: {RoundCount})", match.Id, duration, match.Rounds.Count);
            }
        }

        private static string FormatPercent(int a, int b)
        {
            var value = 100d * ((double)a / b);
            return value < 0.5 ? "0%" : $"{value:##}%";
        }

        private Task OnPlayerPositionAsync(Player player, Position position, Rotation rotation, int ping)
        {
            var round = GetActiveRound();
            if (round == null)
                return Task.CompletedTask;

            var timestamp = (DateTime.UtcNow - round.RoundStart!.Value).TotalMilliseconds;
            round.Players
                .FirstOrDefault(p => p.Player.Hash == player.Hash)
                ?.MovementPath.Add(new RoundPosition
                {
                    Id = player.Index,
                    Timestamp = (int)timestamp,
                    Position = position,
                    Rotation = rotation,
                    Ping = player.Score.Ping
                });

            return Task.CompletedTask;
        }

        private Match GetActiveMatch()
        {
            return _matches.FirstOrDefault(m => m.IsActive);
        }

        private Round GetActiveRound()
        {
            return GetActiveMatch()?.Rounds.FirstOrDefault(r => r.IsActive);
        }

        private Task OnProjectilePositionAsync(Projectile projectile, Position position, Rotation rotation)
        {
            var now = DateTime.UtcNow;
            if (projectile.Owner == null)
                return Task.CompletedTask;

            var round = GetActiveRound();
            var roundPlayer = round?.Players.FirstOrDefault(p => p.Player.Hash == projectile.Owner.Hash);
            var isLoggedTv = _tvLogPlayers.TryGetValue(projectile.Owner.Hash, out var path);
            if (roundPlayer == null && !isLoggedTv)
                return Task.CompletedTask;

            var timestamp = (now - (round?.RoundStart ?? now)).TotalMilliseconds;
            var roundPosition = new RoundPosition
            {
                Id = projectile.Id,
                Timestamp = (int)timestamp,
                Position = projectile.Position,
                Rotation = projectile.Rotation,
                Ping = projectile.Owner.Score.Ping
            };

            if (isLoggedTv)
            {
                if (path.Projectile?.Id != projectile.Id)
                {
                    path.Projectile = projectile;
                    path.Path.Clear();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // TODO: get max X and Y angles per s or ms and see what you can get max as default
                            await Task.Delay(4000);
                            var (distance, angle) = CalculateTvStats(path.Path, true);
                            _gameServer.GameWriter.SendText($"{projectile.Owner.ShortName} TV: est. {distance:###} m {angle:###} degrees");
                        }
                        catch (Exception e)
                        {
                            Log.Error("Projectile message failed");
                        }
                    });
                }

                path.Path.Add(roundPosition);
                //_gameServer.GameWriter.SendText($"P:{position} R:{rotation}");
            }

            if (roundPlayer != null)
            {
                if (roundPlayer.LastProjectileId != projectile.Id)
                {
                    roundPlayer.ProjectilePaths.Add(new List<RoundPosition>());
                    //roundPlayer.LastProjectilePath.Clear();
                    roundPlayer.LastProjectileId = projectile.Id;
                }

                //roundPlayer.LastProjectilePath.Add((position, rotation));
                roundPlayer.ProjectilePaths.Last().Add(roundPosition);
            }

            return Task.CompletedTask;
        }

        private async Task OnPlayerLeftAsync(Player player)
        {
            RemoveActiveRoundWithPlayer(player.Hash);

            var match = GetActiveMatch();
            if (match != null)
            {
                var onlinePlayersCount = match.Rounds.FirstOrDefault()?.Players
                    .Count(p => _gameServer.Players.Any(gp => gp.Hash == p.Player.Hash));
                if (onlinePlayersCount <= 2)
                {
                    match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                    await _matchEndEvent.InvokeAsync(new MatchEndEvent { Match = match });
                }
            }
        }

        private Task OnPlayerTeamAsync(Player player, Team team)
        {
            RemoveActiveRoundWithPlayer(player.Hash);
            return Task.CompletedTask;
        }

        private async void RemoveRoundEvents()
        {
            // Server events
            _gameServer.ChatMessage -= OnChatMessageAsync;
            _gameServer.PlayerKill -= OnPlayerKillAsync;
            _gameServer.PlayerDeath -= OnPlayerDeathAsync;
            _gameServer.PlayerTeam -= OnPlayerTeamAsync;
            _gameServer.PlayerLeft -= OnPlayerLeftAsync;
            _gameServer.PlayerPosition -= OnPlayerPositionAsync;
            _gameServer.ProjectilePosition -= OnProjectilePositionAsync;
            _gameServer.MapChanged -= OnMapChanged;

            // Custom events
            MatchStarted -= OnMatchStartedAsync;
            MatchEnded -= OnMatchEndedAsync;
            RoundStarted -= OnRoundStartedAsync;
            RoundEnded -= OnRoundEndedAsync;

            _roundsActive = false;
            await SendDiscordMessageAsync("2v2 mode disabled");
            //_gameServer.GameWriter.SendText("2v2 mode disabled");
        }

        private void RemoveActiveRoundWithPlayer(string playerHash)
        {
            if (_pendingRound?.Players.Any(p => p.Player.Hash == playerHash) ?? false)
            {
                _pendingRound = null;
            }

            var match = GetActiveMatch();
            if (match != null)
            {
                match.Rounds = match.Rounds.Where(r => !(r.IsActive && r.Players.Any(p => p.Player.Hash == playerHash))).ToList();
            }
        }

        private Round _pendingRound;

        private async Task OnChatMessageAsync(Message message)
        {
            if (message.Type != MessageType.Player || message.Channel != ChatChannel.Global)
                return;

            const string padPattern = @"^j?pad$";
            var isPadMatch = Regex.IsMatch(message.Text, padPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (isPadMatch)
            {
                RemoveActiveRoundWithPlayer(message.Player.Hash);
                return;
            }

            // TODO: fix 2 players ready on 1 team then switching
            //var readyPattern = @"^(j?([rdy]+|[go0]+))";
            //const string readyPattern = @"^j?([1234ready]{1,5}|[go0]{1,3})[A-z0-9\?\!\.]{0,4}$";
            //const string readyPattern = @"^j?(?!gg)(([ready]{1,8}|[go]{1,5})[A-z0-9\?\!\.]{0,2})$";
            const string readyPattern = @"^j?[^0-9A-Za-z]*(?!gg)(([ready]{1,8}|[go]{1,5})[A-z0-9\?\!\.]{0,2})[^0-9A-Za-z]*$";
            const string readyPatternWithOtherText = @"(^(j?rd[y]?|j?g[o]?)[ ]+)|[ ]+(rd[y]?|g[o]?)$";

            var isReadyMatch = Regex.IsMatch(message.Text, readyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var isReadyWithOtherMatch = Regex.IsMatch(message.Text, readyPatternWithOtherText, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if ((!isReadyMatch && !isReadyWithOtherMatch) || message.Text == "yes")
                return;

            var teamPlayers = message.Player.Vehicle?.Players;
            if ((teamPlayers?.Count ?? 0) < 2)
                return;

            if (teamPlayers!.Any(p => p.Team.Id != teamPlayers[0].Team.Id))
            {
                _gameServer.GameWriter.SendText($"Team mismatch for {string.Join(" & ", teamPlayers.Select(p => $"{p.ShortName}"))} - switch team to fix match");
                await SendDiscordMessageAsync($"2v2 team ID mismatch for {string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}"))}");
                Log.Warning("2v2 team ID mismatch for {PlayerTeamNames}", string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}")));
                return;
            }

            if (_pendingRound != null && (DateTime.UtcNow - _pendingRound?.ReadyTime) > TimeSpan.FromMinutes(5))
            {
                Log.Debug("Pending round was over 5 minutes - clearing");
                _pendingRound = null;
            }

            var newReady = true;
            //var match = GetActiveMatch();
            //var round = match?.Rounds.FirstOrDefault(r => !r.IsFinished);
            if (_pendingRound == null)
            {
                _pendingRound = new Round
                {
                    Id = Guid.NewGuid(),
                    ReadyTime = DateTime.UtcNow,
                    PositionTrackerInterval = _gameServer.GameWriter.CurrentTrackerInterval
                };
                //_rounds.Add(round);
            }

            var teamHash = GetTeamHash(teamPlayers[0], teamPlayers[1]);
            foreach (var vehiclePlayer in message.Player.Vehicle!.Players)
            {
                var roundPlayer = _pendingRound.Players.FirstOrDefault(p => p.Player.Hash == vehiclePlayer.Hash);
                if (roundPlayer == null)
                {
                    roundPlayer = new RoundPlayer(vehiclePlayer, teamHash, _pendingRound.Id);
                    _pendingRound.Players.Add(roundPlayer);
                }

                if (roundPlayer.IsReady)
                    newReady = false;
                else
                    roundPlayer.IsReady = true;
            }

            var team1Ready = _pendingRound.Players.Any(p => p.IsReady && p.Player.Team.Id == 1);
            var team2Ready = _pendingRound.Players.Any(p => p.IsReady && p.Player.Team.Id == 2);
            if (_pendingRound.Players.Count == 4 && team1Ready && team2Ready && !_pendingRound.IsActive)
            {
                _pendingRound.RoundStart = DateTime.UtcNow;
                var goTeamId = message.Player.Team.Id;
                foreach (var roundPlayer in _pendingRound.Players)
                {
                    roundPlayer.SaidGo = goTeamId == roundPlayer.TeamId;
                    roundPlayer.SubVehicle = roundPlayer.Player.SubVehicleTemplate;
                    roundPlayer.StartPosition = roundPlayer.Player.Position;
                }

                if (_pendingRound.MatchId == null)
                {
                    var teamHashes = _pendingRound.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
                    var match = _matches.FirstOrDefault(m => m.IsActive && m.TeamHashes.Contains(teamHashes[0]) && m.TeamHashes.Contains(teamHashes[1]));
                    if (match == null)
                    {
                        var previousMatch = GetActiveMatch();
                        if (previousMatch != null)
                        {
                            previousMatch.MatchEnd = previousMatch.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                            await _matchEndEvent.InvokeAsync(new MatchEndEvent { Match = previousMatch });
                        }

                        match = new Match
                        {
                            Id = Guid.NewGuid(),
                            MatchStart = _pendingRound.RoundStart.Value,
                            Map = _gameServer.Map.Name,
                            ServerId = _gameServer.Id,
                            ServerName = _gameServer.Name,
                            TeamHashes = teamHashes,
                            Type = "HELI_2V2",
                        };
                        _matches.Add(match);

                        match.Rounds.Add(_pendingRound);
                        _pendingRound.MatchId = match.Id;

                        await _matchStartEvent.InvokeAsync(new MatchStartEvent { Match = match });
                    }
                    else
                    {
                        match.Rounds.Add(_pendingRound);
                        _pendingRound.MatchId = match.Id;
                    }
                }

                await _roundStartEvent.InvokeAsync(new RoundStartEvent { Round = _pendingRound });

                _pendingRound = null;
            }
            else if (newReady)
            {
                await SendDiscordMessageAsync($"{message.Player.Team?.Name} READY");
                //_gameServer.GameWriter.SendText($"{message.Player.Team.Name} READY");
            }
        }

        private Task OnPlayerKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
        {
            return HandleRoundKillAsync(attacker, attackerPosition, victim, victimPosition, weapon);
        }

        private Task OnPlayerDeathAsync(Player player, Position position, bool isSuicide)
        {
            return HandleRoundKillAsync(null, null, player, position, null);
        }

        private async Task OnRoundStartedAsync(RoundStartEvent e)
        {
            //_gameServer.GameWriter.SendText($"Round {_roundCounter} started ({e.Round.Players.Count})");
            await SendDiscordMessageAsync($"GO");
            //_gameServer.GameWriter.SendText("§3§c1001GO");
            //return Task.CompletedTask;
        }

        private bool enableEndOfRoundTeleport = true;

        private async Task OnRoundEndedAsync(RoundEndEvent e)
        {
            try
            {
                // TODO: combine discord messages to avoid rate limiting
                var winningTeam = _gameServer.Teams.FirstOrDefault(t => t.Id == e.Round.WinningTeamId);
                var timeDiff = e.Round.RoundEnd!.Value - e.Round.RoundStart!.Value;

                //var team1Id = _gameServer.Teams.Min(t => t.Id);
                //var team2Id = _gameServer.Teams.Max(t => t.Id);
                //var team1Players = e.Round.Players.Where(p => p.TeamId == team1Id).ToList();
                //var team2Players = e.Round.Players.Where(p => p.TeamId == team2Id).ToList();
                //var teamRounds = _rounds.Where(r =>
                //{
                //    var allPlayersMatch = r.Players.All(p => e.Round.Players.Any(rp => rp.Player.Hash == p.Player.Hash));
                //    var playerTeamsMatch = team1Players[0].Player.Hash;

                //    return allPlayersMatch;
                //});
                //return string.Compare(p1.Hash, p2.Hash, StringComparison.Ordinal) <= 0 ? p1.Hash + p2.Hash : p2.Hash + p1.Hash;

                //string teamAHash, teamBHash = null;

                var match = GetActiveMatch();

                //var teamHashes = e.Round.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToList();

                var teamAHash = match.TeamHashes.First();
                var teamBHash = match.TeamHashes.Last();

                //var teamAHash = string.Compare(e.Round.WinningTeamHash, e.Round.LosingTeamHash, StringComparison.Ordinal) <= 0 ? e.Round.WinningTeamHash : e.Round.LosingTeamHash;
                //var teamBHash = string.Compare(e.Round.WinningTeamHash, e.Round.LosingTeamHash, StringComparison.Ordinal) <= 0 ? e.Round.LosingTeamHash : e.Round.WinningTeamHash;

                var teamAWins = match.Rounds.Count(r => r.WinningTeamHash == teamAHash && r.LosingTeamHash == teamBHash);
                var teamBWins = match.Rounds.Count(r => r.WinningTeamHash == teamBHash && r.LosingTeamHash == teamAHash);

                var teamAIndicator = teamAHash == e.Round.WinningTeamHash ? "+" : string.Empty;
                var teamBIndicator = teamBHash == e.Round.WinningTeamHash ? "+" : string.Empty;

                var teamAPlayers = e.Round.Players.Where(p => p.TeamHash == teamAHash);
                var teamBPlayers = e.Round.Players.Where(p => p.TeamHash == teamBHash);

                var totalRounds = match.Rounds.Count(r => r.Players.Any(p => p.TeamHash == teamAHash) && r.Players.Any(p => p.TeamHash == teamBHash));

                var tvKillerHash = e.Round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("tv") ?? false)?.KillerHash;
                var tvAttacker = e.Round.Players.FirstOrDefault(p => p.Player.Hash == tvKillerHash);
                var killTvPath = e.Round.Players.FirstOrDefault(p => p.TeamHash == e.Round.WinningTeamHash && p.ProjectilePaths.Any())?.LastProjectilePath;
                var (killTvDistance, killTvAngle) = tvAttacker != null ? CalculateTvStats(killTvPath) : (0d, 0d);

                var victimPlayer = e.Round.Players.FirstOrDefault(p => p.TeamHash == e.Round.LosingTeamHash);
                var hydraKillerHash = e.Round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("launcher") ?? false)?.KillerHash;
                var hydraAttacker = e.Round.Players.FirstOrDefault(p => p.Player.Hash == hydraKillerHash);
                var killHydraDistance = 0d;
                if (victimPlayer != null && hydraAttacker != null)
                {
                    //killHydraDistance = hydraAttacker.KillPosition.Distance(victimPlayer.DeathPosition);
                    killHydraDistance = victimPlayer.KillerPosition.Distance(victimPlayer.DeathPosition);
                }

                var mgKillerHash = e.Round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("gun") ?? false)?.KillerHash;
                var mgAttacker = e.Round.Players.FirstOrDefault(p => p.Player.Hash == mgKillerHash);
                var killMgDistance = 0d;
                if (victimPlayer != null && mgAttacker != null)
                {
                    killMgDistance = victimPlayer.KillerPosition.Distance(victimPlayer.DeathPosition);
                }

                var unknownKillVictim = tvAttacker == null && hydraAttacker == null && mgAttacker == null
                    ? e.Round.Players.Where(p => p.TeamHash == e.Round.LosingTeamHash).FirstOrDefault(p => p.KillerWeapon != null)
                    : null;
                //var unknownAttacker = tvAttacker == null && hydraAttacker == null && mgAttacker == null
                //    ? e.Round.Players.Where(p => p.TeamHash == e.Round.LosingTeamHash).FirstOrDefault(p => p.KillWeapon != null)
                //    : null;

                var isCrash = e.Round.WinningTeamId != -1 && e.Round.Players.Where(p => p.TeamHash == e.Round.LosingTeamHash).All(p => p.KillerWeapon == null);

                //var path1 = e.Round.Players.FirstOrDefault(p => p.TeamId == 2)?.MovementPath;
                //var path2 = e.Round.Players.FirstOrDefault(p => p.TeamId == 1)?.MovementPath;
                //if (path1 != null && path1.Any() && path2 != null && path2.Any())
                //{
                //    var roundMap = MapStatsRenderer.GetMapMovementPathImage(match.Map, path1, path2);
                //}

                var sb = new StringBuilder();

                if (e.Round.WinningTeamId == -1)
                {
                    sb.Append($"```md\nRound {totalRounds} ended in a CLASSIC DRAW in {(int)timeDiff.TotalSeconds} s\n\n");
                }
                else
                {
                    sb.Append($"```md\nRound {totalRounds} won by {winningTeam?.Name ?? "?"} in {(int)timeDiff.TotalSeconds} s\n\n");
                    sb.Append(killTvDistance > 0 ? $"TV: est. {killTvDistance:###}m {killTvAngle:###}°\n\n" : string.Empty);
                    sb.Append(killHydraDistance > 0 ? $"Hydra: est. {killHydraDistance:###}m\n\n" : string.Empty);
                    sb.Append(killMgDistance > 0 ? $"MG: est. {killMgDistance:###}m\n\n" : string.Empty);
                    sb.Append(unknownKillVictim != null ? $"Unknown weapon: {unknownKillVictim?.KillerWeapon}\n\n" : string.Empty);
                    sb.Append(isCrash ? "Crashed\n\n" : string.Empty);
                }

                sb.Append($"<{string.Join("> <", teamAPlayers.Select(p => p.Player.ShortName))}> {teamAWins}{teamAIndicator} vs ");
                sb.Append($"{teamBWins}{teamBIndicator} <{string.Join("> <", teamBPlayers.Select(p => p.Player.ShortName))}>```");

                await SendDiscordMessageAsync(sb.ToString());

                if (e.Round.WinningTeamId == -1)
                {
                    _gameServer.GameWriter.SendText($"Round {match.Rounds.Count} ended in a §C1001CLASSIC DRAW§C1001 in {(int)timeDiff.TotalSeconds} s");
                }
                else
                {
                    _gameServer.GameWriter.SendText($"Round {match.Rounds.Count} won by {winningTeam.Name} in {(int)timeDiff.TotalSeconds} s");
                }

                // TODO: remove after testing
                //await GetMatchStatisticsAsync(match);

                //_gameServer.GameWriter.SendText($"Round {_roundCounter} ended in {(int)timeDiff.TotalSeconds} s ({winningTeam.Name} +1)");

                //var players = e.Round.Players.Where(p => p.TeamId == e.Round.WinningTeamId && p.IsAlive).Select(p => p.Player).ToArray();
                //await BlurAsync(2000, players);
                //await Task.Delay(2000);
                //foreach (var player in players)
                //    _gameServer.GameWriter.SendHealth(player, 1);

                if (enableEndOfRoundTeleport)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var winningPlayers = e.Round.Players.Where(p => p.TeamHash == e.Round.WinningTeamHash && p.IsAlive).ToList();
                            if (winningPlayers.Any(p => _autopadPlayerHashes.ContainsKey(p.Player.Hash)))
                            {
                                await Task.Delay(2000);
                                await FreezeAtPadAsync(0, 0, winningPlayers.First().Player);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error at endOfRoundTeleport");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error at end of round event");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    Log.Information("Saving round {RoundId} on match {MatchId}", e.Round.Id, e.Round.MatchId);

                    await _polichRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
                        await _matchRepository.InsertRoundAsync(ToRoundEntity(e.Round))
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to save round {RoundId} on match {MatchId}", e.Round.Id, e.Round.MatchId);
                }
            });
        }

        private (double distance, double angle) CalculateTvStats(IList<RoundPosition> killTvPath, bool debug = false)
        {
            double killTvDistance = 0d, killTvAngle = 0d;
            if (killTvPath == null || !killTvPath.Any())
                return (killTvDistance, killTvAngle);

            if (debug)
            {
                //_gameServer.GameWriter.SendText($"Path samples: [{killTvPath.Count}]");
            }

            var firstSample = killTvPath.First();
            //var (previousPosition, previousRotation) = killTvPath.First();
            var previousPosition = firstSample.Position;
            var previousRotation = firstSample.Rotation;
            foreach (var sample in killTvPath)
            {
                killTvDistance += previousPosition.Distance(sample.Position);
                var angle = previousRotation.Angle(sample.Rotation);
                killTvAngle += angle;

                if (debug)
                {
                    //_gameServer.GameWriter.SendText($"Angle: [{angle}]");
                }

                previousPosition = sample.Position;
                previousRotation = sample.Rotation;
            }

            return (killTvDistance, killTvAngle);
        }

        private readonly ConcurrentDictionary<string, bool> _autopadPlayerHashes = new ConcurrentDictionary<string, bool>();

        private Task HandleRoundKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
        {
            // TODO: detect draws - wait 1 s before declaring winner - or detect patterns - check vehicle status/id?
            //var round = _rounds.FirstOrDefault(r => r.IsActive);
            var match = GetActiveMatch();
            var round = GetActiveRound();
            if (round == null)
                return Task.CompletedTask;

            var roundVictim = round.Players.FirstOrDefault(p => p.Player.Hash == victim.Hash);
            if (roundVictim == null)
                return Task.CompletedTask;

            roundVictim.IsAlive = false;
            roundVictim.DeathPosition = victimPosition;
            roundVictim.DeathTime = DateTime.UtcNow;
            roundVictim.KillerWeapon = weapon ?? roundVictim.KillerWeapon;
            roundVictim.KillerPosition = attackerPosition ?? roundVictim.KillerPosition;
            roundVictim.KillerHash = round.Players.FirstOrDefault(p => p.Player.Hash == attacker?.Hash)?.Player.Hash ?? roundVictim.KillerHash;

            // Killed by player not in the 2v2?
            //var roundAttacker = round?.Players.FirstOrDefault(p => p.Player.Id == attacker?.Id);
            //if (attacker != null && roundAttacker == null)
            //    return Task.CompletedTask;

            foreach (var roundPlayer in round.Players.Where(p => p.TeamHash == roundVictim.TeamHash))
            {
                roundPlayer.IsReady = false;
            }

            if (round.Players.Any(p => p.TeamHash == roundVictim.TeamHash && p.IsAlive))
                return Task.CompletedTask;

            // TODO: lock gonna work?
            lock (round)
            {
                if (round.IsPendingCompletion)
                    return Task.CompletedTask;

                round.IsPendingCompletion = true;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait 2 seconds and see if it's a draw
                        await Task.Delay(2000);

                        var allPlayersDead = round.Players.All(p => !p.IsAlive);
                        var team1DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[0]).DeathPosition;
                        var team2DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[1]).DeathPosition;
                        var isHeliRam = allPlayersDead && team1DeathPosition.Distance(team2DeathPosition) < 10;
                        var allKilledByTv = round.Players.All(p => p.KillerWeapon?.ToLower().EndsWith("tv") ?? false);

                        if (allPlayersDead && (allKilledByTv || isHeliRam))
                        {
                            // Classic draw
                            round.WinningTeamId = -1;
                        }
                        else
                        {
                            // One winner
                            round.WinningTeamId = roundVictim.TeamId == 1 ? 2 : 1;
                            //round.LosingTeamId = roundVictim.TeamId;
                            round.WinningTeamHash = round.Players.First(p => p.TeamHash != roundVictim.TeamHash).TeamHash;
                            round.LosingTeamHash = round.Players.First(p => p.TeamHash == roundVictim.TeamHash).TeamHash;
                        }

                        round.RoundEnd = DateTime.UtcNow;

                        await _roundEndEvent.InvokeAsync(new RoundEndEvent { Round = round });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error at 2v2 completion task");
                    }
                });
            }

            return Task.CompletedTask;
        }

        private static string GetTeamHash(Player p1, Player p2)
        {
            return string.Compare(p1.Hash, p2.Hash, StringComparison.Ordinal) <= 0 ? p1.Hash + p2.Hash : p2.Hash + p1.Hash;
        }

        #region Commands

        public void Handle(SwitchCommand command)
        {
            var player = _gameServer.GetPlayer(command.Name);
            if (player == null)
                return;

            SwitchPlayer(player);
        }

        public void Handle(SwitchAllCommand command)
        {
            SwitchAll();
        }

        public void Handle(SwitchLaterCommand command)
        {
            if (!_roundsActive)
            {
                _gameServer.GameWriter.SendText("2v2 mode is not currently active");
                return;
            }

            _gameServer.GameWriter.SendText($"Switching in {command.Rounds} rounds");
            _switchRounds = command.Rounds;
            RoundEnded += SwitchCheckAsync;
        }

        private Task SwitchCheckAsync(RoundEndEvent e)
        {
            if (++_switchCounter < _switchRounds)
                return Task.CompletedTask;

            RoundEnded -= SwitchCheckAsync;
            SwitchAll();

            return Task.CompletedTask;
        }

        private void SwitchAll()
        {
            _gameServer.GameWriter.SendText("Switching teams");

            foreach (var player in _gameServer.Players)
                SwitchPlayer(player);
        }

        private void SwitchPlayer(Player player)
        {
            _gameServer.GameWriter.SendTeam(player, player.Team.Id == 1 ? 2 : 1);
            _gameServer.GameWriter.SendHealth(player, 1);
        }

        public void Handle(HeliMgCommand command)
        {
            if (command.Value == 0)
            {
                _gameServer.GameWriter.SendRcon(RconScript.MgOff);
                _gameServer.GameWriter.SendText("Heli MG off");
            }

            if (command.Value == 1)
            {
                _gameServer.GameWriter.SendRcon(RconScript.MgOn);
                _gameServer.GameWriter.SendText("Heli MG on");
            }
        }

        public void Handle(NoclipCommand command)
        {
            var player = _gameServer.GetPlayer(command.Name);
            if (player == null)
                return;

            var objectId = player.Vehicle.RootVehicleId;
            var hasCollision = command.Value == 1 ? 0 : 1;
            player.Vehicle.HasCollision = hasCollision == 1;

            var replacements = new Dictionary<string, string>
            {
                {"{OBJECT_ID}", objectId.ToString()}
            };

            var script = player.Vehicle.HasCollision ? RconScript.NoclipOff : RconScript.NoclipOn;
            script = script.Select(line => line.ReplacePlaceholders(replacements)).ToArray();

            _gameServer.GameWriter.SendRcon(script);
            _gameServer.PlayerVehicle += RemoveNoClipAsync;
        }

        private Task RemoveNoClipAsync(Player player, Vehicle vehicle)
        {
            if (vehicle != null || player.PreviousVehicle.HasCollision && player.PreviousVehicle.Players.Any())
                return Task.CompletedTask;

            var objectId = player.PreviousVehicle.RootVehicleId;
            player.PreviousVehicle.HasCollision = true;

            var replacements = new Dictionary<string, string>
            {
                {"{OBJECT_ID}", objectId.ToString()}
            };

            var script = RconScript.NoclipOff.Select(line => line.ReplacePlaceholders(replacements)).ToArray();

            _gameServer.GameWriter.SendRcon(script);
            _gameServer.PlayerVehicle -= RemoveNoClipAsync;

            return Task.CompletedTask;
        }

        public void Handle(StalkCommand command)
        {
            _stalker = command.Message.Player;
            _stalked = _gameServer.GetPlayer(command.Name);
            if (_stalked == null)
                return;

            _gameServer.PlayerPosition += StalkPlayerAsync;
        }

        private Task StalkPlayerAsync(Player updatedPlayer, Position position, Rotation rotation, int ping)
        {
            if (updatedPlayer.Index != _stalked.Index)
                return Task.CompletedTask;

            var newPosition = new Position(position.X, position.Height + 10, position.Y);
            _gameServer.GameWriter.SendTeleport(_stalker, newPosition);

            return Task.CompletedTask;
        }

        public void Handle(StopCommand command)
        {
            _gameServer.PlayerPosition -= StalkPlayerAsync;
            StopRecording();
            _playbackCancellation?.Cancel();

            _gameServer.GameWriter.SendRcon(RconScript.InitServer);
        }

        public Task HandleAsync(PadCommand command)
        {
            // TODO: Teleport all full helis back to their respective pad (stack multiple)
            return FreezeAtPadAsync(0, command.Message.Player.Index, _gameServer.Players.ToArray());
        }

        private Task FreezeAtPadAsync(int duration, int requesterPlayerIndex, params Player[] players)
        {
            var usPadPos = new Position(239.8, 166.0, -249.9);
            var usPadRot = new Rotation(-56.8, 0.0, 0.0);

            var chPadPos = new Position(-222.8, 166.0, 151.0);
            var chPadRot = new Rotation(133.5, 0.0, 0.0);

            foreach (var player in players)
            {
                if (player.Vehicle == null || !player.Vehicle.RootVehicleTemplate.Contains("ahe"))
                    continue;

                if (players.Length > 4 && player.Vehicle.Players.Count < 2 && player.Index != requesterPlayerIndex)
                    continue;

                _gameServer.GameWriter.SendHealth(player, 875);
                if (player.Team.Id == 1)
                {
                    _gameServer.GameWriter.SendTeleport(player, chPadPos);
                    _gameServer.GameWriter.SendRotate(player, chPadRot);
                }
                else if (player.Team.Id == 2)
                {
                    _gameServer.GameWriter.SendTeleport(player, usPadPos);
                    _gameServer.GameWriter.SendRotate(player, usPadRot);
                }
            }

            //await Task.Delay(100);

            //foreach (var player in players)
            //{
            //    if (player.SubVehicle == null)
            //        continue;

            //    // TODO: to script file
            //    var objectId = player.SubVehicle.RootVehicleId;
            //    _gameServer.GameWriter.SendRcon(
            //        $"object.active id{objectId}",
            //        $"object.setIsDisabledRecursive 1",
            //        $"object.setIsDisabledRecursive 0"
            //    );
            //}

            return Task.CompletedTask;
        }

        public Task HandleAsync(AutoPadCommand command)
        {
            if (!_roundsActive)
            {
                _gameServer.GameWriter.SendText("2v2 mode is not currently active");
                return Task.CompletedTask;
            }

            var player = _gameServer.GetPlayer(command.Name);
            if (player == null)
            {
                _gameServer.GameWriter.SendText("Player not found");
                return Task.CompletedTask;
            }

            if (_autopadPlayerHashes.ContainsKey(player.Hash))
            {
                _gameServer.GameWriter.SendText($"Autopad disabled for {player.DisplayName}");
                _autopadPlayerHashes.Remove(player.Hash, out _);
            }
            else
            {
                _gameServer.GameWriter.SendText($"Autopad enabled for {player.DisplayName}");
                _autopadPlayerHashes.AddOrUpdate(player.Hash, true, (k, v) => true);
            }

            return Task.CompletedTask;
            //RoundEnded += async e =>
            //{
            //    await Task.Delay(2000);
            //    var winner = e.Round.Players.First(p => p.TeamId == e.Round.WinningTeamId);
            //    await FreezeAtPadAsync(0, winner.Player);
            //};
        }

        public Task HandleAsync(AutoPadAllCommand command)
        {
            if (!_roundsActive)
            {
                _gameServer.GameWriter.SendText("2v2 mode is not currently active");
                return Task.CompletedTask;
            }

            throw new NotImplementedException();
        }

        public Task HandleAsync(NasaCommand command)
        {
            if (command.Value == 0)
            {
                StopAntiNasa();
                return Task.FromResult(0);
            }

            _antiNasaAltitude = command.Value + AltitudeOffset;
            StartAntiNasa();

            return Task.FromResult(0);
        }

        private void StartAntiNasa()
        {
            if (_antiNasaActive)
                _gameServer.PlayerPosition -= OnAntiNasaPlayerPositionAsync;

            _antiNasaActive = true;
            _gameServer.PlayerPosition += OnAntiNasaPlayerPositionAsync;
            _gameServer.GameWriter.SendText($"Anti NASA active ({_antiNasaAltitude - AltitudeOffset} m)");
        }

        private Task OnAntiNasaPlayerPositionAsync(Player player, Position position, Rotation rotation, int ping)
        {
            if (position.Height < _antiNasaAltitude)
                return Task.CompletedTask;

            var newPosition = new Position(position.X, _antiNasaAltitude, position.Y);
            _gameServer.GameWriter.SendTeleport(player, newPosition);

            return Task.CompletedTask;
        }

        private void StopAntiNasa()
        {
            _antiNasaActive = false;
            _gameServer.PlayerPosition -= OnAntiNasaPlayerPositionAsync;
            _gameServer.GameWriter.SendText("Anti NASA inactive");
        }

        private async Task BlurAsync(long duration, params Player[] players)
        {
            var startTime = DateTime.UtcNow;
            var startCount = _stopCounter;
            while (IsActive(duration, startTime, startCount))
            {
                foreach (var player in players)
                {
                    _gameServer.GameWriter.SendGameEvent(player, 13, 1);
                }
                await Task.Delay(DefaultDelay);
            }
        }

        private bool IsActive(long duration, DateTime startTime, int startCount)
        {
            return (DateTime.UtcNow - startTime).TotalMilliseconds < duration && startCount == _stopCounter;
        }

        public void Handle(Toggle2v2Command command)
        {
            if (_roundsActive)
                RemoveRoundEvents();
            else
                SetupRoundEvents();
        }

        // TODO: Recording WIP - might have some use - move to own module
        private Player _playerRecordingTarget;
        private Stopwatch _playerRecordingWatch;
        private IList<(long, Position, Rotation)> _playerRecordingPositions;
        private CancellationTokenSource _playbackCancellation;

        public void Handle(RecordCommand command)
        {
            if (_playerRecordingWatch != null && _playerRecordingWatch.IsRunning)
                StopRecording();
            else
                StartRecording(command);
        }

        private void StartRecording(RecordCommand command)
        {
            _playerRecordingPositions = new List<(long, Position, Rotation)>(1000);
            _playerRecordingTarget = command.Message.Player;
            _playerRecordingWatch = Stopwatch.StartNew();
            _gameServer.PlayerPosition += RecordPlayerAsync;
            _gameServer.GameWriter.SendText("Recording started");
        }

        private void StopRecording()
        {
            _playerRecordingWatch?.Stop();
            _gameServer.PlayerPosition -= RecordPlayerAsync;
            _gameServer.GameWriter.SendText("Recording stopped");
        }

        private Task RecordPlayerAsync(Player player, Position position, Rotation rotation, int ping)
        {
            Log.Information("Recorded player {Position} {Rotation}", position, rotation);
            _playerRecordingPositions.Add((_playerRecordingWatch.ElapsedMilliseconds, position, rotation));

            return Task.CompletedTask;
        }

        public async Task HandleAsync(PlaybackCommand command)
        {
            if (_playerRecordingPositions == null)
            {
                _gameServer.GameWriter.SendText("No recording found");
                return;
            }

            _playbackCancellation?.Cancel();
            _playbackCancellation = new CancellationTokenSource();
            await Task.Run(() =>
            {
                _gameServer.GameWriter.SendText("Started playback");

                StartPlayback();

                _gameServer.GameWriter.SendText("Stopped playback");
            }, _playbackCancellation.Token);
        }

        public async Task HandleAsync(LoopCommand command)
        {
            if (_playerRecordingPositions == null)
            {
                _gameServer.GameWriter.SendText("No recording found");
                return;
            }

            _playbackCancellation?.Cancel();
            _playbackCancellation = new CancellationTokenSource();
            await Task.Run(() =>
            {
                _gameServer.GameWriter.SendText("Started looping playback");

                while (!_playbackCancellation.IsCancellationRequested)
                {
                    StartPlayback();
                }

                _gameServer.GameWriter.SendText("Stopped looping playback");
            }, _playbackCancellation.Token);
        }

        private void StartPlayback()
        {
            //var previousTime = 0L;
            var sw = Stopwatch.StartNew();
            foreach (var snapshot in _playerRecordingPositions)
            {
                //await MultimediaTimer.Delay((int)(snapshot.Item1 - previousTime));
                SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > snapshot.Item1, 1000);
                Log.Error("Variance: {VarianceMs}", (sw.ElapsedMilliseconds - snapshot.Item1));
                _gameServer.GameWriter.SendTeleport(_playerRecordingTarget, snapshot.Item2);
                _gameServer.GameWriter.SendRotate(_playerRecordingTarget, snapshot.Item3);
                if (_playbackCancellation.IsCancellationRequested)
                    break;
                //previousTime = snapshot.Item1;
            }
        }

        public IEnumerable<TvMissileSetting> DefaultTvMissileSettings = new[]
        {
            new TvMissileSetting { Name = "trackingDelay", Template = "ObjectTemplate.seek.trackingDelay {0}", DefaultValue = "0.2" },
            new TvMissileSetting { Name = "maxYaw", Template = "ObjectTemplate.follow.maxYaw {0}", DefaultValue = "1.5" },
            new TvMissileSetting { Name = "maxPitch", Template = "ObjectTemplate.follow.maxPitch {0}", DefaultValue = "1.5" },
            new TvMissileSetting { Name = "changePitch", Template = "ObjectTemplate.follow.changePitch {0}", DefaultValue = "0.2" },
            new TvMissileSetting { Name = "changeYaw", Template = "ObjectTemplate.follow.changeYaw {0}", DefaultValue = "0.2" },
            new TvMissileSetting { Name = "minDist", Template = "ObjectTemplate.follow.minDist {0}", DefaultValue = "10" },
            new TvMissileSetting { Name = "drag", Template = "ObjectTemplate.drag {0}", DefaultValue = "0.1" },
            new TvMissileSetting { Name = "mass", Template = "ObjectTemplate.mass {0}", DefaultValue = "500" },
            //new TvMissileSetting { Name = "followStiffness", Template = "ObjectTemplate.followStiffness {0}", DefaultValue = "6" }, // exists on Camera and doesn't work server side
            new TvMissileSetting { Name = "timeToLive", Template = "ObjectTemplate.timeToLive CRD_NONE/{0}/0/0", DefaultValue = "3.5" },
            new TvMissileSetting { Name = "maxSpeed", Template = "ObjectTemplate.maxSpeed {0}", DefaultValue = "125" },
        };

        public async Task HandleAsync(GetTvMissileValuesCommand command)
        {
            var result = new List<(string, string, string)>();
            foreach (var defaultSetting in DefaultTvMissileSettings)
            {
                _gameServer.GameWriter.SendRcon("ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv");
                var value = await _gameServer.GameWriter.GetRconResponseAsync(
                    //"ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv\n" +
                    defaultSetting.Template.Split(' ').FirstOrDefault()
                );
                result.Add((defaultSetting.Name, value, defaultSetting.DefaultValue));
            }

            _gameServer.GameWriter.SendText($"TV missile settings: {string.Join(", ", result.Take(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})"))}");
            _gameServer.GameWriter.SendText(string.Join(", ", result.Skip(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})")), false);
        }

        public Task HandleAsync(SetTvMissileValueCommand command)
        {
            if (string.IsNullOrEmpty(command.Value))
            {
                return Task.CompletedTask;
            }

            var matchingSetting = DefaultTvMissileSettings.FirstOrDefault(s => s.Name.Equals(command.Name, StringComparison.InvariantCultureIgnoreCase));
            if (matchingSetting == null)
            {
                _gameServer.GameWriter.SendText("Setting not found");
                return Task.CompletedTask;
            }

            _gameServer.GameWriter.SendRcon(
                "ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv",
                string.Format(matchingSetting.Template, command.Value)
            );

            _gameServer.GameWriter.SendText($"TV Missile {matchingSetting.Name} set to {command.Value}");

            return Task.CompletedTask;
        }

        public Task HandleAsync(SetTvMissileTypeCommand command)
        {
            if (command.Type.Contains(" "))
            {
                return Task.CompletedTask;
            }

            if (command.Type == "default")
            {
                SetDefaultTvMissile();
            }
            else if (command.Type == "test")
            {
                _gameServer.GameWriter.SendRcon(
                    "ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv",

                    "ObjectTemplate.seek.trackingDelay 0.2", // 0.2

                    "ObjectTemplate.follow.maxYaw 1.5", // 1.5
                    "ObjectTemplate.follow.maxPitch 1.5", // 1.5
                    "ObjectTemplate.follow.changePitch 100", // 0.2
                    "ObjectTemplate.follow.changeYaw 100", // 0.2
                    "ObjectTemplate.follow.minDist 10", // 10

                    "ObjectTemplate.drag 0", // 0.1
                    "ObjectTemplate.mass 0", // 500

                    "ObjectTemplate.timeToLive CRD_NONE/3.5/0/0", // 3.5
                    "ObjectTemplate.maxSpeed 125" // 125
                );
            }
            else
            {
                _gameServer.GameWriter.SendText("Setting not found");
                return Task.CompletedTask;
            }

            _gameServer.GameWriter.SendText($"TV Missile set to {command.Type}");
            return Task.CompletedTask;
        }

        private void SetDefaultTvMissile()
        {
            _gameServer.GameWriter.SendRcon(
                "ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv",
                "ObjectTemplate.seek.trackingDelay 0.2",
                "ObjectTemplate.follow.maxYaw 1.5",
                "ObjectTemplate.follow.maxPitch 1.5",
                "ObjectTemplate.follow.changePitch 0.2",
                "ObjectTemplate.follow.changeYaw 0.2",
                "ObjectTemplate.follow.minDist 10",
                "ObjectTemplate.drag 0.1",
                "ObjectTemplate.mass 500",
                "ObjectTemplate.timeToLive CRD_NONE/3.5/0/0",
                "ObjectTemplate.maxSpeed 125"
            );
        }


        private readonly ConcurrentDictionary<string, ProjectilePath> _tvLogPlayers = new ConcurrentDictionary<string, ProjectilePath>();

        public void Handle(ToggleTvLogCommand command)
        {
            var player = _gameServer.GetPlayer(command.Name);
            if (player == null)
            {
                _gameServer.GameWriter.SendText("Player not found");
                return;
            }

            if (_tvLogPlayers.ContainsKey(player.Hash))
            {
                _tvLogPlayers.Remove(player.Hash, out _);
                _gameServer.GameWriter.SendText($"Disabled TV logging for {player.DisplayName}");
            }
            else
            {
                _tvLogPlayers.TryAdd(player.Hash, new ProjectilePath());
                _gameServer.GameWriter.SendText($"Enabled TV logging for {player.DisplayName}");
            }
        }

        private static Data.Entities.Match ToMatchEntity(Match match, bool mapRounds = false)
        {
            var result = new Data.Entities.Match
            {
                Id = match.Id,
                ServerId = match.ServerId,
                ServerName = match.ServerName,
                Map = match.Map,
                Type = match.Type,
                MatchStart = match.MatchStart,
                MatchEnd = match.MatchEnd
            };

            var teamAHash = match.TeamHashes.First();
            var teamBHash = match.TeamHashes.Last();

            var firstRound = match.Rounds?.FirstOrDefault();
            var teamAPlayers = firstRound?.Players.Where(p => p.TeamHash == teamAHash);
            var teamBPlayers = firstRound?.Players.Where(p => p.TeamHash == teamBHash);

            result.TeamAHash = teamAHash;
            result.TeamBHash = teamBHash;

            result.TeamAName = string.Join(" and ", teamAPlayers?.Select(p => p.Player.ShortName) ?? Enumerable.Empty<string>());
            result.TeamBName = string.Join(" and ", teamBPlayers?.Select(p => p.Player.ShortName) ?? Enumerable.Empty<string>());

            var finishedRounds = match.Rounds?.Where(r => r.IsFinished).ToList();
            if (finishedRounds != null && finishedRounds.Any())
            {
                result.TeamAScore = finishedRounds.Count(r => r.WinningTeamHash == teamAHash);
                result.TeamBScore = finishedRounds.Count(r => r.WinningTeamHash == teamBHash);

                if (mapRounds)
                {
                    result.Rounds = finishedRounds.Select(ToRoundEntity);
                }
            }

            return result;
        }

        private static Data.Entities.MatchRound ToRoundEntity(Round round)
        {
            var result = new Data.Entities.MatchRound
            {
                Id = round.Id,
                MatchId = round.MatchId,
                WinningTeamId = round.WinningTeamId,
                PositionTrackerInterval = round.PositionTrackerInterval,
                RoundStart = round.RoundStart,
                RoundEnd = round.RoundEnd
            };

            if (round.Players != null && round.Players.Any())
            {
                result.Players = round.Players.Select(p => new MatchRoundPlayer
                {
                    RoundId = p.RoundId,
                    PlayerHash = p.PlayerHash,
                    MatchId = round.MatchId!.Value,
                    PlayerName = p.PlayerName,
                    TeamId = p.TeamId,
                    SubVehicle = p.SubVehicle,
                    SaidGo = p.SaidGo,
                    StartPosition = p.StartPosition,
                    DeathPosition = p.DeathPosition,
                    DeathTime = p.DeathTime,
                    KillerHash = p.KillerHash,
                    KillerWeapon = p.KillerWeapon,
                    KillerPosition = p.KillerPosition,
                    MovementPathJson = Compression.CompressText(JsonConvert.SerializeObject(p.MovementPath.Select(p => p.ToString()))),
                    ProjectilePathsJson = Compression.CompressText(JsonConvert.SerializeObject(p.ProjectilePaths.Select(p => p.Select(pp => pp.ToString())))),
                });
            }

            return result;
        }

        #endregion
    }

    public class ProjectilePath
    {
        public Projectile Projectile { get; set; }
        public IList<RoundPosition> Path { get; set; } = new List<RoundPosition>();
    }

    public class TvMissileSetting
    {
        public string Name { get; set; }
        public string Template { get; set; }
        public string DefaultValue { get; set; }
    }

    public class Match
    {
        public IList<Round> Rounds { get; set; }
        public string[] TeamHashes { get; set; }
        public bool IsStarted => MatchStart != null;
        public bool IsFinished => MatchEnd != null;
        public bool IsActive => IsStarted && !IsFinished;

        // Stats properties
        public Guid Id { get; set; }
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string Map { get; set; }
        public string Type { get; set; }
        public DateTime? MatchStart { get; set; }
        public DateTime? MatchEnd { get; set; }

        public Match()
        {
            Rounds = new List<Round>();
        }
    }

    public class Round
    {
        public IList<RoundPlayer> Players { get; set; }
        public bool IsStarted => RoundStart != null;
        public bool IsFinished => RoundEnd != null;
        public bool IsActive => IsStarted && !IsFinished;
        public DateTime ReadyTime { get; set; }
        public string WinningTeamHash { get; set; }
        public string LosingTeamHash { get; set; }
        public bool IsPendingCompletion { get; set; }

        // Entity properties
        public Guid Id { get; set; }
        public Guid? MatchId { get; set; }
        public int WinningTeamId { get; set; }
        public double PositionTrackerInterval { get; set; }
        //public int LosingTeamId { get; set; }
        //public bool IsDraw { get; set; }
        public DateTime? RoundStart { get; set; }
        public DateTime? RoundEnd { get; set; }

        public Round()
        {
            Players = new List<RoundPlayer>();
        }
    }

    public class RoundPlayer
    {
        // TODO: add vehicle/subvehicle and who made the kills here too
        public Player Player { get; set; }
        public string TeamHash { get; set; }
        public bool IsAlive { get; set; }
        public bool IsReady { get; set; }
        public IList<RoundPosition> LastProjectilePath => ProjectilePaths.LastOrDefault();
        public IList<RoundPosition> MovementPath { get; set; }
        public IList<IList<RoundPosition>> ProjectilePaths { get; set; }
        public int LastProjectileId { get; set; }

        // Entity properties
        public Guid RoundId { get; set; }
        public string PlayerHash { get; set; }
        public string PlayerName { get; set; }
        public int TeamId { get; set; }
        public string SubVehicle { get; set; }
        public bool SaidGo { get; set; }
        public Position StartPosition { get; set; }
        public Position DeathPosition { get; set; }
        public DateTime? DeathTime { get; set; }
        public string KillerHash { get; set; }
        public string KillerWeapon { get; set; }
        public Position KillerPosition { get; set; }
        public string MovementPathJson { get; set; }
        public string ProjectilePathsJson { get; set; }

        public RoundPlayer(Player player, string teamHash, Guid roundId)
        {
            RoundId = roundId;
            Player = player;
            PlayerHash = player.Hash;
            PlayerName = player.Name;
            TeamId = player.Team.Id;
            IsAlive = true;
            IsReady = false;
            TeamHash = teamHash;
            SubVehicle = player.SubVehicleTemplate;
            MovementPath = new List<RoundPosition>();
            ProjectilePaths = new List<IList<RoundPosition>>();
        }

        public override string ToString() => Player?.Name ?? PlayerName;
    }

    public class RoundPosition
    {
        public int Id { get; set; }
        public int Timestamp { get; set; }
        public Position Position { get; set; }
        public Rotation Rotation { get; set; }
        public int Ping { get; set; }
        public override string ToString() => $"{Timestamp}|{Position}|{Rotation}|{Ping}";
    }

    public class MatchStartEvent
    {
        public Match Match { get; set; }
    }

    public class MatchEndEvent
    {
        public Match Match { get; set; }
    }

    public class RoundStartEvent
    {
        public Round Round { get; set; }
    }

    public class RoundEndEvent
    {
        public Round Round { get; set; }
    }

    public class DiscordClient : IDisposable
    {
        private ulong _webhookId;
        private string _webhookToken;
        private Discord.Webhook.DiscordWebhookClient _webhookClient;
        private Discord.WebSocket.DiscordSocketClient _websocketClient;

        public DiscordClient(ulong webhookId, string webhookToken)
        {
            _webhookId = webhookId;
            _webhookToken = webhookToken;
            _webhookClient = new Discord.Webhook.DiscordWebhookClient(_webhookId, _webhookToken);
            _websocketClient = new Discord.WebSocket.DiscordSocketClient();
        }

        //public async Task StartAsync()
        //{
        //    await _websocketClient.LoginAsync(Discord.TokenType.Bot, "bot token");
        //    await _websocketClient.StartAsync();
        //    await Task.Delay(-1);
        //}

        public async Task SendMessageAsync(string text)
        {
            await _webhookClient.SendMessageAsync(text);
        }

        public void Dispose()
        {
            _websocketClient?.Dispose();
        }
    }
}
