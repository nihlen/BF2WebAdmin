package net.nihlen.bf2.util;

/**
 * Combine Rcon commands using a StringBuilder and adding
 * the necessary rcon and newling parts.
 *
 * @author Alex
 */
public class RconBuilder {

	private StringBuilder rconString;

	public RconBuilder() {
		this.rconString = new StringBuilder();
	}

	public void append(String newStr) {
		rconString.append("rcon ");
		rconString.append(newStr);
	}

	// Use this?

}
