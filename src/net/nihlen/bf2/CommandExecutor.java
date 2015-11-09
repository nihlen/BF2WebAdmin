package net.nihlen.bf2;

import net.nihlen.bf2.objects.Player;


/**
 * Interface for a class that can execute a chat command. 
 * Used by ModManager modules.
 * 
 * @author Alex
 */
public interface CommandExecutor {

	void executeCommand(String command, String[] args, Player sender);
	
}
