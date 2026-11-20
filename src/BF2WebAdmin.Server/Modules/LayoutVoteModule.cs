using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Builder;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Modules.Heli2v2;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// Votes on loading a map builder layout: started in game with <c>.vote &lt;layout&gt;</c> or from the web API
/// (<see cref="LayoutVoteRequestEvent"/>, BF2v2's layout list), answered with PgUp and PgDn through the admin channel's
/// client votes. The players get the question from the module; this module counts the ballots by
/// <see cref="LayoutVoteRules"/>, ends the vote once its result is certain, announces it and loads the layout when it
/// passes. The same votes run on three other actions (<see cref="ServerVoteAction"/>): <c>.vote clear</c> (a map clear:
/// removes what the builder built, as an admin's <c>.clear</c>), <c>.vote reset</c> (a round reset: restarts the round,
/// which also removes objects the builder lost track of, and resets the scores) and <c>.vote restart</c> (a server
/// restart, as an admin's <c>.restartserver</c>). Admins load layouts directly with <c>.load</c> or the web map builder.
/// </summary>
[LoadedWith(typeof(MapModule))]
public class LayoutVoteModule : BaseModule,
    IHandleCommandAsync<LayoutVoteCommand>,
    IHandleEventAsync<LayoutVoteRequestEvent>,
    IHandleEventAsync<VoteBallotEvent>,
    IHandleEventAsync<VoteResultEvent>
{
    private readonly IMapRepository _maps;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, DateTimeOffset> _lastStarts = new();
    private RunningVote? _vote;

    public LayoutVoteModule(IGameServer server, IMapRepository maps, TimeProvider time, ILogger<LayoutVoteModule> logger,
        CancellationTokenSource cts) : base(server, logger, cts)
    {
        _maps = maps;
        _time = time;
    }

    private MapBuilder? Builder => (GameServer as GameServer)?.Builder;

    public async ValueTask HandleAsync(LayoutVoteCommand command)
    {
        var player = command.Message?.Player;
        if (player == null)
        {
            // Web and Discord users start votes from the layout list instead
            GameServer.GameWriter.SendText("Error: .vote is for players in the game");
            return;
        }

        var starterKey = "player:" + (player.Hash ?? player.Name);
        string? error;
        if (LayoutVoteRules.ActionFor(command.Name) is { } action)
        {
            error = await StartAsync(action, null, starterKey, player.Name);
        }
        else
        {
            var layout = Builder == null ? null : await Builder.FindLayoutAsync(command.Name);
            if (layout == null)
            {
                GameServer.GameWriter.SendText($"Error: no layout '{command.Name}' on this map");
                return;
            }

            error = await StartAsync(ServerVoteAction.LoadLayout, layout, starterKey, player.Name);
        }

        if (error != null)
            GameServer.GameWriter.SendPrivateMessage(player, error);
    }

    public async ValueTask HandleAsync(LayoutVoteRequestEvent e)
    {
        try
        {
            var layout = await _maps.GetAsync(e.LayoutId);
            e.Result.TrySetResult(layout == null ? "No such layout." : await StartAsync(ServerVoteAction.LoadLayout, layout, e.StarterKey, e.StarterName));
        }
        catch (Exception ex)
        {
            e.Result.TrySetException(ex);
        }
    }

    public async ValueTask HandleAsync(VoteBallotEvent e)
    {
        if (_vote is not { Ended: false } vote || e.RequestId != vote.RequestId)
            return;

        if (e.Choice == VoteChoice.None)
            vote.Ballots.Remove(e.PlayerIndex);
        else
            vote.Ballots[e.PlayerIndex] = e.Choice;

        var (yes, no) = vote.Count();
        var decision = LayoutVoteRules.Decide(yes, no, Undecided(vote), vote.RequiredYes);
        if (decision == LayoutVoteDecision.Open)
            return;

        // Certain before the time is up: the module ends it with no line of its own
        vote.Ended = true;
        await GameServer.GameWriter.CancelVoteAsync();
        await FinishAsync(vote, decision == LayoutVoteDecision.Passed, yes, no);
    }

    public async ValueTask HandleAsync(VoteResultEvent e)
    {
        if (_vote is not { } vote || e.RequestId != vote.RequestId)
            return;

        _vote = null;
        if (vote.Ended)
            return;

        vote.Ended = true;
        switch (e.Outcome)
        {
            case EvVoteResult.OutcomeTimeUp:
                await FinishAsync(vote, LayoutVoteRules.PassesAtEnd(e.Yes, e.No, vote.RequiredYes), e.Yes, e.No);
                break;
            case EvVoteResult.OutcomeUnloaded:
                Logger.LogInformation("Vote on {ServerId} ({Subject}) ended by a map change", GameServer.Id, vote.Subject);
                break;
            default:
                GameServer.GameWriter.SendText($"VOTE CANCELLED: {vote.Subject}");
                break;
        }
    }

    /// <summary>
    /// Starts a vote on the action (the layout is the one to load for <see cref="ServerVoteAction.LoadLayout"/>). Returns
    /// null when it started, else why not.
    /// </summary>
    private async Task<string?> StartAsync(ServerVoteAction action, MapMod? layout, string starterKey, string starterName)
    {
        var now = _time.GetUtcNow();
        if (_vote != null)
        {
            // A vote whose result never came (the link closed) is over after its time
            if (now < _vote.Started + TimeSpan.FromSeconds(LayoutVoteRules.VoteSeconds + 15))
                return "A vote is already running.";
            _vote = null;
        }

        foreach (var key in _lastStarts.Where(kv => now - kv.Value >= LayoutVoteRules.Cooldown).Select(kv => kv.Key).ToList())
            _lastStarts.Remove(key);
        if (_lastStarts.TryGetValue(starterKey, out var last) && now - last < LayoutVoteRules.Cooldown)
            return $"Wait {Math.Ceiling((LayoutVoteRules.Cooldown - (now - last)).TotalSeconds)} s before you start another vote.";

        var map = GameServer.Map?.Name?.ToLowerInvariant();
        if (action == ServerVoteAction.MapClear && !HasBuilt())
            return "Nothing is built to clear. Objects left from before can go with .vote reset.";
        if (layout != null && !string.Equals(layout.Map, map, StringComparison.OrdinalIgnoreCase))
            return $"The layout '{layout.Name}' is for {layout.Map}; the server runs {map ?? "no map"}.";

        if (!GameServer.GameWriter.CanStartClientVotes)
            return "This server cannot run layout votes (its module has no client votes).";

        var humans = Humans().ToList();
        if (humans.Count == 0)
            return "Nobody is on the server to vote.";

        var matchPlayers = MatchPlayersOnline();
        var inMatch = matchPlayers.Count >= LayoutVoteRules.MatchRequiredYes;
        var required = LayoutVoteRules.RequiredYes(inMatch, humans.Count);
        var subject = LayoutVoteRules.Subject(action, layout?.Name);
        uint requestId;
        try
        {
            requestId = await GameServer.GameWriter.StartClientVoteAsync(char.ToUpperInvariant(subject[0]) + subject[1..] + "?",
                LayoutVoteRules.VoteSeconds, inMatch ? matchPlayers : []);
        }
        catch (AdminCommandException e)
        {
            Logger.LogInformation("Vote on {ServerId} ({Subject}) not started: {Error}", GameServer.Id, subject, e.Message);
            return e.Code == AdminErrorCode.Refused ? "A vote is already running, or nobody can vote." : "The server did not start the vote.";
        }

        _vote = new RunningVote(requestId, action, layout?.Id, layout?.Name, subject, required,
            inMatch ? matchPlayers.Select(p => p.Index).ToHashSet() : null, now);
        _lastStarts[starterKey] = now;
        Logger.LogInformation("Vote on {ServerId} ({Subject}) started by {Starter} ({Required} yes needed{Match})",
            GameServer.Id, subject, starterName, required, inMatch ? ", match players only" : "");
        GameServer.GameWriter.SendText(
            $"{starterName} started a vote: {_vote.Subject}? {required} yes needed{(inMatch ? " from the match players" : "")}");
        return null;
    }

    private async Task FinishAsync(RunningVote vote, bool passed, int yes, int no)
    {
        _vote = null;
        if (!passed)
        {
            GameServer.GameWriter.SendText($"VOTE FAILED: {vote.Subject} (yes {yes}, no {no}, {vote.RequiredYes} yes needed)");
            return;
        }

        GameServer.GameWriter.SendText($"VOTE PASSED: {vote.Subject} (yes {yes}, no {no})");
        try
        {
            switch (vote.Action)
            {
                case ServerVoteAction.LoadLayout when Builder is { } builder:
                    var result = await builder.LoadLayoutAsync(vote.LayoutId!.Value, "vote");
                    GameServer.GameWriter.SendText($"§C1001Loaded map '{vote.LayoutName}' ({result.Added.Count} objects)");
                    break;
                case ServerVoteAction.MapClear:
                    await ClearBuiltAsync();
                    break;
                case ServerVoteAction.RoundReset:
                    // The plain level: the round start removes every spawned object, and the builder has none to spawn again
                    await DropBuiltAsync();
                    await GameServer.GameWriter.RestartMapAsync();
                    break;
                case ServerVoteAction.ServerRestart:
                    // Nor after the module connects again
                    await DropBuiltAsync();
                    GameServer.GameWriter.SendText("The server restarts now and should be back in a minute");
                    // Time to read it before the link and the players' connections go
                    await Task.Delay(RestartDelay, _time);
                    await GameServer.GameWriter.QuitAsync();
                    break;
            }
        }
        catch (BuilderException e)
        {
            GameServer.GameWriter.SendText($"Error: {e.Message}");
        }
        catch (AdminCommandException e)
        {
            Logger.LogWarning(e, "Vote on {ServerId} ({Subject}) passed but failed", GameServer.Id, vote.Subject);
            GameServer.GameWriter.SendText($"Error: {e.Message}");
        }
    }

    /// <summary>The wait between the announcement of a server restart and the quit. Virtual for the tests.</summary>
    protected virtual TimeSpan RestartDelay => TimeSpan.FromSeconds(3);

    /// <summary>Whether the builder has a layout or objects, so that a map clear changes something. Virtual for the tests.</summary>
    protected virtual bool HasBuilt() => Builder is { } builder && (builder.Items.Count > 0 || builder.Layout != null);

    /// <summary>Removes what the builder built, as <c>.clear</c> does. Virtual for the tests.</summary>
    protected virtual async Task ClearBuiltAsync()
    {
        if (Builder is not { } builder)
            return;
        await builder.ClearAsync("vote");
        GameServer.GameWriter.SendText("Cleared map");
    }

    /// <summary>Drops the layout and what is built before a restart (<see cref="MapBuilder.DropAllAsync"/>). Virtual for the tests.</summary>
    protected virtual async Task DropBuiltAsync()
    {
        if (Builder is { } builder)
            await builder.DropAllAsync("vote");
    }

    /// <summary>The human players on a team, who get a vote for everyone.</summary>
    private IEnumerable<Player> Humans() =>
        GameServer.Players.Where(p => !p.IsBot && p.Team?.Id is 1 or 2);

    /// <summary>The players of the running 2v2 match who are on the server; empty when no match runs.</summary>
    private List<Player> MatchPlayersOnline()
    {
        var match = GameServer.ModManager?.GetModule<Heli2v2Module>() is { RoundsActive: true } heli2v2 ? heli2v2.ActiveMatch : null;
        var round = match?.Rounds.LastOrDefault();
        if (round == null)
            return [];

        return round.Players
            .Select(p => GameServer.GetPlayerByHash(p.PlayerHash))
            .OfType<Player>()
            .Where(p => !p.IsBot)
            .Distinct()
            .ToList();
    }

    private int Undecided(RunningVote vote)
    {
        var voters = vote.Voters == null ? Humans() : GameServer.Players.Where(p => vote.Voters.Contains(p.Index) && p.Team?.Id is 1 or 2);
        return voters.Count(p => !vote.Ballots.ContainsKey(p.Index));
    }

    private sealed class RunningVote(uint requestId, ServerVoteAction action, Guid? layoutId, string? layoutName, string subject,
        int requiredYes, HashSet<int>? voters, DateTimeOffset started)
    {
        public uint RequestId { get; } = requestId;
        public ServerVoteAction Action { get; } = action;

        /// <summary>The layout to load, for <see cref="ServerVoteAction.LoadLayout"/>.</summary>
        public Guid? LayoutId { get; } = layoutId;

        public string? LayoutName { get; } = layoutName;

        /// <summary>What is voted on, in the announcements: "load layout 'X'", "map clear", "round reset" or "server restart".</summary>
        public string Subject { get; } = subject;

        public int RequiredYes { get; } = requiredYes;

        /// <summary>The match players who vote; null for every human on a team.</summary>
        public HashSet<int>? Voters { get; } = voters;

        public DateTimeOffset Started { get; } = started;
        public Dictionary<int, VoteChoice> Ballots { get; } = new();
        public bool Ended { get; set; }

        public (int Yes, int No) Count() =>
            (Ballots.Values.Count(c => c == VoteChoice.Yes), Ballots.Values.Count(c => c == VoteChoice.No));
    }
}

public enum LayoutVoteDecision { Open, Passed, Failed }

/// <summary>What a vote is on: a layout by its name, or one of the words <c>clear</c>, <c>reset</c> and <c>restart</c>.</summary>
public enum ServerVoteAction { LoadLayout, MapClear, RoundReset, ServerRestart }

/// <summary>
/// The rules of a layout vote. It passes with at least the required yes votes and more yes than no. Required: 3 while a 2v2
/// match is played (only its players vote), else 2, or 1 with a single player on the server.
/// </summary>
public static class LayoutVoteRules
{
    public const int VoteSeconds = 30;
    public const int MatchRequiredYes = 3;
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    /// <summary>The action of a <c>.vote</c> word, or null for a layout name. These words are no layout's name (<see cref="MapBuilder.ReservedNames"/>).</summary>
    public static ServerVoteAction? ActionFor(string word) => word.Trim().ToLowerInvariant() switch
    {
        "clear" => ServerVoteAction.MapClear,
        "reset" => ServerVoteAction.RoundReset,
        "restart" => ServerVoteAction.ServerRestart,
        _ => null,
    };

    /// <summary>The action in the vote's lines.</summary>
    public static string Subject(ServerVoteAction action, string? layoutName) => action switch
    {
        ServerVoteAction.MapClear => "map clear",
        ServerVoteAction.RoundReset => "round reset",
        ServerVoteAction.ServerRestart => "server restart",
        _ => $"load layout '{layoutName}'",
    };

    public static int RequiredYes(bool matchRunning, int humansOnline) => matchRunning ? MatchRequiredYes : humansOnline <= 1 ? 1 : 2;

    public static bool PassesAtEnd(int yes, int no, int requiredYes) => yes >= requiredYes && yes > no;

    /// <summary>Whether the result is certain now, with <paramref name="undecided"/> voters still to vote.</summary>
    public static LayoutVoteDecision Decide(int yes, int no, int undecided, int requiredYes)
    {
        if (yes >= requiredYes && yes > no + undecided)
            return LayoutVoteDecision.Passed;
        if (yes + undecided < requiredYes || yes + undecided <= no)
            return LayoutVoteDecision.Failed;
        return LayoutVoteDecision.Open;
    }
}
