using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("pad", Auth.Admin, Description = "Sends the helicopters back to their team's pad, repaired.")]
public class PadCommand : BaseCommand
{
}

[Command("autopad", Auth.All, Description = "Meant to turn on .autopad for every player; not implemented yet.")]
public class AutoPadAllCommand : BaseCommand
{
}

[Command("autopad <Name>", Auth.All, Description = "Toggles sending a player back to the pad after each round their team wins.")]
public class AutoPadCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("pilot|fly", Auth.All, Description = "Makes you the pilot when you spawn into the heli with a teammate.")]
public class PilotCommand : BaseCommand
{
}

[Command("pilot|fly <Name>", Auth.Admin, Description = "Makes a player the pilot when they spawn into the heli with a teammate.")]
public class PilotForCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("gunner|gun", Auth.All, Description = "Makes you the gunner when you spawn into the heli with a teammate.")]
public class GunnerCommand : BaseCommand
{
}

[Command("gunner|gun <Name>", Auth.Admin, Description = "Makes a player the gunner when they spawn into the heli with a teammate.")]
public class GunnerForCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("sw|switch", Auth.Admin, Description = "Switches every player to the other team.")]
public class SwitchAllCommand : BaseCommand
{
}

[Command("sw|switch <Rounds>", Auth.Admin, Description = "Switches the teams after a number of rounds, in 2v2 mode.")]
public class SwitchLaterCommand : BaseCommand
{
    public int Rounds { get; set; }
}

[Command("sw|switch <Name>", Auth.Admin, Description = "Switches a player to the other team.")]
public class SwitchCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("switchid <PlayerId>", Auth.Admin, Description = "Switches a player, by player id, to the other team.")]
public class SwitchIdCommand : BaseCommand
{
    public int PlayerId { get; set; }
}

[Command("nasa <Value>", Auth.Admin, Description = "Sets an altitude limit in metres; 0 turns it off.")]
public class NasaCommand : BaseCommand
{
    public int Value { get; set; }
}

[Command("mg <Value>", Auth.Admin, Description = "Turns the helicopters' machine guns on (1) or off (0).")]
public class HeliMgCommand : BaseCommand
{
    public int Value { get; set; }
}

[Command("noclip <Name> <Value>", Auth.God, Description = "Turns collision off (1) or on (0) for a player's vehicle.")]
public class NoclipCommand : BaseCommand
{
    public string Name { get; set; }
    public int Value { get; set; }
}

[Command("stalk <Name>", Auth.God, Description = "Keeps you 10 m above a player until .stop.")]
public class StalkCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("2v2 <Value>", Auth.God, Description = "Turns 2v2 mode, which plays and saves the rounds and matches, on or off.")]
public class Toggle2v2Command : BaseCommand
{
    public bool Value { get; set; }
}


[Command("tv|tvmissile", Auth.SuperAdmin, Description = "Shows the TV missile's settings and their defaults.")]
public class GetTvMissileValuesCommand : BaseCommand
{
}

[Command("tv|tvmissile <Name> <Value>", Auth.SuperAdmin, Description = "Changes one of the TV missile's settings.")]
public class SetTvMissileValueCommand : BaseCommand
{
    public string Name { get; set; }
    public string Value { get; set; }
}

[Command("tv|tvmissile <Type>", Auth.SuperAdmin, Description = "Sets the TV missile to a preset: default or test.")]
public class SetTvMissileTypeCommand : BaseCommand
{
    public string Type { get; set; }
}

[Command("tvlog <Name>", Auth.SuperAdmin, Description = "Toggles measuring a player's TV missile shots in the chat, in 2v2 mode.")]
public class ToggleTvLogCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("nofences", Auth.Admin, Description = "Removes the fences and lamp posts from the map.")]
public class NoFencesCommand : BaseCommand
{
}

/// <summary>The last kill of the match by a TV missile, or without a match your own last TV missile, at real speed.</summary>
[Command("replay", Auth.All, Description = "Replays the match's last TV missile kill, or your own last TV missile.")]
public class ReplayCommand : BaseCommand
{
}

/// <summary>As <see cref="ReplayCommand"/> at a speed (0.05 to 4; 0.5 is half speed).</summary>
[Command("replay <Speed>", Auth.All, Description = "Replays the last TV missile kill at a speed from 0.05 to 4.")]
public class ReplaySpeedCommand : BaseCommand
{
    public double Speed { get; set; }
}

/// <summary>As <see cref="ReplayCommand"/>, with the helicopters where the server tested the hit (rewound by the shooter's lag).</summary>
[Command("replayhit", Auth.All, Description = "Replays the last TV missile kill with the helicopters where the hit was tested.")]
public class ReplayHitCommand : BaseCommand
{
}

[Command("replayhit <Speed>", Auth.All, Description = "Replays the last TV missile hit as the server tested it, at a speed.")]
public class ReplayHitSpeedCommand : BaseCommand
{
    public double Speed { get; set; }
}
