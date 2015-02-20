package net.nihlen.bf2.modules;

import java.util.ArrayList;

import net.nihlen.bf2.BF2SocketServer;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.WebAdminWebSocketServer;
import net.nihlen.bf2.listeners.ChatListener;
import net.nihlen.bf2.listeners.PlayerUpdateListener;
import net.nihlen.bf2.objects.ChatEntry;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Vehicle;

import org.java_websocket.WebSocket;

public class WebAdminModule implements ChatListener, PlayerUpdateListener {

	private final GameServer server;
	
	public WebAdminModule(GameServer server) {
		this.server = server;
	}
	
	public void sendAll(String msg) {
		ArrayList<WebSocket> webSockets = WebAdminWebSocketServer.getInstance().getWebSockets(server.getIpAddress());
		if (webSockets != null) {
			for (WebSocket s : webSockets) {
				s.send(msg);
			}
		}
	}
	
	public void load(ModManager mm) {
		mm.addChatListener(this);
	}

	/*
	 * Chat
	 */
	public void onChatMessage(ChatEntry entry) {
		System.out.println("WebAdmin: " + entry.text);
		sendAll(entry.text);
		WebAdminWebSocketServer.getInstance().send(server.getIpAddress(), entry.player + " said: " + entry.text);
	}

	/*
	 * Player
	 */
	public void onPlayerConnect(Player player) {
	}

	public void onPlayerSpawn(Player player) {
		BF2SocketServer.getInstance().send(server.getIpAddress(), "Wow good job on spawning!");
		WebAdminWebSocketServer.getInstance().send(server.getIpAddress(), player + " spawned");
	}

	public void onPlayerScore(Player player) {
	}

	public void onPlayerKilled(Player attacker, Player victim, String weapon) {
	}

	public void onPlayerDeath(Player player) {
		BF2SocketServer.getInstance().send(server.getIpAddress(), "rekt");
		WebAdminWebSocketServer.getInstance().send(server.getIpAddress(), "just rekt");
	}

	public void onPlayerDisconnect(Player player) {
	}

	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle) {
		BF2SocketServer.getInstance().send(server.getIpAddress(), "what are you doing in that " + vehicle.templateName);
		WebAdminWebSocketServer.getInstance().send(server.getIpAddress(), "so " + player + " entered" + vehicle.templateName);
	}

	public void onPlayerExitVehicle(Player player, Vehicle vehicle) {
	}

	public void onPlayerPosition(Player player, Position pos) {
	}
	
}
