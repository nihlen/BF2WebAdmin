using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("interval|timerinterval <Value>", Auth.Admin, Description = "Sets how often the server sends player positions, in seconds.")]
public class TimerIntervalCommand : BaseCommand
{
    public double Value { get; set; }
}