using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("r|rank <Name> <Rank>", Auth.Admin, Description = "Sets a player's rank.")]
public class RankCommand : BaseCommand
{
    public string Name { get; set; }
    public int Rank { get; set; }
}