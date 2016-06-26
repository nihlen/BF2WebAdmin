using BF2WebAdmin.Server.Modules;

namespace BF2WebAdmin.Server.Commands
{
    [Command("r|rank <Name> <Rank>", Auth.Admin)]
    public class RankCommand : BaseCommand
    {
        public string Name { get; set; }
        public int Rank { get; set; }
    }
}