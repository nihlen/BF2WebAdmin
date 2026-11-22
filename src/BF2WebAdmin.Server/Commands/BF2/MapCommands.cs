using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("load|mapload <Name>", Auth.Admin, Description = "Loads a saved map builder layout.")]
public class MapLoadCommand : BaseCommand
{
    public string Name { get; set; }
}

/// <summary>
/// A vote on loading a layout of the running map, or with <c>clear</c>, <c>reset</c> or <c>restart</c> on a map clear, a
/// round reset or a server restart (<see cref="Modules.LayoutVoteModule"/>); anyone may start one.
/// </summary>
[Command("vote|votemap <Name>", Auth.All, Description = "Starts a vote on loading a layout, or on clear, reset or restart.")]
public class LayoutVoteCommand : BaseCommand
{
    public string Name { get; set; } = "";
}

[Command("save|mapsave <Name>", Auth.Admin, Description = "Saves what the map builder built as a new layout.")]
public class MapSaveCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("o|mapobject <Template>", Auth.Admin, Description = "Spawns an object where you are.")]
public class MapObjectCommand : BaseCommand
{
    public string Template { get; set; }
}

[Command("clear|mapclear", Auth.Admin, Description = "Removes everything the map builder built.")]
public class MapClearCommand : BaseCommand
{
}

[Command("undo|mapundo <Count>", Auth.Admin, Description = "Removes the most recently built objects.")]
public class MapUndoCommand : BaseCommand
{
    public int Count { get; set; }
}