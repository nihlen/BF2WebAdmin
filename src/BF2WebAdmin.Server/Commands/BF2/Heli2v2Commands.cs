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

[Command("tvlog| <Name>", Auth.SuperAdmin)]
public class ToggleTvLogCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("nofences", Auth.Admin)]
public class NoFencesCommand : BaseCommand
{
}