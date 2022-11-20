using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("starteventrecorder", Auth.God)]
public class StartEventRecordingCommand : BaseCommand
{
}

[Command("stopeventrecorder", Auth.God)]
public class StopEventRecordingCommand : BaseCommand
{
}
