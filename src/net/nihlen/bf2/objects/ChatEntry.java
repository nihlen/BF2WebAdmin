package net.nihlen.bf2.objects;

public class ChatEntry {
	
	public enum ChatType { SERVER, PLAYER };

	public final ChatType type;
	public final String channel;
	public final String flags;
	public final String text;
	
	public final Player player;
	
	public ChatEntry(String channel, String flags, String text) {
		this.type = ChatType.SERVER;
		this.channel = channel;
		this.flags = flags;
		this.player = null;
		this.text = text;
	}
	
	public ChatEntry(String channel, String flags, Player player, String text) {
		this.type = ChatType.PLAYER;
		this.channel = channel;
		this.flags = flags;
		this.player = player;
		this.text = text;
	}
	
}
