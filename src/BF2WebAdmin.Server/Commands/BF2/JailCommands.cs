using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("jail <Name>", Auth.Admin, Description = "Keeps a player at the jail for an hour.")]
public class JailPlayerCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("free <Name>", Auth.Admin, Description = "Lets a player out of the jail.")]
public class FreePlayerCommand : BaseCommand
{
    public string Name { get; set; }
}
