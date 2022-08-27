using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("leave <Minutes>", Auth.All)]
public class LeaveCommand : BaseCommand
{
    public int Minutes { get; set; }
}