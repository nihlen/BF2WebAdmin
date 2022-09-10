using System.Text.RegularExpressions;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using Serilog;

namespace BF2WebAdmin.Server.Abstractions;

public abstract class BaseModule : IModule
{
    protected readonly IGameServer GameServer;
    protected IMediator Mediator => GameServer.ModManager.Mediator;

    protected BaseModule(IGameServer gameServer)
    {
        GameServer = gameServer;
    }

    protected virtual void SpawnObject(string template, Position position, Rotation rotation, bool showMessage = false, int count = 0)
    {
        var replacements = new Dictionary<string, string>
        {
            {"{TEMPLATE}", template},
            {"{POSITION}", position.ToString()},
            {"{ROTATION}", rotation.ToString()}
        };

        var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
        GameServer.GameWriter.SendRcon(script.ToArray());

        if (showMessage)
            GameServer.GameWriter.SendText($"{count} Spawned '{template}' at {position}");
    }

    protected virtual void KillPlayer(Player player)
    {
        GameServer.GameWriter.SendHealth(player, 1);
    }
        
    protected void SwitchAll()
    {
        GameServer.GameWriter.SendText("Switching teams");

        foreach (var player in GameServer.Players)
            SwitchPlayer(player);
    }

    protected void SwitchPlayer(Player player)
    {
        GameServer.GameWriter.SendTeam(player, player.Team.Id == 1 ? 2 : 1);
        GameServer.GameWriter.SendHealth(player, 1);
    }

    protected static string GetRconCommand(string text)
    {
        if (text.StartsWith("!m ")) return "map" + text[2..];
        if (text.StartsWith("!map ")) return "map" + text[4..];
        if (text.StartsWith("!w ")) return "iga warn" + text[2..];
        if (text.StartsWith("!warn ")) return "iga warn" + text[5..];
        if (text.StartsWith("!k ")) return "iga kick" + text[2..];
        if (text.StartsWith("!kick ")) return "iga kick" + text[5..];
        if (text.StartsWith("!b ")) return "iga ban" + text[2..];
        if (text.StartsWith("!ban ")) return "iga ban" + text[4..];
        return text.TrimStart('!');
    }

    protected static string GetObfuscatedResponse(string text)
    {
        var result = Regex.Replace(
            text,
            @"((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
            "[IP REMOVED]",
            RegexOptions.Compiled
        );

        return result.Length > 1 ? result : "Empty response";
    }

    protected async Task<string> SendRconCommandAsync(string command)
    {
        var rcon = new RconClient(GameServer.ConnectedIpAddress, GameServer.ServerInfo.RconPort, GameServer.ServerInfo.RconPassword);
        return await rcon.SendAsync(command);
    }

    protected void RunBackgroundTask(string description, Func<Task> func)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to complete background task: {Description}", description);
            }
        });
    }
}