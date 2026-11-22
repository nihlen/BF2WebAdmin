using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("repair|heal", Auth.Admin, Description = "Repairs every player to full health.")]
public class RepairAllCommand : BaseCommand
{
}

[Command("repair|heal <Name>", Auth.Admin, Description = "Repairs a player to full health.")]
public class RepairCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("autorepair|autoheal", Auth.Admin, Description = "Turns repair from the helipads across the whole map on or off.")]
public class AutoRepairCommand : BaseCommand
{
}
