using System.Text;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Extensions;
using Discord;
using Humanizer;
using Humanizer.Localisation;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>A match's summary for Discord: the text for the debug channels, and the embed, posted only for a long match.</summary>
public sealed record MatchReport(string Text, Embed Embed, bool PostEmbed);

/// <summary>The 2v2 match and round summaries. They only read the match; the module sends them.</summary>
public static class MatchStatistics
{
    /// <summary>The height of the ground on Dalian, subtracted from a position's height for an altitude.</summary>
    public const int AltitudeOffset = 140;

    private static readonly Position DalianUsPad = Position.Parse("255.100/148.400/-258.700");
    private static readonly Position DalianChPad = Position.Parse("-218.000/153.900/159.000");

    public static MatchReport Build(Match match, DateTimeOffset now, ILogger logger)
    {
        var builder = new EmbedBuilder();
        builder.WithColor(Color.Blue);

        var duration = (match.MatchEnd ?? now) - match.MatchStart!.Value;

        var draws = 0;
        var roundTotalDuration = 0d;
        var biggestTvCurveKill = ((RoundPlayer?)null, 0d, 0d);
        var longestHydraKill = ((RoundPlayer?)null, 0d);
        var longestRoundDuration = 0d;
        var shortestRoundDuration = double.MaxValue;
        var teamStats = match.TeamHashes.ToDictionary(h => h, h => new TeamStats { TeamHash = h });
        var playerStats = match.Rounds.First().Players.ToDictionary(player => player.Player.Hash, player => new PlayerStats { PlayerHash = player.Player.Hash });

        foreach (var round in match.Rounds.Where(r => !r.IsActive))
        {
            var roundDuration = ((round.RoundEnd ?? now) - round.RoundStart!.Value).TotalSeconds;
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
                        logger.LogWarning("player.SubVehicle is null in {StatisticsMethod} for {PlayerName}", nameof(Build), player?.Player?.Name);
                    }

                    if (player.SubVehicle != null)
                    {
                        if (player.SubVehicle.ToLower().EndsWith("cogunner")) playerStats[player.Player.Hash].GunnerCount++;
                        else playerStats[player.Player.Hash].PilotCount++;
                    }

                    if (match.Map == "dalian_2_v_2")
                    {
                        playerStats[player.Player.Hash].TotalDistanceFromPad += player.StartPosition?.Distance(player.TeamId == 2 ? DalianUsPad : DalianChPad) ?? 0;
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
                        var tvStats = TvShot(player.LastProjectilePath);
                        if (tvStats.Angle > biggestTvCurveKill.Item3) biggestTvCurveKill = (player, tvStats.Distance, tvStats.Angle);
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
        sb.AppendLine($"# 2v2 ended after {((match.MatchEnd ?? now) - match.MatchStart.Value).Humanize()} on {match.ServerName} #\n");

        builder.WithTitle("View match statistics");
        builder.Url = $"https://bf2.nihlen.net/matches/{match.Id.ToString().ToLower()}";

        builder.WithDescription($"2v2 ended after {((match.MatchEnd ?? now) - match.MatchStart.Value).Humanize(2, minUnit: TimeUnit.Minute)} on {match.ServerName}\n​");

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
                $"`{stats.Wins,3} ` ​ ​ ​ {playerNamesEmbed} {(stats.Wins == maxWins ? "​ :trophy:" : string.Empty)}",
                "​"
            );

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

        // Only a real match: over 10 minutes and 10 rounds
        var postEmbed = duration > TimeSpan.FromMinutes(10) && match.Rounds.Count > 10;
        return new MatchReport(sb.ToString(), builder.Build(), postEmbed);
    }

    /// <summary>The Discord line of a finished round: its result, the kill and the match score.</summary>
    public static string RoundSummary(Match match, Round round, string? winningTeamName)
    {
        var timeDiff = round.RoundEnd!.Value - round.RoundStart!.Value;

        var teamAHash = match.TeamHashes.First();
        var teamBHash = match.TeamHashes.Last();

        var teamAWins = match.Rounds.Count(r => r.WinningTeamHash == teamAHash && r.LosingTeamHash == teamBHash);
        var teamBWins = match.Rounds.Count(r => r.WinningTeamHash == teamBHash && r.LosingTeamHash == teamAHash);

        var teamAIndicator = teamAHash == round.WinningTeamHash ? "+" : string.Empty;
        var teamBIndicator = teamBHash == round.WinningTeamHash ? "+" : string.Empty;

        var teamAPlayers = round.Players.Where(p => p.TeamHash == teamAHash);
        var teamBPlayers = round.Players.Where(p => p.TeamHash == teamBHash);

        var totalRounds = match.Rounds.Count(r => r.Players.Any(p => p.TeamHash == teamAHash) && r.Players.Any(p => p.TeamHash == teamBHash));

        var tvKillerHash = round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("tv") ?? false)?.KillerHash;
        var tvAttacker = round.Players.FirstOrDefault(p => p.Player.Hash == tvKillerHash);
        var killTvPath = round.Players.FirstOrDefault(p => p.TeamHash == round.WinningTeamHash && p.ProjectilePaths.Any())?.LastProjectilePath;
        var (killTvDistance, killTvAngle) = tvAttacker != null ? TvShot(killTvPath) : (0d, 0d);

        var victimPlayer = round.Players.FirstOrDefault(p => p.TeamHash == round.LosingTeamHash);
        var hydraKillerHash = round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("launcher") ?? false)?.KillerHash;
        var hydraAttacker = round.Players.FirstOrDefault(p => p.Player.Hash == hydraKillerHash);
        var killHydraDistance = 0d;
        if (victimPlayer != null && hydraAttacker != null)
        {
            killHydraDistance = victimPlayer.KillerPosition.Distance(victimPlayer.DeathPosition);
        }

        var mgKillerHash = round.Players.FirstOrDefault(p => p.KillerWeapon?.ToLower().EndsWith("gun") ?? false)?.KillerHash;
        var mgAttacker = round.Players.FirstOrDefault(p => p.Player.Hash == mgKillerHash);
        var killMgDistance = 0d;
        if (victimPlayer != null && mgAttacker != null)
        {
            killMgDistance = victimPlayer.KillerPosition.Distance(victimPlayer.DeathPosition);
        }

        var unknownKillVictim = tvAttacker == null && hydraAttacker == null && mgAttacker == null
            ? round.Players.Where(p => p.TeamHash == round.LosingTeamHash).FirstOrDefault(p => p.KillerWeapon != null)
            : null;

        var isCrash = round.WinningTeamId != -1 && round.Players.Where(p => p.TeamHash == round.LosingTeamHash).All(p => p.KillerWeapon == null);

        var sb = new StringBuilder();

        if (round.WinningTeamId == -1)
        {
            sb.Append($"```md\nRound {totalRounds} ended in a CLASSIC DRAW in {(int)timeDiff.TotalSeconds} s\n\n");
        }
        else
        {
            sb.Append($"```md\nRound {totalRounds} won by {winningTeamName ?? "?"} in {(int)timeDiff.TotalSeconds} s\n\n");
            sb.Append(killTvDistance > 0 ? $"TV: est. {killTvDistance:###}m {killTvAngle:###}°\n\n" : string.Empty);
            sb.Append(killHydraDistance > 0 ? $"Hydra: est. {killHydraDistance:###}m\n\n" : string.Empty);
            sb.Append(killMgDistance > 0 ? $"MG: est. {killMgDistance:###}m\n\n" : string.Empty);
            sb.Append(unknownKillVictim != null ? $"Unknown weapon: {unknownKillVictim?.KillerWeapon}\n\n" : string.Empty);
            sb.Append(isCrash ? "Crashed\n\n" : string.Empty);
        }

        sb.Append($"<{string.Join("> <", teamAPlayers.Select(p => p.Player.ShortName))}> {teamAWins}{teamAIndicator} vs ");
        sb.Append($"{teamBWins}{teamBIndicator} <{string.Join("> <", teamBPlayers.Select(p => p.Player.ShortName))}>```");

        return sb.ToString();
    }

    /// <summary>A missile's flight from its samples: the distance flown and the sum of its turns.</summary>
    public static (double Distance, double Angle) TvShot(IList<RoundPosition>? path)
    {
        double distance = 0d, angle = 0d;
        if (path == null || !path.Any())
            return (distance, angle);

        var previousPosition = path[0].Position;
        var previousRotation = path[0].Rotation;
        foreach (var sample in path)
        {
            distance += previousPosition.Distance(sample.Position);
            angle += previousRotation.Angle(sample.Rotation);
            previousPosition = sample.Position;
            previousRotation = sample.Rotation;
        }

        return (distance, angle);
    }

    public static string FormatPercent(int a, int b)
    {
        var value = 100d * ((double)a / b);
        return value < 0.5 ? "0%" : $"{value:##}%";
    }

    private static string Sanitize(string text) => DiscordModule.Sanitize(text);
}
