package net.nihlen.bf2;

public interface GameStatusEventListener {
	
	public void gameStatePreGame();
	public void gameStatePlaying(String team1Name, String team2Name, String mapName, int maxPlayers);
	public void gameStateEndGame(String team1Name, String team1Tickets, String team2Name, String team2Tickets, String mapName);
	public void gameStatePaused();
	public void gameStateRestart();
	public void gameStateNotConnected();
	
}
