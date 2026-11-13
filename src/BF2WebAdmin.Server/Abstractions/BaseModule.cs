using System.Diagnostics;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Abstractions;

public abstract class BaseModule : IModule
{
    protected readonly IGameServer GameServer;
    protected readonly ILogger Logger;
    protected readonly CancellationToken ModuleCancellationToken;
    protected IMediator Mediator => GameServer.ModManager.Mediator;

    protected BaseModule(IGameServer gameServer, ILogger logger, CancellationTokenSource moduleCancellationTokenSource)
    {
        GameServer = gameServer;
        Logger = logger;
        ModuleCancellationToken = moduleCancellationTokenSource.Token;
    }

    protected virtual void SpawnObject(string template, Position position, Rotation rotation, bool showMessage = false, int count = 0)
    {
        var replacements = new Dictionary<string, string>
        {
            { "{TEMPLATE}", template },
            { "{POSITION}", position.ToString() },
            { "{ROTATION}", rotation.ToString() }
        };

        var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
        GameServer.GameWriter.SendConsole(script.ToArray());

        if (showMessage)
            GameServer.GameWriter.SendText($"{count} Spawned '{template}' at {position}");
    }

    protected virtual void KillPlayer(Player player)
    {
        GameServer.GameWriter.SendKill(player, destroyVehicle: true);
    }

    protected void SwitchAll()
    {
        GameServer.GameWriter.SendText("Switching teams");

        foreach (var player in GameServer.Players)
            SwitchPlayer(player);
    }

    protected void SwitchPlayer(Player player)
    {
        // SetTeam with its kill flag: the switch and the kill run in one tick
        GameServer.GameWriter.SendTeam(player, player.Team.Id == 1 ? 2 : 1, kill: true);
    }

    /// <summary>An answer with the IP addresses removed, for chat and Discord.</summary>
    public static string GetObfuscatedResponse(string text)
    {
        var result = Regex.Replace(
            text,
            @"((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
            "[IP REMOVED]",
            RegexOptions.Compiled
        );

        return result.Length > 1 ? result : "Empty response";
    }
}
