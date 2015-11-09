package net.nihlen.bf2.objects;

import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.WebSocketServer;
import net.nihlen.bf2.util.BF2Utils;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;

/**
 * A model for the Battlefield 2 server. Contains players, vehicles and chat messages.
 * 
 * @author Alex
 */
public class GameServer {

	private static final Logger log = LogManager.getLogger();

	public static final int FAST_TIMER_INTERVAL = 500;
	public static final int CHAT_BUFFER_LINES = 100;

	private final ModManager mm;

	private String serverId;
	private String name;
	private final String ipAddress;
	private int gamePort;
	private int queryPort;
	private int rconPort;
	private ArrayList<Map> mapList;
	private String mapName;
	private GameState gameState;
	private int maxPlayers;
	
	private final HashMap<Integer, Player> players;
	private final HashMap<Integer, Vehicle> vehicles;
	private final HashMap<Integer, Projectile> projectiles;

	private final ArrayList<ChatEntry> chatBuffer;
	//private final ChatEntry[] chatBuffer;
	//private int chatBufferPosition;
	
	public GameServer(String IpAddress) {
		this.ipAddress = IpAddress;
		this.rconPort = 4711;
		this.serverId = "NOT SET";
		//this.serverId = ipAddress + ":" + gamePort + ":" + queryPort;

		mapList = new ArrayList<Map>();
		players = new HashMap<>();
		vehicles = new HashMap<>();
		projectiles = new HashMap<>();

		chatBuffer = new ArrayList<ChatEntry>(CHAT_BUFFER_LINES);
		//chatBuffer = new ChatEntry[CHAT_BUFFER_LINES];
		//chatBufferPosition = 0;
		
		mm = new ModManager(this);
	}
	
	public String getServerId() {
		return serverId;
	}
	
	public GameState getGameState() {
		return gameState;
	}
	
	public String getMapName() {
		return mapName;
	}
	
	public void setMapName(String mapName) {
		this.mapName = mapName;
	}
	
	public Collection<Player> getPlayers() {
		return players.values();
	}

	public Collection<Vehicle> getVehicles() {
		return vehicles.values();
	}

	public Collection<Projectile> getProjectiles() {
		return projectiles.values();
	}

	public Map[] getMapList() {
		Map[] mapArr = new Map[mapList.size()];
		mapList.toArray(mapArr);
		return mapArr;
	}

	public ModManager getModManager() {
		return mm;
	}

	public void setInfo(String name, ArrayList<String> mapListStrings, int gamePort, int queryPort, int maxPlayers) {
		this.name = name;
		this.gamePort = gamePort;
		this.queryPort = queryPort;
		this.maxPlayers = maxPlayers;
		//Log.info("Info: " + name + " - " + mapList.toString() + " - " + gamePort + " - " + queryPort + " - " + maxPlayers);
		try {
			for (int i = 0; i < mapListStrings.size(); i++) {
				String[] mapData = mapListStrings.get(i).split("\\|");
				if (mapData.length == 2) {
					Map newMap = new Map(mapData[0].toLowerCase(), i, Integer.parseInt(mapData[1]));
					mapList.add(newMap);
				}
			}
		} catch (Exception e) {
			log.error("Invalid map list.");
		}

		this.serverId = ipAddress + ":" + gamePort + ":" + queryPort;

		// Tell the WebSocketServer of this GameServer (moved from WebSocketClient?)
		WebSocketServer.getInstance().addGameServer(this);
	}

	public synchronized void setGameState(GameState state) {
		gameState = state;
		mm.onGameState(state);
	}
	
	public synchronized void addChatEntry(ChatEntry entry) {
		//chatBuffer[chatBufferPosition++] = entry;
		//if (chatBufferPosition >= chatBuffer.length)
		//	chatBufferPosition = 0;
		if (chatBuffer.size() == CHAT_BUFFER_LINES) {
			chatBuffer.remove(0);
		}
		chatBuffer.add(entry);
		mm.onChatMessage(entry);
	}
	
	public synchronized void addPlayer(int index, String name, int pid, String ip, String hash, int teamId) {
		if (!players.containsKey(index)) {
			Player newPlayer = new Player(index, name, pid, ip, hash, teamId);
			players.put(index, newPlayer);
			mm.onPlayerConnect(newPlayer);
		} else {
			log.error("Player with index {} already exists in this server", index);
		}
	}
	
	public synchronized void removePlayer(int index) {
		if (players.containsKey(index)) {
			Player removedPlayer = players.remove(index);
			mm.onPlayerDisconnect(removedPlayer);
			//removedPlayer.delete();
		} else {
			log.error("Player with index {} was not found", index);
		}
	}
	
	public Player getPlayer(int index) {
		if (players.containsKey(index)) {
			return players.get(index);
		}
		return null;
	}

	public Player findPlayer(String id) {
		
		// Check index
		try {
			Player player = getPlayer(Integer.parseInt(id));
			if (player != null) {
				return player;
			}
		} catch (NumberFormatException e) {
			//Log.error("GameServer: Invalid player index " + id);
		}

		// TODO: Test if the stream works
		// Search by name
		return getPlayers().stream()
				.filter(p -> p.name.toLowerCase().contains(id.toLowerCase()))
				.findFirst()
				.orElseGet(null);

		/*for (Player player : getPlayers())
			if (player.name.toLowerCase().contains(id.toLowerCase()))
				return player;

		return null;*/
	}

	public Player getClosestPlayer(Position position) {
		Player closestPlayer = null;
		double closestDistance = Double.MAX_VALUE;
		for (Player player : getPlayers()) {
			if (closestPlayer == null) {
				closestPlayer = player;
			} else {
				double distance = BF2Utils.getVectorDistance(position, player.position);
				if ((distance < closestDistance) && player.isAlive) {
					closestPlayer = player;
				}
			}
		}
		return closestPlayer;
	}

	public synchronized void updatePlayerPosition(int index, Position pos, Rotation rot) {
		markInactiveProjectiles();
		Player player = players.get(index);
		if (player != null) {
			player.setPosition(pos);
			player.setRotation(rot);
			mm.onPlayerPosition(player, pos, rot);
		}
	}

	public synchronized void updateProjectilePosition(int id, String templateName, Position pos, Rotation rot) {
		Projectile projectile;
		if (!projectiles.containsKey(id)) {
			projectile = new Projectile(id, templateName, pos, rot);
			projectile.owner = getClosestPlayer(projectile.position);
			projectiles.put(projectile.id, projectile);
			mm.onProjectileFired(projectile);
		} else {
			projectile = projectiles.get(id);
			projectile.setPosition(pos);
			projectile.setRotation(rot);
		}
		log.debug("Projectile update: {} {}", projectile.id, projectile.templateName);
		mm.onProjectilePosition(projectile, pos, rot);
	}

	private void markInactiveProjectiles() {
		long currentTime = System.currentTimeMillis();
		for (Projectile projectile : getProjectiles()) {
			Boolean isActive = projectile.active && ((currentTime - projectile.endedTime) > (FAST_TIMER_INTERVAL + 150));
			if (isActive) {
				projectile.active = false;
				//projectile.endedTime += 250;
				mm.onProjectileExploded(projectile);
				projectiles.remove(projectile.id);
			} else if (projectile.active) {
				log.debug("Projectile AliveTimer: {} ms", (currentTime - projectile.endedTime));
			}
		}
	}

	/*public synchronized void updatePlayerScore(int index, int score, int kills, int deaths) {
		Player player = players.get(index);
		if (player != null) {
			player.updateScore(score, kills, deaths);
			mm.onPlayerScore(player);
		}
	}*/

	public synchronized void updatePlayerScore(int index, int score, int teamScore, int kills, int deaths) {
		Player player = players.get(index);
		if (player != null) {
			player.updateScore(score, teamScore, kills, deaths);
			mm.onPlayerScore(player);
		}
	}

	public synchronized void updatePlayerVehicle(int index, int vehicleId, String rootVehicleName, String vehicleName) {
		Player player = players.get(index);
		if (player != null) {
			if (vehicleId == -1) {
				
				// Exit vehicle
				Vehicle v = vehicles.remove(player.rootVehicle.id);
				v.removePlayer(player);
				player.setVehicle(null, vehicleName);
				mm.onPlayerExitVehicle(player, v);
				
			} else {
				
				Vehicle v = vehicles.get(vehicleId);
				if (v != null) {
					
					// Enter existing vehicle
					player.setVehicle(v, vehicleName);
					
				} else {
					
					// Enter new vehicle
					v = new Vehicle(vehicleId, rootVehicleName);
					vehicles.put(vehicleId, v);
					player.setVehicle(v, vehicleName);
				}
				v.addPlayer(player);
				mm.onPlayerEnterVehicle(player, player.rootVehicle, player.subVehicle);
			}
		}
	}

	public void onPlayerSpawn(Player player) {
		player.isAlive = true;
		mm.onPlayerSpawn(player);
	}

	public void onPlayerChangeTeam(Player player, int teamId) {
		// Not handled yet
		player.setTeam(teamId);
		mm.onPlayerChangeTeam(player, teamId);
	}

	public void onPlayerKilled(int attackerIndex, int victimIndex, String weapon) {
		mm.onPlayerKilled(players.get(attackerIndex), players.get(victimIndex), weapon);
	}
	
	public void onPlayerDeath(int index) {
		Player player = players.get(index);
		player.isAlive = false;
		mm.onPlayerDeath(player);
	}
	
}
