package net.nihlen.bf2.objects;

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
			double pitch = Double.parseDouble(parts[2]);
			double roll = Double.parseDouble(parts[1]);
			return new Rotation(yaw, pitch, roll);
		}
		return new Rotation(0, 0, 0);
	}
	
}
