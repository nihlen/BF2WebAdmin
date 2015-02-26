package net.nihlen.bf2;

import java.io.BufferedReader;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.util.HashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.util.Log;

// http://stackoverflow.com/questions/12588476/multithreading-socket-communication-client-server
public class BF2SocketServer implements Runnable {

	private ServerSocket serverSocket;
	private ExecutorService executorService = Executors.newFixedThreadPool(10);

	// Use one or the other eventually
	//private ArrayList<BF2SocketClient> bf2ServerClients = new ArrayList<BF2SocketClient>();
	private HashMap<String, BF2SocketClient> bf2SocketConnections;

	// Singleton
	private static BF2SocketServer instance = null;
	
	protected BF2SocketServer() {
		bf2SocketConnections = new HashMap<String, BF2SocketClient>();
	}

	public static BF2SocketServer getInstance() {
		if (instance == null) {
			instance = new BF2SocketServer();
		}
		return instance;
	}
	
	public void send(String serverId, String message) {
		if (bf2SocketConnections.containsKey(serverId)) {
			if (bf2SocketConnections.get(serverId).clientSocket.isConnected() && !bf2SocketConnections.get(serverId).clientSocket.isClosed()) {
				bf2SocketConnections.get(serverId).write(message);
			}
		}
	}

	@Override
	public void run() {
		runServer();
	}
	
	private void runServer() {

		try {

			serverSocket = new ServerSocket(BF2WebAdmin.BF2_SOCKET_SERVER_PORT);
			Log.write("BF2SocketServer: Started on port " + BF2WebAdmin.BF2_SOCKET_SERVER_PORT);

			while (true) {

				//Log.write("BF2SocketServer: Waiting for request");

				try {
					
					Socket clientSocket = serverSocket.accept();
					
					BF2SocketClient client = new BF2SocketClient(clientSocket);
					String serverId = client.getServerId();
					
					//bf2ServerClients.add(client);//
					
					if (bf2SocketConnections.containsKey(serverId)) {
						bf2SocketConnections.get(client.getServerId()).close();
						bf2SocketConnections.remove(serverId);
						Log.write("BF2SocketServer: Reconnection from " + serverId);
					}
					
					bf2SocketConnections.put(client.getServerId(), client);
					executorService.submit(client);
					
					//Log.write("BF2SocketServer: Processing request");

				} catch (IOException ioe) {
					Log.error("BF2SocketServer: Error accepting connection");
					ioe.printStackTrace();
				}
			}

		} catch (IOException e) {
			Log.error("BF2SocketServer: Error starting Server on "	+ BF2WebAdmin.BF2_SOCKET_SERVER_PORT);
			e.printStackTrace();
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
			Log.error("BF2SocketServer: Error in server shutdown");
			e.printStackTrace();
		}
	}
	
	class BF2SocketClient implements Runnable {

		private Socket clientSocket;

		private GameServer gameServer;
		private GameServerEventHandler eventHandler;
		
		private BufferedReader reader;
		private DataOutputStream writer;

		public BF2SocketClient(Socket clientSocket) {
			this.clientSocket = clientSocket;
			this.gameServer = new GameServer(clientSocket.getInetAddress().getHostAddress());
			this.eventHandler = new GameServerEventHandler(gameServer);
			Log.write("BF2SocketClient: " + clientSocket.getInetAddress().getHostAddress() + " connected.");
		}
		
		public String getServerId() {
			return clientSocket.getInetAddress().getHostAddress();
		}
		
		public synchronized void write(String message) {
			if (writer != null) {
				try {
					writer.writeBytes(message + "\n");
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		}
		
		public void close() {
			if (!clientSocket.isClosed()) {
				try {
					clientSocket.close();
				} catch (IOException e) {
					Log.error("BF2SocketClient: Error closing client connection");
				}
				/*try {
					clientSocket.getInputStream().close();
				} catch (Exception e) {
					e.printStackTrace();
				}*/
			}
		}

		public void run() {
			
			Thread.currentThread().setName("BF2SocketClient: " + getServerId());

			//BufferedReader reader = null;
			//DataOutputStream writer = null;

			try {
				reader = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()));
				writer = new DataOutputStream(clientSocket.getOutputStream());
				// writer = new PrintWriter(clientSocket.getOutputStream(), true);
				
				while (true) {
					// SocketTimeoutException thrown here if nothing read after
					// 3 seconds
					String line = reader.readLine();

					System.out.println("Received: " + line.replace("\t", ","));
					
					if (line == null)
						break;

					eventHandler.processMessage(line);
					//writer.print("Tack!\n");
					// System.out.println(line);
					//writer.print("Hello m8 #### ### ## # \n");
					//writer.writeBytes("Hello m8 #### ### ## # \n");
					// writer.writeBytes("\n");
					// System.out.println("Sent: " + "Hello m8 #### ### ## # ");
				}
				
			} catch (SocketTimeoutException e) {
				Log.write("BF2SocketClient: Connection timed out (" + getServerId() + ")");
				
			} catch (IOException e) {
				Log.write("BF2SocketClient: Connection closed (" + getServerId() + ")");
				//throw new RuntimeException(e);
				
			} catch (Exception e) {
				Log.error("Exception: " + e.getMessage());
				e.printStackTrace();
				
			} finally {
				try {
					if (reader != null)
						reader.close();
					if (writer != null)
						writer.close();
				} catch (IOException e) {
					throw new RuntimeException(e);
				}
			}

			// Make sure to close
			try {
				clientSocket.close();
			} catch (IOException e) {
				Log.error("BF2SocketClient: Error closing client connection");
			}
			
			Thread.currentThread().setName("BF2SocketPool");
		}
	}

}
