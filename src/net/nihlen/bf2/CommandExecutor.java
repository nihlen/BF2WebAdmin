package net.nihlen.bf2;

import net.nihlen.bf2.objects.Player;

public interface CommandExecutor {

	void executeCommand(String command, String[] args, Player player);
	
}
