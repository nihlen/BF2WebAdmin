package net.nihlen.bf2.objects;

import java.util.HashMap;

public class Vehicle {
	
	public final int id;
	public final String templateName;
	
	public Position position;
	public HashMap<Integer, Player> players;
	
	public Vehicle(int id, String templateName) {
		this.id = id;
		this.templateName = templateName;
		this.players = new HashMap<Integer, Player>();
	}
	
	public synchronized void addPlayer(Player player) {
		if (!players.containsKey(player.index)) {
			players.put(player.index, player);
		}
	}

	public synchronized void removePlayer(Player player) {
		if (players.containsKey(player.index)) {
			players.remove(player.index);
		}
	}

}
