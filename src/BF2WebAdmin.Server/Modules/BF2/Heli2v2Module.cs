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
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

// TODO: split match and command handling
namespace BF2WebAdmin.Server.Modules.BF2;

public class Heli2v2Module : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<ChatMessageEvent>,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleEventAsync<PlayerDeathEvent>,
    IHandleEventAsync<PlayerTeamEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleEventAsync<ProjectilePositionEvent>,
    IHandleEventAsync<PlayerVehicleEvent>,
    IHandleEventAsync<MatchStartEvent>,
    IHandleEventAsync<MatchEndEvent>,
    IHandleEventAsync<RoundStartEvent>,
    IHandleEventAsync<RoundEndEvent>,
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
    IHandleCommandAsync<SetTvMissileTypeCommand>,
    IHandleCommandAsync<NoFencesCommand>
{
    private readonly IGameServer _gameServer;
    private readonly IMatchRepository _matchRepository;
    private readonly IReadOnlyPolicyRegistry<string> _policyRegistry;
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

    private Player _stalker;
    private Player _stalked;

    public Heli2v2Module(IGameServer server, IMatchRepository matchRepository, IReadOnlyPolicyRegistry<string> policyRegistry, ILogger<Heli2v2Module> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameServer = server;
        _matchRepository = matchRepository;
        _policyRegistry = policyRegistry;
        ClearMatches();
        SetupRoundEvents();

        //var test = MapStatsRenderer.GetMapMovementPathImage("dalian_2_v_2", null, null);
    }

    private void ClearMatches()
    {
        _switchCounter = 0;
        _stopCounter = 0;
        _antiNasaAltitude = 0;
        _antiNasaActive = false;
        _matches = new List<Match>();
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

    private async void SetupRoundEvents()
    {
        _roundsActive = true;
        await SendDiscordMessageAsync("2v2 mode enabled");
    }

    private string Sanitize(string text)
    {
        return DiscordModule.Sanitize(text);
    }

    private async Task GetMatchStatisticsAsync(Match match)
    {
        // Dalian US pad ground: 255.100/148.400/-258.700
        // Dalian CH pad ground: -218.000/153.900/159.000
        var dalianUsPad = Position.Parse("255.100/148.400/-258.700");
        var dalianChPad = Position.Parse("-218.000/153.900/159.000");

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
                        Logger.LogWarning("player.SubVehicle is null in {StatisticsMethod} for {PlayerName}", nameof(GetMatchStatisticsAsync), player?.Player?.Name);
                    }

                    if (player.SubVehicle != null)
                    {
                        if (player.SubVehicle.ToLower().EndsWith("cogunner")) playerStats[player.Player.Hash].GunnerCount++;
                        else playerStats[player.Player.Hash].PilotCount++;
                    }

                    if (match.Map == "dalian_2_v_2")
                    {
                        playerStats[player.Player.Hash].TotalDistanceFromPad += player.StartPosition?.Distance(player.TeamId == 2 ? dalianUsPad : dalianChPad) ?? 0;
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
            Logger.LogInformation("Sending match results for {MatchId}", match.Id);
            await SendDiscordEmbedAsync(builder.Build());
        }
        else
        {
            Logger.LogInformation("Not sending match results for {MatchId} (duration: {Duration}, rounds: {RoundCount})", match.Id, duration, match.Rounds.Count);
        }
    }

    private static string FormatPercent(int a, int b)
    {
        var value = 100d * ((double)a / b);
        return value < 0.5 ? "0%" : $"{value:##}%";
    }

    private Match GetActiveMatch()
    {
        return _matches.FirstOrDefault(m => m.IsActive);
    }

    private Round GetActiveRound()
    {
        return GetActiveMatch()?.Rounds.FirstOrDefault(r => r.IsActive);
    }
        
    private async void RemoveRoundEvents()
    {
        _roundsActive = false;
        await SendDiscordMessageAsync("2v2 mode disabled");
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
        
    private bool enableEndOfRoundTeleport = true;
        
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

    private ValueTask HandleRoundKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
    {
        // TODO: detect draws - wait 1 s before declaring winner - or detect patterns - check vehicle status/id?
        //var round = _rounds.FirstOrDefault(r => r.IsActive);
        var match = GetActiveMatch();
        var round = GetActiveRound();
        if (round == null)
            return ValueTask.CompletedTask;

        var roundVictim = round.Players.FirstOrDefault(p => p.Player.Hash == victim.Hash);
        if (roundVictim == null)
            return ValueTask.CompletedTask;

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
            return ValueTask.CompletedTask;

        lock (round)
        {
            if (round.IsPendingCompletion)
                return ValueTask.CompletedTask;

            round.IsPendingCompletion = true;

            RunBackgroundTask("Handle 2v2 round completion", async () =>
            {
                // Wait 2 seconds and see if it's a draw
                await Task.Delay(2000);

                // Check if all players are dead or outside the heli (could be "down" but not yet dead)
                var allPlayersDead = round.Players.All(p => !p.IsAlive || p.Player.Vehicle is null);
                var team1DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[0]).DeathPosition;
                var team2DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[1]).DeathPosition;
                var isHeliRam = allPlayersDead && team1DeathPosition.Distance(team2DeathPosition) < 10;
                var allKilledByTv = round.Players.All(p => p.KillerWeapon?.ToLower().EndsWith("tv") ?? false);
                var anyPlayersBailed = round.Players.Any(p => p.Player.Vehicle is null);

                if (allPlayersDead && (allKilledByTv || isHeliRam || anyPlayersBailed))
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

                //await _roundEndEvent.InvokeAsync(new RoundEndEvent { Round = round });
                await Mediator.PublishAsync(new RoundEndEvent(round, DateTimeOffset.UtcNow));
            });
        }

        return ValueTask.CompletedTask;
    }

    private static string GetTeamHash(Player p1, Player p2) => GetTeamHash(p1.Hash, p2.Hash);

    private static string GetTeamHash(IEnumerable<string> playerHashes) => GetTeamHash(playerHashes.First(), playerHashes.Last());

    private static string GetTeamHash(string hash1, string hash2)
    {
        return string.Compare(hash1, hash2, StringComparison.Ordinal) <= 0 ? hash1 + hash2 : hash2 + hash1;
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
        var player = _gameServer.GetPlayerByName(command.Name);
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
    }

    private ValueTask RemoveNoClipAsync(Player? player, Vehicle? vehicle)
    {
        if (vehicle is not null || player is null || player.PreviousVehicle.HasCollision && player.PreviousVehicle.Players.Any())
            return ValueTask.CompletedTask;

        var objectId = player.PreviousVehicle.RootVehicleId;
        player.PreviousVehicle.HasCollision = true;

        var replacements = new Dictionary<string, string>
        {
            {"{OBJECT_ID}", objectId.ToString()}
        };

        var script = RconScript.NoclipOff.Select(line => line.ReplacePlaceholders(replacements)).ToArray();

        _gameServer.GameWriter.SendRcon(script);

        return ValueTask.CompletedTask;
    }

    public void Handle(StalkCommand command)
    {
        _stalker = command.Message.Player;
        _stalked = _gameServer.GetPlayerByName(command.Name);
        if (_stalked == null)
            return;

    }

    public void Handle(StopCommand command)
    {
        _stalker = null;
        _stalked = null;
        StopRecording();
        _playbackCancellation?.Cancel();

        _gameServer.GameWriter.SendRcon(RconScript.InitServer);
    }

    public async ValueTask HandleAsync(PadCommand command)
    {
        // TODO: Teleport all full helis back to their respective pad (stack multiple)
        await FreezeAtPadAsync(0, command.Message.Player.Index, _gameServer.Players.ToArray());
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
        //    // Not very stable if I remember correctly
        //    var objectId = player.SubVehicle.RootVehicleId;
        //    _gameServer.GameWriter.SendRcon(
        //        $"object.active id{objectId}",
        //        $"object.setIsDisabledRecursive 1",
        //        $"object.setIsDisabledRecursive 0"
        //    );
        //}

        return Task.CompletedTask;
    }

    public ValueTask HandleAsync(AutoPadCommand command)
    {
        if (!_roundsActive)
        {
            _gameServer.GameWriter.SendText("2v2 mode is not currently active");
            return ValueTask.CompletedTask;
        }

        var player = _gameServer.GetPlayerByName(command.Name);
        if (player == null)
        {
            _gameServer.GameWriter.SendText("Player not found");
            return ValueTask.CompletedTask;
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

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(AutoPadAllCommand command)
    {
        if (!_roundsActive)
        {
            _gameServer.GameWriter.SendText("2v2 mode is not currently active");
            return ValueTask.CompletedTask;
        }

        throw new NotImplementedException();
    }

    public ValueTask HandleAsync(NasaCommand command)
    {
        if (command.Value == 0)
        {
            StopAntiNasa();
            return ValueTask.CompletedTask;
        }

        _antiNasaAltitude = command.Value + AltitudeOffset;
        StartAntiNasa();

        return ValueTask.CompletedTask;
    }

    private void StartAntiNasa()
    {
        _antiNasaActive = true;
        _gameServer.GameWriter.SendText($"Anti NASA active ({_antiNasaAltitude - AltitudeOffset} m)");
    }

    private void StopAntiNasa()
    {
        _antiNasaActive = false;
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
        _gameServer.GameWriter.SendText("Recording started");
    }

    private void StopRecording()
    {
        _playerRecordingWatch?.Stop();
        _gameServer.GameWriter.SendText("Recording stopped");
    }

    public async ValueTask HandleAsync(PlaybackCommand command)
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

    public async ValueTask HandleAsync(LoopCommand command)
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
        // TODO: get new timestamps from events
        //var previousTime = 0L;
        var sw = Stopwatch.StartNew();
        foreach (var snapshot in _playerRecordingPositions)
        {
            if (ModuleCancellationToken.IsCancellationRequested)
                return;
            
            //await MultimediaTimer.Delay((int)(snapshot.Item1 - previousTime));
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > snapshot.Item1, 1000);
            Logger.LogError("Variance: {VarianceMs}", (sw.ElapsedMilliseconds - snapshot.Item1));
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

    public async ValueTask HandleAsync(GetTvMissileValuesCommand command)
    {
        var result = new List<(string, string, string)>();
        foreach (var defaultSetting in DefaultTvMissileSettings)
        {
            _gameServer.GameWriter.SendRcon("ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv");
            var value = await _gameServer.GameWriter.GetRconResponseAsync(
                defaultSetting.Template.Split(' ').FirstOrDefault()
            );
            result.Add((defaultSetting.Name, value, defaultSetting.DefaultValue));
        }

        _gameServer.GameWriter.SendText($"TV missile settings: {string.Join(", ", result.Take(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})"))}");
        _gameServer.GameWriter.SendText(string.Join(", ", result.Skip(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})")), false);
    }

    public ValueTask HandleAsync(SetTvMissileValueCommand command)
    {
        if (string.IsNullOrEmpty(command.Value))
        {
            return ValueTask.CompletedTask;
        }

        var matchingSetting = DefaultTvMissileSettings.FirstOrDefault(s => s.Name.Equals(command.Name, StringComparison.InvariantCultureIgnoreCase));
        if (matchingSetting == null)
        {
            _gameServer.GameWriter.SendText("Setting not found");
            return ValueTask.CompletedTask;
        }

        _gameServer.GameWriter.SendRcon(
            "ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv",
            string.Format(matchingSetting.Template, command.Value)
        );

        _gameServer.GameWriter.SendText($"TV Missile {matchingSetting.Name} set to {command.Value}");

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(SetTvMissileTypeCommand command)
    {
        if (command.Type.Contains(" "))
        {
            return ValueTask.CompletedTask;
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
            return ValueTask.CompletedTask;
        }

        _gameServer.GameWriter.SendText($"TV Missile set to {command.Type}");
        return ValueTask.CompletedTask;
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

    private readonly ConcurrentDictionary<string, ProjectilePath> _tvLogPlayers = new();

    public void Handle(ToggleTvLogCommand command)
    {
        var player = _gameServer.GetPlayerByName(command.Name);
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
                result.MatchRounds = finishedRounds.Select(ToRoundEntity).ToList();
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
            result.MatchRoundPlayers = round.Players.Select(p => new MatchRoundPlayer
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
            }).ToList();
        }

        return result;
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        if (_roundsActive)
        {
            var match = GetActiveMatch();
            if (match != null && match.Map != e.Map.Name)
            {
                match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                await Mediator.PublishAsync(new MatchEndEvent(match, DateTimeOffset.UtcNow));
            }
        }

        _gameServer.GameWriter.SendRcon(RconScript.InitServer);
        _gameServer.GameWriter.SendTimerInterval(DefaultPositionTrackerInterval);
        SetDefaultTvMissile();
        ClearMatches();
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        if (!_roundsActive)
            return;

        if (e.Message.Type != MessageType.Player || e.Message.Channel != ChatChannel.Global)
            return;

        const string padPattern = @"^j?pad$";
        var isPadMatch = Regex.IsMatch(e.Message.Text, padPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (isPadMatch)
        {
            RemoveActiveRoundWithPlayer(e.Message.Player.Hash);
            return;
        }

        // TODO: fix 2 players ready on 1 team then switching
        const string readyPattern = @"^j?[^0-9A-Za-z]*(?!gg)(([ready]{1,8}|[go]{1,5})[A-z0-9\?\!\.]{0,2})[^0-9A-Za-z]*$";
        const string readyPatternWithOtherText = @"(^(j?rd[y]?|j?g[o]?)[ ]+)|[ ]+(rd[y]?|g[o]?)$";

        var isReadyMatch = Regex.IsMatch(e.Message.Text, readyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var isReadyWithOtherMatch = Regex.IsMatch(e.Message.Text, readyPatternWithOtherText, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if ((!isReadyMatch && !isReadyWithOtherMatch) || e.Message.Text == "yes")
            return;

        var teamPlayers = e.Message.Player.Vehicle?.Players ?? Array.Empty<Player>();
        if ((teamPlayers.Count) < 2)
            return;

        if (teamPlayers!.Any(p => p.Team.Id != teamPlayers[0].Team.Id))
        {
            var isFixed = await _gameServer.FixTeamMismatchAsync(teamPlayers);
            if (!isFixed)
            {
                // TODO: figure ut why this is not 100% fixed
                _gameServer.GameWriter.SendText($"Team mismatch for {string.Join(" & ", teamPlayers.Select(p => $"{p.ShortName}"))} - switch team to fix match");
                await SendDiscordMessageAsync($"2v2 team ID mismatch for {string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}"))}");
                Logger.LogWarning("2v2 team ID mismatch for {PlayerTeamNames}", string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}")));
            }

            return;
        }

        if (_pendingRound != null && (DateTime.UtcNow - _pendingRound?.ReadyTime) > TimeSpan.FromMinutes(5))
        {
            Logger.LogDebug("Pending round was over 5 minutes - clearing");
            _pendingRound = null;
        }

        var newReady = true;
        if (_pendingRound == null)
        {
            _pendingRound = new Round
            {
                Id = Guid.NewGuid(),
                ReadyTime = DateTime.UtcNow,
                PositionTrackerInterval = _gameServer.GameWriter.CurrentTrackerInterval
            };
        }

        var teamHash = GetTeamHash(teamPlayers[0], teamPlayers[1]);
        foreach (var vehiclePlayer in e.Message.Player.Vehicle!.Players)
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
            var goTeamId = e.Message.Player.Team.Id;
            foreach (var roundPlayer in _pendingRound.Players)
            {
                roundPlayer.SaidGo = goTeamId == roundPlayer.TeamId;
                roundPlayer.SubVehicle = roundPlayer.Player.SubVehicleTemplate;
                roundPlayer.StartPosition = roundPlayer.Player.Position;
            }

            if (_pendingRound.MatchId == null)
            {
                await SetCurrentMatchAsync();
            }

            await Mediator.PublishAsync(new RoundStartEvent(_pendingRound, DateTimeOffset.UtcNow));

            _pendingRound = null;
        }
        else if (newReady)
        {
            await SendDiscordMessageAsync($"{e.Message.Player.Team?.Name} READY");
        }

        async Task SetCurrentMatchAsync()
        {
            if (_matches.Count == 0)
            {
                await TryResumePreviousMatchAsync();
            }
            
            var teamHashes = _pendingRound.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
            var match = _matches.FirstOrDefault(m => m.IsActive && m.TeamHashes.Contains(teamHashes[0]) && m.TeamHashes.Contains(teamHashes[1]));
            if (match == null)
            {
                var previousMatch = GetActiveMatch();
                if (previousMatch != null)
                {
                    previousMatch.MatchEnd = previousMatch.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                    await Mediator.PublishAsync(new MatchEndEvent(previousMatch, DateTimeOffset.UtcNow));
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

                await Mediator.PublishAsync(new MatchStartEvent(match, DateTimeOffset.UtcNow));
            }
            else
            {
                match.Rounds.Add(_pendingRound);
                _pendingRound.MatchId = match.Id;
            }
        }

        async Task TryResumePreviousMatchAsync()
        {
            // Try to resume the previous unfinished match from the database if it has the same players, server, map and is within 10 minutes (BF2WA restarted)
            try
            {
                var teamHashes = _pendingRound.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
                var previousMatchDb = (await _matchRepository.GetMatchesByNewestAsync(0, 1)).FirstOrDefault();
                if (previousMatchDb is { MatchEnd: null })
                {
                    previousMatchDb = await _matchRepository.GetMatchAsync(previousMatchDb.Id);
                    var previousTeamHashesUnsorted = new []{ previousMatchDb.TeamAHash, previousMatchDb.TeamBHash };
                    var previousMatch = new Match
                    {
                        Id = previousMatchDb.Id,
                        MatchStart = previousMatchDb.MatchStart,
                        Map = previousMatchDb.Map,
                        ServerId = previousMatchDb.ServerId,
                        ServerName = previousMatchDb.ServerName,
                        TeamHashes = previousTeamHashesUnsorted,
                        Type = previousMatchDb.Type,
                        Rounds = previousMatchDb.MatchRounds?.Select(r => new Round
                        {
                            Id = r.Id,
                            MatchId = r.MatchId,
                            RoundStart = r.RoundStart,
                            RoundEnd = r.RoundEnd,
                            WinningTeamId = r.WinningTeamId,
                            WinningTeamHash = GetTeamHash(r.MatchRoundPlayers.Where(p => p.TeamId == r.WinningTeamId).Select(p => p.PlayerHash)),
                            LosingTeamHash = GetTeamHash(r.MatchRoundPlayers.Where(p => p.TeamId != r.WinningTeamId).Select(p => p.PlayerHash)),
                            PositionTrackerInterval = r.PositionTrackerInterval,
                            Players = r.MatchRoundPlayers?.Select(p =>
                            {
                                var player = _gameServer.GetPlayerByHash(p.PlayerHash);
                                return new RoundPlayer(player, previousTeamHashesUnsorted.FirstOrDefault(h => h.Contains(player.Hash)), r.Id)
                                {
                                    SaidGo = p.SaidGo,
                                    DeathPosition = p.DeathPosition,
                                    DeathTime = p.DeathTime,
                                    KillerHash = p.KillerHash,
                                    KillerWeapon = p.KillerWeapon,
                                    KillerPosition = p.KillerPosition,
                                    StartPosition = p.StartPosition
                                };
                            }).ToList() ?? new List<RoundPlayer>()
                        }).ToList() ?? new List<Round>()
                    };

                    var previousTeamHashes = previousMatch.Rounds.FirstOrDefault()?.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
                    var previousMatchHasSameTeams = teamHashes.All(hash => previousTeamHashes?.Any(oldHash => oldHash == hash) ?? false);
                    var mostRecentRoundEnd = previousMatch.Rounds.MaxBy(r => r.RoundEnd)?.RoundEnd ?? DateTime.UtcNow;
                    var shouldResumePreviousMatch = previousMatchHasSameTeams &&
                        (_pendingRound.RoundStart - mostRecentRoundEnd) < TimeSpan.FromMinutes(10) &&
                        previousMatch.Map == _gameServer.Map.Name &&
                        previousMatch.ServerId == _gameServer.Id;

                    if (shouldResumePreviousMatch)
                    {
                        _matches.Add(previousMatch);
                        previousMatch.Rounds.Add(_pendingRound);
                        _pendingRound.MatchId = previousMatch.Id;
                        _gameServer.GameWriter.SendText("Previous 2v2 match has been resumed");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to resume previous match");
            }
        }
    }

    public ValueTask HandleAsync(PlayerKillEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        return HandleRoundKillAsync(e.Attacker, e.AttackerPosition, e.Victim, e.VictimPosition, e.Weapon);
    }

    public ValueTask HandleAsync(PlayerDeathEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        return HandleRoundKillAsync(null, null, e.Player, e.Position, null);
    }

    public ValueTask HandleAsync(PlayerTeamEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        RemoveActiveRoundWithPlayer(e.Player.Hash);
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        if (!_roundsActive)
            return;

        RemoveActiveRoundWithPlayer(e.Player.Hash);

        var match = GetActiveMatch();
        if (match != null)
        {
            var onlinePlayersCount = match.Rounds.FirstOrDefault()?.Players.Count(p => _gameServer.Players.Any(gp => gp.Hash == p.Player.Hash));
            if (onlinePlayersCount <= 2)
            {
                match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? DateTime.UtcNow;
                await Mediator.PublishAsync(new MatchEndEvent(match, DateTimeOffset.UtcNow));
            }
        }
    }

    public ValueTask HandleAsync(PlayerPositionEvent e)
    {
        if (_roundsActive)
        {
            var round = GetActiveRound();
            if (round != null)
            {
                var timestamp = (DateTime.UtcNow - round.RoundStart!.Value).TotalMilliseconds;
                round.Players
                    .FirstOrDefault(p => p.Player.Hash == e.Player.Hash)
                    ?.MovementPath.Add(new RoundPosition
                    {
                        Id = e.Player.Index,
                        Timestamp = (int)timestamp,
                        Position = e.Position,
                        Rotation = e.Rotation,
                        Ping = e.Player.Score.Ping
                    });
            }
        }

        if (_stalker is not null && _stalked is not null)
        {
            if (e.Player.Index == _stalked.Index)
            {
                var newPosition = new Position(e.Position.X, e.Position.Height + 10, e.Position.Y);
                _gameServer.GameWriter.SendTeleport(_stalker, newPosition);
            }
        }

        if (_antiNasaActive)
        {
            if (e.Position.Height >= _antiNasaAltitude)
            {
                var newPosition = new Position(e.Position.X, _antiNasaAltitude, e.Position.Y);
                _gameServer.GameWriter.SendTeleport(e.Player, newPosition);
            }
        }

        if (_playerRecordingWatch != null && _playerRecordingWatch.IsRunning)
        {
            Logger.LogInformation("Recorded player {Position} {Rotation}", e.Position, e.Rotation);
            _playerRecordingPositions.Add((_playerRecordingWatch.ElapsedMilliseconds, e.Position, e.Rotation));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ProjectilePositionEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        var now = DateTime.UtcNow;
        if (e.Projectile.Owner == null)
            return ValueTask.CompletedTask;

        var round = GetActiveRound();
        var roundPlayer = round?.Players.FirstOrDefault(p => p.Player.Hash == e.Projectile.Owner.Hash);
        var isLoggedTv = _tvLogPlayers.TryGetValue(e.Projectile.Owner.Hash, out var path);
        if (roundPlayer == null && !isLoggedTv)
            return ValueTask.CompletedTask;

        var timestamp = (now - (round?.RoundStart ?? now)).TotalMilliseconds;
        var roundPosition = new RoundPosition
        {
            Id = e.Projectile.Id,
            Timestamp = (int)timestamp,
            Position = e.Projectile.Position,
            Rotation = e.Projectile.Rotation,
            Ping = e.Projectile.Owner.Score.Ping
        };

        if (isLoggedTv)
        {
            if (path.Projectile?.Id != e.Projectile.Id)
            {
                path.Projectile = e.Projectile;
                path.Path.Clear();
                RunBackgroundTask("Send projectile message", async () =>
                {
                    // TODO: get max X and Y angles per s or ms and see what you can get max as default
                    await Task.Delay(4000);
                    var (distance, angle) = CalculateTvStats(path.Path, true);
                    _gameServer.GameWriter.SendText($"{e.Projectile.Owner.ShortName} TV: est. {distance:###} m {angle:###} degrees");
                });
            }

            path.Path.Add(roundPosition);
            //_gameServer.GameWriter.SendText($"P:{position} R:{rotation}");
        }

        if (roundPlayer != null)
        {
            if (roundPlayer.LastProjectileId != e.Projectile.Id)
            {
                roundPlayer.ProjectilePaths.Add(new List<RoundPosition>());
                //roundPlayer.LastProjectilePath.Clear();
                roundPlayer.LastProjectileId = e.Projectile.Id;
            }

            //roundPlayer.LastProjectilePath.Add((position, rotation));
            roundPlayer.ProjectilePaths.Last().Add(roundPosition);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerVehicleEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        // TODO: check if player is the last in the vehicle
        return RemoveNoClipAsync(e.Player, e.Vehicle);
    }

    public async ValueTask HandleAsync(MatchStartEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving new match {MatchId}", e.Match.Id);

        await _policyRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
            await _matchRepository.InsertMatchAsync(ToMatchEntity(e.Match))
        );
        await SendDiscordMessageAsync("New 2v2 match started");
        _gameServer.GameWriter.SendText("New 2v2 match started");
    }

    public async ValueTask HandleAsync(MatchEndEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving match end results for {MatchId}", e.Match.Id);

        await _policyRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
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
            Logger.LogError(ex, "Failed to get match statistics");
        }
    }

    public async ValueTask HandleAsync(RoundStartEvent e)
    {
        if (!_roundsActive)
            return;

        await SendDiscordMessageAsync($"GO");
    }

    public async ValueTask HandleAsync(RoundEndEvent e)
    {
        if (!_roundsActive)
            return;

        try
        {
            var winningTeam = _gameServer.Teams.FirstOrDefault(t => t.Id == e.Round.WinningTeamId);
            var timeDiff = e.Round.RoundEnd!.Value - e.Round.RoundStart!.Value;

            var match = GetActiveMatch();

            var teamAHash = match.TeamHashes.First();
            var teamBHash = match.TeamHashes.Last();

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

            var isCrash = e.Round.WinningTeamId != -1 && e.Round.Players.Where(p => p.TeamHash == e.Round.LosingTeamHash).All(p => p.KillerWeapon == null);

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

            if (enableEndOfRoundTeleport)
            {
                RunBackgroundTask("Handle end of round teleport", async () =>
                {
                    var winningPlayers = e.Round.Players.Where(p => p.TeamHash == e.Round.WinningTeamHash && p.IsAlive).ToList();
                    if (winningPlayers.Any(p => _autopadPlayerHashes.ContainsKey(p.Player.Hash)))
                    {
                        await Task.Delay(2000);
                        await FreezeAtPadAsync(0, 0, winningPlayers.First().Player);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error at end of round event");
        }

        RunBackgroundTask("Save match round", async () =>
        {
            Logger.LogInformation("Saving round {RoundId} on match {MatchId}", e.Round.Id, e.Round.MatchId);

            await _policyRegistry.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(async () =>
                await _matchRepository.InsertRoundAsync(ToRoundEntity(e.Round))
            );
        });

        // Switch command
        var shouldSwitchTeams = _switchRounds > 0 && ++_switchCounter >= _switchRounds;
        if (shouldSwitchTeams)
        {
            _switchRounds = 0;
            SwitchAll();
        }
    }

    public async ValueTask HandleAsync(NoFencesCommand command)
    {
        var fenceTemplates = new[]
        {
            "fence_corrugated_3x12m",
            "fence_corrugated_3x12m_broken",
            "fence_corrugated_3x12m_broken_parts",
            "wirefence_72m",
            "wirefence_end",
            "wirefence_48m",
            "wirefence_24m",
            "fence_corrugated_pole_3m",
            "fence_corrugated_3x48m",
            "fence_corrugated_3x24m",
            "fence_corrugated_3x12m_corner_02",
            "fence_corrugated_3x12m_corner_01",
            "lamppost_highway_01"
        };

        var removedObjects = 0;

        foreach (var fenceTemplate in fenceTemplates)
        {
            var response = await GameServer.GameWriter.GetRconResponseAsync("object.listObjectsOfTemplate " + fenceTemplate);
            foreach (var line in response.Split('\b'))
            {
                var match = Regex.Match(line, "ID ([0-9]+)");
                if (!match.Success)
                    continue;
                
                var objectId = match.Groups[1].Value;
                // GameServer.GameWriter.SendText(objectId + " | " + line);
                GameServer.GameWriter.SendRcon(
                    $"object.active id{objectId}",
                    "object.delete"
                );

                removedObjects++;
            }
        }
        
        GameServer.GameWriter.SendText($"Removed {removedObjects} fences and lamp posts");
    }
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
    public IList<RoundPlayer> Players { get; set; } = new List<RoundPlayer>();
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
}

public class RoundPlayer
{
    // TODO: add vehicle/subvehicle and who made the kills here too
    public Player Player { get; set; }
    public string TeamHash { get; set; }
    public bool IsAlive { get; set; }
    public bool IsReady { get; set; }
    public IList<RoundPosition> LastProjectilePath => ProjectilePaths.LastOrDefault() ?? new List<RoundPosition>();
    public IList<RoundPosition> MovementPath { get; set; } = new List<RoundPosition>();
    public IList<IList<RoundPosition>> ProjectilePaths { get; set; } = new List<IList<RoundPosition>>();
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
