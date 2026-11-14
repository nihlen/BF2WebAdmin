using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// The 2v2 matches of one server as they are played. Ready lines in chat make a pending round; when all four players are
/// ready it starts (in the match of these two teams: a running one, the previous one from the database, or a new one). A
/// team's last death ends the round two seconds later (a draw if both teams died). A player who leaves, switches team or
/// says "pad" takes their round with them; two players leaving or a new map end the match. It publishes the match and round
/// events; <see cref="Heli2v2Module"/> reports and saves them. It runs on the module thread, except <see cref="FindRound"/>.
/// </summary>
public sealed class MatchTracker(
    IGameServer server,
    MatchStore store,
    IDelayProvider delayProvider,
    TimeProvider timeProvider,
    ITaskRunner taskRunner,
    Func<IMediator> mediator,
    Func<string, Task> sendDiscordMessage,
    ILogger logger,
    CancellationToken cancellationToken)
{
    /// <summary>A longer gap between a player's missile samples starts a new missile path</summary>
    private const int NewProjectilePathGapMs = 1000;

    private List<Match> _matches = new();
    private Round? _pendingRound;

    // The started rounds of the last hour, for ViewerFeed to place a traced TV missile in its round. Read from the admin
    // channel's apply loop, so a copy under its own lock instead of _matches
    private readonly object _liveRoundsLock = new();
    private readonly List<LiveRound> _liveRounds = new();

    public Match? ActiveMatch => _matches.FirstOrDefault(m => m.IsActive);

    private Round? ActiveRound => ActiveMatch?.Rounds.FirstOrDefault(r => r.IsActive);

    /// <summary>The round of this server that was being played at <paramref name="utc"/> (start to end), or null.</summary>
    public LiveRound? FindRound(DateTime utc)
    {
        lock (_liveRoundsLock)
            return _liveRounds.LastOrDefault(r => r.Start <= utc && (r.End is null || utc <= r.End));
    }

    /// <summary>
    /// A new map ends the match if it was played on another map (<paramref name="endMatch"/>: when 2v2 mode is on). Returns
    /// whether the matches were cleared: on another map; on the same map the players can go on with theirs.
    /// </summary>
    public async ValueTask<bool> OnMapChangedAsync(string mapName, bool endMatch)
    {
        var match = ActiveMatch;
        if (endMatch && match != null && match.Map != mapName)
            await EndMatchAsync(match);

        if (match?.Map == mapName)
            return false;

        _matches = new List<Match>();
        return true;
    }

    /// <summary>A player's line in global chat: "pad" or a ready line.</summary>
    public async Task OnChatAsync(Message message)
    {
        var chat = ReadyMessage.Parse(message.Text);
        if (chat == ReadyChat.Pad)
        {
            RemovePlayer(message.Player.Hash);
            return;
        }

        if (chat != ReadyChat.Ready)
            return;

        var teamPlayers = message.Player.Vehicle?.Players ?? Array.Empty<Player>();
        if (teamPlayers.Count < 2)
            return;

        if (teamPlayers.Any(p => p.Team.Id != teamPlayers[0].Team.Id))
        {
            // The admin channel reports every team change (any source), so a mismatch is real; nothing to re-query
            // TODO: figure ut why this is not 100% fixed
            server.GameWriter.SendText($"Team mismatch for {string.Join(" & ", teamPlayers.Select(p => $"{p.ShortName}"))} - switch team to fix match");
            await sendDiscordMessage($"2v2 team ID mismatch for {string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}"))}");
            logger.LogWarning("2v2 team ID mismatch for {PlayerTeamNames}", string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}")));
            return;
        }

        var timeSinceLastReady = timeProvider.GetUtcNow() - _pendingRound?.ReadyTime;
        if (_pendingRound != null && timeSinceLastReady > TimeSpan.FromMinutes(5))
        {
            logger.LogDebug("Pending round was over 5 minutes - clearing");
            _pendingRound = null;
        }

        var newReady = true;
        _pendingRound ??= new Round
        {
            Id = Guid.NewGuid(),
            ReadyTime = timeProvider.GetUtcNow().UtcDateTime,
            PositionTrackerInterval = server.GameWriter.CurrentTrackerInterval
        };

        var pending = _pendingRound;
        var teamHash = TeamHash.Of(teamPlayers[0], teamPlayers[1]);
        foreach (var vehiclePlayer in message.Player.Vehicle!.Players)
        {
            var roundPlayer = pending.Players.FirstOrDefault(p => p.Player.Hash == vehiclePlayer.Hash);
            if (roundPlayer == null)
            {
                roundPlayer = new RoundPlayer(vehiclePlayer, teamHash, pending.Id);
                pending.Players.Add(roundPlayer);
            }

            if (roundPlayer.IsReady)
                newReady = false;
            else
                roundPlayer.IsReady = true;
        }

        var team1Ready = pending.Players.Any(p => p.IsReady && p.Player.Team.Id == 1);
        var team2Ready = pending.Players.Any(p => p.IsReady && p.Player.Team.Id == 2);
        if (pending.Players.Count == 4 && team1Ready && team2Ready && !pending.IsActive)
        {
            pending.RoundStart = timeProvider.GetUtcNow().UtcDateTime;
            var goTeamId = message.Player.Team.Id;
            foreach (var roundPlayer in pending.Players)
            {
                roundPlayer.SaidGo = goTeamId == roundPlayer.TeamId;
                roundPlayer.SubVehicle = roundPlayer.Player.SubVehicleTemplate;
                roundPlayer.StartPosition = roundPlayer.Player.Position;
            }

            if (pending.MatchId == null)
                await AddToMatchAsync(pending);

            AddLiveRound(pending);

            await mediator().PublishAsync(new RoundStartEvent(pending, timeProvider.GetUtcNow()));

            _pendingRound = null;
        }
        else if (newReady)
        {
            await sendDiscordMessage($"{message.Player.Team?.Name} READY");
        }
    }

    /// <summary>The round goes into the match of its two teams: the running one, the previous one if it can go on, or a new one.</summary>
    private async Task AddToMatchAsync(Round round)
    {
        if (_matches.Count == 0)
            await TryResumePreviousMatchAsync(round);

        var teamHashes = round.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
        var match = _matches.FirstOrDefault(m => m.IsActive && m.TeamHashes.Contains(teamHashes[0]) && m.TeamHashes.Contains(teamHashes[1]));
        if (match != null)
        {
            match.Rounds.Add(round);
            round.MatchId = match.Id;
            return;
        }

        if (ActiveMatch is { } previousMatch)
            await EndMatchAsync(previousMatch);

        match = new Match
        {
            Id = Guid.NewGuid(),
            MatchStart = round.RoundStart!.Value,
            Map = server.Map.Name,
            ServerId = server.Id,
            ServerName = server.Name,
            TeamHashes = teamHashes,
            Type = "HELI_2V2",
        };
        _matches.Add(match);

        match.Rounds.Add(round);
        round.MatchId = match.Id;

        await mediator().PublishAsync(new MatchStartEvent(match, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// The previous unfinished match from the database goes on if it has the same players, server and map and its last
    /// round ended less than 10 minutes ago (WebAdmin restarted).
    /// </summary>
    private async Task TryResumePreviousMatchAsync(Round round)
    {
        try
        {
            var teamHashes = round.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
            var previousMatch = await store.GetOpenMatchAsync(server.GetPlayerByHash);
            if (previousMatch == null)
                return;

            var previousTeamHashes = previousMatch.Rounds.FirstOrDefault()?.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
            var previousMatchHasSameTeams = teamHashes.All(hash => previousTeamHashes?.Any(oldHash => oldHash == hash) ?? false);
            var mostRecentRoundEnd = previousMatch.Rounds.MaxBy(r => r.RoundEnd)?.RoundEnd ?? timeProvider.GetUtcNow();
            var shouldResumePreviousMatch = previousMatchHasSameTeams &&
                                            round.RoundStart - mostRecentRoundEnd < TimeSpan.FromMinutes(10) &&
                                            previousMatch.Map == server.Map.Name &&
                                            previousMatch.ServerId == server.Id;

            if (shouldResumePreviousMatch)
            {
                // AddToMatchAsync finds it and adds the round (adding it here too counted it twice)
                _matches.Add(previousMatch);
                server.GameWriter.SendText("Previous 2v2 match has been resumed");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resume previous match");
        }
    }

    private ValueTask EndMatchAsync(Match match)
    {
        match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? timeProvider.GetUtcNow().UtcDateTime;
        return mediator().PublishAsync(new MatchEndEvent(match, timeProvider.GetUtcNow()));
    }

    /// <summary>A kill or a death of a round's player. The team's last one ends the round two seconds later.</summary>
    public ValueTask OnKillAsync(Player? attacker, Position? attackerPosition, Player victim, Position victimPosition, string? weapon)
    {
        // TODO: detect draws - wait 1 s before declaring winner - or detect patterns - check vehicle status/id?
        var match = ActiveMatch;
        var round = ActiveRound;
        if (match == null || round == null)
            return ValueTask.CompletedTask;

        var roundVictim = round.Players.FirstOrDefault(p => p.Player.Hash == victim.Hash);
        if (roundVictim == null)
            return ValueTask.CompletedTask;

        roundVictim.IsAlive = false;
        roundVictim.DeathPosition = victimPosition;
        roundVictim.DeathTime = timeProvider.GetUtcNow().UtcDateTime;
        roundVictim.KillerWeapon = weapon ?? roundVictim.KillerWeapon;
        roundVictim.KillerPosition = attackerPosition ?? roundVictim.KillerPosition;
        roundVictim.KillerHash = round.Players.FirstOrDefault(p => p.Player.Hash == attacker?.Hash)?.Player.Hash ?? roundVictim.KillerHash;

        foreach (var roundPlayer in round.Players.Where(p => p.TeamHash == roundVictim.TeamHash))
        {
            roundPlayer.IsReady = false;
        }

        if (round.Players.Any(p => p.TeamHash == roundVictim.TeamHash && p.IsAlive))
            return ValueTask.CompletedTask;

        if (round.IsPendingCompletion)
            return ValueTask.CompletedTask;

        round.IsPendingCompletion = true;
        taskRunner.RunBackgroundTask("Handle 2v2 round completion", () => CompleteRoundAsync(match, round, roundVictim), cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task CompleteRoundAsync(Match match, Round round, RoundPlayer lastVictim)
    {
        // Wait 2 seconds and see if it's a draw
        await delayProvider.DelayAsync(2000, cancellationToken);

        // The round may be gone by now: a player left or switched team ("pad", a map change), or the match ended
        if (!ReferenceEquals(ActiveMatch, match) || !match.Rounds.Contains(round) || round.RoundEnd != null)
        {
            logger.LogInformation("Round {RoundId} was removed or ended before its completion; not ending it", round.Id);
            return;
        }

        try
        {
            // Check if all players are dead or outside the heli (could be "down" but not yet dead)
            var allPlayersDead = round.Players.All(p => !p.IsAlive || p.Player.Vehicle is null);
            var team1DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[0]).DeathPosition;
            var team2DeathPosition = round.Players.First(p => p.TeamHash == match.TeamHashes[1]).DeathPosition;
            // A player who bailed out alive has no death position
            var isHeliRam = allPlayersDead && team1DeathPosition != null && team2DeathPosition != null &&
                            team1DeathPosition.Distance(team2DeathPosition) < 10;
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
                round.WinningTeamId = lastVictim.TeamId == 1 ? 2 : 1;
                round.WinningTeamHash = round.Players.First(p => p.TeamHash != lastVictim.TeamHash).TeamHash;
                round.LosingTeamHash = round.Players.First(p => p.TeamHash == lastVictim.TeamHash).TeamHash;
            }
        }
        catch
        {
            // Let the next kill try again instead of leaving the round pending for good
            round.IsPendingCompletion = false;
            throw;
        }

        round.RoundEnd = timeProvider.GetUtcNow().UtcDateTime;
        EndLiveRound(round);

        await mediator().PublishAsync(new RoundEndEvent(round, timeProvider.GetUtcNow()));
    }

    /// <summary>The player's pending and active rounds are dropped (they switched team or went back to the pad).</summary>
    public void RemovePlayer(string playerHash)
    {
        if (_pendingRound?.Players.Any(p => p.Player.Hash == playerHash) ?? false)
        {
            _pendingRound = null;
        }

        var match = ActiveMatch;
        if (match != null)
        {
            foreach (var removed in match.Rounds.Where(r => r.IsActive && r.Players.Any(p => p.Player.Hash == playerHash)))
                RemoveLiveRound(removed.Id);
            match.Rounds = match.Rounds.Where(r => !(r.IsActive && r.Players.Any(p => p.Player.Hash == playerHash))).ToList();
        }
    }

    /// <summary>A player left: their rounds go, and the match ends when two of its players are left on the server.</summary>
    public async ValueTask OnPlayerLeftAsync(string playerHash)
    {
        RemovePlayer(playerHash);

        var match = ActiveMatch;
        if (match == null)
            return;

        // By the team hashes (a team's is its two player hashes), not the first round: a match whose only round went with
        // a leaving player has no rounds, and it would never end
        var onlinePlayersCount = server.Players.Count(p => match.TeamHashes.Any(h => h.Contains(p.Hash)));
        if (onlinePlayersCount <= 2)
            await EndMatchAsync(match);
    }

    /// <summary>A position of a round's player goes into their movement path.</summary>
    public void AddPosition(Player player, Position position, Rotation rotation)
    {
        var round = ActiveRound;
        if (round == null)
            return;

        var timestamp = (timeProvider.GetUtcNow() - round.RoundStart!.Value).TotalMilliseconds;
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
    }

    /// <summary>A position of a round's player's missile goes into the path of that missile.</summary>
    public void AddProjectile(Projectile projectile)
    {
        if (projectile.Owner == null)
            return;

        var now = timeProvider.GetUtcNow();
        var round = ActiveRound;
        var roundPlayer = round?.Players.FirstOrDefault(p => p.Player.Hash == projectile.Owner.Hash);
        if (roundPlayer == null)
            return;

        var timestamp = (now - round!.RoundStart!.Value).TotalMilliseconds;
        var roundPosition = new RoundPosition
        {
            Id = projectile.Id,
            Timestamp = (int)timestamp,
            Position = projectile.Position,
            Rotation = projectile.Rotation,
            Ping = projectile.Owner.Score.Ping
        };

        // The game reuses a missile's network id for the same gunner's next missile, so a gap in the samples
        // (a missile is sampled every ~250 ms, the next one comes after a reload) also starts a new path
        var lastPath = roundPlayer.ProjectilePaths.LastOrDefault();
        if (roundPlayer.LastProjectileId != projectile.Id || lastPath is not { Count: > 0 } ||
            roundPosition.Timestamp - lastPath[lastPath.Count - 1].Timestamp > NewProjectilePathGapMs)
        {
            roundPlayer.ProjectilePaths.Add(new List<RoundPosition>());
            roundPlayer.LastProjectileId = projectile.Id;
        }

        roundPlayer.ProjectilePaths.Last().Add(roundPosition);
    }

    private void AddLiveRound(Round round)
    {
        var match = _matches.FirstOrDefault(m => m.Id == round.MatchId);
        if (match == null || round.RoundStart is not { } start)
            return;

        // BF2v2 numbers a match's rounds in start order, as WebAdmin adds them
        var number = match.Rounds.IndexOf(round) + 1;
        lock (_liveRoundsLock)
        {
            _liveRounds.RemoveAll(r => r.Start < start.AddHours(-1));
            _liveRounds.Add(new LiveRound(match.Id, round.Id, number, start));
        }
    }

    private void EndLiveRound(Round round)
    {
        lock (_liveRoundsLock)
        {
            var live = _liveRounds.FirstOrDefault(r => r.RoundId == round.Id);
            if (live != null)
                live.End = round.RoundEnd;
        }
    }

    private void RemoveLiveRound(Guid roundId)
    {
        lock (_liveRoundsLock)
            _liveRounds.RemoveAll(r => r.RoundId == roundId);
    }
}
