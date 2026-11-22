using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("auth <Name> <Level>", Auth.Admin, Description = "Sets a player's auth level in the server group, below your own level.")]
public class SetAuthCommand : BaseCommand
{
    public string Name { get; set; }
    public int Level { get; set; }
}

[Command("auth <Name>", Auth.Admin, Description = "Shows a player's auth level.")]
public class GetAuthCommand : BaseCommand
{
    public string Name { get; set; }
}