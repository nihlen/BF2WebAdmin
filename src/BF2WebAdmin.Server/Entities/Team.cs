using System.Collections.Generic;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server.Entities
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Player> Players { get; set; }

        public Team()
        {
            Players = new List<Player>();
        }

    }
}