using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands.BF2;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// .replay: replays a kill shot in the game, where it happened, with objects spawned for it and moved by the game script
/// <c>replay</c> (script API 2, so a module with game.activateProjectile). In a match, between rounds, it is the match's
/// last kill if a TV missile made it: the missile and the four helicopters, empty, without collision. Without a match it
/// is the player's own last TV missile, the missile alone. The replay holds the impact for ten seconds when the missile
/// hit a vehicle, two when it hit the terrain or a static object; then the objects are removed. One at a time; never during a round. Loaded with <see cref="Heli2v2Module"/>. docs/replay.md.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class ReplayModule : BaseModule,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleCommand<ReplayCommand>,
    IHandleCommand<ReplaySpeedCommand>,
    IHandleCommand<ReplayHitCommand>,
    IHandleCommand<ReplayHitSpeedCommand>
{
    public const string ReplayScript = "replay";
    public const double MinSpeed = 0.05, MaxSpeed = 4;

    /// <summary>How long the first frame is held before the replay plays (clients get the new objects meanwhile).</summary>
    public const int PrerollTicks = 15;

    /// <summary>How long the impact is held when the missile hit a vehicle: ten seconds.</summary>
    public const int HoldTicks = 300;

    /// <summary>How long the impact is held when it hit the terrain or a static object, or nothing: two seconds.</summary>
    public const int MissHoldTicks = 60;

    private const int TickRate = 30;

    private readonly ITaskRunner _taskRunner;
    private Kill? _lastKill;
    private int _running;

    /// <summary>A kill of a round's player: who, when, and the round's players then.</summary>
    private sealed record Kill(Guid MatchId, int Attacker, string AttackerName, int Victim, string VictimName, string Weapon,
        DateTimeOffset Time, int[] RoundPlayers);

    public ReplayModule(IGameServer server, ITaskRunner taskRunner, ILogger<ReplayModule> logger, CancellationTokenSource cts)
        : base(server, logger, cts)
    {
        _taskRunner = taskRunner;
    }

    private Heli2v2Module? Heli2v2 => GameServer.ModManager?.GetModule<Heli2v2Module>();

    public ValueTask HandleAsync(PlayerKillEvent e)
    {
        var match = Heli2v2?.ActiveMatch;
        var round = match?.Rounds.FirstOrDefault(r => r.IsActive);
        if (match == null || round == null || e.Victim == null || round.Players.All(p => p.Player.Hash != e.Victim.Hash))
            return ValueTask.CompletedTask;

        _lastKill = new Kill(match.Id, e.Attacker?.Index ?? -1, e.Attacker?.ShortName ?? "", e.Victim.Index, e.Victim.ShortName,
            e.Weapon ?? "", e.TimeStamp, round.Players.Select(p => p.Player.Index).ToArray());
        return ValueTask.CompletedTask;
    }

    public void Handle(ReplayCommand command) => Start(command.Message.Player, 1, tested: false);
    public void Handle(ReplaySpeedCommand command) => Start(command.Message.Player, command.Speed, tested: false);
    public void Handle(ReplayHitCommand command) => Start(command.Message.Player, 1, tested: true);
    public void Handle(ReplayHitSpeedCommand command) => Start(command.Message.Player, command.Speed, tested: true);

    private void Start(Player? requester, double speed, bool tested)
    {
        if (requester == null)
            return;

        if (!(speed >= MinSpeed && speed <= MaxSpeed))
        {
            Reply(requester, FormattableString.Invariant($"Replay speed is {MinSpeed} to {MaxSpeed} (1 is real time)"));
            return;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            Reply(requester, "A replay is running");
            return;
        }

        var refusal = Prepare(requester, tested, out var plan, out var title);
        if (refusal != null)
        {
            Volatile.Write(ref _running, 0);
            Reply(requester, refusal);
            return;
        }

        _taskRunner.RunBackgroundTask("Kill replay", async () =>
        {
            try
            {
                await RunAsync(requester, plan!, title!, speed);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }, ModuleCancellationToken);
    }

    /// <summary>What to replay, or why there is nothing (null when there is something).</summary>
    private string? Prepare(Player requester, bool tested, out ReplayPlan? plan, out string? title)
    {
        plan = null;
        title = null;
        if (!GameServer.Scripts.Available)
            return "This server cannot run replays";

        var match = Heli2v2?.ActiveMatch;
        if (match != null && match.Rounds.Any(r => r.IsActive))
            return "No replays during a round";

        var viewer = GameServer.Viewer;
        MissileReplayDto? missile;
        var helis = new List<(uint, string)>();
        if (match != null)
        {
            if (_lastKill is not { } kill || kill.MatchId != match.Id)
                return "No kill in this match yet";
            if (!kill.Weapon.EndsWith("tv", StringComparison.OrdinalIgnoreCase))
                return $"The last kill was not a TV missile ({kill.Weapon})";

            missile = FindMissile(viewer, kill);
            if (missile == null)
                return "The last kill's missile was not traced";

            foreach (var player in kill.RoundPlayers)
                if (viewer.Poses.PlayerRootAt(player, missile.Summary.FirstTick) is { } root && helis.All(h => h.Item1 != root.RootNetId))
                    helis.Add((root.RootNetId, root.Template));

            title = $"{kill.AttackerName} -> {kill.VictimName}";
        }
        else
        {
            var mine = viewer.Missiles.FirstOrDefault(m => m.Shooter == requester.Index);
            missile = mine == null ? null : viewer.GetMissile(mine.Key);
            if (missile == null)
                return "You have no TV missile to replay";

            title = $"{requester.ShortName}'s last TV missile";
        }

        plan = KillReplay.Build(missile, helis, (root, tick) => viewer.Poses.PoseAt(root, tick), tested);
        if (plan.Frames < 2)
            return "That missile has nothing to replay";
        return null;
    }

    /// <summary>The traced missile of the kill: the killer's, a hit, in flight when the kill came.</summary>
    private static MissileReplayDto? FindMissile(ViewerFeed viewer, Kill kill)
    {
        // A trace arrives when its flight ends, its sample count of ticks after the launch
        var summary = viewer.Missiles.FirstOrDefault(m => m.Shooter == kill.Attacker && m.Outcome == "hit" &&
            m.Time >= kill.Time - TimeSpan.FromSeconds(1) &&
            m.Time - TimeSpan.FromSeconds(m.Samples / (double)TickRate + 1) <= kill.Time);
        return summary == null ? null : viewer.GetMissile(summary.Key);
    }

    private async Task RunAsync(Player requester, ReplayPlan plan, string title, double speed)
    {
        var spawned = new List<(ReplayItem Item, ObjectSpawned Object)>();
        try
        {
            foreach (var item in plan.Items)
            {
                var f = item.Frames;
                var spawn = await GameServer.GameWriter.SpawnObjectAsync(item.Template, new Position(f[0], f[1], f[2]),
                    new Rotation(f[3], f[4], f[5]));
                spawned.Add((item, spawn));
                if (spawn.NetId == 0)
                    throw new InvalidOperationException($"{item.Template} has no networkable id: no client would see it");
                if (!item.Missile)
                    GameServer.GameWriter.SetObjectCollision(spawn.NetId, false);
            }

            IScriptRun run;
            try
            {
                run = await GameServer.Scripts.StartAsync(ReplayScript, new
                {
                    speed,
                    preroll = PrerollTicks,
                    hold = plan.HitVehicle ? HoldTicks : MissHoldTicks,
                    frames = plan.Frames,
                    objects = spawned.Select(s => new { id = s.Object.NetId, missile = s.Item.Missile, f = s.Item.Frames }).ToArray(),
                }, CmdStartScript.ApiObjects);
            }
            catch (AdminCommandException e) when (e.Code == AdminErrorCode.Unavailable)
            {
                Reply(requester, "This server's module is too old for replays");
                return;
            }

            GameServer.GameWriter.SendText(speed == 1 ? $"Replay: {title}" : FormattableString.Invariant($"Replay: {title} at {speed:0.##}x"));
            using var stopping = ModuleCancellationToken.Register(() => _ = run.StopAsync());
            var result = await run.Finished;
            if (result.Failed)
                Logger.LogWarning("Replay on {ServerId} failed: {Error}", GameServer.Id, result.Text);
        }
        catch (Exception e) when (e is AdminCommandException or InvalidOperationException or ArgumentException)
        {
            Logger.LogWarning(e, "Replay on {ServerId}", GameServer.Id);
            Reply(requester, "The replay could not be started");
        }
        finally
        {
            foreach (var (_, o) in spawned)
                GameServer.GameWriter.SendDeleteObject(o.ConsoleId);
        }
    }

    private void Reply(Player player, string text) => GameServer.GameWriter.SendPrivateMessage(player, text);
}
