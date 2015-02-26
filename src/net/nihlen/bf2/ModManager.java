package net.nihlen.bf2;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;

import net.nihlen.bf2.listeners.ChatListener;
import net.nihlen.bf2.listeners.GameStateListener;
import net.nihlen.bf2.listeners.PlayerUpdateListener;
import net.nihlen.bf2.modules.Heli2v2Module;
import net.nihlen.bf2.modules.UrbanDictionaryModule;
import net.nihlen.bf2.modules.WebAdminModule;
import net.nihlen.bf2.objects.ChatEntry;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.GameState;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Vehicle;
import net.nihlen.bf2.util.Log;

public class ModManager {

	private final GameServer server;

	// Modules
	private ArrayList<BF2Module> modules = new ArrayList<BF2Module>();

	// Listeners
	private ArrayList<GameStateListener> gameStateListeners;
	private ArrayList<PlayerUpdateListener> playerUpdateListeners;
	private ArrayList<ChatListener> chatListeners;

	// Commands
	private String cmdPrefix = ".";
	private HashMap<String, Command> commands = new HashMap<String, Command>();
	private ArrayList<Command> cmdHistory = new ArrayList<Command>();

	public ModManager(GameServer server) {
		this.server = server;

		gameStateListeners = new ArrayList<GameStateListener>();
		playerUpdateListeners = new ArrayList<PlayerUpdateListener>();
		chatListeners = new ArrayList<ChatListener>();

		loadModules();
	}

	private void loadModules() {
		modules.add(new WebAdminModule(server));
		modules.add(new UrbanDictionaryModule(server));
		modules.add(new Heli2v2Module(server));

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
				Log.error("Command already exists, " + alias);
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
			Log.write("Processing command - " + cmdType + ", " + Arrays.toString(args) + ", " + entry.player);
			cmd.callback.executeCommand(cmdType, args, entry.player);
		} else {
			Log.error("Unknown command, " + cmdType);
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
		Log.write("ModManager: onGameState(" + state + ")");
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
		if (entry.text.startsWith(cmdPrefix)) {
			processCommand(entry);
		}
		for (ChatListener listener : chatListeners)
			listener.onChatMessage(entry);
	}

}
