package net.nihlen.bf2;

import java.util.ArrayList;

import net.nihlen.bf2.listeners.ChatListener;
import net.nihlen.bf2.listeners.GameStateListener;
import net.nihlen.bf2.listeners.PlayerUpdateListener;
import net.nihlen.bf2.modules.WebAdminModule;
import net.nihlen.bf2.objects.ChatEntry;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.GameState;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Vehicle;

public class ModManager {
	
	private final GameServer server;
	
	private ArrayList<GameStateListener> gameStateListeners;
	private ArrayList<PlayerUpdateListener> playerUpdateListeners;
	private ArrayList<ChatListener> chatListeners;
	
	public ModManager(GameServer server) {
		this.server = server;
		
		gameStateListeners = new ArrayList<GameStateListener>();
		playerUpdateListeners = new ArrayList<PlayerUpdateListener>();
		chatListeners = new ArrayList<ChatListener>();
		
		loadModules();
	}
	
	public void loadModules() {
		new WebAdminModule(server).load(this);
	}
	

	/*
	 * Game state
	 */
	public void addGameStateListener(GameStateListener listener) {
		gameStateListeners.add(listener);
	}

	public void removeGameStateListener(GameStateListener listener) {
		gameStateListeners.remove(listener);
	}

	public void onGameState(GameState state) {
		for (GameStateListener listener : gameStateListeners)
			listener.onGameState(state);
		System.out.println("ModManager: onGameState(" + state + ")");
	}
	
		
	/*
	 * Player update
	 */
	public void addPlayerUpdateListener(PlayerUpdateListener listener) {
		playerUpdateListeners.add(listener);
	}

	public void removePlayerUpdateListener(PlayerUpdateListener listener) {
		playerUpdateListeners.remove(listener);
	}

	public void onPlayerConnect(Player player) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerConnect(player);
	}
	
	public void onPlayerSpawn(Player player) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerSpawn(player);
	}
	
	public void onPlayerScore(Player player) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerScore(player);
	}
	
	public void onPlayerKilled(Player attacker, Player victim, String weapon) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerKilled(attacker, victim, weapon);
	}
	
	public void onPlayerDeath(Player player) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerDeath(player);
	}
	
	public void onPlayerDisconnect(Player player) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerDisconnect(player);
	}

	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerEnterVehicle(player, vehicle, subVehicle);
	}

	public void onPlayerExitVehicle(Player player, Vehicle vehicle) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerExitVehicle(player, vehicle);
	}

	public void onPlayerPosition(Player player, Position pos) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerPosition(player, pos);
	}

	
	/*
	 * Chat
	 */
	public void addChatListener(ChatListener listener) {
		chatListeners.add(listener);
	}

	public void removeChatListener(ChatListener listener) {
		chatListeners.remove(listener);
	}

	public void onChatMessage(ChatEntry entry) {
		for (ChatListener listener : chatListeners)
			listener.onChatMessage(entry);
	}

}
