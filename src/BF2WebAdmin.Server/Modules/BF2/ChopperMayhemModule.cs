using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using SixLabors.ImageSharp;

namespace BF2WebAdmin.Server.Modules.BF2;

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
            var objectId = player.Vehicle?.RootVehicleId;
            GameServer.GameWriter.SendRcon(
                $"object.active id{objectId}"
                //"object.hasMobilePhysics 0"
                //"object.setIsDisabledRecursive 1"
            );
            //GameServer.GameWriter.SendText($"NASA detected ({player.Name.Trim()})");
        }
        else if (!isAstronaut && isExistingAstronaut)
        {
            _astronauts.Remove(player.Index);
            var objectId = player.Vehicle?.RootVehicleId;
            GameServer.GameWriter.SendRcon(
                $"object.active id{objectId}"
                //"object.hasMobilePhysics 1"
                //"object.setIsDisabledRecursive 0"
            );
            //GameServer.GameWriter.SendText($"No NASA detected ({player.Name.Trim()})");
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

                    //await Task.Delay(200);
                }
            }
        }

        if (message.Text == ".nudes")
        {
            var startX = 900;
            var startZ = 310;
            var distanceX = 12.5;
            var distanceZ = 11.5;
            var i2 = 0;

            var rotation = Rotation.Neutral;
            using var image = Image.Load(@"C:\Users\Alex\Pictures\bf2text.png");
            for (var ix = 0; ix < image.Width; ix++)
            {
                for (var iy = 0; iy < image.Height; iy++)
                {
                    var pixel = image[ix, iy];
                    if (pixel.A == 0)
                        continue;

                    Logger.LogInformation("Pixel found at {x},{y}", ix, iy);

                    var xPos = startX - ix * distanceX;
                    var zPos = startZ - iy * distanceZ;
                    await Task.Delay(100);
                    SpawnObject("concrete_pillar_wall", new Position(xPos, zPos, -320), rotation, false);
                }
            }
        }
    }
        
    public async ValueTask HandleAsync(MapChangedEvent e)
    {
        GameServer.GameWriter.SendRcon(RconScript.HeliLessBsDamageOn);
        GameServer.GameWriter.SendRcon(
            // No J10 damage
            "ObjectTemplate.activeSafe GenericProjectile AIR_J10_Cannon_Projectile",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 5",
            "ObjectTemplate.detonation.explosionForce 1000",
            "ObjectTemplate.detonation.explosionDamage 1",
            "ObjectTemplate.activeSafe GenericProjectile aa11_archer",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 25",
            "ObjectTemplate.detonation.explosionForce 10000000",
            "ObjectTemplate.detonation.explosionDamage 1",
            "ObjectTemplate.activeSafe GenericProjectile mec_250_bomb",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 25",
            "ObjectTemplate.detonation.explosionForce 10000000",
            "ObjectTemplate.detonation.explosionDamage 1",

            // No F35B damage
            "ObjectTemplate.activeSafe GenericProjectile Air_F35b_AutoCannon_Projectile",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 5",
            "ObjectTemplate.detonation.explosionForce 1000",
            "ObjectTemplate.detonation.explosionDamage 1",
            "ObjectTemplate.activeSafe GenericProjectile aim9m_sidewinder",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 25",
            "ObjectTemplate.detonation.explosionForce 10000000",
            "ObjectTemplate.detonation.explosionDamage 1",
            "ObjectTemplate.activeSafe GenericProjectile mk82_dumbbomb",
            "ObjectTemplate.detonation.explosionDamage 0",
            "ObjectTemplate.minDamage 1",
            "ObjectTemplate.damage 1",
            "ObjectTemplate.detonation.explosionRadius 25",
            "ObjectTemplate.detonation.explosionForce 10000000",
            "ObjectTemplate.detonation.explosionDamage 1"
        );

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
        // victimPosition bugged
        var victimPosition = e.Victim.Position;

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

        GameServer.GameWriter.SendRotate(e.Player, inPortal.OutRotation);
        GameServer.GameWriter.SendTeleport(e.Player, inPortal.OutPosition);

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