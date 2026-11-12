using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Abstractions;

/// <summary>The modules of one game server. Disposing it disposes the modules.</summary>
public interface IModManager : IAsyncDisposable
{
    Data.Entities.Server ServerSettings { get; }
    ILookup<string, ServerPlayerAuth> AuthPlayers { get; }
    IMediator Mediator { get; }
    T? GetModule<T>() where T : IModule;
    T GetGlobalService<T>() where T : notnull;
    Task GetAuthPlayersAsync();

    /// <summary>
    /// Runs a chat command from the web UI or Discord. With <paramref name="wait"/> it returns when the command's handlers have
    /// finished, or after that time; without, at once.
    /// </summary>
    Task HandleFakeChatMessageAsync(Message message, TimeSpan? wait = null);

    /// <summary>Runs a game chat line that is a command; the handlers run in the background.</summary>
    ValueTask HandleChatMessageAsync(Message message);
    IEnumerable<string> GetModules();

    /// <summary>The chat commands of the loaded modules.</summary>
    IReadOnlyList<CommandInfo> GetCommands();

    /// <summary>Whether a loaded module has a chat command with this alias (without the "." prefix, any case).</summary>
    bool IsCommand(string alias);
}
