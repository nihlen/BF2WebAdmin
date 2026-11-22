using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("p|position", Auth.Trusted, Description = "Shows your position and rotation.")]
public class PositionSelfCommand : BaseCommand
{
}

[Command("p|position <Name>", Auth.Trusted, Description = "Shows a player's position and rotation.")]
public class PositionCommand : BaseCommand
{
    public string Name { get; set; }
}