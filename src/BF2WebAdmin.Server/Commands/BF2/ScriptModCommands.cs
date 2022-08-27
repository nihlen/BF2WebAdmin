using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("mod <Name> <Value>", Auth.God)]
public class ScriptModCommand : BaseCommand
{
    public string Name { get; set; }
    public string Value { get; set; }
}