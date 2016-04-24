package net.nihlen.bf2.servers;

import net.nihlen.bf2.BF2WebAdmin;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.objects.GameServer;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.java_websocket.WebSocket;
import org.java_websocket.framing.CloseFrame;
import org.java_websocket.handshake.ClientHandshake;

import javax.inject.Inject;
import java.net.InetSocketAddress;
import java.util.ArrayList;
import java.util.HashMap;


/**
 * The WebSocket server accepting connections from web clients.
 * Based on the simple chat server from java_websocket
 * 
 * @author Alex
 */
public class WebSocketServer extends org.java_websocket.server.WebSocketServer {

	private static final Logger log = LogManager.getLogger();
	private Database database;

	//private BF2SocketServer bf2SocketServer;

	// CURRENTLY CONNECTED =========

	// WebSockets <-> GameServers Lookup (many to many relationship)
	private final HashMap<String, ArrayList<WebSocket>> gameServerWebSockets; // <ServerId, WebSockets> to that server
	private final HashMap<WebSocket, ArrayList<String>> webSocketGameServers; // <WebSocket, ServerIds>

	// ALLOWED CONNECTIONS =========

	// User Key -> GameServers
	private final HashMap<String, ArrayList<String>> userKeyGameServers;

	// WebSocket -> User Key
	private final HashMap<WebSocket, String> webSocketUserKey;

	// LOOKUP ======================

	// Server Id -> GameServer
	private final HashMap<String, GameServer> gameServers;

	@Inject
	public WebSocketServer(Database database) {
		super(new InetSocketAddress(BF2WebAdmin.WEB_SOCKET_SERVER_PORT));
		this.database = database;
		//this.bf2SocketServer = BF2SocketServer.getInstance();

		gameServerWebSockets = new HashMap<>();
		webSocketGameServers = new HashMap<>();
		userKeyGameServers = new HashMap<>();
		webSocketUserKey = new HashMap<>();

		gameServers = new HashMap<>();

		log.info("Started on port {}", BF2WebAdmin.WEB_SOCKET_SERVER_PORT);
	}

	/*public WebAdminWebSocketServer(int port, BF2SocketServer bf2SocketServer) throws UnknownHostException {
		super(new InetSocketAddress(port));
		this.bf2SocketServer = bf2SocketServer;
		System.out.println("Starting WebSocket Server");
	}

	public WebAdminWebSocketServer(InetSocketAddress address) {
		super(address);
	}*/

	public ArrayList<WebSocket> getConnectedWebSockets(String serverId) {
		return gameServerWebSockets.get(serverId);
	}

	public ArrayList<GameServer> getConnectedGameServers(WebSocket webSocket) {
		ArrayList<GameServer> servers = new ArrayList<>();
		for (String serverId : webSocketGameServers.get(webSocket)) {
			servers.add(gameServers.get(serverId));
		}
		return servers;
	}

	public boolean isUserAllowedGameServer(String userKey, String serverId) {
		// TODO: check
		return true;
	}

	public boolean isAuthenticated(WebSocket webSocket) {
		return webSocketUserKey.containsKey(webSocket);
	}

	public void addWebSocket(WebSocket webSocket, String userKey) {

		webSocketUserKey.put(webSocket, userKey);
		webSocketGameServers.put(webSocket, new ArrayList<>());

		if (gameServers.size() > 0) {

			// TODO: WebSocket client tells which serverId he wants to listen to, then authentication happens... or not
			//String selectedServerId = "127.0.0.1";
			// Connect this websocket to ALL gameservers (currently)
			for (GameServer server : gameServers.values()) {

				String selectedServerId = server.getServerId();

				if (!gameServerWebSockets.containsKey(selectedServerId)) {
					gameServerWebSockets.put(selectedServerId, new ArrayList<WebSocket>());
				}

				gameServerWebSockets.get(selectedServerId).add(webSocket);
				webSocketGameServers.get(webSocket).add(selectedServerId);
			}

			for (GameServer gameServer : getConnectedGameServers(webSocket)) {
				gameServer.getModManager().onWebSocketConnect(webSocket);
			}
		}
	}

	public void removeWebSocket(WebSocket webSocket) {
		if (webSocketGameServers.containsKey(webSocket)) {

			for (GameServer gameServer : getConnectedGameServers(webSocket)) {
				// TODO: NullPointerException
				gameServerWebSockets.get(gameServer.getServerId()).remove(webSocket);
				gameServer.getModManager().onWebSocketDisconnect(webSocket);
			}
			/*for (String serverId : webSocketGameServers.get(webSocket)) {
				gameServerWebSockets.get(serverId).remove(webSocket);
				if (gameServers.get(serverId) != null) {
					gameServers.get(serverId).getModManager().onWebSocketDisconnect(webSocket);
				}
			}*/
			webSocketGameServers.remove(webSocket);
		}
		// TODO: Remove from hashmaps?
	}

	public void addGameServer(GameServer server) {
		String serverId = server.getServerId();
		gameServers.put(serverId, server);

		// TODO: temporary ALL
		// Connect this server with ALL websockets (currently)
		// Doesn't seem to work when you have a page open then start the bf2 server
		for (WebSocket conn : connections()) {

			if (!gameServerWebSockets.containsKey(serverId)) {
				gameServerWebSockets.put(serverId, new ArrayList<WebSocket>());
			}

			gameServerWebSockets.get(serverId).add(conn);
			webSocketGameServers.get(conn).add(serverId);

			server.getModManager().onWebSocketConnect(conn);

			log.info("Added new BF2 server {} to WebSocket {}", serverId, conn.getRemoteSocketAddress().getHostName());
		}

	}

	public void removeGameServer(GameServer server) {
		gameServers.remove(server.getServerId());
		// TODO: Remove from hashmaps?
	}

	@Override
	public void onOpen(WebSocket conn, ClientHandshake handshake) {

		String socketKey = handshake.getResourceDescriptor().replace("/?key=", "");
		boolean acceptedKey = database.authorize(socketKey);

		if (acceptedKey) {

			addWebSocket(conn, socketKey);

			//Log.info("Handshake: " + handshake.getResourceDescriptor() + " | " + handshake.getFieldValue("key") + " | " + handshake.getContent() + " | " + handshake.toString());
			//Log.info("Conn: " + conn.getDraft());

			//this.sendToAll("WebSocketServer: New connection: " + handshake.getResourceDescriptor());
			log.info("WebSocket {} connected", conn.getRemoteSocketAddress().getHostName());

		} else {

			// Wrong key, close connection
			conn.close(CloseFrame.NORMAL, "Invalid key.");
			//conn.close(7);
			log.info("WebSocket connection {} refused (Invalid key: {})", conn.getRemoteSocketAddress().getHostName(), socketKey);
		}

	}

	@Override
	public void onClose(WebSocket conn, int code, String reason, boolean remote) {

		removeWebSocket(conn);

		//this.sendToAll(conn + " has left the room!");
		log.info("WebSocket {} disconnected", conn.getRemoteSocketAddress().getHostName());
	}

	@Override
	public void onMessage(WebSocket conn, String message) {
		// sockets with invalid keys can send messages for a short while until it's closed
		// so check if it's in the accepted connections list
		if (isAuthenticated(conn)) {
			for (GameServer gameServer : getConnectedGameServers(conn)) {
				gameServer.getModManager().onWebSocketMessage(conn, message);
			}
			/*for (String serverId : webSocketGameServers.get(conn)) {
				if (gameServers.get(serverId) != null) {
					gameServers.get(serverId).getModManager().onWebSocketMessage(conn, message);
				}
			}*/
		}

		//this.sendToAll(message);
		log.info("{}: {}", conn.getRemoteSocketAddress().getHostName(), message);
	}

	@Override
	public void onError(WebSocket conn, Exception ex) {
		log.error(ex);
		if (conn != null) {
			// some errors like port binding failed may not be assignable to a
			// specific websocket
		}
	}

	public void send(String serverId, String message) {
		log.debug("Sending to {}: {}", serverId, message);
		if (gameServerWebSockets.containsKey(serverId))
			for (WebSocket socket : gameServerWebSockets.get(serverId))
				send(socket, message);
	}

	public void send(WebSocket socket, String message) {
		message = message + "\n";
		if (socket.getReadyState() == WebSocket.READY_STATE_OPEN)
			socket.send(message);
	}

}
