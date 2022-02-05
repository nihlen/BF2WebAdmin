using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("startstream", Auth.God)]
public class StartStreamCommand : BaseCommand
{
}

[Command("stopstream|kickbot", Auth.God)]
public class StopStreamCommand : BaseCommand
{
}
