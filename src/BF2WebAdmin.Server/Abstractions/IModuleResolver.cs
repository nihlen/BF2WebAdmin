using BF2WebAdmin.Common;

namespace BF2WebAdmin.Server.Abstractions;

public interface IModuleResolver
{
    /// <summary>The parsers of each chat command alias (lowercase, without the "." or "!").</summary>
    IReadOnlyDictionary<string, IReadOnlyList<CommandParser>> CommandParsers { get; }
    IDictionary<Type, IList<Func<ICommand, ValueTask>>> CommandHandlers { get; }
    IDictionary<Type, IList<Func<IEvent, ValueTask>>> EventHandlers { get; }
    IDictionary<Type, Auth> AuthLevels { get; }
    IEnumerable<Type> ModuleTypes { get; }
    IDictionary<Type, IModule> Modules { get; }
    IReadOnlyList<CommandInfo> GetCommands();

    /// <summary>Whether a loaded module handles commands of this type.</summary>
    bool IsHandled(Type commandType);
}

/// <summary>
/// A chat command (".alias param ..."): its aliases, parameter names, required auth level, the loaded modules that handle
/// it and what it does in one sentence.
/// </summary>
public record CommandInfo(IReadOnlyList<string> Aliases, IReadOnlyList<string> Parameters, Auth AuthLevel, IReadOnlyList<string> Modules, string? Description = null);

/// <summary>
/// A module a server group can enable: its class name, its name and description in words, the chat commands it adds
/// (with those of the modules that come with it, as ".alias") and the display names of those modules.
/// </summary>
public record ModuleInfo(string Name, string DisplayName, string? Description, IReadOnlyList<string> Commands, IReadOnlyList<string> LoadedWith);
