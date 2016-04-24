package net.nihlen.bf2.servers;

import net.nihlen.bf2.BF2WebAdmin;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.io.IOException;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.HashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * The socket server accepting connections from Battlefield 2 servers. 
 * Each new connection will create a BF2SocketClient running on its own thread,
 * sending and receiving messages. Events are handed to the GameServerEventHandler.
 * Based on http://stackoverflow.com/questions/12588476/multithreading-socket-communication-client-server
 * 
 * @author Alex
 */
public class BF2SocketServer implements Runnable {

	private static final Logger log = LogManager.getLogger();

	private static boolean MULTIPLE_CONNECTIONS_PER_IP_ALLOWED = true;

	private ServerSocket serverSocket;
	private final ExecutorService executorService = Executors.newFixedThreadPool(10);

	// Use one or the other eventually
	// private ArrayList<BF2SocketClient> bf2ServerClients = new
	// ArrayList<BF2SocketClient>();
	private final HashMap<String, BF2SocketClient> bf2SocketConnections;

	public BF2SocketServer() {
		bf2SocketConnections = new HashMap<>();
	}

	public void send(String serverId, String message) {
		if (bf2SocketConnections.containsKey(serverId)) {
			Socket clientSocket = bf2SocketConnections.get(serverId).getSocket();
			boolean connOpen = clientSocket.isConnected() && !clientSocket.isClosed();
			if (connOpen) {
				bf2SocketConnections.get(serverId).write(message);
			}
		}
	}

	public String sendRcon(String serverId, String message) {
		if (bf2SocketConnections.containsKey(serverId)) {
			Socket clientSocket = bf2SocketConnections.get(serverId).getSocket();
			boolean connOpen = clientSocket.isConnected() && !clientSocket.isClosed();
			if (connOpen) {
				return bf2SocketConnections.get(serverId).rcon( message);
			}
			return "Server not connected";
		}
		return "Server not found.";
	}

	@Override
	public void run() {
		try {
			runServer();
		} catch (Exception e) {
			log.error("Unhandled exception", e);
		}
	}

	private void runServer() {

		try {

			serverSocket = new ServerSocket(BF2WebAdmin.BF2_SOCKET_SERVER_PORT);
			log.info("Started on port " + BF2WebAdmin.BF2_SOCKET_SERVER_PORT);

			boolean running = true;
			while (running) {

				// log.info("Waiting for request");

				try {

					Socket clientSocket = serverSocket.accept();

					BF2SocketClient client = new BF2SocketClient(clientSocket);

					String serverId = client.getServerId();

					if (MULTIPLE_CONNECTIONS_PER_IP_ALLOWED) {
						bf2SocketConnections.put(client.getServerId() + " - " + client.hashCode(), client);

					} else {
						// bf2ServerClients.add(client);//

						if (bf2SocketConnections.containsKey(serverId)) {
							bf2SocketConnections.get(client.getServerId()).close();
							bf2SocketConnections.remove(serverId);
							log.info("Reconnection from " + serverId);
						}

						bf2SocketConnections.put(client.getServerId(), client);
					}

					executorService.submit(client);

					// Log.info("BF2SocketServer: Processing request");

				} catch (IOException e) {
					log.error("Error accepting connection", e);
				}

				if (serverSocket.isClosed()) {
					running = false;
				}
			}

		} catch (IOException e) {
			log.error("Error starting Server on " + BF2WebAdmin.BF2_SOCKET_SERVER_PORT, e);
		}
	}

	// Call the method when you want to stop your server
	private void stopServer() {

		// Stop the executor service.
		executorService.shutdownNow();

		try {
			// Stop accepting requests.
			serverSocket.close();

		} catch (IOException e) {
			log.error("Error in server shutdown", e);
		}
	}

}
