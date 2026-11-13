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
    private const string TvMissileType = "GenericProjectile";
    private const string TvMissileTemplate = "agm114_hellfire_tv";

    public static readonly IReadOnlyList<TvMissileSetting> DefaultSettings =
    [
        new("trackingDelay", "seek.trackingDelay", "0.2"),
        new("maxYaw", "follow.maxYaw", "1.5"),
        new("maxPitch", "follow.maxPitch", "1.5"),
        new("changePitch", "follow.changePitch", "0.2"),
        new("changeYaw", "follow.changeYaw", "0.2"),
        new("minDist", "follow.minDist", "10"),
        new("drag", "drag", "0.1"),
        new("mass", "mass", "500"),
        // No followStiffness: it exists on Camera and doesn't work server side
        new("timeToLive", "timeToLive", "3.5", "CRD_NONE/{0}/0/0"),
        new("maxSpeed", "maxSpeed", "125"),
    ];

    /// <summary>The "test" missile: no drag or mass and fast turns.</summary>
    private static readonly IReadOnlyDictionary<string, string> TestValues = new Dictionary<string, string>
    {
        ["changePitch"] = "100",
        ["changeYaw"] = "100",
        ["drag"] = "0",
        ["mass"] = "0",
    };

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
            var value = await GameServer.GameWriter.GetTemplateValueAsync(TvMissileType, TvMissileTemplate, defaultSetting.Property);
            result.Add((defaultSetting.Name, value, defaultSetting.DefaultValue));
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

        GameServer.GameWriter.SetTemplate(TvMissileType, TvMissileTemplate, matchingSetting.ToValue(command.Value));
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
            SetTvMissile(name => TestValues.GetValueOrDefault(name));
        }
        else
        {
            GameServer.GameWriter.SendText("Setting not found");
            return ValueTask.CompletedTask;
        }

        GameServer.GameWriter.SendText($"TV Missile set to {command.Type}");
        return ValueTask.CompletedTask;
    }

    private void SetDefaultTvMissile() => SetTvMissile(_ => null);

    /// <summary>Every TV missile setting: the value <paramref name="valueOf"/> gives, else the default.</summary>
    private void SetTvMissile(Func<string, string?> valueOf)
    {
        GameServer.GameWriter.SetTemplate(TvMissileType, TvMissileTemplate,
            DefaultSettings.Select(setting => setting.ToValue(valueOf(setting.Name) ?? setting.DefaultValue)).ToArray());
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

/// <summary>A TV missile setting: its name in .tv, the template property, the default and how a value is written.</summary>
public sealed record TvMissileSetting(string Name, string Property, string DefaultValue, string ValueFormat = "{0}")
{
    public (string Property, string Value) ToValue(string value) => (Property, string.Format(ValueFormat, value));
}
