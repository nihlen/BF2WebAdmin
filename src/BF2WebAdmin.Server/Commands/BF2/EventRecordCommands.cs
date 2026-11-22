using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("starteventrecorder", Auth.God, Description = "Starts writing the server's game events to a file on the WebAdmin host.")]
public class StartEventRecordingCommand : BaseCommand
{
}

[Command("stopeventrecorder", Auth.God, Description = "Stops writing the server's game events to a file.")]
public class StopEventRecordingCommand : BaseCommand
{
}
