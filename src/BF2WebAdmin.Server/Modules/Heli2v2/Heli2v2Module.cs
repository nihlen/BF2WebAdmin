using System.Collections.Concurrent;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using Discord;
using Humanizer;
using Polly.Registry;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

public class Heli2v2Module : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<ChatMessageEvent>,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleEventAsync<PlayerDeathEvent>,
    IHandleEventAsync<PlayerTeamEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleEventAsync<ProjectilePositionEvent>,
    IHandleEventAsync<MatchStartEvent>,
    IHandleEventAsync<MatchEndEvent>,
    IHandleEventAsync<RoundStartEvent>,
    IHandleEventAsync<RoundEndEvent>,
    IHandleCommand<SwitchLaterCommand>,
    IHandleCommand<Toggle2v2Command>
{
    private readonly IGameServer _gameServer;
    private readonly IDelayProvider _delayProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ITaskRunner _taskRunner;
    private const double DefaultPositionTrackerInterval = 0.25; // Dalian?

    private int _switchCounter;
    private int _switchRounds;

    private bool _roundsActive;
    private IList<Match> _matches;

    /// <summary>2v2 mode (.2v2 toggles it): ready lines start rounds, and the practice commands that need it work.</summary>
    public bool RoundsActive => _roundsActive;

    // Match and round writes, in order and in the background
    private readonly MatchStore _store;

    public Heli2v2Module(IGameServer server, IMatchRepository matchRepository, IDelayProvider delayProvider, TimeProvider timeProvider, ITaskRunner taskRunner, IReadOnlyPolicyRegistry<string> policyRegistry, ILogger<Heli2v2Module> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameServer = server;
        _delayProvider = delayProvider;
        _timeProvider = timeProvider;
        _taskRunner = taskRunner;
        _store = new MatchStore(matchRepository, policyRegistry, logger);
        ClearMatches();
        SetupRoundEvents();
    }

    private void ClearMatches()
    {
        _switchCounter = 0;
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

    private async Task SendMatchReportAsync(Match match)
    {
        var report = MatchStatistics.Build(match, _timeProvider.GetUtcNow(), Logger);
        await SendDiscordMessageAsync(report.Text);

        if (report.PostEmbed)
        {
            Logger.LogInformation("Sending match results for {MatchId}", match.Id);
            await SendDiscordEmbedAsync(report.Embed);
        }
        else
        {
            var duration = (match.MatchEnd ?? _timeProvider.GetUtcNow()) - match.MatchStart!.Value;
            Logger.LogInformation("Not sending match results for {MatchId} (duration: {Duration}, rounds: {RoundCount})", match.Id, duration, match.Rounds.Count);
        }
    }

    private Match? GetActiveMatch()
    {
        return _matches.FirstOrDefault(m => m.IsActive);
    }

    // The started rounds of the last hour, for ViewerFeed to place a traced TV missile in its round. Read from the admin
    // channel's apply loop, so a copy under its own lock instead of _matches
    private readonly object _liveRoundsLock = new();
    private readonly List<LiveRound> _liveRounds = new();

    /// <summary>The round of this server that was being played at <paramref name="utc"/> (start to end), or null.</summary>
    public LiveRound? FindRound(DateTime utc)
    {
        lock (_liveRoundsLock)
            return _liveRounds.LastOrDefault(r => r.Start <= utc && (r.End is null || utc <= r.End));
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

    private Round? GetActiveRound()
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
            foreach (var removed in match.Rounds.Where(r => r.IsActive && r.Players.Any(p => p.Player.Hash == playerHash)))
                RemoveLiveRound(removed.Id);
            match.Rounds = match.Rounds.Where(r => !(r.IsActive && r.Players.Any(p => p.Player.Hash == playerHash))).ToList();
        }
    }

    private Round _pendingRound;

    private ValueTask HandleRoundKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
    {
        // TODO: detect draws - wait 1 s before declaring winner - or detect patterns - check vehicle status/id?
        var match = GetActiveMatch();
        var round = GetActiveRound();
        if (round == null)
            return ValueTask.CompletedTask;

        var roundVictim = round.Players.FirstOrDefault(p => p.Player.Hash == victim.Hash);
        if (roundVictim == null)
            return ValueTask.CompletedTask;

        roundVictim.IsAlive = false;
        roundVictim.DeathPosition = victimPosition;
        roundVictim.DeathTime = _timeProvider.GetUtcNow().UtcDateTime;
        roundVictim.KillerWeapon = weapon ?? roundVictim.KillerWeapon;
        roundVictim.KillerPosition = attackerPosition ?? roundVictim.KillerPosition;
        roundVictim.KillerHash = round.Players.FirstOrDefault(p => p.Player.Hash == attacker?.Hash)?.Player.Hash ?? roundVictim.KillerHash;

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

            _taskRunner.RunBackgroundTask("Handle 2v2 round completion", async () =>
            {
                // Wait 2 seconds and see if it's a draw
                await _delayProvider.DelayAsync(2000, ModuleCancellationToken);

                // The round may be gone by now: a player left or switched team ("pad", a map change), or the match ended
                if (!ReferenceEquals(GetActiveMatch(), match) || !match.Rounds.Contains(round) || round.RoundEnd != null)
                {
                    Logger.LogInformation("Round {RoundId} was removed or ended before its completion; not ending it", round.Id);
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
                        round.WinningTeamId = roundVictim.TeamId == 1 ? 2 : 1;
                        round.WinningTeamHash = round.Players.First(p => p.TeamHash != roundVictim.TeamHash).TeamHash;
                        round.LosingTeamHash = round.Players.First(p => p.TeamHash == roundVictim.TeamHash).TeamHash;
                    }
                }
                catch
                {
                    // Let the next kill try again instead of leaving the round pending for good
                    round.IsPendingCompletion = false;
                    throw;
                }

                round.RoundEnd = _timeProvider.GetUtcNow().UtcDateTime;
                EndLiveRound(round);

                await Mediator.PublishAsync(new RoundEndEvent(round, _timeProvider.GetUtcNow()));
            }, ModuleCancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    public void Handle(SwitchLaterCommand command)
    {
        if (!_roundsActive)
        {
            _gameServer.GameWriter.SendText("2v2 mode is not currently active");
            return;
        }

        _gameServer.GameWriter.SendText($"Switching in {command.Rounds} rounds");
        // Count from now, not from the rounds already played on this map
        _switchCounter = 0;
        _switchRounds = command.Rounds;
    }

    public void Handle(Toggle2v2Command command)
    {
        if (_roundsActive)
            RemoveRoundEvents();
        else
            SetupRoundEvents();
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        var match = GetActiveMatch();
        if (_roundsActive)
        {
            if (match != null && match.Map != e.Map.Name)
            {
                match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? _timeProvider.GetUtcNow().UtcDateTime;
                await Mediator.PublishAsync(new MatchEndEvent(match, _timeProvider.GetUtcNow()));
            }
        }

        _gameServer.GameWriter.SendConsole(RconScript.InitServer);
        _gameServer.GameWriter.SendTimerInterval(DefaultPositionTrackerInterval);

        // Only clear match if it's a different map, else keep counting as long as it's the same players
        if (match?.Map != e.Map.Name)
        {
            ClearMatches();
        }
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        if (!_roundsActive)
            return;

        if (e.Message.Type != MessageType.Player || e.Message.Channel != ChatChannel.Global)
            return;

        var chat = ReadyMessage.Parse(e.Message.Text);
        if (chat == ReadyChat.Pad)
        {
            RemoveActiveRoundWithPlayer(e.Message.Player.Hash);
            return;
        }

        if (chat != ReadyChat.Ready)
            return;

        var teamPlayers = e.Message.Player.Vehicle?.Players ?? Array.Empty<Player>();
        if (teamPlayers.Count < 2)
            return;

        if (teamPlayers!.Any(p => p.Team.Id != teamPlayers[0].Team.Id))
        {
            // The admin channel reports every team change (any source), so a mismatch is real; nothing to re-query
            {
                // TODO: figure ut why this is not 100% fixed
                _gameServer.GameWriter.SendText($"Team mismatch for {string.Join(" & ", teamPlayers.Select(p => $"{p.ShortName}"))} - switch team to fix match");
                await SendDiscordMessageAsync($"2v2 team ID mismatch for {string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}"))}");
                Logger.LogWarning("2v2 team ID mismatch for {PlayerTeamNames}", string.Join(", ", teamPlayers.Select(p => $"{p.DisplayName}={p.Team.Name}")));
            }

            return;
        }

        var timeSinceLastReady = _timeProvider.GetUtcNow() - _pendingRound?.ReadyTime;
        if (_pendingRound != null && timeSinceLastReady > TimeSpan.FromMinutes(5))
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
                ReadyTime = _timeProvider.GetUtcNow().UtcDateTime,
                PositionTrackerInterval = _gameServer.GameWriter.CurrentTrackerInterval
            };
        }

        var teamHash = TeamHash.Of(teamPlayers[0], teamPlayers[1]);
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
            _pendingRound.RoundStart = _timeProvider.GetUtcNow().UtcDateTime;
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

            AddLiveRound(_pendingRound);

            await Mediator.PublishAsync(new RoundStartEvent(_pendingRound, _timeProvider.GetUtcNow()));

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
                    previousMatch.MatchEnd = previousMatch.Rounds.LastOrDefault()?.RoundEnd ?? _timeProvider.GetUtcNow().UtcDateTime;
                    await Mediator.PublishAsync(new MatchEndEvent(previousMatch, _timeProvider.GetUtcNow()));
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

                await Mediator.PublishAsync(new MatchStartEvent(match, _timeProvider.GetUtcNow()));
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
                var previousMatch = await _store.GetOpenMatchAsync(_gameServer.GetPlayerByHash);
                if (previousMatch != null)
                {
                    var previousTeamHashes = previousMatch.Rounds.FirstOrDefault()?.Players.Select(p => p.TeamHash).Distinct().OrderBy(v => v).ToArray();
                    var previousMatchHasSameTeams = teamHashes.All(hash => previousTeamHashes?.Any(oldHash => oldHash == hash) ?? false);
                    var mostRecentRoundEnd = previousMatch.Rounds.MaxBy(r => r.RoundEnd)?.RoundEnd ?? _timeProvider.GetUtcNow();
                    var shouldResumePreviousMatch = previousMatchHasSameTeams &&
                                                    _pendingRound.RoundStart - mostRecentRoundEnd < TimeSpan.FromMinutes(10) &&
                                                    previousMatch.Map == _gameServer.Map.Name &&
                                                    previousMatch.ServerId == _gameServer.Id;

                    if (shouldResumePreviousMatch)
                    {
                        // SetCurrentMatchAsync finds it and adds the round (adding it here too counted it twice)
                        _matches.Add(previousMatch);
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
                match.MatchEnd = match.Rounds.LastOrDefault()?.RoundEnd ?? _timeProvider.GetUtcNow().UtcDateTime;
                await Mediator.PublishAsync(new MatchEndEvent(match, _timeProvider.GetUtcNow()));
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
                var timestamp = (_timeProvider.GetUtcNow() - round.RoundStart!.Value).TotalMilliseconds;
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

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ProjectilePositionEvent e)
    {
        if (!_roundsActive)
            return ValueTask.CompletedTask;

        var now = _timeProvider.GetUtcNow();
        if (e.Projectile.Owner == null)
            return ValueTask.CompletedTask;

        var round = GetActiveRound();
        var roundPlayer = round?.Players.FirstOrDefault(p => p.Player.Hash == e.Projectile.Owner.Hash);
        if (roundPlayer == null)
            return ValueTask.CompletedTask;

        var timestamp = (now - round!.RoundStart!.Value).TotalMilliseconds;
        var roundPosition = new RoundPosition
        {
            Id = e.Projectile.Id,
            Timestamp = (int)timestamp,
            Position = e.Projectile.Position,
            Rotation = e.Projectile.Rotation,
            Ping = e.Projectile.Owner.Score.Ping
        };

        if (roundPlayer.LastProjectileId != e.Projectile.Id)
        {
            roundPlayer.ProjectilePaths.Add(new List<RoundPosition>());
            roundPlayer.LastProjectileId = e.Projectile.Id;
        }

        roundPlayer.ProjectilePaths.Last().Add(roundPosition);

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(MatchStartEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving new match {MatchId}", e.Match.Id);

        _store.InsertMatch(e.Match);
        await SendDiscordMessageAsync("New 2v2 match started");
        _gameServer.GameWriter.SendText("New 2v2 match started");
    }

    public async ValueTask HandleAsync(MatchEndEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving match end results for {MatchId}", e.Match.Id);

        _store.UpdateMatch(e.Match);

        var duration = e.Match.MatchEnd!.Value - e.Match.MatchStart!.Value;
        _gameServer.GameWriter.SendText($"2v2 match ended after {duration.Humanize()}.");

        try
        {
            await SendMatchReportAsync(e.Match);
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

            await SendDiscordMessageAsync(MatchStatistics.RoundSummary(match, e.Round, winningTeam?.Name));

            if (e.Round.WinningTeamId == -1)
            {
                _gameServer.GameWriter.SendText($"Round {match.Rounds.Count} ended in a §C1001CLASSIC DRAW§C1001 in {(int)timeDiff.TotalSeconds} s");
            }
            else
            {
                _gameServer.GameWriter.SendText($"Round {match.Rounds.Count} won by {winningTeam.Name} in {(int)timeDiff.TotalSeconds} s");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error at end of round event");
        }

        Logger.LogInformation("Saving round {RoundId} on match {MatchId}", e.Round.Id, e.Round.MatchId);
        _store.InsertRound(e.Round);

        // Switch command
        var shouldSwitchTeams = _switchRounds > 0 && ++_switchCounter >= _switchRounds;
        if (shouldSwitchTeams)
        {
            _switchRounds = 0;
            SwitchAll();
        }
    }
}
