package net.nihlen.bf2.modules;

import java.util.HashMap;

import net.nihlen.bf2.BF2Module;
import net.nihlen.bf2.BF2SocketServer;
import net.nihlen.bf2.CommandExecutor;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Rotation;
import net.nihlen.bf2.objects.Vehicle;
import net.nihlen.bf2.util.Log;

public class Heli2v2Module implements BF2Module, CommandExecutor {

	// Height difference in meters when sending many vehicles back to pad
	public static final int PAD_HEIGHT_DIFF = 10;

	private final GameServer server;

	// Key is a combination of map name + team id
	private HashMap<String, Position> mapPadPositions;
	private HashMap<String, Rotation> mapPadRotations;

	public Heli2v2Module(GameServer server) {
		this.server = server;
		mapPadPositions = new HashMap<String, Position>();
		mapPadRotations = new HashMap<String, Rotation>();
	}

	public void load(ModManager mm) {
		mm.addCommand("pad", "[<player>]", 0, this);

		// dalian_2_v_2
		addPad("dalian_2_v_2", 2, new Position(239.8, 166.0, -249.9), new Rotation(-56.8, 0.0, 0.0));	// USMC
		addPad("dalian_2_v_2", 1, new Position(-222.8, 166.0, 151.0), new Rotation(133.5, 0.0, 0.0));	// China
	}

	public void executeCommand(String command, String[] args, Player player) {
		switch (command) {
			
			case "pad":
				executePadCommand(args, player);
				break;
				
		}
	}
	
	private void executePadCommand(String[] args, Player player) {
		if (args.length == 0) {
			
			if (player.getAuthLevel() == 100) {

				// No player defined, Admin sends all helis to pad
				int pos = 0;
				for (Vehicle v : server.getVehicles()) {
					Player driver = v.getDriver();
					if (driver != null) {
						sendPlayerToPad(driver, pos++);
					}
				}

			} else if (player.isDriver()) {
				
				sendPlayerToPad(player, 0);
			}

		} else if (args.length == 1) {

			// Send player with this index to pad
			Player p = server.findPlayer(args[0]);
			if ((p != null) && (p.isDriver())) {
				sendPlayerToPad(p, 0);
			}

		} else {
			Log.error(String.format("Heli2v2Module: Invalid pad args %s", args.toString()));
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
				
				BF2SocketServer.getInstance().send(server.getIpAddress(), cmd);
				Log.write("Sent " + p.name + " to pad \n" + cmd);
				
			} else {
				BF2SocketServer.getInstance().send(server.getIpAddress(), "rcon game.sayall \"No pad found for map \"" + server.getMapName());
			}
			
		} else {
			Log.error(p.name + " has no vehicle!");
		}
	}

}

/*
 * # US Pad if p.getTeam() == 2: self.TeleportPlayer(p, (239.8, 166.0, -249.9))
 * v.setRotation((-56.8, 0.0, 0.0)) v.setDamage(875) self.TeleportPlayer(p,
 * (239.8, 166.0, -249.9))
 * 
 * # CH Pad elif p.getTeam() == 1: self.TeleportPlayer(p, (-222.8, 166.0,
 * 151.0)) v.setRotation((133.5, 0.0, 0.0)) v.setDamage(875)
 * self.TeleportPlayer(p, (-222.8, 166.0, 151.0))
 */