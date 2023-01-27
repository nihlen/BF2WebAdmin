using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Abstractions;

public interface IModManager : IDisposable
{
    Data.Entities.Server ServerSettings { get; }
    ILookup<string, ServerPlayerAuth> AuthPlayers { get; }
    IMediator Mediator { get; }
    T? GetModule<T>() where T : IModule;
    T GetGlobalService<T>() where T : notnull;
    Task GetAuthPlayersAsync();
    Task HandleFakeChatMessageAsync(Message message);
    ValueTask HandleChatMessageAsync(Message message);
    IEnumerable<string> GetModules();
}
