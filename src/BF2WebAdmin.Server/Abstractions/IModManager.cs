using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Abstractions
{
    public interface IModManager
    {
        Data.Entities.Server ServerSettings { get; }
        ILookup<string, ServerPlayerAuth> AuthPlayers { get; }
        T GetModule<T>() where T : IModule;
        //T GetModule<T>(IGameServer gameServer) where T : IModule;
        Task GetAuthPlayersAsync();
        Task HandleFakeChatMessageAsync(Message message);
    }
}