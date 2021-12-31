using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;
using Serilog;
using SixLabors.ImageSharp;

namespace BF2WebAdmin.Server.Modules.BF2
{
    public class ChopperMayhemModule : BaseModule
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<ChopperMayhemModule>();
        private static readonly IDictionary<string, IList<SafeArea>> MapSafeAreas = new Dictionary<string, IList<SafeArea>>
        {
            {
                "dalian_plant",
                new List<SafeArea>
                {
                    new SafeArea
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

                    new SafeArea
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
                    new Portal
                    {
                        Name = "Carrier to South Docks",
                        InPosition = new Position(776.5, 147.4, 4.6),
                        //OutPosition = new Position(246.8, 148.5, -222.6),
                        //OutPosition = new Position(246.8, 153, -222.6),
                        OutPosition = new Position(311.6, 151.3, -286.8),
                        OutRotation = new Rotation(-76.2, 0, 0)
                    },
                    new Portal
                    {
                        Name = "Airfield to Warehouse",
                        InPosition = new Position(-792.1, 185, -120),
                        //OutPosition = new Position(-158.6, 152.1, 242.6),
                        //OutRotation = new Rotation(-178.8, 0.06, 0)
                        OutPosition = new Position(-217.4, 159.7, 166),
                        OutRotation = new Rotation(137.1, 0, 0)
                    }
                }
            }
        };

        private readonly IDictionary<int, DateTime> _astronauts = new Dictionary<int, DateTime>();

        private readonly double _altitudeOffset = 140;

        public ChopperMayhemModule(IGameServer gameServer) : base(gameServer)
        {
            GameServer.MapChanged += OnMapChangedAsync;
            GameServer.PlayerKill += OnPlayerKillAntiBaserapeAsync;
            GameServer.PlayerPosition += OnPlayerPositionPortalAsync;
            //GameServer.PlayerPosition += OnPlayerPositionAntiNasa;

            // Testing
            GameServer.PlayerSpawn += OnPlayerSpawnAsync;
            GameServer.PlayerJoin += OnPlayerJoinAsync;
            GameServer.ChatMessage += OnChatMessageAsync;
        }

        private async Task OnMapChangedAsync(Map map)
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

            if (map.Name.ToLower() == "dalian_plant")
            {
                await GameServer.ModManager.GetModule<MapModule>().HandleAsync(new MapLoadCommand
                {
                    Name = "dalian_mayhem6",
                    Message = new Message()
                });
            }

            GameServer.GameWriter.SendText("Chopper Mayhem init");
        }

        private Task OnPlayerPositionPortalAsync(Player player, Position position, Rotation rotation, int ping)
        {
            var isInHeli = player.Vehicle?.RootVehicleTemplate.StartsWith("ahe_") ?? false;
            if (!isInHeli)
                return Task.CompletedTask;

            var hasPortals = MapPortals.TryGetValue(GameServer.Map.Name.ToLower(), out var portals);
            if (!hasPortals)
                return Task.CompletedTask;

            var inPortal = portals.FirstOrDefault(p => p.InPosition.Distance(position) < 10);
            if (inPortal == null)
                return Task.CompletedTask;

            //var objectId = player.SubVehicle.RootVehicleId;
            //var replacements = new Dictionary<string, string>
            //{
            //    {"{OBJECT_ID}", objectId.ToString()}
            //};
            //var scriptOn = RconScript.NoclipOn.Select(line => line.ReplacePlaceholders(replacements)).ToArray();
            //var scriptOff = RconScript.NoclipOff.Select(line => line.ReplacePlaceholders(replacements)).ToArray();

            //GameServer.GameWriter.SendRcon(scriptOn);
            //for (var i = 0; i < 100; i++)
            //{
            //    await Task.Delay(50);
            //    GameServer.GameWriter.SendText("Tele #" + i);
            //    GameServer.GameWriter.SendRotate(player, inPortal.OutRotation);
            //    GameServer.GameWriter.SendTeleport(player, new Position(0, 10000, 0));
            //}
            GameServer.GameWriter.SendRotate(player, inPortal.OutRotation);
            GameServer.GameWriter.SendTeleport(player, inPortal.OutPosition);

            //Task.Run(async () =>
            //{
            //    await Task.Delay(5000);
            //    GameServer.GameWriter.SendRcon(scriptOff);
            //    GameServer.GameWriter.SendText("Done");
            //});
            return Task.CompletedTask;
        }

        private Task OnPlayerPositionAntiNasaAsync(Player player, Position position, Rotation rotation, int ping)
        {
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

        private Task OnPlayerKillAntiBaserapeAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon)
        {
            //if (attacker.Team == victim.Team)
            //    return;

            // vicimPosition bugged
            victimPosition = victim.Position;

            //GameServer.GameWriter.SendText($"Attacker: {attackerPosition}, Victim: {victimPosition}");

            var hasSafeAreas = MapSafeAreas.TryGetValue(GameServer.Map.Name.ToLower(), out var safeAreas);
            if (!hasSafeAreas)
                return Task.CompletedTask;

            var isVictimWithinSafeArea = safeAreas
                //.Where(z => z.TeamId == victim.Team.Id)
                .Any(z => victimPosition.Height < z.MaxAltitude && victimPosition.IsInArea(z.Area));

            if (isVictimWithinSafeArea)
            {
                KillPlayer(attacker);
                GameServer.GameWriter.SendScore(attacker, attacker.Score.Total - 2, attacker.Score.Team, attacker.Score.Kills - 1, attacker.Score.Deaths);
                GameServer.GameWriter.SendScore(victim, victim.Score.Total, victim.Score.Team, victim.Score.Kills, victim.Score.Deaths - 1);
                GameServer.GameWriter.SendText($"Baserape is not allowed ({attacker.DisplayName})");
            }
            //else
            //{
            //    GameServer.GameWriter.SendText($"{victim.Name} is not in a safe area {victimPosition}");
            //}

            return Task.CompletedTask;
        }

        private async Task OnChatMessageAsync(Message message)
        {
            //if (message.Type != MessageType.Player)
            //    return;

            //var vehicleId = message.Player.SubVehicle.RootVehicleId.ToString(); // TODO: Why is subvehicle set?
            ////GameServer.GameWriter.SendRcon(
            ////    "object.active id{OBJECT_ID}".Replace("{OBJECT_ID}", vehicleId),
            ////    //"object.mapMaterial 0 Armor_6_helicopter 0",
            ////    //"object.mapMaterial 1 Glass_bulletproof 0",
            ////    //"object.mapMaterial 2 helicopter_landing_rail 0",
            ////    //"object.mapMaterial 3 helicopter_rotor 0",
            ////    //"object.mapMaterial 4 wreck 0",
            ////    //"object.mapMaterial 5 MetalSolid 0"
            ////    "object.material 0",
            ////    "object.armor.maxHitPoints 99999",
            ////    "object.armor.hitPoints 99999"
            ////);
            //GameServer.GameWriter.SendHealth(message.Player, 99999);

            //GameServer.GameWriter.SendText("Set material of " + vehicleId);

            //var rotation = new Rotation(0, -90, 0);
            //var i = 0;
            //for (var x = 100; x > -350; x -= 12)
            //{
            //    for (var y = 100; y > -350; y -= 12)
            //    {
            //        if (i++ > 900)
            //        {
            //            GameServer.GameWriter.SendText("Too many objects!");
            //            return;
            //        }

            //        SpawnObject("concrete_pillar_wall", new Position(x, 220, y), rotation, true, i);
            //        //await Task.Delay(200);
            //    }
            //}


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

                        Log.Information("Pixel found at {x},{y}", ix, iy);

                        var xPos = startX - ix * distanceX;
                        var zPos = startZ - iy * distanceZ;
                        await Task.Delay(100);
                        SpawnObject("concrete_pillar_wall", new Position(xPos, zPos, -320), rotation, false);
                    }
                }
            }

            //var startX = 650;
            //var startY = 0;
            //var distanceX = 12;
            //var distanceY = 12;
            //var i2 = 0;

            //var rotation = new Rotation(0, -90, 0);
            //using (var image = Image.Load(@"C:\Users\Alex\Pictures\bf2text.png"))
            //{
            //    for (var ix = 0; ix < image.Width; ix++)
            //    {
            //        for (var iy = 0; iy < image.Height; iy++)
            //        {
            //            var pixel = image[ix, iy];
            //            if (pixel.A == 0)
            //                continue;

            //            Logger.LogInformation($"Pixel found at {ix},{iy}");

            //            var xPos = startX + ix * distanceX;
            //            var yPos = startY + iy * distanceY;
            //            SpawnObject("concrete_pillar_wall", new Position(xPos, 370, yPos), rotation, true, i2++);
            //        }
            //    }
            //}
        }

        private Task OnPlayerJoinAsync(Player player)
        {
            //var position = Position.Parse("709.000/197.300/-119.300");
            //var rotation = Rotation.Parse("-82.300/-2.800/1.600");
            //SpawnObject("ahe_ah1z", position.NewRelativePosition(0, -5, 0), rotation, true);
            return Task.CompletedTask;
        }

        private Task OnPlayerSpawnAsync(Player player, Position position, Rotation rotation)
        {
            //GameServer.GameWriter.SendRcon(
            //    "ObjectTemplate.activeSafe SpawnPoint usthe_uh60_AISpawnPoint",
            //    "ObjectTemplate.setEnterOnSpawn 1",
            //    "ObjectTemplate.setOnlyForAI 0",

            //    "ObjectTemplate.activeSafe PlayerControlObject ahe_ah1z",
            //    "ObjectTemplate.addTemplate usthe_uh60_AISpawnPoint"
            //);

            //SpawnObject("ahe_ah1z", position.NewRelativePosition(0, -12, 0), rotation, true);
            //GameServer.GameWriter.SendRcon(
            //    //"ObjectTemplate.create SpawnPoint test1", 
            //    //"ObjectTemplate.activeSafe SpawnPoint test1", 
            //    //"ObjectTemplate.isNotSaveable 1", 
            //    //"ObjectTemplate.setControlPointId 1001", 
            //    //"Object.create test1", 
            //    //"Object.absolutePosition 709.000/197.300/-119.300", 
            //    //"Object.rotation -82.300/-2.800/1.600", 
            //    //"Object.layer 2"

            //    "ObjectTemplate.create ControlPoint Base2",
            //    "ObjectTemplate.activeSafe ControlPoint Base2",
            //    "ObjectTemplate.setNetworkableInfo ControlPointInfo",
            //    "ObjectTemplate.addTemplate flagpole",
            //    "ObjectTemplate.setControlPointName Base2",
            //    "ObjectTemplate.radius 1",
            //    "ObjectTemplate.team 2",
            //    "ObjectTemplate.timeToGetControl 0",
            //    "ObjectTemplate.timeToLoseControl 0",
            //    "ObjectTemplate.unableToChangeTeam 1",
            //    "ObjectTemplate.ControlPointId 77",
            //    "Object.create Base2",
            //    "Object.absolutePosition 709.000/197.300/-119.300",
            //    "Object.layer 3",
            //    "ObjectTemplate.create SpawnPoint Sniper_Spot1_1",
            //    "ObjectTemplate.activeSafe SpawnPoint Sniper_Spot1_1",
            //    "ObjectTemplate.modifiedByUser nla",
            //    "ObjectTemplate.isNotSaveable 1",
            //    "ObjectTemplate.setControlPointId 77",
            //    "Object.create Sniper_Spot1_1",
            //    "Object.absolutePosition 709.000/210.300/-119.300",
            //    "Object.rotation -82.300/-2.800/1.600",
            //    "Object.layer 3"
            //);
            //GameServer.GameWriter.SendText($"Created custom spawn point");
            return Task.CompletedTask;
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
}