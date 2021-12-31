using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands
{
    [Command("kill <Name>", Auth.Admin)]
    public class KillCommand : BaseCommand
    {
        public string Name { get; set; }
    }
}