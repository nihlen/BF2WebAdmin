using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("d|dance", Auth.Admin)]
public class DanceAllCommand : BaseCommand
{
}

[Command("d|dance <Name>", Auth.Admin)]
public class DanceCommand : BaseCommand
{
    public string Name { get; set; }
}