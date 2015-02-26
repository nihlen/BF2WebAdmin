package net.nihlen.bf2;

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
	
	public void setAuthLevel(int authLevel) {
		this.authLevel = authLevel;
	}
}
