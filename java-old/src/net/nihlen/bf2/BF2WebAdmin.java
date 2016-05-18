package net.nihlen.bf2;

import net.nihlen.bf2.dependency.BF2WebAdminComponent;
import net.nihlen.bf2.dependency.DaggerBF2WebAdminComponent;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.servers.WebSocketServer;
import net.nihlen.bf2.util.BF2Rcon;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import javax.inject.Inject;
import java.io.BufferedReader;
import java.io.InputStreamReader;

/**
 * The main class which starts BF2SocketServer and WebSocketServer 
 * in separate threads. It then sends connection requests using
 * BF2Rcon.
 * 
 * Also listens to input from System.in, which is sent as RCon commands.
 * (Only supports one server at the moment)
 * 
 * @author Alex
 */
public class BF2WebAdmin {

	private static final Logger log = LogManager.getLogger();

	public static final int BF2_SOCKET_SERVER_PORT = 4300;
	public static final int WEB_SOCKET_SERVER_PORT = 4301;

	private final BF2SocketServer bf2SocketServer;
	private final WebSocketServer webSocketServer;

	@Inject
	public BF2WebAdmin(BF2SocketServer bf2SocketServer, WebSocketServer webSocketServer) {
		this.bf2SocketServer = bf2SocketServer;
		this.webSocketServer = webSocketServer;
	}

	public BF2SocketServer getBF2SocketServer() {
		return bf2SocketServer;
	}

	public WebSocketServer getWebSocketServer() {
		return webSocketServer;
	}

	public static void main(String[] args) {

		try {

			Thread.currentThread().setName("BF2WebAdmin Main");

			//BF2WebAdmin bf2WebAdmin = BF2WebAdminComponent.create();
			BF2WebAdminComponent bf2WebAdmin = DaggerBF2WebAdminComponent.create();
			bf2WebAdmin.bf2SocketServer();

			// BF2 Socket Server accepts one socket connection per BF2 Server
			BF2SocketServer bf2Socketserver = BF2SocketServer.getInstance();
			Thread bf2SocketServerThread = new Thread(bf2Socketserver);
			bf2SocketServerThread.setName("BF2SocketServer Thread");
			bf2SocketServerThread.start();

			// Web Socket Server accepts multiple WebSocket connections (each assigned to one BF2 Server)
			WebSocketServer webSocketServer = WebSocketServer.getInstance();
			Thread webSocketServerThread = new Thread(webSocketServer);
			webSocketServerThread.setName("WebSocketServer Thread");
			webSocketServerThread.start();

			// TODO: Wait for the BF2SocketServer to be initialized
			Thread.sleep(2000);
			BF2WebAdmin.requestConnections();

			// Listen to commands
			InputStreamReader streamReader = new InputStreamReader(System.in);
			BufferedReader bufferedReader = new BufferedReader(streamReader);
			String input = "";
			while (!input.equals("exit")) {

				if (input.length() > 0) {
					log.debug("Read: {}", input);
					String response = BF2SocketServer.getInstance().sendRcon("127.0.0.1", input);
					log.debug("RCON Response: {}", response);
				}
				input = bufferedReader.readLine();

			}

			log.info("Exiting due to request");

		} catch (Exception e) {
			log.fatal("Unhandled exception", e);
			System.exit(0);
		}
		
		System.exit(1);
	}

	private static void requestConnections() {

		// TODO: config file?
		requestServerConnection("127.0.0.1", 4711, "sickrconpw");
		requestServerConnection("127.0.0.1", 4712, "sickrconpw");
	}

	// Send reconnect requests to the BF2 servers via rcon
	// This is currently the only usage of BF2Rcon
	private static void requestServerConnection(String ipAddress, int rconPort, String password) {

		String command = "wa connect";
		try {
			String response = BF2Rcon.send(ipAddress, rconPort, password, command);
			log.info("Reconnect response: " + response);
		} catch (Exception e) {
			log.error(e);
		}
	}

}
