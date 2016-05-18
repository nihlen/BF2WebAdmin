package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.GameState;

/**
 * Interface for listeners of game server state events.
 * 
 * @author Alex
 */
public interface GameStateListener {
	
	public void onGameState(GameState state);

}
