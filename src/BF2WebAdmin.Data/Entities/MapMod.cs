using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Data.Entities;

public class MapMod
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime EditedDate { get; set; }

    public List<MapModObject> MapModObjects { get; set; } = new();
}

public class MapModObject
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; }
    public Position Position { get; set; }
    public Rotation Rotation { get; set; }

    public Guid MapModId { get; set; }
    public MapMod MapMod { get; set; }
}