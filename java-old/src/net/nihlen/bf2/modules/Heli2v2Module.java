package net.nihlen.bf2.modules;

import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.CommandExecutor;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.listeners.GameStateListener;
import net.nihlen.bf2.listeners.PlayerUpdateListener;
import net.nihlen.bf2.listeners.ProjectileUpdateListener;
import net.nihlen.bf2.objects.*;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.util.Arrays;
import java.util.HashMap;

/**
 * The module dedicated to Helicopter 2v2 specific stuff.
 * 
 * @author Alex
 */
public class Heli2v2Module implements BF2Module,
		CommandExecutor,
		GameStateListener,
		PlayerUpdateListener,
		ProjectileUpdateListener {

	private static final Logger log = LogManager.getLogger();

	// Height difference in meters when sending multiple vehicles back to pad
	public static final int PAD_HEIGHT_DIFF = 10;

	private final GameServer server;

	// Key is a combination of map name + team id
	private final HashMap<String, Position> mapPadPositions;
	private final HashMap<String, Rotation> mapPadRotations;

	// Track when a player exited a vehicle, to show shootouts/headshots

	public Heli2v2Module(GameServer server) {
		this.server = server;
		mapPadPositions = new HashMap<String, Position>();
		mapPadRotations = new HashMap<String, Rotation>();
	}

	public void load(ModManager mm) {
		
		// Events
		mm.addGameStateListener(this);
		mm.addPlayerUpdateListener(this);
		mm.addProjectileUpdateListener(this);
		
		// Commands
		mm.addCommand("pad", "[<player>]", 0, this);

		// Pad positions
		// dalian_2_v_2
		addPad("dalian_2_v_2", 2, new Position(239.8, 166.0, -249.9), new Rotation(-56.8, 0.0, 0.0));	// USMC
		addPad("dalian_2_v_2", 1, new Position(-222.8, 166.0, 151.0), new Rotation(133.5, 0.0, 0.0));	// China
	}

	public void executeCommand(String command, String[] args, Player sender) {
		switch (command) {
			
			case "pad":
				executePadCommand(args, sender);
				break;
				
		}
	}
	
	private void executePadCommand(String[] args, Player sender) {
		if (args.length == 0) {
			
			if (sender.getAuthLevel() == 100) {

				// No player defined, Admin sends all helis to pad
				int pos = 0;
				for (Vehicle v : server.getVehicles()) {
					Player driver = v.getDriver();
					if (driver != null) {
						sendPlayerToPad(driver, pos++);
					}
				}

			} else if (sender.isDriver()) {
				
				sendPlayerToPad(sender, 0);
			}

		} else if (args.length == 1) {

			// Send player with this index to pad
			Player p = server.findPlayer(args[0]);
			if ((p != null) && (p.isDriver())) {
				sendPlayerToPad(p, 0);
			}

		} else {
			log.error("Invalid pad args {}", Arrays.toString(args));
		}
	}

	private void addPad(String mapName, int teamId, Position position, Rotation rotation) {
		String key = mapName + teamId;
		mapPadPositions.put(key, position);
		mapPadRotations.put(key, rotation);
	}

	private Position getPadPosition(String mapName, int teamId) {
		return mapPadPositions.get(mapName + teamId);
	}

	private Rotation getPadRotation(String mapName, int teamId) {
		return mapPadRotations.get(mapName + teamId);
	}

	private void sendPlayerToPad(Player p, int verticalPosition) {
		
		Vehicle v = p.rootVehicle;
		
		if (v != null && v.templateName.startsWith("ahe_")) {
			
			Position pos = getPadPosition(server.getMapName(), p.teamId); //mapPadPositions.get(key);
			Rotation rot = getPadRotation(server.getMapName(), p.teamId); //mapPadRotations.get(key);
			
			if (pos != null && rot != null) {
				
				double stackedHeight = pos.height + verticalPosition * PAD_HEIGHT_DIFF;
				
				String cmd = String.format("position %s %s %s %s\n", p.index, pos.x, stackedHeight, pos.y);
				cmd += String.format("rotation %s %s %s %s\n", p.index, rot.yaw, rot.pitch, rot.roll);
				cmd += String.format("damage %s 875", p.index);
				
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
				log.info("Sent {} to pad {}", p.name, cmd);
				
			} else {
				BF2SocketServer.getInstance().send(server.getServerId(), "rcon game.sayall \"No pad found for map " + server.getMapName() + "\"");
			}
			
		} else {
			log.error("{} has no vehicle!", p.name);
		}
	}

	public void onGameState(GameState state) {
		if (state == GameState.PLAYING) {
			
			// Improved helipads
			String cmd = "rcon ObjectTemplate.activeSafe SupplyObject concreteblock_helipad_SupplyObject_helipad\n";
			cmd += "rcon ObjectTemplate.radius 10\n";
			cmd += "rcon ObjectTemplate.workOnSoldiers 1\n";
			cmd += "rcon ObjectTemplate.workOnVehicles 1\n";
			cmd += "rcon ObjectTemplate.healSpeed 3000\n";
			cmd += "rcon ObjectTemplate.refillAmmoSpeed 3000\n";
			
			// Faster AH-1Z startup
			cmd += "rcon ObjectTemplate.activeSafe Engine ahe_ah1z_Rotor\n";
			cmd += "rcon ObjectTemplate.setAcceleration 0/0/80\n";
			
			// Faster Z-10 startup
			cmd += "rcon ObjectTemplate.activeSafe Engine ahe_z10_Rotor\n";
			cmd += "rcon ObjectTemplate.setAcceleration 0/0/90";
			
			// Test
			cmd += "\nrcon sv.numPlayersNeededToStart 1";
			
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);

			log.info("Applied 2v2 settings");
		}
	}

	public void onPlayerConnect(Player player) { }
	public void onPlayerSpawn(Player player) { }
	public void onPlayerScore(Player player) { }
	public void onPlayerKilled(Player attacker, Player victim, String weapon) { }
	public void onPlayerDeath(Player player) { }
	public void onPlayerChangeTeam(Player player, int teamId) { }

	public void onPlayerDisconnect(Player player) {
		if (server.getPlayers().size() == 0) {
			
			// Change back to default map
			// TODO: check current map id
			String cmd = "rcon admin.setNextLevel 0\n";
			cmd += "rcon admin.runNextLevel";
			
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);

			log.info("Changed back to default map");
		}
	}
	
	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle) { }
	public void onPlayerExitVehicle(Player player, Vehicle vehicle) { }
	public void onPlayerPosition(Player player, Position pos, Rotation rot) { }

	public void onProjectileFired(Projectile projectile) {
		log.debug("{} Projectile fired", projectile.id);
	}

	public void onProjectileExploded(Projectile projectile) {
		String playerName = (projectile.owner != null) ? projectile.owner.name.trim() : "Unknown";
		if (playerName.trim().contains(" ")) {
			String[] parts = playerName.split(" ");
			playerName = parts[parts.length - 1];
		}
		long degreesPerSecond = Math.round(projectile.turnedDegrees / ((projectile.endedTime - projectile.startedTime) / 1000.0));

		log.debug("{} Projectile explode", projectile.id);
		String cmd = String.format("rcon game.sayall \"§C1001%s:§C1001 %sm | %s* (H:%s*) | %s deg/s | %su\"",
				playerName,	Math.round(projectile.distance), Math.round(projectile.turnedDegrees),
				Math.round(projectile.horizontalDegrees), degreesPerSecond, projectile.counter);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);

		/*String cmd2 = String.format("rcon game.sayall \"%s dps, %s dpmin, %s dpmeter (%s)\"",
				Math.round(projectile.turnedDegrees / ((projectile.endedTime - projectile.startedTime) / 1000)),
				Math.round(projectile.turnedDegrees / (((projectile.endedTime - projectile.startedTime) / 1000) * 60)),
				Math.round(projectile.turnedDegrees / projectile.distance), projectile.counter);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd2);
		BF2SocketServer.getInstance().send(server.getServerId(), "rcon game.sayall \"Start: " + projectile.startedTime + "\"");
		BF2SocketServer.getInstance().send(server.getServerId(), "rcon game.sayall \"End: " + projectile.endedTime + "\"");

		Log.info("rcon game.sayall \"Start: " + projectile.startedTime + "\"");
		Log.info("rcon game.sayall \"End: " + projectile.endedTime + "\"");*/
	}

	public void onProjectilePosition(Projectile projectile, Position pos, Rotation rot) {
	}

	private class PlayerVehicle {
		public Player player;
		public Vehicle vehicle;
	}

}
