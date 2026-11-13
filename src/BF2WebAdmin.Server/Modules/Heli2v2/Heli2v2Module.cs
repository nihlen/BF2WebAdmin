using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using Discord;
using Humanizer;
using Polly.Registry;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;
using BF2WebAdmin.Server.Modules.DiscordBot;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// 2v2 heli matches. While 2v2 mode is on (.2v2 toggles it, on by default) the game's events go to the
/// <see cref="MatchTracker"/>, which plays the matches and rounds; this module reports them (in game and on Discord),
/// saves them (<see cref="MatchStore"/>) and switches the teams after .sw N rounds. The practice commands are in
/// <see cref="PracticeModule"/>, <see cref="TvMissileModule"/> and <see cref="RecordingModule"/>, which come with it.
/// </summary>
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
    private const double DefaultPositionTrackerInterval = 0.25; // Dalian?

    private readonly TimeProvider _timeProvider;
    private readonly MatchStore _store;
    private readonly MatchTracker _tracker;

    private bool _roundsActive;
    private int _switchCounter;
    private int _switchRounds;

    public Heli2v2Module(IGameServer server, IMatchRepository matchRepository, IDelayProvider delayProvider, TimeProvider timeProvider, ITaskRunner taskRunner, IReadOnlyPolicyRegistry<string> policyRegistry, ILogger<Heli2v2Module> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _timeProvider = timeProvider;
        _store = new MatchStore(matchRepository, policyRegistry, logger);
        _tracker = new MatchTracker(server, _store, delayProvider, timeProvider, taskRunner, () => Mediator, SendDiscordMessageAsync, logger, ModuleCancellationToken);
        SetRoundsActive(true);
    }

    /// <summary>2v2 mode (.2v2 toggles it): ready lines start rounds, and the practice commands that need it work.</summary>
    public bool RoundsActive => _roundsActive;

    /// <summary>The round of this server that was being played at <paramref name="utc"/> (start to end), or null.</summary>
    public LiveRound? FindRound(DateTime utc) => _tracker.FindRound(utc);

    private async void SetRoundsActive(bool active)
    {
        _roundsActive = active;
        await SendDiscordMessageAsync(active ? "2v2 mode enabled" : "2v2 mode disabled");
    }

    private Task SendDiscordMessageAsync(string text) =>
        GameServer.ModManager?.GetModule<DiscordModule>()?.SendTextMessageToChannelsAsync(text, debug: true) ?? Task.CompletedTask;

    private Task SendDiscordEmbedAsync(Embed embed) =>
        GameServer.ModManager?.GetModule<DiscordModule>()?.SendEmbedMessageToChannelsAsync(embed) ?? Task.CompletedTask;

    public void Handle(Toggle2v2Command command) => SetRoundsActive(!_roundsActive);

    public void Handle(SwitchLaterCommand command)
    {
        if (!_roundsActive)
        {
            GameServer.GameWriter.SendText("2v2 mode is not currently active");
            return;
        }

        GameServer.GameWriter.SendText($"Switching in {command.Rounds} rounds");
        // Count from now, not from the rounds already played on this map
        _switchCounter = 0;
        _switchRounds = command.Rounds;
    }

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        // On the same map the players can go on with their match
        if (await _tracker.OnMapChangedAsync(e.Map.Name, endMatch: _roundsActive))
            _switchCounter = 0;

        GameServer.GameWriter.SendConsole(RconScript.InitServer);
        GameServer.GameWriter.SendTimerInterval(DefaultPositionTrackerInterval);
    }

    public async ValueTask HandleAsync(ChatMessageEvent e)
    {
        if (!_roundsActive || e.Message.Type != MessageType.Player || e.Message.Channel != ChatChannel.Global)
            return;

        await _tracker.OnChatAsync(e.Message);
    }

    public ValueTask HandleAsync(PlayerKillEvent e) =>
        _roundsActive ? _tracker.OnKillAsync(e.Attacker, e.AttackerPosition, e.Victim, e.VictimPosition, e.Weapon) : ValueTask.CompletedTask;

    public ValueTask HandleAsync(PlayerDeathEvent e) =>
        _roundsActive ? _tracker.OnKillAsync(null, null, e.Player, e.Position, null) : ValueTask.CompletedTask;

    public ValueTask HandleAsync(PlayerTeamEvent e)
    {
        if (_roundsActive)
            _tracker.RemovePlayer(e.Player.Hash);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerLeftEvent e) =>
        _roundsActive ? _tracker.OnPlayerLeftAsync(e.Player.Hash) : ValueTask.CompletedTask;

    public ValueTask HandleAsync(PlayerPositionEvent e)
    {
        if (_roundsActive)
            _tracker.AddPosition(e.Player, e.Position, e.Rotation);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ProjectilePositionEvent e)
    {
        if (_roundsActive)
            _tracker.AddProjectile(e.Projectile);
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(MatchStartEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving new match {MatchId}", e.Match.Id);
        _ = _store.InsertMatch(e.Match);

        await SendDiscordMessageAsync("New 2v2 match started");
        GameServer.GameWriter.SendText("New 2v2 match started");
    }

    public async ValueTask HandleAsync(MatchEndEvent e)
    {
        if (!_roundsActive)
            return;

        Logger.LogInformation("Saving match end results for {MatchId}", e.Match.Id);
        _ = _store.UpdateMatch(e.Match);

        var duration = e.Match.MatchEnd!.Value - e.Match.MatchStart!.Value;
        GameServer.GameWriter.SendText($"2v2 match ended after {duration.Humanize()}.");

        try
        {
            await SendMatchReportAsync(e.Match);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get match statistics");
        }
    }

    private async Task SendMatchReportAsync(Match match)
    {
        // A match whose only round was dropped (a player left during it) has nothing to report
        if (match.Rounds.Count == 0)
            return;

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

    public async ValueTask HandleAsync(RoundStartEvent e)
    {
        if (!_roundsActive)
            return;

        await SendDiscordMessageAsync("GO");
    }

    public async ValueTask HandleAsync(RoundEndEvent e)
    {
        if (!_roundsActive)
            return;

        try
        {
            var winningTeam = GameServer.Teams.FirstOrDefault(t => t.Id == e.Round.WinningTeamId);
            var timeDiff = e.Round.RoundEnd!.Value - e.Round.RoundStart!.Value;
            var match = _tracker.ActiveMatch;

            await SendDiscordMessageAsync(MatchStatistics.RoundSummary(match, e.Round, winningTeam?.Name));

            if (e.Round.WinningTeamId == -1)
            {
                GameServer.GameWriter.SendText($"Round {match.Rounds.Count} ended in a §C1001CLASSIC DRAW§C1001 in {(int)timeDiff.TotalSeconds} s");
            }
            else
            {
                GameServer.GameWriter.SendText($"Round {match.Rounds.Count} won by {winningTeam.Name} in {(int)timeDiff.TotalSeconds} s");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error at end of round event");
        }

        Logger.LogInformation("Saving round {RoundId} on match {MatchId}", e.Round.Id, e.Round.MatchId);
        _ = _store.InsertRound(e.Round);

        // Switch command
        var shouldSwitchTeams = _switchRounds > 0 && ++_switchCounter >= _switchRounds;
        if (shouldSwitchTeams)
        {
            _switchRounds = 0;
            SwitchAll();
        }
    }
}
