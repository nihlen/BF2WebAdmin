package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.ChatEntry;

/**
 * Interface for listeners of chat events.
 * 
 * @author Alex
 */
public interface ChatListener {

	public void onChatMessage(ChatEntry entry);
	
}
