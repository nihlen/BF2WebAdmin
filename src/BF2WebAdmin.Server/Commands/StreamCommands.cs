using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("startstream", Auth.God, Description = "Asks the stream bot to start streaming the server.")]
public class StartStreamCommand : BaseCommand
{
}

[Command("stopstream|kickbot", Auth.God, Description = "Asks the stream bot to stop streaming the server.")]
public class StopStreamCommand : BaseCommand
{
}
