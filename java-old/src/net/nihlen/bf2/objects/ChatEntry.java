package net.nihlen.bf2.objects;

/**
 * A chat message from the Battlefield 2 server.
 * 
 * @author Alex
 */
public class ChatEntry {
	
	public enum ChatType { SERVER, PLAYER }

	public final ChatType type;
	public final String channel;
	public final String flags;
	public final String text;
	
	public final Player player;
	public final long time;
	
	public ChatEntry(String channel, String flags, String text) {
		this.type = ChatType.SERVER;
		this.channel = channel;
		this.flags = flags;
		this.player = null;
		this.text = fixText(text);
		this.time = System.currentTimeMillis();
	}
	
	public ChatEntry(String channel, String flags, Player player, String text) {
		this.type = ChatType.PLAYER;
		this.channel = channel;
		this.flags = flags;
		this.player = player;
		this.text = fixText(text);
		this.time = System.currentTimeMillis();
	}

	private static String fixText(String text) {
		return text.replace("�", "§").replace("§C1001", "").replace("§3", "").trim();
	}
	
}
