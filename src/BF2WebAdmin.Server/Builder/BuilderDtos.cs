using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Builder;

// Positions are BF2's [x, height, z] in metres (z north), rotations [yaw, pitch, roll] in degrees, as the console's
// object.absolutePosition and object.rotation take them.

/// <param name="Map">The map the items are on, lower case.</param>
/// <param name="Revision">Changes so far; an apply names the revision its drafts were made against.</param>
/// <param name="Connected">The game server's admin channel is up.</param>
/// <param name="CanResync">The module can resync clients, which removing and moving need.</param>
/// <param name="LayoutId">The saved layout the items came from or were last saved as.</param>
/// <param name="PendingResync">Players whose client still holds removed objects, and why each waits.</param>
public record BuilderStateDto(
    string? Map,
    int Revision,
    bool Connected,
    bool CanResync,
    Guid? LayoutId,
    string? LayoutName,
    IReadOnlyList<BuiltItemDto> Items,
    IReadOnlyList<PendingResyncDto> PendingResync);

/// <param name="Spawned">False when the item exists in the builder but not in the game (its spawn failed this round).</param>
/// <param name="SpawnReady">False for a spawn point or helipad that players cannot pick on the spawn screen until the
/// next round restart (its spawn group is new); true for everything else.</param>
public record BuiltItemDto(int Id, MapModObjectKind Kind, string Template, int Team, double[] Position, double[] Rotation, bool Spawned, bool SpawnReady);

/// <param name="Blocker">"dead" or "missile" while the resync waits for the player, null when it goes next.</param>
public record PendingResyncDto(int Index, string Name, string? Blocker);

public record BuilderAddDto(MapModObjectKind Kind, string Template, int Team, double[] Position, double[] Rotation);

public record BuilderUpdateDto(int Id, double[] Position, double[] Rotation);

/// <summary>Drafts to apply in one go: removed ids, moved items, new items.</summary>
public record BuilderApplyRequest(int Revision, IReadOnlyList<BuilderAddDto> Add, IReadOnlyList<BuilderUpdateDto> Update, IReadOnlyList<int> Remove);

/// <param name="Added">The ids of the new items, in the order of the request's adds that spawned.</param>
/// <param name="Errors">Items that could not be spawned.</param>
public record BuilderApplyResult(BuilderStateDto State, IReadOnlyList<int> Added, IReadOnlyList<string> Errors);

/// <param name="Map">The map it is for; a name is unique per map.</param>
public record LayoutDto(Guid Id, string Name, string Map, string CreatedBy, DateTime EditedDate);

public record SaveLayoutRequest(string Name, bool Overwrite);

/// <summary>Who starts a layout vote from another site: the user's id there and the name the players see.</summary>
public record StartLayoutVoteRequest(string StarterId, string StarterName);

// The layout API for other sites (/api/layouts, LayoutEndpoints)

/// <summary>An object of a layout. Team is 1 or 2 for a spawn point or helipad, 0 for an object. A spawn point's
/// template is "spawnpoint"; a helipad's is the helicopter it spawns, the team's default when empty or "helipad".</summary>
public record LayoutObjectDto(MapModObjectKind Kind, string Template, int Team, double[] Position, double[] Rotation);

/// <param name="CreatedBy">The author's name.</param>
/// <param name="OwnerId">Who may update it through this API (such as "discord:123"); null for a layout saved in WebAdmin or in game.</param>
public record LayoutSummaryDto(Guid Id, string Name, string Map, string CreatedBy, string? OwnerId, DateTime CreatedDate, DateTime EditedDate, int ObjectCount);

public record LayoutDetailDto(Guid Id, string Name, string Map, string CreatedBy, string? OwnerId, DateTime CreatedDate, DateTime EditedDate,
    IReadOnlyList<LayoutObjectDto> Objects);

/// <param name="OwnerId">The user of the other site, who alone may update it (such as "discord:123").</param>
/// <param name="OwnerName">The name shown as its author, at most 32 characters.</param>
public record CreateLayoutRequest(string Name, string Map, string OwnerId, string OwnerName, IReadOnlyList<LayoutObjectDto> Objects);

/// <param name="OwnerId">Must be the layout's owner.</param>
/// <param name="Name">A new name, or null to keep it.</param>
public record UpdateLayoutRequest(string OwnerId, string OwnerName, string? Name, IReadOnlyList<LayoutObjectDto> Objects);
