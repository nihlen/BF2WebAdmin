package net.nihlen.bf2.objects;

/**
 * A game server object rotation. Euler angles.
 * Format: yaw/pitch/roll
 * 
 * @author Alex
 */
public class Rotation {

	public final double yaw, pitch, roll;
	
	public Rotation(double yaw, double pitch, double roll) {
		this.yaw = yaw;
		this.pitch = pitch;
		this.roll = roll;
	}
	
	public static Rotation createRotation(String rotationString) {
		String[] parts = rotationString.split("/");
		if (parts.length == 3) {
			double yaw = Double.parseDouble(parts[0]);
			double pitch = Double.parseDouble(parts[1]);
			double roll = Double.parseDouble(parts[2]);
			return new Rotation(yaw, pitch, roll);
		}
		return new Rotation(0, 0, 0);
	}
	
	@Override
	public String toString() {
		return String.format("%s/%s/%s", yaw, pitch, roll);
	}
	
}
