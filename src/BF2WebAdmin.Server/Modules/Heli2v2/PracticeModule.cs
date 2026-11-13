using System.Collections.Concurrent;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// Heli2v2's practice and admin tools, apart from the matches: back to the pad (.pad, .autopad), an altitude limit
/// (.nasa), no collisions (.noclip), following a player (.stalk), the heli MG (.helimg) and removing fences (.nofences).
/// Loaded with <see cref="Heli2v2Module"/>; the commands that are about 2v2 rounds work while its 2v2 mode is on.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class PracticeModule : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleEventAsync<PlayerVehicleEvent>,
    IHandleEventAsync<RoundEndEvent>,
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

    private int _antiNasaAltitude;
    private bool _antiNasaActive;
    private string? _mapName;

    private Player? _stalker;
    private Player? _stalked;

    private readonly ConcurrentDictionary<string, bool> _autopadPlayerHashes = new();
    // The vehicles .noclip took collision from (this module's state, not the game's)
    private readonly HashSet<Vehicle> _noclipVehicles = new();

    public PracticeModule(IGameServer server, IDelayProvider delayProvider, ITaskRunner taskRunner, ILogger<PracticeModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _delayProvider = delayProvider;
        _taskRunner = taskRunner;
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
        return ValueTask.CompletedTask;
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

        if (_antiNasaActive)
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
        if (!Is2v2Active)
            return ValueTask.CompletedTask;

        // TODO: check if player is the last in the vehicle
        var player = e.Player;
        if (e.Vehicle is not null || player?.PreviousVehicle is not { } left || !_noclipVehicles.Contains(left) && left.Players.Any())
            return ValueTask.CompletedTask;

        _noclipVehicles.Remove(left);
        GameServer.GameWriter.SetVehicleCollision(left, true);

        return ValueTask.CompletedTask;
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
        if (hasCollision)
            _noclipVehicles.Remove(player.Vehicle);
        else
            _noclipVehicles.Add(player.Vehicle);

        GameServer.GameWriter.SetVehicleCollision(player.Vehicle, hasCollision);
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

    public ValueTask HandleAsync(NasaCommand command)
    {
        if (command.Value == 0)
        {
            _antiNasaActive = false;
            GameServer.GameWriter.SendText("Anti NASA inactive");
            return ValueTask.CompletedTask;
        }

        _antiNasaAltitude = command.Value + MatchStatistics.AltitudeOffset;
        _antiNasaActive = true;
        GameServer.GameWriter.SendText($"Anti NASA active ({_antiNasaAltitude - MatchStatistics.AltitudeOffset} m)");

        return ValueTask.CompletedTask;
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
