namespace BF2WebAdmin.Server.Constants;

public static class RconScript
{
    public static readonly string RestartMap = "admin.restartmap";

    public static readonly string[] InitServer = {

        // KillCommand: Set players to bleed out
        "ObjectTemplate.active us_heavy_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.active us_light_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.active ch_heavy_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.active ch_light_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "GeometryTemplate.active mec_heavy_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.activeSafe Soldier mec_light_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.active eu_heavy_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",
        "ObjectTemplate.active eu_soldier",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 100",
        "ObjectTemplate.armor.criticalDamage 10",

        // Improve helipads
        "ObjectTemplate.activeSafe SupplyObject concreteblock_helipad_SupplyObject_helipad",
        "ObjectTemplate.radius 10",
        "ObjectTemplate.workOnSoldiers 1",
        "ObjectTemplate.workOnVehicles 1",
        "ObjectTemplate.healSpeed 3000",
        "ObjectTemplate.refillAmmoSpeed 3000",

        //// Improve carrier (Disabled due to messing up dogfights)
        //"ObjectTemplate.activeSafe SupplyObject concreteblock_helipad_SupplyObject_helipad",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_front_SupplyObject_deck2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_front_SupplyObject_helipad_1",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_front_SupplyObject_helipad_2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_front_SupplyObject_helipad_3",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_front_SupplyObject_helipad_4",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_helipad_starboard",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_helipad_port",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_deck1",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_deck2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_garage",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_helipad_deck_1",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_back_SupplyObject_helipad_deck_2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_mid_SupplyObject_deck1",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_mid_SupplyObject_deck2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_mid_SupplyObject_helipad_1",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.activeSafe SupplyObject us_carrier_wasp_mid_SupplyObject_helipad_2",
        //"ObjectTemplate.radius 20",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",

        //// Improve airstrip (Disabled due to messing up dogfights)
        //"ObjectTemplate.activeSafe SupplyObject concreteblock_airstrip_long_endstr_SupplyObject_air1",
        //"ObjectTemplate.radius 25",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.addVehicleType 3",
        //"ObjectTemplate.activeSafe SupplyObject concreteblock_airstrip_long_endstr_SupplyObject_air2",
        //"ObjectTemplate.radius 25",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.addVehicleType 3",
        //"ObjectTemplate.activeSafe SupplyObject concreteblock_airstrip_long_endstr_SupplyObject_air3",
        //"ObjectTemplate.radius 25",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.addVehicleType 3",
        //"ObjectTemplate.activeSafe SupplyObject concreteblock_airstrip_long_endstr_SupplyObject_air4",
        //"ObjectTemplate.radius 25",
        //"ObjectTemplate.workOnSoldiers 1",
        //"ObjectTemplate.workOnVehicles 1",
        //"ObjectTemplate.healSpeed 3000",
        //"ObjectTemplate.refillAmmoSpeed 3000",
        //"ObjectTemplate.addVehicleType 3",
		
        // Faster heli startup
        "ObjectTemplate.activeSafe Engine ahe_ah1z_Rotor",
        "ObjectTemplate.setAcceleration 0/0/80",
        "ObjectTemplate.activeSafe Engine ahe_z10_Rotor",
        "ObjectTemplate.setAcceleration 0/0/90",

        // TV testing
        //"ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv",
        //"ObjectTemplate.seek.trackingDelay 0",
        //"ObjectTemplate.seek.maxAngleLock 360",
        //"ObjectTemplate.seek.maxDistLock 3000",
        //"ObjectTemplate.drag 0",
        //"ObjectTemplate.mass 0",
        //"ObjectTemplate.gravityModifier 0",
        //"ObjectTemplate.follow.maxYaw 15",
        //"ObjectTemplate.follow.maxPitch 15",
        //"ObjectTemplate.follow.changePitch 12",
        //"ObjectTemplate.follow.changeYaw 12",
        //"ObjectTemplate.follow.minDist 0",
        //"ObjectTemplate.damage 10",

        //// Flares
        //"ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        //"ObjectTemplate.projectileTemplate ahe_z10",
        //"ObjectTemplate.fire.addFireRate 1",
        //"ObjectTemplate.fire.burstSize 100",
        //"ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        //"ObjectTemplate.projectileTemplate ahe_ah1z",
        //"ObjectTemplate.fire.addFireRate 1",
        //"ObjectTemplate.fire.burstSize 100",
        //"ObjectTemplate.ammo.magSize 100",
        //"ObjectTemplate.ammo.minimumTimeUntilReload 0.1",

        // Make transport helis spawnable
        "ObjectTemplate.activeSafe SpawnPoint usthe_uh60_AISpawnPoint",
        "ObjectTemplate.setOnlyForAI 0",
        "ObjectTemplate.activeSafe SpawnPoint chthe_z8_AISpawnPoint",
        "ObjectTemplate.setOnlyForAI 0",
        "ObjectTemplate.activeSafe SpawnPoint the_mi17_AISpawnPoint",
        "ObjectTemplate.setOnlyForAI 0",

        // Less damage while upside down or in water
        "ObjectTemplate.activeSafe PlayerControlObject ahe_ah1z",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 35",
        "ObjectTemplate.armor.hpLostWhileInWater 50",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 50",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18",
        "ObjectTemplate.activeSafe PlayerControlObject ahe_z10",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 35",
        "ObjectTemplate.armor.hpLostWhileInWater 40",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 40",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18"
    };

    public static readonly string[] MgOff = {
        // Heli MG off
        "ObjectTemplate.activeSafe GenericProjectile AHE_AH1Z_Gun_Projectile",
        "ObjectTemplate.detonation.explosionDamage 0",
        "ObjectTemplate.minDamage 0",
        "ObjectTemplate.damage 0",
        "ObjectTemplate.activeSafe GenericProjectile AHE_Z10_Gun_Projectile",
        "ObjectTemplate.detonation.explosionDamage 0",
        "ObjectTemplate.minDamage 0",
        "ObjectTemplate.damage 0",
    };

    public static readonly string[] MgOn = {
        // Heli MG on
        "ObjectTemplate.activeSafe GenericProjectile AHE_AH1Z_Gun_Projectile",
        "ObjectTemplate.detonation.explosionDamage 75",
        "ObjectTemplate.minDamage 1",
        "ObjectTemplate.damage 20",
        "ObjectTemplate.activeSafe GenericProjectile AHE_Z10_Gun_Projectile",
        "ObjectTemplate.detonation.explosionDamage 75",
        "ObjectTemplate.minDamage 1",
        "ObjectTemplate.damage 20",
    };

    public static readonly string[] AddObject = {
        // Make this template networkable
        "objecttemplate.active {TEMPLATE}",
        "objecttemplate.setNetworkableInfo BasicInfo",

        // Create an object spawner template
        "objecttemplate.create ObjectSpawner tmp_spawner_{TEMPLATE}",
        "objecttemplate.activeSafe ObjectSpawner tmp_spawner_{TEMPLATE}",
        "objecttemplate.isNotSaveable 1",
        "objecttemplate.hasMobilePhysics 0",
        "objecttemplate.setObjectTemplate 0 {TEMPLATE}",
        "objecttemplate.setObjectTemplate 1 {TEMPLATE}",
        "objecttemplate.setObjectTemplate 2 {TEMPLATE}",
        "objecttemplate.setNetworkableInfo BasicInfo",

        // Create the object spawner
        "object.create tmp_spawner_{TEMPLATE}",
        "object.absolutePosition {POSITION}",
        "object.rotation {ROTATION}",
        "object.name obj_{TEMPLATE}",
        "object.team 1",
        "object.layer 1",
        "object.delete"
    };

    public static readonly string[] NoclipOn = {
        // Remove object collision
        "object.active id{OBJECT_ID}",
        "object.hasCollision 0",
        "object.armor.hpLostWhileUpSideDown 0",
        "object.armor.hpLostWhileInWater 0",
        "object.armor.hpLostWhileInDeepWater 0",
        "object.armor.hpLostWhileCriticalDamage 0",
    };

    public static readonly string[] NoclipOff = {
        // Re-add object collision
        "object.active id{OBJECT_ID}",
        "object.hasCollision 1",
        "object.armor.hpLostWhileUpSideDown 350",
        "object.armor.hpLostWhileInWater 500",
        "object.armor.hpLostWhileInDeepWater 500",
        "object.armor.hpLostWhileCriticalDamage 18"
    };


    public static readonly string[] SniperArtilleryOn = {
        // Sniper shoots artillery
        "ObjectTemplate.activeSafe GenericFireArm gbrif_l96a1",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile"
    };

    public static readonly string[] SniperArtilleryOff = {
        // TODO: Remove sniper shoots artillery
        "ObjectTemplate.activeSafe GenericFireArm gbrif_l96a1",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile"
    };


    public static readonly string[] HydraArtilleryOn = {
        // Heli shoots artillery
        "ObjectTemplate.activeSafe GenericProjectile USART_LW155_Barrel_Projectile",
        "ObjectTemplate.gravityModifier 0.05",
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_HydraLauncher",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_S8Launcher",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile"
    };

    public static readonly string[] HydraArtilleryOff = {
        // Remove heli shoots artillery
        "ObjectTemplate.activeSafe GenericProjectile USART_LW155_Barrel_Projectile",
        "ObjectTemplate.gravityModifier 5",
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_HydraLauncher",
        "ObjectTemplate.projectileTemplate ahe_ah1z_HydraLauncher_Projectile",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_S8Launcher",
        "ObjectTemplate.projectileTemplate ahe_z10_S8Launcher_Projectile"
    };


    public static readonly string[] HeliSmokeFlaresOn = {
        // Heli flares are now smokes
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate Smokeflare",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate Smokeflare"
    };

    public static readonly string[] HeliAh1zFlaresOn = {
        // Heli flares are now us helis
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_ah1z",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_ah1z"
    };

    public static readonly string[] HeliZ10FlaresOn = {
        // Heli flares are now china helis
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_z10",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_z10"
    };

    public static readonly string[] HeliArtyFlaresOn = {
        // Heli flares are now arty
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate USART_LW155_Barrel_Projectile"
    };

    public static readonly string[] HeliSupplyFlaresOn = {
        // Heli flares are now arty
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate supply_crate",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate supply_crate"
    };

    public static readonly string[] HeliFlareDefault = {
        // Heli flares are back to normal
        "ObjectTemplate.activeSafe GenericFireArm AHE_AH1Z_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_ah1z_flareLauncher_Projectile",
        "ObjectTemplate.activeSafe GenericFireArm ahe_z10_FlareLauncher",
        "ObjectTemplate.projectileTemplate ahe_z10_flareLauncher_Projectile"
    };


    public static readonly string[] HeliLessBsDamageOn = {
        // Less damage while upside down or in water
        "ObjectTemplate.activeSafe PlayerControlObject ahe_ah1z",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 35",
        "ObjectTemplate.armor.hpLostWhileInWater 50",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 50",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18",
        "ObjectTemplate.activeSafe PlayerControlObject ahe_z10",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 35",
        "ObjectTemplate.armor.hpLostWhileInWater 40",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 40",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18"
    };

    public static readonly string[] HeliLessBsDamageOff = {
        // Default damage while upside down or in water
        "ObjectTemplate.activeSafe PlayerControlObject ahe_ah1z",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 350",
        "ObjectTemplate.armor.hpLostWhileInWater 500",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 500",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18",
        "ObjectTemplate.activeSafe PlayerControlObject ahe_z10",
        "ObjectTemplate.armor.hpLostWhileUpSideDown 350",
        "ObjectTemplate.armor.hpLostWhileInWater 400",
        "ObjectTemplate.armor.hpLostWhileInDeepWater 400",
        "ObjectTemplate.armor.hpLostWhileCriticalDamage 18"
    };

    public static readonly string[] AutoRepairOn = {
        "ObjectTemplate.activeSafe SupplyObject concreteblock_helipad_SupplyObject_helipad",  
        "ObjectTemplate.radius 1000",  
        // "ObjectTemplate.workOnSoldiers 1",  
        // "ObjectTemplate.workOnVehicles 1",  
        // "ObjectTemplate.healSpeed 3000",  
        // "ObjectTemplate.refillAmmoSpeed 3000"
    };

    public static readonly string[] AutoRepairOff = {
        "ObjectTemplate.activeSafe SupplyObject concreteblock_helipad_SupplyObject_helipad",  
        "ObjectTemplate.radius 10",  
        // "ObjectTemplate.workOnSoldiers 1",  
        // "ObjectTemplate.workOnVehicles 1",  
        // "ObjectTemplate.healSpeed 3000",  
        // "ObjectTemplate.refillAmmoSpeed 3000"
    };

    //def setFirearmProjectile(firearmTemp, projTemp):
    //host.rcon_invoke("ObjectTemplate.active " + firearmTemp)
    //host.rcon_invoke("ObjectTemplate.projectileTemplate " + projTemp)
    //host.rcon_invoke("ObjectTemplate.setNetworkableInfo BasicInfo")

    // TODO daemontools restart bf2 server after running this closing the bf2 process
    // exec quit
}