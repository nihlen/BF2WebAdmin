using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("time", Auth.All, Description = "Shows the time where WebAdmin runs.")]
public class TimeCommand : BaseCommand { }