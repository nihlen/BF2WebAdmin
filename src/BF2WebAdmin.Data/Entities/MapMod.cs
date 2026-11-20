using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Data.Entities;

public class MapMod
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// The map it is for (lower case, such as dalian_2_v_2). A name is unique per map. Layouts from before the builder had
    /// none; migration LayoutOwners gave them dalian_2_v_2.
    /// </summary>
    public string Map { get; set; }

    /// <summary>The author's name, as shown.</summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Who may update it through the layout API: another site's user, such as "discord:123" for a BF2v2 user. Null for
    /// a layout saved in WebAdmin's builder or in game, which only WebAdmin admins and in-game admins change.
    /// </summary>
    public string OwnerId { get; set; }

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

public static class MapModLimits
{
    public const int NameLength = 50;
    public const int MapLength = 50;
    public const int OwnerIdLength = 100;
}

public enum MapModObjectKind
{
    Object = 0,
    SpawnPoint = 1,
    Helipad = 2,
}
