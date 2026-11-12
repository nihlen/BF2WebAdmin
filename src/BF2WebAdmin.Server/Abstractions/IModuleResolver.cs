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

/// <summary>A chat command (".alias param ..."): its aliases, parameter names, required auth level and the loaded modules that handle it.</summary>
public record CommandInfo(IReadOnlyList<string> Aliases, IReadOnlyList<string> Parameters, Auth AuthLevel, IReadOnlyList<string> Modules);
