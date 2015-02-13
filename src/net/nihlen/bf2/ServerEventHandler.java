package net.nihlen.bf2;

import net.nihlen.bf2.objects.Server;

public class ServerEventHandler implements GameStatusEventListener {
	
	private final Server server;
	
	public ServerEventHandler(Server server) {
		this.server = server;
	}

	/*
	 * Game status events
	 */
	public void gameStatePreGame() {
		System.out.println("PreGame!!");
	}

	public void gameStatePlaying(String team1Name, String team2Name, String mapName, int maxPlayers) {
	}

	public void gameStateEndGame(String team1Name, String team1Tickets, String team2Name, String team2Tickets, String mapName) {
	}

	public void gameStatePaused() {
	}

	public void gameStateRestart() {
	}

	public void gameStateNotConnected() {
	}
	
}
