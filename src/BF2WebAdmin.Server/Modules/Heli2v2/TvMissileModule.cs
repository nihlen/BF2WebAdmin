using System.Collections.Concurrent;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// The TV-guided missile: its settings (.tvvalues, .tv), back to the defaults on each map, and a player's shots measured in
/// chat (.tvlog, while 2v2 mode is on). Loaded with <see cref="Heli2v2Module"/>.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class TvMissileModule : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<ProjectilePositionEvent>,
    IHandleCommand<ToggleTvLogCommand>,
    IHandleCommandAsync<GetTvMissileValuesCommand>,
    IHandleCommandAsync<SetTvMissileValueCommand>,
    IHandleCommandAsync<SetTvMissileTypeCommand>
{
    private const string ActivateTvMissile = "ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv";

    public static readonly IReadOnlyList<TvMissileSetting> DefaultSettings =
    [
        new() { Name = "trackingDelay", Template = "ObjectTemplate.seek.trackingDelay {0}", DefaultValue = "0.2" },
        new() { Name = "maxYaw", Template = "ObjectTemplate.follow.maxYaw {0}", DefaultValue = "1.5" },
        new() { Name = "maxPitch", Template = "ObjectTemplate.follow.maxPitch {0}", DefaultValue = "1.5" },
        new() { Name = "changePitch", Template = "ObjectTemplate.follow.changePitch {0}", DefaultValue = "0.2" },
        new() { Name = "changeYaw", Template = "ObjectTemplate.follow.changeYaw {0}", DefaultValue = "0.2" },
        new() { Name = "minDist", Template = "ObjectTemplate.follow.minDist {0}", DefaultValue = "10" },
        new() { Name = "drag", Template = "ObjectTemplate.drag {0}", DefaultValue = "0.1" },
        new() { Name = "mass", Template = "ObjectTemplate.mass {0}", DefaultValue = "500" },
        // No followStiffness: it exists on Camera and doesn't work server side
        new() { Name = "timeToLive", Template = "ObjectTemplate.timeToLive CRD_NONE/{0}/0/0", DefaultValue = "3.5" },
        new() { Name = "maxSpeed", Template = "ObjectTemplate.maxSpeed {0}", DefaultValue = "125" },
    ];

    private readonly IDelayProvider _delayProvider;
    private readonly ITaskRunner _taskRunner;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, ProjectilePath> _tvLogPlayers = new();

    public TvMissileModule(IGameServer server, IDelayProvider delayProvider, ITaskRunner taskRunner, TimeProvider timeProvider, ILogger<TvMissileModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _delayProvider = delayProvider;
        _taskRunner = taskRunner;
        _timeProvider = timeProvider;
    }

    private bool Is2v2Active => GameServer.ModManager?.GetModule<Heli2v2Module>()?.RoundsActive ?? false;

    public ValueTask HandleAsync(MapChangedEvent e)
    {
        SetDefaultTvMissile();
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(GetTvMissileValuesCommand command)
    {
        var result = new List<(string, string, string)>();
        foreach (var defaultSetting in DefaultSettings)
        {
            // Commands run in order, so the getter reads the template made active just before it
            GameServer.GameWriter.SendConsole(ActivateTvMissile);
            var lines = await GameServer.GameWriter.ConsoleAsync(defaultSetting.Template.Split(' ').First());
            result.Add((defaultSetting.Name, string.Join(" ", lines).Trim(), defaultSetting.DefaultValue));
        }

        GameServer.GameWriter.SendText($"TV missile settings: {string.Join(", ", result.Take(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})"))}");
        GameServer.GameWriter.SendText(string.Join(", ", result.Skip(6).Select(r => $"{r.Item1}: {r.Item2} ({r.Item3})")), false);
    }

    public ValueTask HandleAsync(SetTvMissileValueCommand command)
    {
        if (string.IsNullOrEmpty(command.Value))
        {
            return ValueTask.CompletedTask;
        }

        var matchingSetting = DefaultSettings.FirstOrDefault(s => s.Name.Equals(command.Name, StringComparison.InvariantCultureIgnoreCase));
        if (matchingSetting == null)
        {
            GameServer.GameWriter.SendText("Setting not found");
            return ValueTask.CompletedTask;
        }

        GameServer.GameWriter.SendConsole(ActivateTvMissile, string.Format(matchingSetting.Template, command.Value));
        GameServer.GameWriter.SendText($"TV Missile {matchingSetting.Name} set to {command.Value}");

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
            GameServer.GameWriter.SendConsole(
                ActivateTvMissile,
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
            GameServer.GameWriter.SendText("Setting not found");
            return ValueTask.CompletedTask;
        }

        GameServer.GameWriter.SendText($"TV Missile set to {command.Type}");
        return ValueTask.CompletedTask;
    }

    private void SetDefaultTvMissile()
    {
        GameServer.GameWriter.SendConsole(
            ActivateTvMissile,
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

    public void Handle(ToggleTvLogCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
        {
            GameServer.GameWriter.SendText("Player not found");
            return;
        }

        if (_tvLogPlayers.ContainsKey(player.Hash))
        {
            _tvLogPlayers.Remove(player.Hash, out _);
            GameServer.GameWriter.SendText($"Disabled TV logging for {player.DisplayName}");
        }
        else
        {
            _tvLogPlayers.TryAdd(player.Hash, new ProjectilePath());
            GameServer.GameWriter.SendText($"Enabled TV logging for {player.DisplayName}");
        }
    }

    public ValueTask HandleAsync(ProjectilePositionEvent e)
    {
        if (!Is2v2Active || e.Projectile.Owner == null)
            return ValueTask.CompletedTask;

        if (!_tvLogPlayers.TryGetValue(e.Projectile.Owner.Hash, out var path))
            return ValueTask.CompletedTask;

        var now = _timeProvider.GetUtcNow();
        if (path.Projectile?.Id != e.Projectile.Id)
        {
            path.Projectile = e.Projectile;
            path.Start = now;
            path.Path.Clear();
            _taskRunner.RunBackgroundTask("Send projectile message", async () =>
            {
                // TODO: get max X and Y angles per s or ms and see what you can get max as default
                await _delayProvider.DelayAsync(4000, ModuleCancellationToken);
                var (distance, angle) = MatchStatistics.TvShot(path.Path);
                GameServer.GameWriter.SendText($"{e.Projectile.Owner.ShortName} TV: est. {distance:###} m {angle:###} degrees");
            }, ModuleCancellationToken);
        }

        path.Path.Add(new RoundPosition
        {
            Id = e.Projectile.Id,
            Timestamp = (int)(now - path.Start).TotalMilliseconds,
            Position = e.Projectile.Position,
            Rotation = e.Projectile.Rotation,
            Ping = e.Projectile.Owner.Score.Ping
        });

        return ValueTask.CompletedTask;
    }
}

/// <summary>The samples of a logged player's latest missile.</summary>
public class ProjectilePath
{
    public Projectile Projectile { get; set; }
    public DateTimeOffset Start { get; set; }
    public IList<RoundPosition> Path { get; set; } = new List<RoundPosition>();
}

public class TvMissileSetting
{
    public string Name { get; set; }
    public string Template { get; set; }
    public string DefaultValue { get; set; }
}
