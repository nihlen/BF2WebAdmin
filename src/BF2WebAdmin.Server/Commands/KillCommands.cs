using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("kill <Name>", Auth.Admin, Description = "Kills a player and destroys their vehicle.")]
public class KillCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("killid <PlayerId>", Auth.Admin, Description = "Kills a player by player id and destroys their vehicle.")]
public class KillIdCommand : BaseCommand
{
    public int PlayerId { get; set; }
}