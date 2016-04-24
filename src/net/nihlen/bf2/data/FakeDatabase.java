package net.nihlen.bf2.data;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

/**
 * Fake database for testing
 *
 * Created by Alex on 2015-11-10.
 */
public class FakeDatabase implements Database {

	private static final Logger log = LogManager.getLogger();

	@Override
	public String[] getTopPlayerMatch(String ip) {
		return new String[] { "krische", "snapaction" };
	}

	@Override
	public boolean authorize(String key) {
		log.info("WebSocket user " + key + " authorized");
		return true;
	}
}
