using System.Text.RegularExpressions;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Server.Builder;

/// <summary>
/// The saved layouts for other sites (BF2v2's offline editor, through <see cref="LayoutEndpoints"/>): list and get any
/// layout, create one for a user, update it only for that user (<see cref="MapMod.OwnerId"/>). A submitted layout has
/// not been through the builder, so it is checked here; which templates the level uses is checked again when a layout
/// loads on a game server (<see cref="LevelTemplates"/>).
/// </summary>
public sealed partial class LayoutService(IMapRepository repository, TimeProvider time)
{
    /// <summary>The most layouts one owner may have.</summary>
    public const int MaxPerOwner = 50;

    public async Task<IReadOnlyList<LayoutSummaryDto>> ListAsync(string? map)
    {
        if (map != null)
            map = CheckMap(map);
        return (await repository.GetSummariesAsync(map)).Select(ToDto).ToList();
    }

    public async Task<LayoutDetailDto> GetAsync(Guid id) =>
        ToDto(await repository.GetAsync(id) ?? throw new BuilderException("No such layout.", notFound: true));

    /// <summary>A new layout of the request's owner. 409 when the map has a layout of that name, or the owner has <see cref="MaxPerOwner"/>.</summary>
    public async Task<LayoutDetailDto> CreateAsync(CreateLayoutRequest request)
    {
        var name = CheckName(request.Name);
        var map = CheckMap(request.Map);
        var (ownerId, ownerName) = CheckOwner(request.OwnerId, request.OwnerName);
        var objects = CheckObjects(request.Objects);

        if (await repository.FindByNameAsync(map, name) is { } existing)
            throw new BuilderException($"{map} has a layout '{existing.Name}'. Pick another name.", conflict: true);
        if (await repository.CountByOwnerAsync(ownerId) >= MaxPerOwner)
            throw new BuilderException($"You have {MaxPerOwner} layouts, the most one user can have.", conflict: true);

        var now = time.GetUtcNow().UtcDateTime;
        var layout = new MapMod
        {
            Id = Guid.NewGuid(),
            Name = name,
            Map = map,
            CreatedBy = ownerName,
            OwnerId = ownerId,
            CreatedDate = now,
            EditedDate = now,
        };
        layout.MapModObjects = objects.Select(o => ToEntity(o, layout.Id)).ToList();
        await SaveAsync(() => repository.CreateAsync(layout), map, name);
        return ToDto(layout);
    }

    /// <summary>
    /// Replaces a layout's objects, and its name when the request has one. Only its owner may (403); a layout saved in
    /// WebAdmin or in game has none. The map stays.
    /// </summary>
    public async Task<LayoutDetailDto> UpdateAsync(Guid id, UpdateLayoutRequest request)
    {
        var (ownerId, ownerName) = CheckOwner(request.OwnerId, request.OwnerName);
        var objects = CheckObjects(request.Objects);
        var layout = await repository.GetAsync(id) ?? throw new BuilderException("No such layout.", notFound: true);
        if (!string.Equals(layout.OwnerId, ownerId, StringComparison.Ordinal))
            throw new BuilderException($"Only the author of '{layout.Name}' can change it. Save yours under a new name.", forbidden: true);

        var name = request.Name == null ? layout.Name : CheckName(request.Name);
        if (!string.Equals(name, layout.Name, StringComparison.Ordinal)
            && await repository.FindByNameAsync(layout.Map, name) is { } other && other.Id != layout.Id)
            throw new BuilderException($"{layout.Map} has a layout '{other.Name}'. Pick another name.", conflict: true);

        layout.Name = name;
        layout.CreatedBy = ownerName;
        layout.EditedDate = time.GetUtcNow().UtcDateTime;
        layout.MapModObjects = objects.Select(o => ToEntity(o, layout.Id)).ToList();
        await SaveAsync(() => repository.ReplaceAsync(layout), layout.Map, name);
        return ToDto(layout);
    }

    /// <summary>The unique index on map and name catches a layout saved at the same time under the same name.</summary>
    private static async Task SaveAsync(Func<Task> save, string map, string name)
    {
        try
        {
            await save();
        }
        catch (DbUpdateException)
        {
            throw new BuilderException($"{map} has a layout '{name}'. Pick another name.", conflict: true);
        }
    }

    internal static string CheckName(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > MapModLimits.NameLength || name.Any(char.IsControl))
            throw new BuilderException($"A layout name has 1 to {MapModLimits.NameLength} characters.");
        if (MapBuilder.IsReservedName(name))
            throw new BuilderException($"'{name}' is reserved for .vote {name.ToLowerInvariant()}; pick another name.");
        return name;
    }

    internal static string CheckMap(string? map)
    {
        map = map?.Trim().ToLowerInvariant() ?? "";
        if (map.Length > MapModLimits.MapLength || !MapName().IsMatch(map))
            throw new BuilderException($"'{map}' is not a map name.");
        return map;
    }

    private static (string Id, string Name) CheckOwner(string? id, string? name)
    {
        id = id?.Trim() ?? "";
        name = name?.Trim() ?? "";
        if (id.Length is 0 or > MapModLimits.OwnerIdLength || id.Any(char.IsControl))
            throw new BuilderException($"An owner id has 1 to {MapModLimits.OwnerIdLength} characters.");
        if (name.Length is 0 or > OwnerNameLength || name.Any(char.IsControl))
            throw new BuilderException($"An owner name has 1 to {OwnerNameLength} characters.");
        return (id, name);
    }

    /// <summary>The longest author name; the players see it in the vote's announcement.</summary>
    private const int OwnerNameLength = 32;

    private static IReadOnlyList<LayoutObjectDto> CheckObjects(IReadOnlyList<LayoutObjectDto>? objects)
    {
        if (objects is not { Count: > 0 })
            throw new BuilderException("A layout has at least one object.");
        if (objects.Count > MapBuilder.MaxItems)
            throw new BuilderException($"A layout has at most {MapBuilder.MaxItems} objects, not {objects.Count}.");

        return objects.Select(o =>
        {
            if (o == null)
                throw new BuilderException("An object is missing.");
            MapBuilder.Validate(o.Kind, o.Template, o.Team, o.Position, o.Rotation);
            if (Math.Abs(o.Rotation[0]) > 360 || Math.Abs(o.Rotation[1]) > 360 || Math.Abs(o.Rotation[2]) > 360)
                throw new BuilderException("A rotation's angles are -360 to 360 degrees.");
            var team = o.Kind == MapModObjectKind.Object ? 0 : o.Team;
            return o with { Template = MapBuilder.TemplateFor(o.Kind, o.Template, team), Team = team };
        }).ToList();
    }

    private static MapModObject ToEntity(LayoutObjectDto o, Guid layoutId) => new()
    {
        Id = Guid.NewGuid(),
        MapModId = layoutId,
        Kind = o.Kind,
        Team = o.Team,
        TemplateName = o.Template,
        Position = MapBuilder.ToPosition(o.Position),
        Rotation = MapBuilder.ToRotation(o.Rotation),
    };

    private static LayoutSummaryDto ToDto(MapModSummary s) =>
        new(s.Id, s.Name, s.Map, s.CreatedBy, s.OwnerId, Utc(s.CreatedDate), Utc(s.EditedDate), s.ObjectCount);

    private static LayoutDetailDto ToDto(MapMod l) =>
        new(l.Id, l.Name, l.Map, l.CreatedBy, l.OwnerId, Utc(l.CreatedDate), Utc(l.EditedDate),
            l.MapModObjects.Select(o => new LayoutObjectDto(o.Kind, o.TemplateName, o.Team,
                [o.Position.X, o.Position.Height, o.Position.Y], [o.Rotation.Yaw, o.Rotation.Pitch, o.Rotation.Roll])).ToList());

    /// <summary>The dates are stored in UTC; SQLite gives them back without a kind.</summary>
    private static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex MapName();
}
