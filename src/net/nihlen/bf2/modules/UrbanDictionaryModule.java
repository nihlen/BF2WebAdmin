package net.nihlen.bf2.modules;

import com.owlike.genson.Genson;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.CommandExecutor;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.listeners.ChatListener;
import net.nihlen.bf2.objects.ChatEntry;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.util.WebUtils;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.io.UnsupportedEncodingException;
import java.net.URLEncoder;
import java.util.ArrayList;

/**
 * A module for defining words and phrases via ingame commands.
 * Uses the Urban Dictionary API.
 * 
 * @author Alex
 */
public class UrbanDictionaryModule implements BF2Module, CommandExecutor, ChatListener {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;

	public UrbanDictionaryModule(GameServer server) {
		this.server = server;
	}

	public void load(ModManager mm) {
		mm.addChatListener(this);

		mm.addCommand("define", "[<word>]", 0, this);
	}

	public void executeCommand(String command, String[] args, Player sender) {
		switch (command) {

			case "define":
				String term = String.join(" ", args);
				String content;
				try {
					content = WebUtils.getWebPageContents("http://api.urbandictionary.com/v0/define?term=" + URLEncoder.encode(term, "UTF-8"));

					UDDefinition def = getFirstDefinition(content);
					int score = def.thumbs_up - def.thumbs_down;
					String scoreStr = (score > 0) ? ("+" + score) : Integer.toString(score);
					String asciiDef = new String(def.definition.getBytes(), "US-ASCII").replace("\n", "").replace("\"", "'");

					String cmd = String.format("rcon game.sayall \"§C1001%s§C1001: %s (%s)\"", def.word, abbreviate(asciiDef, 120), scoreStr);
					BF2SocketServer.getInstance().send(server.getServerId(), cmd);
					log.info("Command: {}", cmd);
					
				} catch (UnsupportedEncodingException e) {
					log.error(e);
				}
				break;

		}

	}

	public void onChatMessage(ChatEntry entry) {
		/*String msg = entry.text.trim();
		if (msg.startsWith("define: ")) {
			String word = msg.replaceFirst("define: ", "");
			word = word.substring(0, Math.min(word.length(), 50));
			try {
				String content = UrbanDictionaryModule.getWebPageContents("http://api.urbandictionary.com/v0/define?term=" + URLEncoder.encode(word, "UTF-8"));
				UDDefinition def = getFirstDefinition(content);
				String asciiDef = new String (def.definition.getBytes(), "US-ASCII").replace("\n", "");
				String cmd = "rcon game.sayall \"�C1001" + def.word + "�C1001: " + abbreviate(asciiDef, 120) + "\"";
				BF2SocketServer.getInstance().send(server.getServerId(), cmd);
			} catch (UnsupportedEncodingException e) {
				e.printStackTrace();
			}
		}*/
	}

	public static String abbreviate(String str, int maxWidth) {
		if (null == str)
			return null;
		if (str.length() <= maxWidth)
			return str;
		return str.substring(0, maxWidth) + "...";
	}

	public static UDDefinition getFirstDefinition(String content) {
		try {

			Genson genson = new Genson();
			UDResult result = genson.deserialize(content, UDResult.class);
			if (result.list != null && result.list.size() > 0) {
				UDDefinition definition = result.list.get(0);
				return definition;
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
		return new UDDefinition("No definition found.");
	}

	static class UDResult {
		public String result_type;
		public ArrayList<UDDefinition> list;

		public UDResult() {
		}
	}

	static class UDDefinition {
		public int defid;
		public String word;
		public String author;
		public String definition;
		public String example;
		public int thumbs_up;
		public int thumbs_down;

		public UDDefinition() {
		}

		public UDDefinition(String def) {
			this.word = "Unknown";
			this.definition = def;
		}
	}

}
