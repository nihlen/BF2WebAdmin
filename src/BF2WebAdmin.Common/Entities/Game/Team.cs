using System.Collections.Generic;

namespace BF2WebAdmin.Common.Entities.Game
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