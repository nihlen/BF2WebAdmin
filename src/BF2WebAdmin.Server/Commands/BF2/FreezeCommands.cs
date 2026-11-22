using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("freeze", Auth.God, Description = "Freezes every player's vehicle for an hour or until .stop.")]
public class FreezeAllCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("freeze <Name>", Auth.God, Description = "Freezes a player's vehicle for an hour or until .stop.")]
public class FreezeCommand : BaseCommand
{
    public string Name { get; set; }
}