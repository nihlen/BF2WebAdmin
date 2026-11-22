using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("blur <Name>", Auth.God, Description = "Blurs a player's screen for two seconds.")]
public class BlurCommand : BaseCommand
{
    public string Name { get; set; }
}