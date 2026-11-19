using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("pad", Auth.Admin)]
public class PadCommand : BaseCommand
{
}

[Command("autopad", Auth.All)]
public class AutoPadAllCommand : BaseCommand
{
}

[Command("autopad <Name>", Auth.All)]
public class AutoPadCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("pilot|fly", Auth.All)]
public class PilotCommand : BaseCommand
{
}

[Command("pilot|fly <Name>", Auth.Admin)]
public class PilotForCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("gunner|gun", Auth.All)]
public class GunnerCommand : BaseCommand
{
}

[Command("gunner|gun <Name>", Auth.Admin)]
public class GunnerForCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("sw|switch", Auth.Admin)]
public class SwitchAllCommand : BaseCommand
{
}

[Command("sw|switch <Rounds>", Auth.Admin)]
public class SwitchLaterCommand : BaseCommand
{
    public int Rounds { get; set; }
}

[Command("sw|switch <Name>", Auth.Admin)]
public class SwitchCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("switchid <PlayerId>", Auth.Admin)]
public class SwitchIdCommand : BaseCommand
{
    public int PlayerId { get; set; }
}

[Command("nasa <Value>", Auth.Admin)]
public class NasaCommand : BaseCommand
{
    public int Value { get; set; }
}

[Command("mg <Value>", Auth.Admin)]
public class HeliMgCommand : BaseCommand
{
    public int Value { get; set; }
}

[Command("noclip <Name> <Value>", Auth.God)]
public class NoclipCommand : BaseCommand
{
    public string Name { get; set; }
    public int Value { get; set; }
}

[Command("stalk <Name>", Auth.God)]
public class StalkCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("2v2 <Value>", Auth.God)]
public class Toggle2v2Command : BaseCommand
{
    public bool Value { get; set; }
}


[Command("tv|tvmissile", Auth.SuperAdmin)]
public class GetTvMissileValuesCommand : BaseCommand
{
}

[Command("tv|tvmissile <Name> <Value>", Auth.SuperAdmin)]
public class SetTvMissileValueCommand : BaseCommand
{
    public string Name { get; set; }
    public string Value { get; set; }
}

[Command("tv|tvmissile <Type>", Auth.SuperAdmin)]
public class SetTvMissileTypeCommand : BaseCommand
{
    public string Type { get; set; }
}

[Command("tvlog <Name>", Auth.SuperAdmin)]
public class ToggleTvLogCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("nofences", Auth.Admin)]
public class NoFencesCommand : BaseCommand
{
}

/// <summary>The last kill of the match by a TV missile, or without a match your own last TV missile, at real speed.</summary>
[Command("replay", Auth.All)]
public class ReplayCommand : BaseCommand
{
}

/// <summary>As <see cref="ReplayCommand"/> at a speed (0.05 to 4; 0.5 is half speed).</summary>
[Command("replay <Speed>", Auth.All)]
public class ReplaySpeedCommand : BaseCommand
{
    public double Speed { get; set; }
}

/// <summary>As <see cref="ReplayCommand"/>, with the helicopters where the server tested the hit (rewound by the shooter's lag).</summary>
[Command("replayhit", Auth.All)]
public class ReplayHitCommand : BaseCommand
{
}

[Command("replayhit <Speed>", Auth.All)]
public class ReplayHitSpeedCommand : BaseCommand
{
    public double Speed { get; set; }
}
