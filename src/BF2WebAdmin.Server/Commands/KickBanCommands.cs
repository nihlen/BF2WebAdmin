using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("kick <Name>", Auth.Admin)]
public class KickCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("ban <Name>", Auth.SuperAdmin)]
public class BanCommand : BaseCommand
{
    public string Name { get; set; }
}
