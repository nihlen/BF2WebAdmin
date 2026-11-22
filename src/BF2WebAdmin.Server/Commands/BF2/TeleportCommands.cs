using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("tp|teleport <Name> <Location>", Auth.Admin, Description = "Teleports a player high above the middle of the map.")]
public class TeleportLocationCommand : BaseCommand
{
    public string Name { get; set; }
    public string Location { get; set; }
}

[Command("tp|teleport <Name> <X> <Y> <Altitude>", Auth.Admin, Description = "Teleports a player to map coordinates and an altitude.")]
public class TeleportCoordinatesCommand : BaseCommand
{
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Altitude { get; set; }
}