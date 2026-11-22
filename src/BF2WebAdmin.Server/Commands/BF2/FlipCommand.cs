using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("flip <Name>", Auth.Admin, Description = "Turns a player's vehicle upside down.")]
public class FlipCommand : BaseCommand
{
    public string Name { get; set; }
}