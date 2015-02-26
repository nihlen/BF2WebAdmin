package net.nihlen.bf2.objects;

public class Position {
	
	public final double x, y;
	public final double height;
	
	public Position(double x, double height, double y) {
		this.x = x;
		this.height = height;
		this.y = y;
	}
	
	public static Position createPosition(String positionString) {
		String[] parts = positionString.split("/");
		if (parts.length == 3) {
			double x = Double.parseDouble(parts[0]);
			double height = Double.parseDouble(parts[1]);
			double y = Double.parseDouble(parts[2]);
			return new Position(x, y, height);
		}
		return new Position(0, 0, 0);
	}
	
}
