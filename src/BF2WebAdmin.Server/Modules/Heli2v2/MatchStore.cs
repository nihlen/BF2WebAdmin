using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using System.Text.Json;
using Polly;
using Polly.Registry;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// The 2v2 matches in the database. The writes run in the background in the order they were queued (a match is inserted
/// before its rounds and its end), off the apply loop and the module thread; each keeps retrying for up to ~34 minutes
/// (<see cref="PolicyNames.RetryPolicyLongAsync"/>) while the game goes on.
/// </summary>
public sealed class MatchStore(IMatchRepository repository, IReadOnlyPolicyRegistry<string> policies, ILogger logger)
{
    private readonly SerialTaskQueue _saves = new(logger);

    public Task InsertMatch(Match match)
    {
        var entity = ToEntity(match);
        return Queue($"Save new match {match.Id}", () => repository.InsertMatchAsync(entity));
    }

    public Task UpdateMatch(Match match)
    {
        var entity = ToEntity(match);
        return Queue($"Save match end {match.Id}", () => repository.UpdateMatchAsync(entity));
    }

    public Task InsertRound(Round round)
    {
        var entity = ToEntity(round);
        return Queue($"Save round {round.Id} of match {round.MatchId}", () => repository.InsertRoundAsync(entity));
    }

    private Task Queue(string description, Func<Task> save) =>
        _saves.Enqueue(description, () => policies.Get<IAsyncPolicy>(PolicyNames.RetryPolicyLongAsync).ExecuteAsync(save));

    /// <summary>The newest match if it has not ended, with its rounds; its players from <paramref name="getPlayer"/>.</summary>
    public async Task<Match?> GetOpenMatchAsync(Func<string, Player?> getPlayer)
    {
        var newest = (await repository.GetMatchesByNewestAsync(0, 1)).FirstOrDefault();
        if (newest is not { MatchEnd: null })
            return null;

        return FromEntity(await repository.GetMatchAsync(newest.Id), getPlayer);
    }

    public static Data.Entities.Match ToEntity(Match match, bool mapRounds = false)
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
                result.MatchRounds = finishedRounds.Select(ToEntity).ToList();
            }
        }

        return result;
    }

    public static MatchRound ToEntity(Round round)
    {
        var result = new MatchRound
        {
            Id = round.Id,
            MatchId = round.MatchId,
            WinningTeamId = round.WinningTeamId,
            PositionTrackerInterval = round.PositionTrackerInterval,
            RoundStart = round.RoundStart,
            RoundEnd = round.RoundEnd
        };

        if (round.Players.Any())
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
                MovementPathJson = Compression.CompressText(JsonSerializer.Serialize(p.MovementPath.Select(p => p.ToString()))),
                ProjectilePathsJson = Compression.CompressText(JsonSerializer.Serialize(p.ProjectilePaths.Select(p => p.Select(pp => pp.ToString())))),
            }).ToList();
        }

        return result;
    }

    /// <summary>
    /// A stored match as a live one, to go on with it. The players come from <paramref name="getPlayer"/> (the server's);
    /// one who is not there makes it fail. The paths are not read back.
    /// </summary>
    public static Match FromEntity(Data.Entities.Match entity, Func<string, Player?> getPlayer)
    {
        var teamHashes = new[] { entity.TeamAHash, entity.TeamBHash };
        return new Match
        {
            Id = entity.Id,
            MatchStart = entity.MatchStart,
            Map = entity.Map,
            ServerId = entity.ServerId,
            ServerName = entity.ServerName,
            TeamHashes = teamHashes,
            Type = entity.Type,
            Rounds = entity.MatchRounds?.Select(r =>
            {
                // A draw has no winner (it made this fail, so a match with a draw was never resumed)
                var isDraw = r.WinningTeamId == -1;
                return new Round
                {
                    Id = r.Id,
                    MatchId = r.MatchId,
                    RoundStart = r.RoundStart,
                    RoundEnd = r.RoundEnd,
                    WinningTeamId = r.WinningTeamId,
                    WinningTeamHash = isDraw ? null : TeamHash.Of(r.MatchRoundPlayers.Where(p => p.TeamId == r.WinningTeamId).Select(p => p.PlayerHash)),
                    LosingTeamHash = isDraw ? null : TeamHash.Of(r.MatchRoundPlayers.Where(p => p.TeamId != r.WinningTeamId).Select(p => p.PlayerHash)),
                    PositionTrackerInterval = r.PositionTrackerInterval,
                    Players = r.MatchRoundPlayers?.Select(p =>
                    {
                        var player = getPlayer(p.PlayerHash) ?? throw new InvalidOperationException($"Player {p.PlayerHash} of match {entity.Id} is not on the server");
                        return new RoundPlayer(player, teamHashes.FirstOrDefault(h => h.Contains(player.Hash)), r.Id)
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
                };
            }).ToList() ?? new List<Round>()
        };
    }
}
