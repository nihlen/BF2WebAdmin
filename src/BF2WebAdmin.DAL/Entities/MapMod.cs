using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.DAL.Entities
{
    public class MapMod
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public IEnumerable<MapObject> Objects { get; set; }
        public string AuthorGuid { get; set; }
    }

    public class MapObject
    {
        public string TemplateName { get; set; }
        public Position Position { get; set; }
        public Rotation Rotation { get; set; }
    }
}