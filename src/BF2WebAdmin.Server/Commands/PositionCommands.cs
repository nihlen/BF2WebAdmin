using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("p|position", Auth.Trusted)]
public class PositionSelfCommand : BaseCommand
{
}

[Command("p|position <Name>", Auth.Trusted)]
public class PositionCommand : BaseCommand
{
    public string Name { get; set; }
}