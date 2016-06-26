using System.Net;

namespace BF2WebAdmin.Server.Entities.Game
{
    public class Player : Entity
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public IPAddress IpAddress { get; set; }
        public string Hash { get; set; }
        public Country Country { get; set; }
        public Rank Rank { get; set; }
        public Vehicle RootVehicle { get; set; }
        public Vehicle SubVehicle { get; set; }
        public Team Team { get; set; }
        public bool IsAlive { get; set; }
        public Score Score { get; set; }
    }

    public class Score
    {
        public int Total { get; set; }
        public int Team { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Ping { get; set; }
    }

    public class Country
    {
        public string Name { get; set; }
        public string Code { get; set; }
    }
}
