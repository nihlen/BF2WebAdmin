package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Vehicle;

public interface PlayerUpdateListener {

	public void onPlayerConnect(Player player);
	public void onPlayerSpawn(Player player);
	public void onPlayerScore(Player player);
	public void onPlayerKilled(Player attacker, Player victim, String weapon);
	public void onPlayerDeath(Player player);
	public void onPlayerDisconnect(Player player);
	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle);
	public void onPlayerExitVehicle(Player player, Vehicle vehicle);
	public void onPlayerPosition(Player player, Position pos);
	
}
