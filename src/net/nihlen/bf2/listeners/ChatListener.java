package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.ChatEntry;

public interface ChatListener {

	public void onChatMessage(ChatEntry entry);
	
}
