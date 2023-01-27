using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("jail <Name>", Auth.Admin)]
public class JailPlayerCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("free <Name>", Auth.Admin)]
public class FreePlayerCommand : BaseCommand
{
    public string Name { get; set; }
}
