using System.Linq;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Tests;

public static class Data
{
    public static readonly Position DalianUsPad = Position.Parse("255.100/148.400/-258.700");
    public static readonly Position DalianChPad = Position.Parse("-218.000/153.900/159.000");
    
    public static (Team[] teams, Vehicle[] vehicles, Player[] players) Setup2v2Players()
    {
        var teams = new Team[]
        {
            new() { Id = 1, Name = "China" },
            new() { Id = 2, Name = "USMC" }
        };

        var vehicles = new Vehicle[]
        {
            new() { Id = 1, RootVehicleTemplate = "ahe_z10", Template = "ahe_z10", Position = DalianChPad, Rotation = Rotation.Neutral },
            new() { Id = 2, RootVehicleTemplate = "ahe_ah1z", Template = "ahe_ah1z", Position = DalianUsPad, Rotation = Rotation.Neutral }
        };

        var players = new Player[]
        {
            new() { Id = 1, Index = 1, Hash = "A", Team = teams[0], Vehicle = vehicles[0], Name = "CH Pilot", SubVehicleTemplate = "ahe_z10", Position = DalianChPad, Rotation = Rotation.Neutral  },
            new() { Id = 2, Index = 2, Hash = "B", Team = teams[0], Vehicle = vehicles[0], Name = "CH Gunner", SubVehicleTemplate = "ahe_z10_cogunner", Position = DalianChPad, Rotation = Rotation.Neutral },
            new() { Id = 3, Index = 3, Hash = "C", Team = teams[1], Vehicle = vehicles[1], Name = "US Pilot", SubVehicleTemplate = "ahe_ah1z", Position = DalianUsPad, Rotation = Rotation.Neutral },
            new() { Id = 4, Index = 4, Hash = "D", Team = teams[1], Vehicle = vehicles[1], Name = "US Gunner", SubVehicleTemplate = "ahe_ah1z_cogunner", Position = DalianUsPad, Rotation = Rotation.Neutral },
        };

        vehicles[0].Players = players.Take(0..2).ToList();
        vehicles[1].Players = players.Take(2..4).ToList();

        return (teams, vehicles, players);
    }
}
