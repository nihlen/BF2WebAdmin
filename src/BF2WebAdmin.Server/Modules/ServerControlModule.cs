using System.Globalization;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// The game server itself: restart (.restartserver) and crash it, gravity, the script mods (artillery, flares, auto
/// repair) and the position timer. Loaded on every server with <see cref="BF2Module"/>.
/// </summary>
[LoadedWith(typeof(BF2Module))]
public class ServerControlModule : BaseModule,
    IHandleCommandAsync<CrashCommand>,
    IHandleCommandAsync<RestartServerCommand>,
    IHandleCommand<GravityCommand>,
    IHandleCommand<GravityDefaultCommand>,
    IHandleCommand<ScriptModCommand>,
    IHandleCommand<AutoRepairCommand>,
    IHandleCommand<TimerIntervalCommand>
{
    private bool _autoRepairOn;

    public ServerControlModule(IGameServer server, ILogger<ServerControlModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
    }

    public async ValueTask HandleAsync(CrashCommand command)
    {
        var replacements = new Dictionary<string, string>
        {
            {"{TEMPLATE}", "air_j10"},
            {"{POSITION}", "0/0/0"},
            {"{ROTATION}", "0/0/0"}
        };

        GameServer.GameWriter.SendText($"{command.Message.Player.DisplayName} is crashing the server with no survivors");
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameServer.GameWriter.SendText("3", false);
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameServer.GameWriter.SendText("2", false);
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameServer.GameWriter.SendText("1", false);

        var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
        GameServer.GameWriter.SendConsole(script.ToArray());
    }

    /// <summary>
    /// A clean restart: the console's quit makes the engine exit with code 0 at the next frame (the module unloads and the
    /// admin channel closes normally), and the container's restart policy starts it again. Unlike .crash, nothing is
    /// left half-written, and no crash dump or non-zero exit is needed.
    /// </summary>
    public async ValueTask HandleAsync(RestartServerCommand command)
    {
        GameServer.GameWriter.SendText($"{command.Message.Player.DisplayName} is restarting the server");
        await Task.Delay(TimeSpan.FromSeconds(3));
        try
        {
            await GameServer.GameWriter.QuitAsync();
            GameServer.GameWriter.SendText("The server is quitting and should be back in a minute", false);
        }
        catch (AdminCommandException e)
        {
            Logger.LogWarning(e, "Restart of {ServerId} failed", GameServer.Id);
            GameServer.GameWriter.SendText($"Restart failed: {e.Message}");
        }
    }

    public void Handle(GravityDefaultCommand command)
    {
        GameServer.GameWriter.SetGravity(IGameWriter.DefaultGravity);
        GameServer.GameWriter.SendText($"Gravity: {IGameWriter.DefaultGravity.ToString(CultureInfo.InvariantCulture)} (Default)");
    }

    public void Handle(GravityCommand command)
    {
        GameServer.GameWriter.SetGravity(command.Value);
        GameServer.GameWriter.SendText($"Gravity: {command.Value.ToString(CultureInfo.InvariantCulture)}");
    }

    public void Handle(TimerIntervalCommand command)
    {
        Logger.LogInformation("Timer interval set to {interval}", command.Value);
        GameServer.GameWriter.SendTimerInterval(command.Value);
    }

    public void Handle(AutoRepairCommand command)
    {
        _autoRepairOn = !_autoRepairOn;

        GameServer.GameWriter.SendConsole(_autoRepairOn ? RconScript.AutoRepairOn : RconScript.AutoRepairOff);
        GameServer.GameWriter.SendText(_autoRepairOn ? "Auto repair on" : "Auto repair off");
    }

    public void Handle(ScriptModCommand command)
    {
        string[] commands;
        var activate = command.Value.ToLower() == "on";
        switch (command.Name)
        {
            case "sniper-arty":
                commands = activate ? RconScript.SniperArtilleryOn : RconScript.SniperArtilleryOff;
                break;

            case "hydra-arty":
                commands = activate ? RconScript.HydraArtilleryOn : RconScript.HydraArtilleryOff;
                break;

            case "less-bs-dmg":
                commands = activate ? RconScript.HeliLessBsDamageOn : RconScript.HeliLessBsDamageOff;
                break;

            case "flare-smoke":
                commands = activate ? RconScript.HeliSmokeFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-ah1z":
                commands = activate ? RconScript.HeliAh1zFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-z10":
                commands = activate ? RconScript.HeliZ10FlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-arty":
                commands = activate ? RconScript.HeliArtyFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "flare-supply":
                commands = activate ? RconScript.HeliSupplyFlaresOn : RconScript.HeliFlareDefault;
                break;

            case "auto-repair":
                commands = activate ? RconScript.AutoRepairOn : RconScript.AutoRepairOff;
                break;

            default:
                GameServer.GameWriter.SendText($"Unknown command: {command.Name}");
                return;
        }

        GameServer.GameWriter.SendText("OK");
        GameServer.GameWriter.SendConsole(commands);
    }
}
