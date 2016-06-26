﻿using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server.Commands
{
    public abstract class BaseCommand : ICommand
    {
        public Message Message { get; set; }
    }
}