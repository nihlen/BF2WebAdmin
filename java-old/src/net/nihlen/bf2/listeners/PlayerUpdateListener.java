package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Rotation;
import net.nihlen.bf2.objects.Vehicle;

/**
 * Interface for listeners of player update events.
 * 
 * @author Alex
 */
public interface PlayerUpdateListener {

	public void onPlayerConnect(Player player);
	public void onPlayerSpawn(Player player);
	public void onPlayerScore(Player player);
	public void onPlayerKilled(Player attacker, Player victim, String weapon);
	public void onPlayerDeath(Player player);
	public void onPlayerChangeTeam(Player player, int teamId);
	public void onPlayerDisconnect(Player player);
	public void onPlayerEnterVehicle(Player player, Vehicle vehicle, String subVehicle);
	public void onPlayerExitVehicle(Player player, Vehicle vehicle);
	public void onPlayerPosition(Player player, Position pos, Rotation rot);
	
}
