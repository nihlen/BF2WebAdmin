using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BF2WebAdmin.Tests;

/// <summary>The layout API for other sites (<see cref="LayoutService"/>) on SQLite, and the migration to names unique per map.</summary>
public class LayoutServiceTests : IAsyncLifetime
{
    private const string Dalian = "dalian_2_v_2";
    private const string Alice = "discord:111";
    private const string Bob = "discord:222";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<BF2Context> _options = null!;
    private LayoutService _layouts = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(_connection).Options;
        await using (var context = new BF2Context(_options))
            await context.Database.MigrateAsync();
        _layouts = new LayoutService(new MapRepository(new DelegateContextFactory(() => new BF2Context(_options))), TimeProvider.System);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static LayoutObjectDto Block(double x = 10) => new(MapModObjectKind.Object, "concreteblock", 0, [x, 150, 20], [90, 0, 0]);

    private static CreateLayoutRequest Create(string name, string owner = Alice, string map = Dalian, params LayoutObjectDto[] objects) =>
        new(name, map, owner, owner == Alice ? "Alice" : "Bob", objects.Length > 0 ? objects : [Block()]);

    [Fact]
    public async Task ALayoutIsCreatedForItsOwner_ListedWithoutObjects_AndReadWithThem()
    {
        var created = await _layouts.CreateAsync(Create(" Towers ", objects:
        [
            new(MapModObjectKind.Object, "concreteblock", 2, [10, 150, 20], [90, 0, 0]),
            new(MapModObjectKind.SpawnPoint, "anything", 1, [11, 150, 21], [0, 0, 0]),
            new(MapModObjectKind.Helipad, "", 2, [12, 150, 22], [0, 0, 0]),
        ]));

        Assert.Equal(("Towers", Dalian, "Alice", Alice), (created.Name, created.Map, created.CreatedBy, created.OwnerId));
        // An object has no team, a spawn point no template, and a helipad's helicopter is the team's default
        Assert.Equal(new[] { ("concreteblock", 0), ("spawnpoint", 1), ("ahe_ah1z", 2) }, created.Objects.Select(o => (o.Template, o.Team)));

        var listed = Assert.Single(await _layouts.ListAsync(Dalian));
        Assert.Equal((created.Id, 3), (listed.Id, listed.ObjectCount));
        Assert.Empty(await _layouts.ListAsync("dalian_plant"));
        Assert.Single(await _layouts.ListAsync(null));

        var read = await _layouts.GetAsync(created.Id);
        Assert.Equal(3, read.Objects.Count);
        var block = read.Objects.Single(o => o.Kind == MapModObjectKind.Object);
        Assert.Equal(new double[] { 10, 150, 20 }, block.Position);
        Assert.Equal(new double[] { 90, 0, 0 }, block.Rotation);
    }

    [Fact]
    public async Task ANameIsUniquePerMap_IgnoringCase()
    {
        await _layouts.CreateAsync(Create("Towers"));
        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.CreateAsync(Create("TOWERS", Bob)));
        Assert.True(e.Conflict);
        Assert.Equal("dalian_2_v_2 has a layout 'Towers'. Pick another name.", e.Message);

        await _layouts.CreateAsync(Create("Towers", Bob, "dalian_plant"));
        Assert.Equal(2, (await _layouts.ListAsync(null)).Count);
    }

    [Fact]
    public async Task OnlyTheOwnerUpdates_TheIdStays_AndTheObjectsAreReplaced()
    {
        var created = await _layouts.CreateAsync(Create("Towers", objects: [Block(1), Block(2)]));

        var updated = await _layouts.UpdateAsync(created.Id, new UpdateLayoutRequest(Alice, "Alice B", null, [Block(3)]));
        Assert.Equal((created.Id, "Towers", "Alice B"), (updated.Id, updated.Name, updated.CreatedBy));
        var read = await _layouts.GetAsync(created.Id);
        Assert.Equal(3, Assert.Single(read.Objects).Position[0]);
        Assert.Single(await _layouts.ListAsync(Dalian));

        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.UpdateAsync(created.Id, new UpdateLayoutRequest(Bob, "Bob", null, [Block()])));
        Assert.True(e.Forbidden);
        Assert.Equal("Only the author of 'Towers' can change it. Save yours under a new name.", e.Message);
    }

    [Fact]
    public async Task ALayoutSavedInWebAdminHasNoOwner_AndNobodyUpdatesItThroughTheApi()
    {
        var id = Guid.NewGuid();
        await using (var context = new BF2Context(_options))
        {
            context.MapMods.Add(new MapMod { Id = id, Name = "Admin's", Map = Dalian, CreatedBy = "admin", CreatedDate = DateTime.UtcNow, EditedDate = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.UpdateAsync(id, new UpdateLayoutRequest(Alice, "Alice", null, [Block()])));
        Assert.True(e.Forbidden);
        Assert.Null((await _layouts.GetAsync(id)).OwnerId);
    }

    [Fact]
    public async Task ARenameKeepsTheNameUnique()
    {
        await _layouts.CreateAsync(Create("Towers", Bob));
        var mine = await _layouts.CreateAsync(Create("Ramps"));

        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.UpdateAsync(mine.Id, new UpdateLayoutRequest(Alice, "Alice", "towers", [Block()])));
        Assert.True(e.Conflict);

        // Only the case changes: the same layout
        Assert.Equal("RAMPS", (await _layouts.UpdateAsync(mine.Id, new UpdateLayoutRequest(Alice, "Alice", "RAMPS", [Block()]))).Name);
        Assert.Equal("Big ramps", (await _layouts.UpdateAsync(mine.Id, new UpdateLayoutRequest(Alice, "Alice", "Big ramps", [Block()]))).Name);
    }

    [Fact]
    public async Task AnOwnerHasAtMostFiftyLayouts()
    {
        for (var i = 0; i < LayoutService.MaxPerOwner; i++)
            await _layouts.CreateAsync(Create($"L{i}"));

        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.CreateAsync(Create("One more")));
        Assert.True(e.Conflict);
        await _layouts.CreateAsync(Create("Bob's", Bob));
    }

    public static TheoryData<CreateLayoutRequest, string> BadLayouts => new()
    {
        { Create(""), "A layout name has 1 to 50 characters." },
        { Create(new string('x', 51)), "A layout name has 1 to 50 characters." },
        { Create("Reset"), "'Reset' is reserved for .vote reset; pick another name." },
        { Create("Towers", map: "../etc"), "'../etc' is not a map name." },
        { Create("Towers", owner: ""), "An owner id has 1 to 100 characters." },
        { new CreateLayoutRequest("Towers", Dalian, Alice, new string('n', 33), [Block()]), "An owner name has 1 to 32 characters." },
        { new CreateLayoutRequest("Towers", Dalian, Alice, "Alice", []), "A layout has at least one object." },
        { new CreateLayoutRequest("Towers", Dalian, Alice, "Alice", Enumerable.Repeat(Block(), MapBuilder.MaxItems + 1).ToList()), "A layout has at most 1000 objects, not 1001." },
        { Create("Towers", objects: new LayoutObjectDto(MapModObjectKind.SpawnPoint, "", 3, [0, 0, 0], [0, 0, 0])), "A spawn point or helipad needs team 1 or 2." },
        { Create("Towers", objects: new LayoutObjectDto(MapModObjectKind.Object, "two words", 0, [0, 0, 0], [0, 0, 0])), "'two words' is not a template name." },
        { Create("Towers", objects: new LayoutObjectDto((MapModObjectKind)7, "x", 0, [0, 0, 0], [0, 0, 0])), "Unknown kind 7." },
        { Create("Towers", objects: new LayoutObjectDto(MapModObjectKind.Object, "x", 0, [9000, 0, 0], [0, 0, 0])), "A position is [x, height, z] inside the world and a rotation [yaw, pitch, roll] in degrees." },
        { Create("Towers", objects: new LayoutObjectDto(MapModObjectKind.Object, "x", 0, [0, 0], [0, 0, 0])), "A position is [x, height, z] inside the world and a rotation [yaw, pitch, roll] in degrees." },
        { Create("Towers", objects: new LayoutObjectDto(MapModObjectKind.Object, "x", 0, [0, 0, 0], [0, 720, 0])), "A rotation's angles are -360 to 360 degrees." },
    };

    [Theory]
    [MemberData(nameof(BadLayouts))]
    public async Task ASubmittedLayoutIsChecked(CreateLayoutRequest request, string error)
    {
        var e = await Assert.ThrowsAsync<BuilderException>(() => _layouts.CreateAsync(request));
        Assert.Equal(error, e.Message);
        Assert.False(e.Conflict);
        Assert.Empty(await _layouts.ListAsync(null));
    }

    [Fact]
    public async Task AMissingLayoutIsNotFound()
    {
        Assert.True((await Assert.ThrowsAsync<BuilderException>(() => _layouts.GetAsync(Guid.NewGuid()))).NotFound);
        Assert.True((await Assert.ThrowsAsync<BuilderException>(() =>
            _layouts.UpdateAsync(Guid.NewGuid(), new UpdateLayoutRequest(Alice, "Alice", null, [Block()])))).NotFound);
    }

    [Fact]
    public async Task TheDatabaseRefusesTheSameNameTwiceOnAMap()
    {
        await using var context = new BF2Context(_options);
        context.MapMods.Add(new MapMod { Id = Guid.NewGuid(), Name = "Towers", Map = Dalian, CreatedBy = "a", CreatedDate = DateTime.UtcNow, EditedDate = DateTime.UtcNow });
        context.MapMods.Add(new MapMod { Id = Guid.NewGuid(), Name = "Towers", Map = Dalian, CreatedBy = "b", CreatedDate = DateTime.UtcNow, EditedDate = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TheMigrationGivesLayoutsWithoutAMapDalian_AndOneWithTheNameOfADalianLayoutAnOldName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(connection).Options;
        await using var context = new BF2Context(options);
        await context.GetService<IMigrator>().MigrateAsync("20261117153714_MapBuilder");
        foreach (var (name, map) in new[] { ("Towers", Dalian), ("towers", null), ("Ramps", null), (new string('L', 50), Dalian), (new string('l', 50), null) })
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""MapMods"" (""Id"", ""Name"", ""Map"", ""CreatedBy"", ""CreatedDate"", ""EditedDate"") VALUES ({0}, {1}, {2}, 'test', {3}, {3})",
                Guid.NewGuid(), name, map!, DateTime.UtcNow);
        }

        await context.Database.MigrateAsync();
        var layouts = await context.MapMods.OrderBy(m => m.Name).Select(m => new { m.Name, m.Map, m.OwnerId }).ToListAsync();
        Assert.All(layouts, l => Assert.Equal((Dalian, (string?)null), (l.Map, l.OwnerId)));
        Assert.Equal(
            new[] { new string('L', 50), "Ramps", "Towers", new string('l', 44) + " (old)", "towers (old)" }.Order(StringComparer.Ordinal),
            layouts.Select(l => l.Name).Order(StringComparer.Ordinal));
    }
}
