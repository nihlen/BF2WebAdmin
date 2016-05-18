package net.nihlen.bf2.listeners;

import org.java_websocket.WebSocket;

/**
 * Interface for classes listening to WebSocket client events.
 *
 * @author Alex
 */
public interface WebSocketListener {

	public void onWebSocketConnect(WebSocket socket);
	public void onWebSocketDisconnect(WebSocket socket);
	public void onWebSocketMessage(WebSocket socket, String message);

}
