using System.Collections.Concurrent;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// .jail keeps a player at the jail for an hour: teleported back when they spawn or get more than 100 m away. Leaving
/// the server doesn't end it; the hour, .free or a module reload do.
/// </summary>
public class JailModule : BaseModule,
    IHandleCommandAsync<JailPlayerCommand>,
    IHandleCommandAsync<FreePlayerCommand>,
    IHandleEventAsync<PlayerSpawnEvent>
{
    private static readonly TimeSpan Sentence = TimeSpan.FromHours(1);

    private readonly ITaskRunner _taskRunner;

    // Player hash and when they were jailed
    private readonly ConcurrentDictionary<string, DateTime> _jailedPlayers = new();
    private readonly Position _jailPosition = new(784.300, 174.000, 65.400); // dalian carrier center
    private int _guardRunning;

    public JailModule(IGameServer server, ITaskRunner taskRunner, ILogger<JailModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _taskRunner = taskRunner;
    }

    public ValueTask HandleAsync(JailPlayerCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return ValueTask.CompletedTask;

        _jailedPlayers[player.Hash] = DateTime.UtcNow;
        GameServer.GameWriter.SendText($"Player {player.Name} has been jailed");
        StartGuard();
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(FreePlayerCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return ValueTask.CompletedTask;

        _jailedPlayers.TryRemove(player.Hash, out _);
        GameServer.GameWriter.SendText($"Player {player.Name} has been freed");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (_jailedPlayers.ContainsKey(e.Player.Hash))
            GameServer.GameWriter.SendTeleport(e.Player, _jailPosition);

        return ValueTask.CompletedTask;
    }

    /// <summary>Starts the loop that keeps the prisoners in, unless it runs; it ends when the jail is empty.</summary>
    private void StartGuard()
    {
        if (Interlocked.Exchange(ref _guardRunning, 1) == 1)
            return;

        _taskRunner.RunBackgroundTask("Player jail teleport", async () =>
        {
            try
            {
                while (!ModuleCancellationToken.IsCancellationRequested && !_jailedPlayers.IsEmpty)
                {
                    GuardPrisoners();
                    await Task.Delay(2_000, ModuleCancellationToken);
                }
            }
            finally
            {
                Volatile.Write(ref _guardRunning, 0);

                // A player jailed while the loop was ending
                if (!_jailedPlayers.IsEmpty && !ModuleCancellationToken.IsCancellationRequested)
                    StartGuard();
            }
        }, ModuleCancellationToken);
    }

    private void GuardPrisoners()
    {
        var heightMod = 0;
        foreach (var (prisonerHash, jailedTime) in _jailedPlayers)
        {
            if (DateTime.UtcNow - jailedTime > Sentence)
            {
                _jailedPlayers.TryRemove(prisonerHash, out _);
                GameServer.GameWriter.SendText("Player has been freed after 1 hour");
                continue;
            }

            // Not on the server: kept, in case they rejoin
            var prisoner = GameServer.GetPlayerByHash(prisonerHash);
            if (prisoner?.Position == null)
                continue;

            // No escape
            if (_jailPosition.Distance(prisoner.Position) > 100)
            {
                GameServer.GameWriter.SendTeleport(prisoner, new Position(_jailPosition.X, _jailPosition.Height + heightMod, _jailPosition.Y));
                heightMod += 10;
            }
        }
    }
}
