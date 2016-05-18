package net.nihlen.bf2.modules;

import com.owlike.genson.Genson;
import com.owlike.genson.GensonBuilder;
import com.owlike.genson.reflect.VisibilityFilter;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.servers.WebSocketServer;
import net.nihlen.bf2.listeners.*;
import net.nihlen.bf2.objects.*;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.java_websocket.WebSocket;

/**
 * The WebAdmin module which is required in order to serve WebSocket clients
 * with data from the Battlefield 2 server.
 * 
 * @author Alex
 */
public class WebAdminModule implements BF2Module,
		WebSocketListener,
		GameStateListener,
		ChatListener,
		PlayerUpdateListener,
		ProjectileUpdateListener {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;
	private final Genson genson;
	
	public WebAdminModule(GameServer server) {
		this.server = server;
		this.genson = configureGenson();
	}

	public void load(ModManager mm) {
		mm.addWebSocketListener(this);
		mm.addGameStateListener(this);
		mm.addChatListener(this);
		mm.addPlayerUpdateListener(this);
	}

	private Genson configureGenson() {
		return new GensonBuilder()
				.useFields(true, VisibilityFilter.ALL)
				.useMethods(false)
						// GameServer
				.exclude("CHAT_BUFFER_LINES", GameServer.class)
				.exclude("FAST_TIMER_INTERVAL", GameServer.class)
				.exclude("mm", GameServer.class)
				.rename("serverId", GameServer.class, "id")
				.rename("ipAddress", GameServer.class, "ip")
				.rename("rconPort", GameServer.class, "rcon_port")
				.rename("mapName", GameServer.class, "map")
				.rename("mapList", GameServer.class, "maplist")
				.rename("gameState", GameServer.class, "gamestate")
						//.exclude("chatBuffer", GameServer.class)
						//.exclude("chatBufferPosition", GameServer.class)
						// Player
				.rename("ipAddress", Player.class, "ip")
				.rename("countryCode", Player.class, "country_code")
				.rename("hash", Player.class, "key")
				.rename("rootVehicle", Player.class, "vehicle")
				.rename("subVehicle", Player.class, "sub_vehicle")
				.rename("teamId", Player.class, "team_id")
				.rename("isAlive", Player.class, "alive")
				.rename("totalScore", Player.class, "total_score")
				.rename("teamScore", Player.class, "team_score")
				.exclude("authLevel", Player.class)
						// Vehicle
				.rename("templateName", Vehicle.class, "template_name")
				.exclude("players", Vehicle.class)
				.create();
	}

	/*private void sendAll(String msg) {
		ArrayList<WebSocket> webSockets = WebSocketServer.getInstance().getWebSockets(server.getServerId());
		if (webSockets != null) {
			for (WebSocket s : webSockets) {
				s.send(msg);
			}
		}
	}*/

	private void sendAllServerData() {
		JsonMessage jsonObj = new JsonMessage(server);
		jsonObj.status = "success";
		jsonObj.message = "";
		//jsonObj.data.server = server;
		//jsonObj.data.players = server.getPlayers();
		//jsonObj.data.vehicles = server.getVehicles();
		//jsonObj.data.projectiles = server.getProjectiles();
		String json = genson.serialize(jsonObj);
		WebSocketServer.getInstance().send(server.getServerId(), json);
	}

	// Send data to a specific websocket only
	private void sendSocketServerData(WebSocket socket) {
		JsonMessage jsonObj = new JsonMessage(server);
		jsonObj.status = "success";
		jsonObj.message = "";
		String json = genson.serialize(jsonObj);
		WebSocketServer.getInstance().send(socket, json);
	}

	private void sendEventData(JsonMessage message) {
		String json = genson.serialize(message);
		WebSocketServer.getInstance().send(server.getServerId(), json);
	}

	/*
	 * WebSocket
	 */
	public void onWebSocketConnect(WebSocket socket) {
		sendSocketServerData(socket);
		log.info("WebSocket connected: {}", socket);
	}

	public void onWebSocketDisconnect(WebSocket socket) {
		log.info("WebSocket disconnected: {}", socket);
	}

	public void onWebSocketMessage(WebSocket socket, String message) {
		// TODO: Handle key checks here or in WebSocketServer?
	}

	/*
	 * Game state
	 */
	public void onGameState(GameState state) {
		sendAllServerData();
	}

	/*
	 * Chat
	 */
	public void onChatMessage(ChatEntry entry) {
		//sendAll(entry.text + "<br/>\n");
		//sendServerData();
		JsonMessage message = new JsonMessage(server.getServerId(), "chatmessage", entry);
		sendEventData(message);
	}

	/*
	 * Player
	 */
	public void onPlayerConnect(Player player) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerconnect", player);
		sendEventData(message);
	}

	public void onPlayerSpawn(Player player) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerspawn", player);
		sendEventData(message);
	}

	public void onPlayerScore(Player player) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerscore", player);
		sendEventData(message);
	}

	public void onPlayerKilled(Player attacker, Player victim, String weapon) {
	}

	public void onPlayerDeath(Player player) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerdeath", player);
		sendEventData(message);
	}

	public void onPlayerChangeTeam(Player player, int teamId) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerteam", player);
		sendEventData(message);
	}

	public void onPlayerDisconnect(Player player) {
		JsonMessage message = new JsonMessage(server.getServerId(), "playerdisconnect", player);
		sendEventData(message);
	}

	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle) {
	}

	public void onPlayerExitVehicle(Player player, Vehicle vehicle) {
	}

	public void onPlayerPosition(Player player, Position pos, Rotation rot) {
	}

	/*
	 * Player
	 */
	public void onProjectileFired(Projectile projectile) {
	}

	public void onProjectileExploded(Projectile projectile) {
	}

	public void onProjectilePosition(Projectile projectile, Position pos, Rotation rot) {
	}

}
