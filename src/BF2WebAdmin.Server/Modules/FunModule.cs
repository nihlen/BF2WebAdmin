using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// What admins do to players: dance, freeze, blur (ended by .stop), flip, repair, teleport, rank and score. Loaded on
/// every server with <see cref="BF2Module"/>.
/// </summary>
[LoadedWith(typeof(BF2Module))]
public class FunModule : BaseModule,
    IHandleCommand<StopCommand>,
    IHandleCommand<FlipCommand>,
    IHandleCommand<RepairCommand>,
    IHandleCommand<RepairAllCommand>,
    IHandleCommand<RankCommand>,
    IHandleCommand<ScoreCommand>,
    IHandleCommand<ScoreResetCommand>,
    IHandleCommand<TeleportLocationCommand>,
    IHandleCommand<TeleportCoordinatesCommand>,
    IHandleCommandAsync<DanceCommand>,
    IHandleCommandAsync<DanceAllCommand>,
    IHandleCommandAsync<FreezeCommand>,
    IHandleCommandAsync<FreezeAllCommand>,
    IHandleCommandAsync<BlurCommand>
{
    private const int DefaultDelay = 25; // 1000/35 fps ~ 28.5
    private static readonly TimeSpan FreezeDuration = TimeSpan.FromHours(1);

    private readonly Random _random = new();

    // The running effects (dance, blur, freeze) end when this is cancelled: by .stop, which starts a new one, or by a
    // module reload
    private CancellationTokenSource _stop;

    public FunModule(IGameServer server, ILogger<FunModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(ModuleCancellationToken);
    }

    private CancellationToken StopToken => Volatile.Read(ref _stop).Token;

    public void Handle(StopCommand command)
    {
        var stopped = Interlocked.Exchange(ref _stop, CancellationTokenSource.CreateLinkedTokenSource(ModuleCancellationToken));
        stopped.Cancel();
        stopped.Dispose();

        Unfreeze(GameServer.Players.ToArray());
    }

    public void Handle(FlipCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        var newRotation = new Rotation(player.Rotation.Yaw, player.Rotation.Pitch, 180);
        GameServer.GameWriter.SendRotate(player, newRotation);
    }

    public void Handle(RepairCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendHealthFraction(player, 1);
    }

    public void Handle(RepairAllCommand command)
    {
        foreach (var player in GameServer.Players)
        {
            GameServer.GameWriter.SendHealthFraction(player, 1);
        }
    }

    public void Handle(RankCommand command)
    {
        var validRank = Enum.IsDefined(typeof(Rank), command.Rank);
        if (!validRank)
            return;

        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendRank(player, (Rank)command.Rank, true);
    }

    public void Handle(ScoreCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendScore(player, command.TotalScore, command.TeamScore, command.Kills, command.Deaths);
    }

    public void Handle(ScoreResetCommand command)
    {
        if (command.Reset.ToLower() != "reset")
            return;

        foreach (var player in GameServer.Players)
        {
            GameServer.GameWriter.SendScore(player, 0, 0, 0, 0);
        }
    }

    public void Handle(TeleportLocationCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendTeleport(player, new Position(0, 300, 0), LevelRotation(player));
    }

    public void Handle(TeleportCoordinatesCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        var newPosition = new Position(command.X, command.Altitude, command.Y);
        GameServer.GameWriter.SendTeleport(player, newPosition, LevelRotation(player));
    }

    // Keeps the heading but clears pitch and roll, so a tilted vehicle doesn't arrive tilted
    private static Rotation LevelRotation(Player player) => new(player.Rotation?.Yaw ?? 0, 0, 0);

    public async ValueTask HandleAsync(DanceCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        await DanceAsync(5000, player);
    }

    public async ValueTask HandleAsync(DanceAllCommand command)
    {
        await DanceAsync(5000, GameServer.Players.ToArray());
    }

    private async Task DanceAsync(long duration, params Player[] players)
    {
        var snapshots = players.Select(PlayerSnapshot.Save).ToList();
        var startTime = DateTime.UtcNow;
        var stop = StopToken;
        while (IsActive(duration, startTime, stop))
        {
            foreach (var playerSnapshot in snapshots)
            {
                var newRotation = new Rotation(_random.Next(-180, 180), _random.Next(-180, 180), _random.Next(-180, 180));
                GameServer.GameWriter.SendTeleport(playerSnapshot.Player, playerSnapshot.Position, newRotation);
            }
            await Task.Delay(DefaultDelay);
        }

        foreach (var playerSnapshot in snapshots)
        {
            GameServer.GameWriter.SendTeleport(playerSnapshot.Player, playerSnapshot.Position, new Rotation(playerSnapshot.Rotation.Yaw, 0, 0));
        }
    }

    public async ValueTask HandleAsync(FreezeCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        await FreezeAsync(FreezeDuration, player);
    }

    public async ValueTask HandleAsync(FreezeAllCommand command)
    {
        await FreezeAsync(FreezeDuration, GameServer.Players.ToArray());
    }

    /// <summary>
    /// Freezes the players' vehicles until the time is up, .stop (which unfreezes everyone) or a module reload (which
    /// unfreezes them here, so a reload leaves nobody frozen and no hour-long task holds the old module).
    /// </summary>
    private async Task FreezeAsync(TimeSpan duration, params Player[] players)
    {
        var stop = StopToken;
        foreach (var player in players)
        {
            if (player.Vehicle == null)
                continue;

            GameServer.GameWriter.SetVehicleDisabled(player.Vehicle, true);
        }

        try
        {
            await Task.Delay(duration, stop);
        }
        catch (OperationCanceledException) when (!ModuleCancellationToken.IsCancellationRequested)
        {
            return; // .stop
        }
        catch (OperationCanceledException)
        {
            // Reload: unfreeze below
        }

        try
        {
            Unfreeze(players);
        }
        catch (Exception ex) when (ModuleCancellationToken.IsCancellationRequested)
        {
            Logger.LogWarning(ex, "Unfreezing on {ServerId} after a module reload failed", GameServer.Id);
        }
    }

    private void Unfreeze(params Player[] players)
    {
        foreach (var player in players)
        {
            var vehicle = player.Vehicle ?? player.PreviousVehicle;
            if (vehicle == null)
                continue;

            GameServer.GameWriter.SetVehicleDisabled(vehicle, false);
        }
    }

    public async ValueTask HandleAsync(BlurCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        await BlurAsync(2000, player);
    }

    private async Task BlurAsync(long duration, params Player[] players)
    {
        var startTime = DateTime.UtcNow;
        var stop = StopToken;
        while (IsActive(duration, startTime, stop))
        {
            foreach (var player in players)
            {
                GameServer.GameWriter.SendGameEvent(player, 13, 1);
            }
            await Task.Delay(DefaultDelay);
        }
    }

    private static bool IsActive(long duration, DateTime startTime, CancellationToken stop)
    {
        return (DateTime.UtcNow - startTime).TotalMilliseconds < duration && !stop.IsCancellationRequested;
    }
}

public class PlayerSnapshot
{
    public Player Player { get; }
    public Position Position { get; }
    public Rotation Rotation { get; }

    private PlayerSnapshot(Player player)
    {
        Player = player;
        Position = Player.Position;
        Rotation = Player.Rotation;
    }

    public static PlayerSnapshot Save(Player player)
    {
        return new PlayerSnapshot(player);
    }
}
