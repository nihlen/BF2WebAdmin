using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// .pilot (.fly) and .gunner (.gun): who takes which seat when teammates spawn into the heli together (died together,
/// the heli spawn selected). The game server's module spawns them in that order, so one teammate's preference is
/// enough, and the latest one wins: a teammate who wanted the same seat loses that preference. An admin can set it for
/// another player (.pilot name). It holds until the player leaves the server. Needs a module with seat preferences
/// (admin channel capability bit 4). Loaded with <see cref="Heli2v2Module"/>, in or out of its 2v2 mode.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class SeatModule : BaseModule,
    IHandleEventAsync<PlayerLeftEvent>,
    IHandleCommand<PilotCommand>,
    IHandleCommand<PilotForCommand>,
    IHandleCommand<GunnerCommand>,
    IHandleCommand<GunnerForCommand>
{
    // What this module asked for; the game server keeps its own copy until the player disconnects
    private readonly Dictionary<Player, SeatPreference> _preferences = new();

    public SeatModule(IGameServer server, ILogger<SeatModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
    }

    public SeatPreference PreferenceOf(Player player) => _preferences.GetValueOrDefault(player, SeatPreference.None);

    public ValueTask HandleAsync(PlayerLeftEvent e)
    {
        _preferences.Remove(e.Player);
        return ValueTask.CompletedTask;
    }

    public void Handle(PilotCommand command) => Set(command.Message.Player, SeatPreference.Pilot, command.Message.Player);

    public void Handle(GunnerCommand command) => Set(command.Message.Player, SeatPreference.Gunner, command.Message.Player);

    public void Handle(PilotForCommand command) => SetFor(command.Name, SeatPreference.Pilot, command.Message.Player);

    public void Handle(GunnerForCommand command) => SetFor(command.Name, SeatPreference.Gunner, command.Message.Player);

    private void SetFor(string name, SeatPreference preference, Player requester)
    {
        var player = GameServer.GetPlayerByName(name);
        if (player == null)
        {
            GameServer.GameWriter.SendPrivateMessage(requester, "Player not found");
            return;
        }

        Set(player, preference, requester);
    }

    private void Set(Player player, SeatPreference preference, Player requester)
    {
        if (!GameServer.GameWriter.SetSeatPreference(player, preference))
        {
            GameServer.GameWriter.SendPrivateMessage(requester, "This server can't choose seats");
            return;
        }

        _preferences[player] = preference;

        // The latest one wins: a teammate who wanted the same seat now takes the other one
        var rivals = _preferences
            .Where(p => p.Key != player && p.Value == preference && p.Key.Team?.Id == player.Team?.Id)
            .Select(p => p.Key)
            .ToList();
        foreach (var rival in rivals)
        {
            _preferences.Remove(rival);
            GameServer.GameWriter.SetSeatPreference(rival, SeatPreference.None);
        }

        var seat = preference == SeatPreference.Pilot ? "pilot" : "gunner";
        GameServer.GameWriter.SendText($"{player.DisplayName} is {seat} when spawning into the heli with a teammate");
    }
}
