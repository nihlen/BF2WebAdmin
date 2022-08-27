using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("g|gravity", Auth.Admin)]
public class GravityDefaultCommand : BaseCommand
{
}

[Command("g|gravity <Value>", Auth.Admin)]
public class GravityCommand : BaseCommand
{
    public double Value { get; set; }
}