using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;
using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Entities
{
    public class MapMod
    {
        [ExplicitKey]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime EditedDate { get; set; }

        [Write(false)]
        public IEnumerable<MapModObject> Objects { get; set; }
    }

    public class MapModObject
    {
        [ExplicitKey]
        public Guid Id { get; set; }
        public Guid MapModId { get; set; }
        public string TemplateName { get; set; }
        public Position Position { get; set; }
        public Rotation Rotation { get; set; }
    }
}