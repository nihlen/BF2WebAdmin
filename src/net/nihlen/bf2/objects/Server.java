package net.nihlen.bf2.objects;

import java.util.HashMap;

public class Server {
	
	public static final int CHAT_BUFFER_LINES = 100;
	
	private String name;
	private String IpAddress;
	private String mapName;
	private GameState gameState;
	
	private HashMap<Integer, Player> players;
	private HashMap<Integer, Vehicle> vehicles;
	
	private ChatEntry[] chatBuffer;
	private int chatBufferPosition;
	
	public Server(String IpAddress) {
		this.IpAddress = IpAddress;
		
		players = new HashMap<Integer, Player>();
		vehicles = new HashMap<Integer, Vehicle>();
		
		chatBuffer = new ChatEntry[CHAT_BUFFER_LINES];
		chatBufferPosition = 0;
	}
	
	public void addChatEntry(ChatEntry entry) {
		chatBuffer[chatBufferPosition++] = entry;
		if (chatBufferPosition >= chatBuffer.length)
			chatBufferPosition = 0;
	}
	
	public void addPlayer(int index, String name, int pid, String ip, String hash, int teamId) {
		if (!players.containsKey(index)) {
			Player newPlayer = new Player(index, name, pid, ip, hash, teamId);
			players.put(index, newPlayer);
		} else {
			System.out.println("Error: player with index " + index + " already exists in this server.");
		}
	}
	
	public void removePlayer(int index) {
		if (players.containsKey(index)) {
			Player removedPlayer = players.remove(index);
			removedPlayer.delete();
		} else {
			System.out.println("Error: player with index " + index + " was not found.");
		}
	}
	
	public void updatePlayerScore(int index, int score, int kills, int deaths) {
		Player p = players.get(index);
		if (p != null) {
			p.updateScore(score, kills, deaths);
		}
	}
	
	public void playerEnterVehicle(int index, int vehicleId, String rootVehicleName, String vehicleName) {
		
	}


}
