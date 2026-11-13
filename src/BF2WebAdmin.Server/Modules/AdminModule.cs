using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Services;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// The admin commands in game: kick, warn, ban and the rest of <see cref="ServerAdmin"/> (the same as "!" in the web chat
/// box and Discord), switching and killing players, and auth levels. Loaded on every server with <see cref="BF2Module"/>.
/// </summary>
[LoadedWith(typeof(BF2Module))]
public class AdminModule : BaseModule,
    IHandleCommandAsync<KickCommand>,
    IHandleCommandAsync<WarnCommand>,
    IHandleCommandAsync<BanCommand>,
    IHandleCommandAsync<UnbanCommand>,
    IHandleCommandAsync<BanListCommand>,
    IHandleCommandAsync<ClearBansCommand>,
    IHandleCommandAsync<RunMapCommand>,
    IHandleCommandAsync<ListPlayersCommand>,
    IHandleCommand<SwitchCommand>,
    IHandleCommand<SwitchIdCommand>,
    IHandleCommand<SwitchAllCommand>,
    IHandleCommand<KillCommand>,
    IHandleCommand<KillIdCommand>,
    IHandleCommandAsync<GetAuthCommand>,
    IHandleCommandAsync<SetAuthCommand>
{
    private readonly IServerSettingsRepository _serverSettingsRepository;
    private readonly ServerAdmin _admin;

    public AdminModule(IGameServer server, IServerSettingsRepository serverSettingsRepository, IServerBanRepository banRepository, ILogger<AdminModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _serverSettingsRepository = serverSettingsRepository;
        _admin = new ServerAdmin(server, banRepository);
    }

    // Kicks, warnings and bans are announced in the chat by ServerAdmin; the other answers and every refusal go to the
    // player who asked
    public ValueTask HandleAsync(KickCommand command) => RunAdminCommandAsync(command, announced: true);
    public ValueTask HandleAsync(WarnCommand command) => RunAdminCommandAsync(command, announced: true);
    public ValueTask HandleAsync(BanCommand command) => RunAdminCommandAsync(command, announced: true);
    public ValueTask HandleAsync(UnbanCommand command) => RunAdminCommandAsync(command, announced: false);
    public ValueTask HandleAsync(BanListCommand command) => RunAdminCommandAsync(command, announced: false);
    public ValueTask HandleAsync(ClearBansCommand command) => RunAdminCommandAsync(command, announced: false);
    public ValueTask HandleAsync(RunMapCommand command) => RunAdminCommandAsync(command, announced: false);
    public ValueTask HandleAsync(ListPlayersCommand command) => RunAdminCommandAsync(command, announced: false);

    private async ValueTask RunAdminCommandAsync(AdminChatCommand command, bool announced)
    {
        var issuer = command.Message.Player;
        try
        {
            var answer = await _admin.RunAsync(command.Line, issuer.DisplayName);
            if (!announced)
            {
                foreach (var line in GetObfuscatedResponse(answer).Split('\n'))
                    GameServer.GameWriter.SendPrivateMessage(issuer, line);
            }
        }
        catch (AdminCommandException e)
        {
            GameServer.GameWriter.SendPrivateMessage(issuer, ServerAdmin.Describe(e));
        }
    }

    public void Handle(SwitchCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        SwitchPlayer(player);
    }

    public void Handle(SwitchIdCommand command)
    {
        var player = GameServer.GetPlayer(command.PlayerId);
        if (player == null)
            return;

        SwitchPlayer(player);
    }

    public void Handle(SwitchAllCommand command)
    {
        SwitchAll();
    }

    public void Handle(KillCommand command)
    {
        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
            return;

        GameServer.GameWriter.SendKill(player, destroyVehicle: true);
    }

    public void Handle(KillIdCommand command)
    {
        var player = GameServer.GetPlayer(command.PlayerId);
        if (player == null)
            return;

        GameServer.GameWriter.SendKill(player, destroyVehicle: true);
    }

    public ValueTask HandleAsync(GetAuthCommand command)
    {
        if (command.Name.Contains(" "))
            return ValueTask.CompletedTask;

        var player = GameServer.GetPlayerByName(command.Name);
        if (player == null)
        {
            GameServer.GameWriter.SendText("No matching player found");
            return ValueTask.CompletedTask;
        }

        var playerAuth = GameServer.ModManager.AuthPlayers[player.Hash].FirstOrDefault();
        if (playerAuth != null)
        {
            GameServer.GameWriter.SendText($"Player {command.Name} auth level {playerAuth.AuthLevel}");
        }
        else
        {
            GameServer.GameWriter.SendText($"No auth level set for {player.DisplayName}");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(SetAuthCommand command)
    {
        var player = command.Message.Player;
        var target = GameServer.GetPlayerByName(command.Name);
        if (target == null)
        {
            GameServer.GameWriter.SendText("Player not found");
            return;
        }

        var playerAuthLevel = GameServer.ModManager.AuthPlayers[player.Hash].FirstOrDefault()?.AuthLevel ?? 0;
        var targetAuthLevel = GameServer.ModManager.AuthPlayers[target.Hash].FirstOrDefault()?.AuthLevel ?? 0;
        if (playerAuthLevel <= command.Level || playerAuthLevel <= targetAuthLevel || player.Index == target.Index)
        {
            GameServer.GameWriter.SendText("Insufficient permissions");
            return;
        }

        await _serverSettingsRepository.SetPlayerAuthAsync(GameServer.ModManager.ServerSettings.ServerGroup, target.Hash, command.Level);
        await GameServer.ModManager.GetAuthPlayersAsync();
        GameServer.GameWriter.SendText($"Player {command.Name} auth level set to {command.Level}");
    }
}
