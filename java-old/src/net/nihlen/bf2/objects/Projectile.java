package net.nihlen.bf2.objects;

import net.nihlen.bf2.util.BF2Utils;

/**
 * A model of a projectile on the Battlefield 2 server.
 *
 * @author Alex
 */
public class Projectile {

	public int id;
	public String templateName;
	public boolean active;
	public Position position;
	public Rotation rotation;
	public Player owner;

	public double distance;
	public double turnedDegrees;
	public double horizontalDegrees;

	public final long startedTime;
	public long endedTime;

	public int counter = 1;

	public Projectile(int id, String templateName, Position position, Rotation rotation) {
		this.id = id;
		this.templateName = templateName;
		this.active = true;
		this.position = position;
		this.rotation = rotation;
		this.startedTime = System.currentTimeMillis();
		this.endedTime = startedTime;
	}

	public void setPosition(Position position) {
		this.endedTime = System.currentTimeMillis();
		this.distance += BF2Utils.getVectorDistance(this.position, position);
		this.position = position;
		counter++;
	}

	public void setRotation(Rotation rotation) {
		this.turnedDegrees += BF2Utils.getRotationDegrees(this.rotation, rotation);
		this.horizontalDegrees += BF2Utils.getAngleDifference(this.rotation.yaw, rotation.yaw);
		//this.horizontalDegrees += Math.abs(this.rotation.yaw - rotation.yaw);
		this.rotation = rotation;
	}

}
