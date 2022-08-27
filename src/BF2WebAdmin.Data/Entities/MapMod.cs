using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;
using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Entities;

public class MapMod
{
    [ExplicitKey] public Guid Id { get; set; }
    public string Name { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime EditedDate { get; set; }

    [Computed] public List<MapModObject> MapModObjects { get; set; } = new();
}

public class MapModObject
{
    [ExplicitKey] public Guid Id { get; set; }
    public string TemplateName { get; set; }
    public Position Position { get; set; }
    public Rotation Rotation { get; set; }

    public Guid MapModId { get; set; }
    [Computed] public MapMod MapMod { get; set; }
}