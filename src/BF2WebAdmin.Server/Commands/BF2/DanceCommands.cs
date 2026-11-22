using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("d|dance", Auth.Admin, Description = "Makes every player dance for five seconds.")]
public class DanceAllCommand : BaseCommand
{
}

[Command("d|dance <Name>", Auth.Admin, Description = "Makes a player dance for five seconds.")]
public class DanceCommand : BaseCommand
{
    public string Name { get; set; }
}