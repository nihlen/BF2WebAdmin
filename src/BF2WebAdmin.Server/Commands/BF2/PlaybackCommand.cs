using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("record", Auth.Admin, Description = "Starts or stops recording your flight.")]
public class RecordCommand : BaseCommand
{
}

[Command("playback", Auth.Admin, Description = "Plays the recorded flight back once.")]
public class PlaybackCommand : BaseCommand
{
}

[Command("loop", Auth.Admin, Description = "Plays the recorded flight back over and over until .stop.")]
public class LoopCommand : BaseCommand
{
}