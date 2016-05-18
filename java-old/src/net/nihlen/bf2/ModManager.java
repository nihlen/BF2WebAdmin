package net.nihlen.bf2;

import net.nihlen.bf2.listeners.*;
import net.nihlen.bf2.modules.*;
import net.nihlen.bf2.objects.*;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.java_websocket.WebSocket;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;

/**
 * Loads and manages the modules, keeps track of the listeners and chat commands.
 * 
 * @author Alex
 */
public class ModManager {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;

	// Modules
	private final ArrayList<BF2Module> modules;

	// Listeners
	private final ArrayList<GameStateListener> gameStateListeners;
	private final ArrayList<PlayerUpdateListener> playerUpdateListeners;
	private final ArrayList<ProjectileUpdateListener> projectileUpdateListeners;
	private final ArrayList<ChatListener> chatListeners;
	private final ArrayList<WebSocketListener> webSocketListeners;

	// Commands
	private String cmdPrefix = ".";
	private final HashMap<String, Command> commands;
	private final ArrayList<Command> cmdHistory;

	public ModManager(GameServer server) {
		this.server = server;

		modules = new ArrayList<BF2Module>();

		gameStateListeners = new ArrayList<GameStateListener>();
		playerUpdateListeners = new ArrayList<PlayerUpdateListener>();
		projectileUpdateListeners = new ArrayList<ProjectileUpdateListener>();
		chatListeners = new ArrayList<ChatListener>();
		webSocketListeners = new ArrayList<WebSocketListener>();

		commands = new HashMap<String, Command>();
		cmdHistory = new ArrayList<Command>();

		loadModules();
	}

	private void loadModules() {
		modules.add(new BaseModule(server));
		modules.add(new WebAdminModule(server));
		modules.add(new Heli2v2Module(server));
		modules.add(new UrbanDictionaryModule(server));
		modules.add(new MapBuilder(server));

		for (BF2Module module : modules)
			module.load(this);
	}

	// addCommand("spawn|vehicle", "<object> [<player>|<x h y>]", 100, this);
	public void addCommand(String aliasesStr, String argsFormat, int authLevel, CommandExecutor callback) {
		String[] aliases = (aliasesStr.contains("|")) ? aliasesStr.split("|") : new String[] { aliasesStr };
		Command cmd = new Command(aliases, argsFormat, authLevel, callback);
		for (String alias : aliases) {
			if (!commands.containsKey(alias)) {
				commands.put(alias, cmd);
			} else {
				log.error("Command already exists: " + alias);
			}
		}
	}

	private void processCommand(ChatEntry entry) {
		String message = entry.text.replaceFirst(cmdPrefix, "");
		String[] parts = (message + " ").split(" ");
		String cmdType = parts[0];
		if (commands.containsKey(cmdType)) {
			Command cmd = commands.get(cmdType);
			String[] args = (message.contains(" ")) ? message.replaceFirst(cmdType, "").trim().split(" ") : new String[] {};
			log.info("Processing command: {}, {}, {}", cmdType, Arrays.toString(args), entry.player);
			cmd.callback.executeCommand(cmdType, args, entry.player);
		} else {
			log.error("Unknown command: " + cmdType);
		}
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
		log.info("Game state: " + state);
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

	public void onPlayerChangeTeam(Player player, int teamId) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerChangeTeam(player, teamId);
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

	public void onPlayerPosition(Player player, Position pos, Rotation rot) {
		for (PlayerUpdateListener listener : playerUpdateListeners)
			listener.onPlayerPosition(player, pos, rot);
	}

	/*
	 * Projectile update
	 */
	public void addProjectileUpdateListener(ProjectileUpdateListener listener) {
		projectileUpdateListeners.add(listener);
	}

	public void removeProjectileUpdateListener(ProjectileUpdateListener listener) {
		projectileUpdateListeners.remove(listener);
	}

	public void onProjectileFired(Projectile projectile) {
		log.debug("{} Projectile fired", projectile.id);
		for (ProjectileUpdateListener listener : projectileUpdateListeners)
			listener.onProjectileFired(projectile);
	}

	public void onProjectileExploded(Projectile projectile) {
		log.debug("{} Projectile exploded", projectile.id);
		for (ProjectileUpdateListener listener : projectileUpdateListeners)
			listener.onProjectileExploded(projectile);
	}

	public void onProjectilePosition(Projectile projectile, Position pos, Rotation rot) {
		for (ProjectileUpdateListener listener : projectileUpdateListeners)
			listener.onProjectilePosition(projectile, pos, rot);
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
		if (entry.text.startsWith(cmdPrefix))
			processCommand(entry);

		for (ChatListener listener : chatListeners)
			listener.onChatMessage(entry);
	}

	/*
	 * WebSocket
	 */
	public void addWebSocketListener(WebSocketListener listener) {
		webSocketListeners.add(listener);
	}

	public void removeWebSocketListener(WebSocketListener listener) {
		webSocketListeners.remove(listener);
	}

	public void onWebSocketConnect(WebSocket socket) {
		for (WebSocketListener listener : webSocketListeners)
			listener.onWebSocketConnect(socket);
	}

	public void onWebSocketDisconnect(WebSocket socket) {
		for (WebSocketListener listener : webSocketListeners)
			listener.onWebSocketDisconnect(socket);
	}

	public void onWebSocketMessage(WebSocket socket, String message) {
		for (WebSocketListener listener : webSocketListeners)
			listener.onWebSocketMessage(socket, message);
	}


}
