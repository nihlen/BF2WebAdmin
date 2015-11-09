package net.nihlen.bf2.util;

import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Rotation;

/**
 * Helpful utility methods.
 *
 * @author Alex
 */
public class BF2Utils {

	public static double getVectorDistance(Position pos1, Position pos2) {
		double xDiff = Math.abs(pos1.x - pos2.x);
		double heightDiff = Math.abs(pos1.height - pos2.height);
		double yDiff = Math.abs(pos1.y - pos2.y);
		return Math.sqrt(xDiff * xDiff + heightDiff * heightDiff + yDiff * yDiff);
	}

	// TODO: Do it correctly
	public static double getRotationDegrees(Rotation rot1, Rotation rot2) {
		//double yawDiff = Math.abs(rot1.yaw - rot2.yaw);
		//double pitchDiff = Math.abs(rot1.pitch - rot2.pitch);
		double yawDiff = getAngleDifference(rot1.yaw, rot2.yaw);
		double pitchDiff = getAngleDifference(rot1.pitch, rot2.pitch);
		return Math.sqrt(yawDiff * yawDiff + pitchDiff * pitchDiff);
	}

	public static double getAngleDifference(double angle1, double angle2) {
		double diff = Math.abs(angle1 - angle2) % 360;
		return (diff > 180) ? 360 - diff : diff;
	}

}
