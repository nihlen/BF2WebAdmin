using System.Collections.Concurrent;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// Heli2v2's practice and admin tools, apart from the matches: back to the pad (.pad, .autopad), an altitude limit
/// (.nasa), no collisions (.noclip), following a player (.stalk), the heli MG (.helimg) and removing fences (.nofences).
/// Loaded with <see cref="Heli2v2Module"/>; the commands that are about 2v2 rounds work while its 2v2 mode is on.
/// The altitude limit is the game script "nasa" where the server runs scripts (a soft ceiling, checked every tick), and
/// else a teleport back to the limit when a position update shows a player above it.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class PracticeModule : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleEventAsync<PlayerVehicleEvent>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<RoundEndEvent>,
    IHandleEventAsync<SocketStateChangedEvent>,
    IHandleEventAsync<ScriptFinishedEvent>,
    IHandleCommand<HeliMgCommand>,
    IHandleCommand<NoclipCommand>,
    IHandleCommand<StalkCommand>,
    IHandleCommand<StopCommand>,
    IHandleCommandAsync<PadCommand>,
    IHandleCommandAsync<AutoPadCommand>,
    IHandleCommandAsync<AutoPadAllCommand>,
    IHandleCommandAsync<NasaCommand>,
    IHandleCommandAsync<NoFencesCommand>
{
    private readonly IDelayProvider _delayProvider;
    private readonly ITaskRunner _taskRunner;

    public const string NasaScript = "nasa";

    private int _antiNasaAltitude;
    private bool _antiNasaActive;
    private string? _mapName;
    // The altitude limit as a game script, while one runs; without it the position updates enforce the limit
    private IScriptRun? _nasaScript;
    private int _nasaScriptAltitude;
    private bool _nasaScriptStarting;
    private bool _nasaScriptFailed;

    private Player? _stalker;
    private Player? _stalked;

    private readonly ConcurrentDictionary<string, bool> _autopadPlayerHashes = new();
    // The vehicles .noclip took collision from (this module's state, not the game's)
    private readonly HashSet<Vehicle> _noclipVehicles = new();

    public PracticeModule(IGameServer server, IDelayProvider delayProvider, ITaskRunner taskRunner, ILogger<PracticeModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _delayProvider = delayProvider;
        _taskRunner = taskRunner;

        // A module reload: the script is this module's, and the new module starts without a limit
        ModuleCancellationToken.Register(() => _ = StopScriptAsync(_nasaScript));
    }

    private bool Is2v2Active => GameServer.ModManager?.GetModule<Heli2v2Module>()?.RoundsActive ?? false;

    public ValueTask HandleAsync(MapChangedEvent e)
    {
        // A new round has new vehicles
        _noclipVehicles.Clear();

        // The altitude limit goes with the map (it went with the match, which a new map ends)
        if (_mapName != e.Map.Name)
        {
            _antiNasaAltitude = 0;
            _antiNasaActive = false;
        }

        _mapName = e.Map.Name;

        // A level unload ends the script; on the same map the limit stays, so the script starts again
        SyncNasaScript();
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(SocketStateChangedEvent e)
    {
        // Scripts do not outlive their link: after a reconnect it starts again
        if (e.SocketState == Communication.SocketState.Connected)
            SyncNasaScript();

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ScriptFinishedEvent e)
    {
        if (e.Script != _nasaScript)
            return ValueTask.CompletedTask;

        _nasaScript = null;
        if (e.Result.Failed && _antiNasaActive)
        {
            // Not started again until .nasa is used again; the position updates enforce the limit meanwhile
            _nasaScriptFailed = true;
            GameServer.GameWriter.SendText("Anti NASA script failed, using the slower altitude limit");
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Starts, updates or stops the script off the event handler, which the admin channel waits for.</summary>
    private void SyncNasaScript() =>
        _taskRunner.RunBackgroundTask("Anti NASA script", ApplyNasaScriptAsync, ModuleCancellationToken);

    /// <summary>Makes the script match the limit: running with the current altitude, or stopped. Does nothing on a server without scripts.</summary>
    private async Task ApplyNasaScriptAsync()
    {
        if (!_antiNasaActive)
        {
            var stopped = _nasaScript;
            _nasaScript = null;
            await StopScriptAsync(stopped);
            return;
        }

        if (_nasaScriptStarting || _nasaScriptFailed || !GameServer.Scripts.Available)
            return;

        var altitude = _antiNasaAltitude;
        try
        {
            if (_nasaScript is { IsRunning: true } running)
            {
                if (_nasaScriptAltitude != altitude)
                {
                    _nasaScriptAltitude = altitude;
                    await running.SendAsync(new { altitude });
                }

                return;
            }

            _nasaScriptStarting = true;
            try
            {
                _nasaScript = await GameServer.Scripts.StartAsync(NasaScript, new { altitude });
                _nasaScriptAltitude = altitude;
            }
            finally
            {
                _nasaScriptStarting = false;
            }
        }
        catch (AdminCommandException ex)
        {
            Logger.LogWarning("Anti NASA script on {ServerId}: {Error}; using the position updates", GameServer.Id, ex.Message);
            return;
        }

        // .nasa was used again while the script started
        if (!_antiNasaActive || _antiNasaAltitude != altitude)
            await ApplyNasaScriptAsync();
    }

    public ValueTask HandleAsync(PlayerPositionEvent e)
    {
        if (_stalker is not null && _stalked is not null)
        {
            if (e.Player.Index == _stalked.Index)
            {
                var newPosition = new Position(e.Position.X, e.Position.Height + 10, e.Position.Y);
                GameServer.GameWriter.SendTeleport(_stalker, newPosition);
            }
        }

        if (_antiNasaActive && _nasaScript is not { IsRunning: true })
        {
            if (e.Position.Height >= _antiNasaAltitude)
            {
                var newPosition = new Position(e.Position.X, _antiNasaAltitude, e.Position.Y);
                GameServer.GameWriter.SendTeleport(e.Player, newPosition);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerVehicleEvent e)
    {
        // Out of a vehicle, or straight into another one (a death and the stream correction also come here); whatever
        // the 2v2 mode is now, since .noclip may have been used while it was on
        if (e.Player.PreviousVehicle is { } left && left != e.Vehicle)
            RestoreCollisionWhenEmpty(left);

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerLeftEvent e)
    {
        // A player who leaves the server in a vehicle gets no vehicle event; the vehicle has already let go of the player
        if (e.Player.Vehicle is { } left)
            RestoreCollisionWhenEmpty(left);

        return ValueTask.CompletedTask;
    }

    /// <summary>Gives collision back to a vehicle .noclip took it from, once its last player is out. Vehicles .noclip never touched are left alone.</summary>
    private void RestoreCollisionWhenEmpty(Vehicle vehicle)
    {
        if (vehicle.Players.Count > 0 || !_noclipVehicles.Remove(vehicle))
            return;

        GameServer.GameWriter.SetVehicleCollision(vehicle, true);
    }

    public ValueTask HandleAsync(RoundEndEvent e)
    {
        if (!Is2v2Active)
            return ValueTask.CompletedTask;

        // .autopad: the winners go back to their pad
        _taskRunner.RunBackgroundTask("Handle end of round teleport", async () =>
        {
            var winningPlayers = e.Round.Players.Where(p => p.TeamHash == e.Round.WinningTeamHash && p.IsAlive).ToList();
            if (winningPlayers.Any(p => _autopadPlayerHashes.ContainsKey(p.Player.Hash)))
            {
                await _delayProvider.DelayAsync(2000, ModuleCancellationToken);
                FreezeAtPad(0, winningPlayers.First().Player);
            }
        }, ModuleCancellationToken);

        return ValueTask.CompletedTask;
    }

    public void Handle(HeliMgCommand command)
    {
        if (command.Value == 0)
        {
            GameServer.GameWriter.SendConsole(RconScript.MgOff);
            GameServer.GameWriter.SendText("Heli MG off");
        }

        if (command.Value == 1)
        {
            GameServer.GameWriter.SendConsole(RconScript.MgOn);
            GameServer.GameWriter.SendText("Heli MG on");
        }
    }

    public void Handle(NoclipCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        if (player.Vehicle == null)
            return;

        var hasCollision = command.Value != 1;
        if (!GameServer.GameWriter.SetVehicleCollision(player.Vehicle, hasCollision))
        {
            GameServer.GameWriter.SendText("This server can't turn off collision");
            return;
        }

        if (hasCollision)
            _noclipVehicles.Remove(player.Vehicle);
        else
            _noclipVehicles.Add(player.Vehicle);
    }

    public void Handle(StalkCommand command)
    {
        _stalker = command.Message.Player;
        _stalked = GameServer.GetPlayerByName(command.Name);
    }

    public void Handle(StopCommand command)
    {
        _stalker = null;
        _stalked = null;

        GameServer.GameWriter.SendConsole(RconScript.InitServer);
    }

    public ValueTask HandleAsync(PadCommand command)
    {
        // TODO: Teleport all full helis back to their respective pad (stack multiple)
        FreezeAtPad(command.Message.Player.Index, GameServer.Players.ToArray());
        return ValueTask.CompletedTask;
    }

    private void FreezeAtPad(int requesterPlayerIndex, params Player[] players)
    {
        var usPadPos = new Position(239.8, 166.0, -249.9);
        var chPadPos = new Position(-222.8, 166.0, 151.0);

        // Level, facing the other pad
        var usPadRot = new Rotation(YawTowards(usPadPos, chPadPos), 0.0, 0.0);
        var chPadRot = new Rotation(YawTowards(chPadPos, usPadPos), 0.0, 0.0);

        foreach (var player in players)
        {
            if (player.Vehicle == null || !player.Vehicle.RootVehicleTemplate.Contains("ahe"))
                continue;

            if (players.Length > 4 && player.Vehicle.Players.Count < 2 && player.Index != requesterPlayerIndex)
                continue;

            GameServer.GameWriter.SendHealth(player, 875);
            if (player.Team.Id == 1)
            {
                GameServer.GameWriter.SendTeleport(player, chPadPos, chPadRot);
            }
            else if (player.Team.Id == 2)
            {
                GameServer.GameWriter.SendTeleport(player, usPadPos, usPadRot);
            }
        }
    }

    // BF2 yaw in degrees: 0 faces +Z (Position.Y), 90 faces +X
    private static double YawTowards(Position from, Position to) =>
        Math.Atan2(to.X - from.X, to.Y - from.Y) * 180 / Math.PI;

    public ValueTask HandleAsync(AutoPadCommand command)
    {
        if (!Is2v2Active)
        {
            GameServer.GameWriter.SendText("2v2 mode is not currently active");
            return ValueTask.CompletedTask;
        }

        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
        {
            GameServer.GameWriter.SendText("Player not found");
            return ValueTask.CompletedTask;
        }

        if (_autopadPlayerHashes.ContainsKey(player.Hash))
        {
            GameServer.GameWriter.SendText($"Autopad disabled for {player.DisplayName}");
            _autopadPlayerHashes.Remove(player.Hash, out _);
        }
        else
        {
            GameServer.GameWriter.SendText($"Autopad enabled for {player.DisplayName}");
            _autopadPlayerHashes.AddOrUpdate(player.Hash, true, (k, v) => true);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(AutoPadAllCommand command)
    {
        if (!Is2v2Active)
        {
            GameServer.GameWriter.SendText("2v2 mode is not currently active");
            return ValueTask.CompletedTask;
        }

        throw new NotImplementedException();
    }

    public async ValueTask HandleAsync(NasaCommand command)
    {
        _nasaScriptFailed = false;
        if (command.Value == 0)
        {
            _antiNasaActive = false;
            GameServer.GameWriter.SendText("Anti NASA inactive");
            await ApplyNasaScriptAsync();
            return;
        }

        _antiNasaAltitude = command.Value + MatchStatistics.AltitudeOffset;
        _antiNasaActive = true;
        GameServer.GameWriter.SendText($"Anti NASA active ({_antiNasaAltitude - MatchStatistics.AltitudeOffset} m)");
        await ApplyNasaScriptAsync();
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
            // Static map objects have a console (grid) id and usually no networkable id
            var objects = await GameServer.GameWriter.ListObjectsAsync(fenceTemplate);
            foreach (var o in objects)
            {
                GameServer.GameWriter.SendDeleteObject(o.ConsoleId);
                removedObjects++;
            }
        }

        GameServer.GameWriter.SendText($"Removed {removedObjects} fences and lamp posts");
    }
}
