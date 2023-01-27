using System.Collections.Concurrent;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules.BF2;

public class JailModule : BaseModule,
    IHandleCommandAsync<JailPlayerCommand>,
    IHandleCommandAsync<FreePlayerCommand>,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleEventAsync<PlayerSpawnEvent>
{
    // TODO: datetime when they were jailed, only keep for 1 hour
    private readonly ConcurrentDictionary<string, DateTime> _jailedPlayers = new();
    // private readonly Position _jailPosition = new(777.500, 164.000, -5.000); // dalian carrier south
    // private readonly Position _jailPosition = new(776.600, 164.000, 181.200); // dalian carrier north
    private readonly Position _jailPosition = new(784.300, 174.000, 65.400); // dalian carrier center

    public JailModule(IGameServer server, ILogger<JailModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
    }

    public async ValueTask HandleAsync(JailPlayerCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        var jailExists = _jailedPlayers.Any();
        _jailedPlayers.AddOrUpdate(player.Hash, id => DateTime.UtcNow, (id, p) => DateTime.UtcNow);
        GameServer.GameWriter.SendText($"Player {player.Name} has been jailed");

        if (jailExists)
            return;
        
        RunBackgroundTask("Player jail teleport", async () =>
        {
            while (!ModuleCancellationToken.IsCancellationRequested && _jailedPlayers.Any())
            {
                var heightMod = 0;

                foreach (var (prisonerHash, jailedTime) in _jailedPlayers)
                {
                    // Player has served their time
                    var isTimeOver = (DateTime.UtcNow - jailedTime).TotalHours > 1;
                    if (isTimeOver)
                    {
                        _jailedPlayers.TryRemove(prisonerHash, out _);
                        GameServer.GameWriter.SendText("Player has been freed after 1 hour");
                        continue;
                    }
                    
                    // Player disconnected - don't remove in case they rejoin
                    var prisoner = GameServer.GetPlayerByHash(prisonerHash);
                    if (prisoner == null)
                    {
                        continue;
                    }

                    // No escape
                    var distance = _jailPosition.Distance(prisoner.Position);
                    if (distance > 100)
                    {
                        GameServer.GameWriter.SendTeleport(prisoner, new Position(_jailPosition.X, _jailPosition.Height + heightMod, _jailPosition.Y));
                        heightMod += 10;
                    }
                }

                await Task.Delay(2_000);
            }
        });
    }

    public async ValueTask HandleAsync(FreePlayerCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        _jailedPlayers.TryRemove(player.Hash, out _);
        GameServer.GameWriter.SendText($"Player {player.Name} has been freed");
    }

    public async ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _jailedPlayers.TryRemove(e.Player.Hash, out _);
    }

    public async ValueTask HandleAsync(PlayerSpawnEvent e)
    {
        if (!_jailedPlayers.ContainsKey(e.Player.Hash))
            return;

        GameServer.GameWriter.SendTeleport(e.Player, _jailPosition);
    }
}
