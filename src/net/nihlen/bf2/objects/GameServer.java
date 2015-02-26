package net.nihlen.bf2.objects;

import java.util.Collection;
import java.util.HashMap;

import net.nihlen.bf2.ModManager;

public class GameServer {
	
	public static final int CHAT_BUFFER_LINES = 100;
	
	private ModManager mm;
	
	private String name;
	private String IpAddress;
	private String mapName;
	private GameState gameState;
	
	private HashMap<Integer, Player> players;
	private HashMap<Integer, Vehicle> vehicles;
	
	private ChatEntry[] chatBuffer;
	private int chatBufferPosition;
	
	public GameServer(String IpAddress) {
		this.IpAddress = IpAddress;
		
		
		players = new HashMap<Integer, Player>();
		vehicles = new HashMap<Integer, Vehicle>();
		
		chatBuffer = new ChatEntry[CHAT_BUFFER_LINES];
		chatBufferPosition = 0;
		
		mm = new ModManager(this);
	}
	
	public String getIpAddress() {
		return IpAddress;
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
	
	public synchronized void setGameState(GameState state) {
		gameState = state;
		mm.onGameState(state);
	}
	
	public synchronized void addChatEntry(ChatEntry entry) {
		//System.out.println("le gs " + entry.text);
		chatBuffer[chatBufferPosition++] = entry;
		if (chatBufferPosition >= chatBuffer.length)
			chatBufferPosition = 0;
		mm.onChatMessage(entry);
	}
	
	public synchronized void addPlayer(int index, String name, int pid, String ip, String hash, int teamId) {
		if (!players.containsKey(index)) {
			Player newPlayer = new Player(index, name, pid, ip, hash, teamId);
			players.put(index, newPlayer);
			mm.onPlayerConnect(newPlayer);
		} else {
			System.out.println("Error: player with index " + index + " already exists in this server.");
		}
	}
	
	public synchronized void removePlayer(int index) {
		if (players.containsKey(index)) {
			Player removedPlayer = players.remove(index);
			mm.onPlayerDisconnect(removedPlayer);
			//removedPlayer.delete();
		} else {
			System.out.println("Error: player with index " + index + " was not found.");
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
			Player p = getPlayer(Integer.parseInt(id));
			if (p != null) {
				return p;
			}
		} catch (NumberFormatException e) {
		}
		
		// Search by name
		for (Player p : getPlayers()) {
			if (p.name.toLowerCase().contains(id.toLowerCase())) {
				return p;
			}
		}
		
		return null;
	}


	public synchronized void updatePlayerPosition(int index, Position pos) {
		Player p = players.get(index);
		if (p != null) {
			p.setPosition(pos);
			mm.onPlayerPosition(p, pos);
		}
	}
	
	public synchronized void updatePlayerScore(int index, int score, int kills, int deaths) {
		Player p = players.get(index);
		if (p != null) {
			p.updateScore(score, kills, deaths);
			mm.onPlayerScore(p);
		}
	}
	
	public synchronized void updatePlayerVehicle(int index, int vehicleId, String rootVehicleName, String vehicleName) {
		Player p = players.get(index);
		if (p != null) {
			if (vehicleId == -1) {
				
				// Exit vehicle
				Vehicle v = vehicles.remove(p.rootVehicle.id);
				v.removePlayer(p);
				p.setVehicle(null, vehicleName);
				mm.onPlayerExitVehicle(p, v);
				
			} else {
				
				Vehicle v = vehicles.get(vehicleId);
				if (v != null) {
					
					// Enter existing vehicle
					p.setVehicle(v, vehicleName);
					
				} else {
					
					// Enter new vehicle
					v = new Vehicle(vehicleId, rootVehicleName);
					vehicles.put(vehicleId, v);
					p.setVehicle(v, vehicleName);
				}
				v.addPlayer(p);
				mm.onPlayerEnterVehicle(p, p.rootVehicle, p.subVehicle);
			}
		}
	}

	public void onPlayerSpawn(Player p) {
		mm.onPlayerSpawn(p);
	}

	public void onPlayerChangeTeam(Player p, int teamId) {
		// Not handled yet
	}

	public void onPlayerKilled(int attackerIndex, int victimIndex, String weapon) {
		mm.onPlayerKilled(players.get(attackerIndex), players.get(victimIndex), weapon);
	}
	
	public void onPlayerDeath(int index) {
		mm.onPlayerDeath(players.get(index));
	}
	
}
