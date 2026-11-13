using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules;

public class ChopperMayhemModule : BaseModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleEventAsync<PlayerKillEvent>,
    IHandleEventAsync<PlayerPositionEvent>
{
    private static readonly IDictionary<string, IList<SafeArea>> MapSafeAreas = new Dictionary<string, IList<SafeArea>>
    {
        {
            "dalian_plant",
            new List<SafeArea>
            {
                new()
                {
                    Name = "USMC Carrier",
                    TeamId = 2,
                    MaxAltitude = 240,
                    Area = new[]
                    {
                        new Position(745, 100, -70),
                        new Position(807, 100, -70),
                        new Position(807, 100, 224),
                        new Position(745, 100, 224),
                    }
                },

                new()
                {
                    Name = "China Airfield",
                    TeamId = 1,
                    MaxAltitude = 260,
                    Area = new[]
                    {
                        new Position(-542, 100, -316),
                        new Position(-664, 100, -465),
                        new Position(-849, 100, -119),
                        new Position(-778, 100, -83),
                        new Position(-667, 100, -278),
                        new Position(-684, 100, -271),
                        new Position(-592, 100, -313),
                    }
                }
            }
        }
    };

    private static readonly IDictionary<string, IList<Portal>> MapPortals = new Dictionary<string, IList<Portal>>
    {
        {
            "dalian_plant",
            new List<Portal>
            {
                new()
                {
                    Name = "Carrier to South Docks",
                    InPosition = new Position(776.5, 147.4, 4.6),
                    OutPosition = new Position(311.6, 151.3, -286.8),
                    OutRotation = new Rotation(-76.2, 0, 0)
                },
                new()
                {
                    Name = "Airfield to Warehouse",
                    InPosition = new Position(-792.1, 185, -120),
                    OutPosition = new Position(-217.4, 159.7, 166),
                    OutRotation = new Rotation(137.1, 0, 0)
                }
            }
        }
    };

    private readonly IDictionary<int, DateTime> _astronauts = new Dictionary<int, DateTime>();

    private readonly double _altitudeOffset = 140;

    public ChopperMayhemModule(IGameServer server, ILogger<ChopperMayhemModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
    }
        
    private Task OnPlayerPositionAntiNasaAsync(Player player, Position position, Rotation rotation, int ping)
    {
        // TODO: use this again?
        var isInHeli = player.Vehicle?.RootVehicleTemplate.StartsWith("ahe_") ?? false;
        if (!isInHeli)
            return Task.CompletedTask;

        var maxAltitude = 100;
        var isAstronaut = position.Height > maxAltitude + _altitudeOffset;
        var isExistingAstronaut = _astronauts.ContainsKey(player.Index);
        if (isAstronaut == isExistingAstronaut)
            return Task.CompletedTask;

        if (isAstronaut && !isExistingAstronaut)
        {
            _astronauts.Add(player.Index, DateTime.UtcNow);
            GameServer.GameWriter.SendSetObjectActive(player.Vehicle?.ConsoleId ?? -1);
        }
        else if (!isAstronaut && isExistingAstronaut)
        {
            _astronauts.Remove(player.Index);
            GameServer.GameWriter.SendSetObjectActive(player.Vehicle?.ConsoleId ?? -1);
        }

        return Task.CompletedTask;
    }
        
    private async Task OnChatMessageAsync(Message message)
    {
        // Generate some objects in different patterns - TODO: are these both in DB now?
        if (message.Text == ".shadeforsandro")
        {
            var delta = -30;
            var rotation = new Rotation(0, -180, 0);
            var i = 0;

            var startX = 300;
            var endX = -350;

            var startY = 300;
            var endY = -350;

            for (var x = startX; x > endX; x += delta)
            {
                for (var y = startY; y > endY; y += delta)
                {
                    if (i++ > 900)
                    {
                        GameServer.GameWriter.SendText("Too many objects!");
                        return;
                    }

                    SpawnObject("coolingtower_01", new Position(x, 258, y), rotation, true, i);

                    var isEdge = Math.Abs(x - startX) < Math.Abs(delta) ||
                                 Math.Abs(x - endX) < Math.Abs(delta) ||
                                 Math.Abs(y - startY) < Math.Abs(delta) ||
                                 Math.Abs(y - endY) < Math.Abs(delta);

                    if (isEdge)
                    {
                        SpawnObject("coolingtower_01", new Position(x, 190, y), Rotation.Neutral, true, i);
                        SpawnObject("coolingtower_01", new Position(x, 120, y), rotation, true, i);
                    }
                }
            }
        }
    }
        
    /// <summary>The jets' weapons made harmless on map change: template, explosion radius and force.</summary>
    private static readonly (string Template, string Radius, string Force)[] HarmlessJetWeapons =
    [
        ("AIR_J10_Cannon_Projectile", "5", "1000"),
        ("aa11_archer", "25", "10000000"),
        ("mec_250_bomb", "25", "10000000"),
        ("Air_F35b_AutoCannon_Projectile", "5", "1000"),
        ("aim9m_sidewinder", "25", "10000000"),
        ("mk82_dumbbomb", "25", "10000000")
    ];

    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        GameServer.GameWriter.SendConsole(RconScript.HeliLessBsDamageOn);
        // No J10 or F35B damage: their cannons, missiles and bombs only push
        foreach (var (template, radius, force) in HarmlessJetWeapons)
        {
            GameServer.GameWriter.SetTemplate("GenericProjectile", template,
                ("detonation.explosionDamage", "0"), ("minDamage", "1"), ("damage", "1"),
                ("detonation.explosionRadius", radius), ("detonation.explosionForce", force), ("detonation.explosionDamage", "1"));
        }

        if (e.Map.Name.ToLower() == "dalian_plant")
        {
            await GameServer.ModManager.GetModule<MapModule>().HandleAsync(new MapLoadCommand
            {
                Name = "dalian_mayhem6",
                Message = new Message()
            });
        }

        GameServer.GameWriter.SendText("Chopper Mayhem init");
    }

    public ValueTask HandleAsync(PlayerKillEvent e)
    {
        // The admin channel reports the victim's own root position
        var victimPosition = e.VictimPosition ?? e.Victim.Position;

        var hasSafeAreas = MapSafeAreas.TryGetValue(GameServer.Map.Name.ToLower(), out var safeAreas);
        if (!hasSafeAreas)
            return ValueTask.CompletedTask;

        var isVictimWithinSafeArea = safeAreas
            .Any(z => victimPosition.Height < z.MaxAltitude && victimPosition.IsInArea(z.Area));

        if (isVictimWithinSafeArea)
        {
            KillPlayer(e.Attacker);
            GameServer.GameWriter.SendScore(e.Attacker, e.Attacker.Score.Total - 2, e.Attacker.Score.Team, e.Attacker.Score.Kills - 1, e.Attacker.Score.Deaths);
            GameServer.GameWriter.SendScore(e.Victim, e.Victim.Score.Total, e.Victim.Score.Team, e.Victim.Score.Kills, e.Victim.Score.Deaths - 1);
            GameServer.GameWriter.SendText($"Baserape is not allowed ({e.Attacker.DisplayName})");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PlayerPositionEvent e)
    {
        var isInHeli = e.Player.Vehicle?.RootVehicleTemplate.StartsWith("ahe_") ?? false;
        if (!isInHeli)
            return ValueTask.CompletedTask;

        var hasPortals = MapPortals.TryGetValue(GameServer.Map.Name.ToLower(), out var portals);
        if (!hasPortals)
            return ValueTask.CompletedTask;

        var inPortal = portals.FirstOrDefault(p => p.InPosition.Distance(e.Position) < 10);
        if (inPortal == null)
            return ValueTask.CompletedTask;

        GameServer.GameWriter.SendTeleport(e.Player, inPortal.OutPosition, inPortal.OutRotation);

        return ValueTask.CompletedTask;
    }
}

public class SafeArea
{
    public string Name { get; set; }
    public int TeamId { get; set; }
    public int MaxAltitude { get; set; }
    public Position[] Area { get; set; }
}

public class Portal
{
    public string Name { get; set; }
    public Position InPosition { get; set; }
    public Position OutPosition { get; set; }
    public Rotation OutRotation { get; set; }
}