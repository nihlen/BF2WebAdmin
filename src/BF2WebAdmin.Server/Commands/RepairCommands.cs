using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("repair|heal", Auth.Admin)]
public class RepairAllCommand : BaseCommand
{
}

[Command("repair|heal <Name>", Auth.Admin)]
public class RepairCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("autorepair|autoheal", Auth.Admin)]
public class AutoRepairCommand : BaseCommand
{
}
