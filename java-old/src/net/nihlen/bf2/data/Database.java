package net.nihlen.bf2.data;

/**
 * Database interface for accessing players, maps and logins
 *
 * Created by Alex on 2015-11-10.
 */
public interface Database {
	String[] getTopPlayerMatch(String ip);
	boolean authorize(String key);
}
