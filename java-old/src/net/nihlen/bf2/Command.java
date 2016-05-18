package net.nihlen.bf2;

/**
 * A chat command from the Battlefield 2 game server.
 * 
 * @author Alex
 */
public class Command {

	public final String[] aliases;
	public final String argsFormat;
	public final CommandExecutor callback;
	private int authLevel;
	
	public Command(String[] aliases, String argsFormat, int authLevel, CommandExecutor callback) {
		this.aliases = aliases;
		this.argsFormat = argsFormat;
		this.authLevel = authLevel;
		this.callback = callback;
	}
	
	public int getAuthLevel() {
		return authLevel;
	}
	
	// TODO: Some way to set different auth depending on number of passed args
	public int getAuthLevel(int argsCount) {
		return authLevel;
	}
	
	public void setAuthLevel(int authLevel) {
		this.authLevel = authLevel;
	}
}
