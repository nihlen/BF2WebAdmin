using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("blur <Name>", Auth.God)]
public class BlurCommand : BaseCommand
{
    public string Name { get; set; }
}