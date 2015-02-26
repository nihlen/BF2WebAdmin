package net.nihlen.bf2;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;

public class BF2WebAdmin {
	
	public static final int BF2_SOCKET_SERVER_PORT = 4300;
	public static final int WEB_SOCKET_SERVER_PORT = 4301;

	public static void main(String[] args) throws IOException {

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
		
		InputStreamReader sr = new InputStreamReader(System.in);
		BufferedReader br = new BufferedReader(sr);
		
		String input = "";
		while (!input.equals("exit")) {
			
			if (input.length() > 0) {
				System.out.println("Read: " + input);
				BF2SocketServer.getInstance().send("127.0.0.1", input);
			}
			input = br.readLine();
			
		}
		
		System.exit(1);

	}

}
