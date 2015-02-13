package net.nihlen.bf2;

import java.io.BufferedReader;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

// http://stackoverflow.com/questions/12588476/multithreading-socket-communication-client-server
public class BF2SocketServer {

	private static BF2SocketServer server;
	private ServerSocket serverSocket;
	private ExecutorService executorService = Executors.newFixedThreadPool(10);
	private int serverPort = 4300;

	public static void main(String[] args) throws IOException {
		server = new BF2SocketServer();
		server.runServer();
	}

	private void runServer() {

		try {
			System.out.println("Starting Server");
			serverSocket = new ServerSocket(serverPort);

			while (true) {
				System.out.println("Waiting for request");
				try {
					Socket clientSocket = serverSocket.accept();
					System.out.println("Processing request");
					executorService.submit(new BF2SocketClient(clientSocket));
				} catch (IOException ioe) {
					System.out.println("Error accepting connection");
					ioe.printStackTrace();
				}
			}
		} catch (IOException e) {
			System.out.println("Error starting Server on " + serverPort);
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
			System.out.println("Error in server shutdown");
			e.printStackTrace();
		}
		System.exit(0);
	}

	class BF2SocketClient implements Runnable {

		private Socket clientSocket;

		public BF2SocketClient(Socket clientSocket) {
			this.clientSocket = clientSocket;
		}

		public void run() {

			BufferedReader reader = null;
			//PrintWriter writer = null;
			PrintWriter writer = null;
			
			try {
				reader = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()));
				writer = new PrintWriter(clientSocket.getOutputStream(), true);
				//writer = new DataOutputStream(clientSocket.getOutputStream());

				while (true) {
					// SocketTimeoutException thrown here if nothing read after
					// 3 seconds
					String line = reader.readLine();
					if (line == null)
						break;
					
					System.out.println(line);
					writer.print("Hello m8 #### ### ## # \n");
					//writer.writeBytes("Hello m8 #### ### ## # \n");
					//writer.writeBytes("\n");
					//System.out.println("Sent: " + "Hello m8 #### ### ## # ");
				}
			} catch (SocketTimeoutException e) {
				System.out.println("Connection timed out");
			} catch (IOException e) {
				throw new RuntimeException(e);
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
				System.out.println("Error closing client connection");
			}
		}
	}
}
