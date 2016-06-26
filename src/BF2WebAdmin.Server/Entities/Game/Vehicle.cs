﻿using System.Collections.Generic;

namespace BF2WebAdmin.Server.Entities.Game
{
    public class Vehicle : Entity
    {
        public List<Player> Players { get; set; }

        public Vehicle()
        {
            Players = new List<Player>();
        }
    }
}
