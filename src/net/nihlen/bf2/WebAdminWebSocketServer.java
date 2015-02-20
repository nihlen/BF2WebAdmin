package net.nihlen.bf2;

import java.net.InetSocketAddress;
import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;

import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

/**
 * A simple WebSocketServer implementation. Keeps track of a "chatroom".
 */
public class WebAdminWebSocketServer extends WebSocketServer {
	
	//private BF2SocketServer bf2SocketServer;
	private HashMap<String, ArrayList<WebSocket>> webSocketConnections; // <ServerIP, WebSockets> to that server
	
	// Singleton
	private static WebAdminWebSocketServer instance = null;
	
	protected WebAdminWebSocketServer() {
		super(new InetSocketAddress(BF2WebAdmin.WEB_SOCKET_SERVER_PORT));
		//this.bf2SocketServer = BF2SocketServer.getInstance();
		webSocketConnections = new HashMap<String, ArrayList<WebSocket>>();
		System.out.println("WebSocketServer: Starting");
	}

	public static WebAdminWebSocketServer getInstance() {
		if (instance == null) {
			instance = new WebAdminWebSocketServer();
		}
		return instance;
	}

	/*public WebAdminWebSocketServer(int port, BF2SocketServer bf2SocketServer) throws UnknownHostException {
		super(new InetSocketAddress(port));
		this.bf2SocketServer = bf2SocketServer;
		System.out.println("Starting WebSocket Server");
	}

	public WebAdminWebSocketServer(InetSocketAddress address) {
		super(address);
	}*/

	@Override
	public void onOpen(WebSocket conn, ClientHandshake handshake) {
		if (!webSocketConnections.containsKey("127.0.0.1")) {
			webSocketConnections.put("127.0.0.1", new ArrayList<WebSocket>());
		}
		webSocketConnections.get("127.0.0.1").add(conn);
		this.sendToAll("WebSocketServer: New connection: " + handshake.getResourceDescriptor());
		System.out.println(conn.getRemoteSocketAddress().getHostName() + " entered the room!");
	}

	@Override
	public void onClose(WebSocket conn, int code, String reason, boolean remote) {
		// TODO: webSocketConnections remove ...
		this.sendToAll(conn + " has left the room!");
		System.out.println(conn + " has left the room!");
	}

	@Override
	public void onMessage(WebSocket conn, String message) {
		this.sendToAll(message);
		System.out.println(conn.getRemoteSocketAddress().getHostName() + ": " + message);
	}

	@Override
	public void onError(WebSocket conn, Exception ex) {
		ex.printStackTrace();
		if (conn != null) {
			// some errors like port binding failed may not be assignable to a
			// specific websocket
		}
	}

	/**
	 * Sends <var>text</var> to all currently connected WebSocket clients.
	 * 
	 * @param text
	 *            The String to send across the network.
	 * @throws InterruptedException
	 *             When socket related I/O errors occur.
	 */
	public void sendToAll(String text) {
		Collection<WebSocket> con = connections();
		synchronized (con) {
			for (WebSocket c : con) {
				c.send(text);
			}
		}
	}
	
	public void send(String serverId, String message) {
		if (webSocketConnections.containsKey(serverId)) {
			for (WebSocket s : webSocketConnections.get(serverId)) {
				s.send(message);
			}
		}
	}
	
	public ArrayList<WebSocket> getWebSockets(String serverIp) {
		if (!webSocketConnections.containsKey(serverIp))
			return null;
		return webSocketConnections.get(serverIp);
	}
}
