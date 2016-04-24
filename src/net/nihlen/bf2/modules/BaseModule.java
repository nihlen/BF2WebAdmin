package net.nihlen.bf2.modules;

import com.owlike.genson.Genson;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.CommandExecutor;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.listeners.GameStateListener;
import net.nihlen.bf2.listeners.PlayerUpdateListener;
import net.nihlen.bf2.objects.*;
import net.nihlen.bf2.util.WebUtils;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import javax.inject.Inject;
import java.text.DateFormat;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.HashMap;
import java.util.TimeZone;

/**
 * The base module. Creates the common commands, gets GeoIP information 
 * and sets starting values for soldier objects.
 * GeoIP information from the Telize API (GeoLite by MaxMind).
 * 
 * @author Alex
 */
public class BaseModule implements BF2Module, CommandExecutor, GameStateListener, PlayerUpdateListener {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;
	private final Database database;

	private final HashMap<Player, GeoipResult> geoipPlayers;
	private final HashMap<Player, Boolean> welcomedPlayers;
	private final HashMap<String, Position> checkpoints;

	private boolean switchNext = false;

	@Inject
	public BaseModule(GameServer server, Database database) {
		this.server = server;
		this.database = database;
		geoipPlayers = new HashMap<Player, GeoipResult>();
		welcomedPlayers = new HashMap<Player, Boolean>();
		checkpoints = new HashMap<String, Position>();
	}

	public void load(ModManager mm) {

		// Commands
		mm.addCommand("rank", "<player> <ranknum>", 0, this);
		mm.addCommand("kill", "<player>", 0, this);
		mm.addCommand("time", "[<timezone>]", 0, this);
		mm.addCommand("tp", "<player> <player>|<checkpoint>|<x h y>", 0, this);
		mm.addCommand("pos", "[<player>|<checkpoint>]", 0, this);
		mm.addCommand("g", "[<gravity>]", 0, this);
		mm.addCommand("repair", "[<player>]", 0, this);
		mm.addCommand("score", "<player> [<totalscore>] <teamscore> <kills> <deaths>", 0, this);
		mm.addCommand("sw", "[<player>]", 0, this);
		mm.addCommand("noclip", "<player>", 0, this);
		mm.addCommand("freeze", "<player>", 0, this);
		mm.addCommand("restart", "", 0, this);
		mm.addCommand("map", "<mapname>", 0, this);
		mm.addCommand("whois", "<player>", 0, this);

		// Events
		mm.addGameStateListener(this);
		mm.addPlayerUpdateListener(this);
	}

	public void executeCommand(String command, String[] args, Player sender) {
		switch (command) {

			case "rank":
				executeRankCommand(args, sender);
				break;

			case "kill":
				executeKillCommand(args, sender);
				break;

			case "time":
				executeTimeCommand(args, sender);
				break;

			case "tp":
				executeTeleportCommand(args, sender);
				break;

			case "pos":
				executePositionCommand(args, sender);
				break;

			case "g":
				executeGravityCommand(args, sender);
				break;

			case "repair":
				executeRepairCommand(args, sender);
				break;

			case "score":
				executeScoreCommand(args, sender);
				break;

			case "sw":
				executeSwitchCommand(args, sender);
				break;

			case "noclip":
				executeNoclipCommand(args, sender);
				break;

			case "freeze":
				executeFreezeCommand(args, sender);
				break;

			case "restart":
				executeRestartCommand(args, sender);
				break;

			case "map":
				executeMapCommand(args, sender);
				break;

			case "whois":
				executeWhoisCommand(args, sender);
				break;

			default:
				log.error("Unknown command: {}", command);
				break;

		}
	}

	private void executeRankCommand(String[] args, Player sender) {
		try {
			if (args.length == 2) {
				Player player = server.findPlayer(args[0]);
				if (player != null) {
					int rankNum = Integer.parseInt(args[1]);
					String cmd = String.format("rank %s %s 1", player.index, rankNum);
					BF2SocketServer.getInstance().send(server.getServerId(), cmd);
					log.info("Set rank of {} to {}", player.name, rankNum);
				}
			}
		} catch (NumberFormatException e) {
			log.error("Invalid rank number: {}", args[1]);
		}
	}

	private void executeKillCommand(String[] args, Player sender) {
		if (args.length == 1) {
			Player player = server.findPlayer(args[0]);
			if (player != null) {
				killPlayer(player);
				log.info("Killed player {}", player.name);
			}
		}
	}

	private void executeTimeCommand(String[] args, Player sender) {
		GeoipResult geoip = geoipPlayers.get(sender);
		Date date = new Date();
		DateFormat dateFormat = new SimpleDateFormat("HH:mm");

		String cmd = "";
		if (geoip.timezone != null) {
			TimeZone tz = TimeZone.getTimeZone(geoip.timezone);
			dateFormat.setTimeZone(tz);
			cmd = String.format("rcon game.sayall \"§C1001 Time:§C1001 %s (%s)\"", dateFormat.format(date), geoip.timezone);
		} else {
			TimeZone tz = TimeZone.getTimeZone("Europe/Stockholm");
			dateFormat.setTimeZone(tz);
			cmd = String.format("rcon game.sayall \"§C1001 Time:§C1001 %s (Europe/Stockholm)\"", dateFormat.format(date));
		}
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	private void executeTeleportCommand(String[] args, Player sender) {
		try {
			
			Player player = server.findPlayer(args[0]);
			if (player != null) {
				if (args.length == 2) {
					
					// Checkpoint
					if (checkpoints.containsKey(args[1])) {
						Position pos = checkpoints.get(args[1]);
						teleportPlayer(player, pos);
					}
					
					// Player
					Player targetPlayer = server.findPlayer(args[0]);
					if (targetPlayer != null) {
						teleportPlayer(player, targetPlayer.position);
					}
					
				} else if (args.length == 4) {
					
					// Coordinates
					Position pos = new Position(Double.parseDouble(args[1]), Double.parseDouble(args[2]), Double.parseDouble(args[3]));
					teleportPlayer(player, pos);
					
				} else {
					log.error("Invalid teleport argument count: {}", args.length);
				}
			}
			
		} catch (Exception e) {
			log.error("Invalid teleport arguments");
		}
	}

	private void executePositionCommand(String[] args, Player sender) {
		if (args.length == 0) {
			String cmd = String.format("rcon game.sayall \"§C1001Position:§C1001 %s Pos:[%s] Rot:[%s]\"", sender.name.trim(), sender.position, sender.rotation);
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			
		} else if (args.length == 1) {
			Player player = server.findPlayer(args[0]);
			if (player != null) {
				
				// Show this player's position
				String cmd = String.format("rcon game.sayall \"§C1001Position:§C1001 %s Pos:[%s] Rot:[%s]\"", player.name.trim(), player.position, player.rotation);
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			} else {
				
				// Create new checkpoint
				checkpoints.put(args[0], sender.position);
				String cmd = String.format("rcon game.sayall \"Created checkpoint %s\"", args[0]);
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			}
			
		} else {
			log.error("Invalid position argument count: {}", args.length);
		}
	}
	
	private void executeGravityCommand(String[] args, Player sender) {
		
		if (args.length == 0) {
			
			// Default gravity
			String cmd = "rcon physics.gravity -14.73\n";
			cmd += "rcon game.sayall \"§C1001Gravity:§C1001 -14.73 (Default)\"";
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			
		} else if (args.length == 1) {
			
			try {
				// Set gravity
				String cmd = String.format("rcon physics.gravity %s\n", Double.parseDouble(args[0]));
				cmd += String.format("rcon game.sayall \"§C1001Gravity:§C1001 %s\"", args[0]);
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			} catch (NumberFormatException e) {
				log.error("Invalid gravity double: {}", args[0]);
			}
			
		} else {
			log.error("Invalid gravity argument count: {}", args.length);
		}
		
	}
	
	private void executeRepairCommand(String[] args, Player sender) {
		
		if (args.length == 0) {
			
			String cmd = String.format("damage %s 5000", sender.index);
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			
		} else if (args.length == 1) {
			
			Player player = server.findPlayer(args[0]);
			if (player != null) {
				String cmd = String.format("damage %s 5000", player.index);
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			}
		} else {
			log.error("Invalid repair argument count: {}", args.length);
		}
	}
	
	private void executeScoreCommand(String[] args, Player sender) {
		
		// Reset score
		if ((args.length == 1) && args[0].equals("reset")) {
			for (Player currentPlayer : server.getPlayers()) {
				setPlayerScore(currentPlayer, 0, 0, 0, 0);
			}
			return;
		}
			
		Player player = server.findPlayer(args[0]);
		if (player != null) {
			
			if (args.length == 4) {
				int teamScore = Integer.parseInt(args[1]);
				int kills = Integer.parseInt(args[2]);
				int deaths = Integer.parseInt(args[3]);
				int totalScore = teamScore + 2 * kills;
				setPlayerScore(player, totalScore, teamScore, kills, deaths);
				
			} else if (args.length == 5) {
				int totalScore = Integer.parseInt(args[1]);
				int teamScore = Integer.parseInt(args[2]);
				int kills = Integer.parseInt(args[3]);
				int deaths = Integer.parseInt(args[4]);
				setPlayerScore(player, totalScore, teamScore, kills, deaths);
			}
		}
	}

	private void executeSwitchCommand(String[] args, Player sender) {

		if (args.length == 0) {
			switchAllPlayers();

		} else if (args.length >= 1) {
			if (args[0].equals("next")) {
				switchNext = true;

			} else {
				Player player = server.findPlayer(args[0]);
				if (player != null) {
					int newTeam = (player.teamId == 1) ? 2 : 1;
					if (args.length == 2) {
						newTeam = (args[1].equals("us")) ? 2 : 1;
					}
					setPlayerTeam(player, newTeam);
				}
			}
		}
	}

	private void executeNoclipCommand(String[] args, Player sender) {

		if (args.length == 1) {

			Player player = server.findPlayer(args[0]);
			if (player != null) {
				noclipPlayer(player, true);
			}
		} else if (args.length == 2) {

			Player player = server.findPlayer(args[0]);
			if (player != null) {
				boolean setNoclip = args[1].equals("1");
				noclipPlayer(player, setNoclip);
			}
		}
	}

	private void executeFreezeCommand(String[] args, Player sender) {
		// TODO: Make freeze work with multiple players, deaths, positions ...
		if (args.length == 1) {

			Player player = server.findPlayer(args[0]);
			if (player != null) {

				(new Thread() {
					public void run() {
						String cmd = String.format("position %s %s %s %s", player.index, player.position.x, player.position.height, player.position.y);
						while (!Thread.currentThread().isInterrupted()) {
							try {
								Thread.sleep(250);
								BF2SocketServer.getInstance().send(server.getServerId(), cmd);
							} catch (InterruptedException e) {
							}
						}
					}
				}).start();
			}
		}
	}

	private void executeRestartCommand(String[] args, Player sender) {
		String cmd = "rcon admin.restartMap";
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	private void executeMapCommand(String[] args, Player sender) {

		Map[] mapList = server.getMapList();
		String[] mapNames = new String[mapList.length];
		for (int i = 0; i < mapNames.length; i++) {
			mapNames[i] = mapList[i].name;
		}

		if (args.length == 1) {

			if (args[0].equals("list")) {
				String cmd = String.format("rcon game.sayall \"Maps: %s\"", String.join(", ", mapNames));
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);

			} else {

				String wantedMap = args[0];
				ArrayList<Map> matchingMaps = new ArrayList<Map>();
				for (Map map : mapList) {
					if (map.name.contains(wantedMap)) {
						matchingMaps.add(map);
					}
				}
				if (matchingMaps.size() == 1) {
					Map nextMap = matchingMaps.get(0);
					String cmd = String.format("rcon game.sayall \"§C1001§3Changing map to %s§C1001§3\"", nextMap.name);
					cmd += "\nrcon admin.nextLevel " + nextMap.index;
					cmd += "\nrcon admin.runNextLevel";
					BF2SocketServer.getInstance().send(server.getServerId(), cmd);
					log.info("Changing map {}", nextMap.name);

				} else if (matchingMaps.size() > 1) {
					String cmd = "rcon game.sayall \"" + matchingMaps.size() + " matching maps: (" + String.join(", ", mapNames) + ")\"";
					BF2SocketServer.getInstance().send(server.getServerId(), cmd);
					log.info("Too many maps {}", String.join(", ", (String[]) matchingMaps.toArray()));
				}
			}

		} else {
			String cmd = String.format("rcon game.sayall \"Current map: %s\"", server.getMapName());
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);
		}


		/*if mapinmaplist:
 			host.rcon_invoke("admin.nextLevel " + str(result.index((shortmaps[mapvars[0]],mapvars[1]))))
 			host.rcon_invoke("admin.runNextLevel")
 			ctx.info('Map in maplist - Changing map to ' + shortmaps[mapvars[0]] + ' - ' + mapvars[1] + ' players\n')
 		else:
 			ctx.info('Changing map to ' + shortmaps[mapvars[0]] + ' - ' + mapvars[1] + ' players\n')
 			host.rcon_invoke('mapList.append ' + shortmaps[mapvars[0]] + ' gpm_cq ' + mapvars[1])
 			host.rcon_invoke("game.sayAll \"Changing map to " + shortmaps[mapvars[0]] + " - " + mapvars[1] + " players\"")
 			host.rcon_invoke("admin.nextLevel " + str(len(result)))
 			host.rcon_invoke("admin.runNextLevel")*/
	}

	private void executeWhoisCommand(String[] args, Player sender) {
		if (args.length == 1) {
			Player player = server.findPlayer(args[0]);
			if (player != null) {
				String[] namesArr = database.getTopPlayerMatch(player.ipAddress);
				String names = String.join(", ", namesArr);
				String cmd = String.format("rcon game.sayall \"§C1001Whois:§C1001 %s\"", names);
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			}
		}
	}

	private void teleportPlayer(Player player, Position pos) {
		noclipPlayer(player, true);
		String cmd = String.format("position %s %s %s %s", player.index, pos.x, pos.height, pos.y);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
		noclipPlayer(player, false);
	}
	
	private void noclipPlayer(Player player, boolean noclip) {
		String cmd = String.format("rcon object.active id%s\n", player.rootVehicle.id);
		cmd += (noclip) ? "rcon object.hasCollision 0" : "rcon object.hasCollision 1" ;
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}
	
	private void setPlayerScore(Player player, int totalScore, int teamScore, int kills, int deaths) {
		String cmd = String.format("score %s %s %s %s %s", player.index, totalScore, teamScore, kills, deaths);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	private void setPlayerTeam(Player player, int teamId) {
		String cmd = String.format("team %s %s", player.index, teamId);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	private void killPlayer(Player player) {
		String cmd = String.format("damage %s 1", player.index);
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	private void switchAllPlayers() {
		for (Player currentPlayer : server.getPlayers()) {
			int newTeam = (currentPlayer.teamId == 1) ? 2 : 1;
			setPlayerTeam(currentPlayer, newTeam);
		}
		String cmd = "rcon game.sayall \"§C1001§3SWITCHED TEAMS\"";
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	public void onGameState(GameState state) {
		if (state == GameState.PLAYING) {
			applySoldierMods();
		}
	}

	private void applySoldierMods() {

		// Fix stamina and bleed for kill commands
		String cmd = "rcon ObjectTemplate.active us_heavy_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.active us_light_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.active ch_heavy_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.active ch_light_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon GeometryTemplate.active mec_heavy_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.activeSafe Soldier mec_light_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.active eu_heavy_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1\n";
		
		cmd += "rcon ObjectTemplate.active eu_soldier\n";
		cmd += "rcon ObjectTemplate.armor.hpLostWhilecriticalDamage 10\n";
		cmd += "rcon ObjectTemplate.armor.criticalDamage 1\n";
		cmd += "rcon ObjectTemplate.SprintRecoverTime 0.1\n";
		cmd += "rcon ObjectTemplate.SprintDissipationTime 9000\n";
		cmd += "rcon ObjectTemplate.SprintLimit 0\n";
		cmd += "rcon ObjectTemplate.SprintLossAtJump 0.1";
		
		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
	}

	public void onPlayerConnect(Player player) {
		if (!geoipPlayers.containsKey(player)) {
			GeoipResult result = BaseModule.getGeoipInfo(player.ipAddress);
			geoipPlayers.put(player, result);
			player.country = result.country;
			player.countryCode = result.country_code;
		}
	}

	public void onPlayerSpawn(Player player) {
		if (!welcomedPlayers.containsKey(player)) {
			GeoipResult result = geoipPlayers.get(player);
			String country = (result.code == 0) ? result.country : "The Moon";
			String joinMsg = String.format("%s connected from %s", player.name.replace("\"", "").replace("?C1001", "").replace("?3", "").trim(), country);
			String cmd = String.format("rcon game.sayall \"%s\"", joinMsg);
			BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			welcomedPlayers.put(player, true);
		}
	}

	public void onPlayerScore(Player player) {
	}

	public void onPlayerKilled(Player attacker, Player victim, String weapon) {
	}

	public void onPlayerDeath(Player player) {
		if (switchNext) {
			switchAllPlayers();
			switchNext = false;
		}
	}

	public void onPlayerChangeTeam(Player player, int teamId) {
	}

	public void onPlayerDisconnect(Player player) {
	}

	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle) {
	}

	public void onPlayerExitVehicle(Player player, Vehicle vehicle) {
	}

	public void onPlayerPosition(Player player, Position pos, Rotation rot) {
	}

	public void onPlayerPosition(Player player, Position pos) {
	}

	private static GeoipResult getGeoipInfo(String ipAddress) {
		try {
			// TODO: change to actual ipAddress
			String content = WebUtils.getWebPageContents("http://www.telize.com/geoip/81.229.166.30");
			Genson genson = new Genson();
			GeoipResult result = genson.deserialize(content, GeoipResult.class);
			return result;
		} catch (Exception e) {
			log.error(e);
		}
		return null;
	}

	static class GeoipResult {

		public int code; // Only set when invalid? (401)
		public String message; // "Input string is not a valid IP address"

		public double longitude;
		public double latitude;
		public String ip;
		public String continent_code;
		public String city;
		public String timezone;
		public String region;
		public String country_code;
		public String isp;
		public String country;
		public String country_code3;
		public String region_code;

		public GeoipResult() {
		}

	}

}
