package net.nihlen.bf2.listeners;

import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Projectile;
import net.nihlen.bf2.objects.Rotation;

/**
 * Interface for listeners of player and projectile position updates. Called often.
 * 
 * @author Alex
 */
public interface ProjectileUpdateListener {

	public void onProjectileFired(Projectile projectile);
	public void onProjectileExploded(Projectile projectile);
	public void onProjectilePosition(Projectile projectile, Position pos, Rotation rot);

}
