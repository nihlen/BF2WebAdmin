package net.nihlen.bf2.servers;

import net.nihlen.bf2.GameServerEventHandler;
import net.nihlen.bf2.objects.GameServer;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.io.BufferedReader;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.nio.charset.Charset;
import java.util.concurrent.ConcurrentHashMap;

/**
 * A BF2SocketClient holds a socket connection to a Battlefield 2 server.
 * Created by BF2SocketServer.
 *
 * @author Alex
 */
public class BF2SocketClient implements Runnable {

	private static final Logger log = LogManager.getLogger();

	private final Socket clientSocket;

	private final GameServer gameServer;
	private final GameServerEventHandler eventHandler;

	private BufferedReader reader;
	private DataOutputStream writer;

	// Not sure if it's safe to use the reader/info as monitor instead
	//private final Object readerMonitor = new Object();
	//private final Object writerMonitor = new Object();

	private final ConcurrentHashMap<String, Response> responses;

	private final Charset windows1252charset = Charset.forName("Windows-1252");

	public BF2SocketClient(Socket clientSocket) {
		this.clientSocket = clientSocket;

		this.gameServer = new GameServer(clientSocket.getInetAddress().getHostAddress());
		this.eventHandler = new GameServerEventHandler(gameServer);
		this.responses = new ConcurrentHashMap<String, Response>();
		log.info(clientSocket.getInetAddress().getHostAddress() + " connected.");

		// Tell the WebSocketServer of this GameServer (moved to setinfo)
		//WebSocketServer.getInstance().addGameServer(gameServer);
	}

	public String getServerId() {
		return clientSocket.getInetAddress().getHostAddress();
	}

	public Socket getSocket() {
		return clientSocket;
	}

	public GameServer getGameServer() {
		return gameServer;
	}

	// Send a message and let the main loop handle any response
	// Example duration: 2-15 ms
	public synchronized void write(String message) {
		if (writer != null) {
			message = message + "\n";
			try {
				writer.write(message.getBytes(windows1252charset));
				//writer.writeBytes(message);
			} catch (IOException e) {
				log.error(e);
			}
		}
	}

	// Drawbacks of having to send wait message then having to wait for the
	// main read-thread
	// Increased latency and worse performance (synchronized)
	// Python rcon should send "wait" by itself
	// Could potentially receive the wrong message (event)
	// Only use for special occasions, the rest can be written as events
	// Example duration: 30-70 ms
	public synchronized String rcon(String message) {
		Response response = new Response();
		responses.put(response.code, response);

		message = String.format("rconresponse %s %s", response.code, message);
		write(message);

		try {
			synchronized (response.monitor) {
				response.monitor.wait(2000);
			}
		} catch (InterruptedException e) {
			log.error(e);
		}

		/*if (writer != null) {
			try {
				message = String.format("rconresponse %s %s\n", response.code, message);
				writer.info(message.getBytes(windows1252charset));
				//writer.writeBytes(String.format("rconresponse %s %s\n", response.code, message));

				// Wait 2 seconds for a response from the reader
				// Should wait in a loop? (unexpected wake up)
				synchronized (response.monitor) {
					response.monitor.wait(2000);
				}

			} catch (IOException | InterruptedException e) {
				Log.error(this, e);
			}
		}*/

		responses.remove(response.code);

		if (response.text == null) {
			log.error("No rcon response within the specified time.");
			return "";
		}
		// \b used by python so it can be received on one line
		return response.text.replace("\b", "\n");
	}

	public void close() {
		if (!clientSocket.isClosed()) {
			try {
				clientSocket.close();
			} catch (IOException e) {
				log.error("Error closing client connection");
			}
		}
	}

	public void run() {

		Thread.currentThread().setName("BF2SocketClient: " + getServerId());

		// BufferedReader reader = null;
		// DataOutputStream writer = null;

		try {
			reader = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()));
			writer = new DataOutputStream(clientSocket.getOutputStream());
			// writer = new PrintWriter(clientSocket.getOutputStream(),
			// true);

			String line = "";
			while (true) {

				// SocketTimeoutException thrown here if nothing read
				// after
				// 3 seconds
				line = reader.readLine();

				if (line == null)
					break;

				//Log.info("Received: " + line.replace("\t", ","));

				if (line.startsWith("response")) {

					handleResponseMessage(line);

				} else {
					eventHandler.processMessage(line);
				}

			}

		} catch (SocketTimeoutException e) {
			log.info("Connection timed out ({})", getServerId());

		} catch (IOException e) {
			log.info("BF2SocketClient: Connection closed ({})", getServerId());

		} catch (Exception e) {
			log.error(e);

		} finally {
			try {
				if (reader != null)
					reader.close();
				if (writer != null)
					writer.close();
			} catch (IOException e) {
				log.error(e);
			}
		}

		// Tell the WebSocketServer to remove GameServer
		WebSocketServer.getInstance().removeGameServer(gameServer);

		// Make sure to close
		try {
			clientSocket.close();
		} catch (IOException e) {
			log.error("Error closing client connection", e);
		}

		Thread.currentThread().setName("BF2SocketPool");
	}

	private void handleResponseMessage(String line) {
		// Store this response and notify the monitor of the waiting thread
		// Include empty strings
		String[] args = line.split("\t", -1);
		if (args.length == 3) {
			String hash = args[1];
			if (responses.containsKey(hash)) {
				Response response = responses.get(hash);
				response.text = args[2];
				synchronized (response.monitor) {
					response.monitor.notify();
				}
			}
		}
	}

	private class Response {
		public final String code;
		public final Object monitor;
		public String text;
		public Response() {
			code = Integer.toString(hashCode());
			monitor = new Object();
			text = null;
		}
	}
}
