using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Data.Entities;

public class MapMod
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    /// <summary>The map it was built on (lower case, such as dalian_2_v_2); null for a layout from before the builder, offered on every map.</summary>
    public string Map { get; set; }

    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime EditedDate { get; set; }

    public List<MapModObject> MapModObjects { get; set; } = new();
}

public class MapModObject
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; }

    /// <summary>What it is: a static object of <see cref="TemplateName"/>, or a team's spawn point or helipad.</summary>
    public MapModObjectKind Kind { get; set; }

    /// <summary>The team of a spawn point or helipad (1 or 2); 0 for a static object.</summary>
    public int Team { get; set; }

    public Position Position { get; set; }
    public Rotation Rotation { get; set; }

    public Guid MapModId { get; set; }
    public MapMod MapMod { get; set; }
}

public enum MapModObjectKind
{
    Object = 0,
    SpawnPoint = 1,
    Helipad = 2,
}
