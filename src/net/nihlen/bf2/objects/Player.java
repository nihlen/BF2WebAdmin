package net.nihlen.bf2.objects;

/**
 * A model of a player on the Battlefield 2 server.
 * 
 * @author Alex
 */
public class Player {
	
	public final int index;
	public final String name;
	public final int pid;
	public final String ipAddress;
	public final String hash;
	public String country;
	public String countryCode;
	public int rank;

	public Position position;
	public Rotation rotation;
	public Vehicle rootVehicle;
	public String subVehicle;

	public int teamId;
	public boolean isAlive;

	public int totalScore;
	public int teamScore;
	public int kills;
	public int deaths;
	public int ping;

	private int authLevel;

	public Player(int index, String name, int pid, String ipAddress, String hash, int teamId) {
		this.index = index;
		this.name = name;
		this.pid = pid;
		this.ipAddress = ipAddress;
		this.hash = hash;
		this.teamId = teamId;
		this.isAlive = false;
		this.authLevel = 0;
	}
	
	/*public void delete() {
		if (rootVehicle != null) {
			rootVehicle.removePlayer(this);
		}
	}*/
	
	public boolean isDriver() {
		return ((rootVehicle != null) && (rootVehicle.templateName.equals(subVehicle)));
	}
	
	public synchronized void updateScore(int totalScore, int teamScore, int kills, int deaths) {
		this.totalScore = totalScore;
		this.teamScore = teamScore;
		this.kills = kills;
		this.deaths = deaths;
	}

	public synchronized void setPosition(Position pos) {
		this.position = pos;
	}

	public synchronized void setRotation(Rotation rot) {
		this.rotation = rot;
	}

	public synchronized void setAlive(boolean isAlive) {
		this.isAlive = isAlive;
	}
	
	public synchronized void setTeam(int teamId) {
		this.teamId = teamId;
	}
	
	public synchronized void setVehicle(Vehicle rootVehicle, String subVehicle) {
		this.rootVehicle = rootVehicle;
		this.subVehicle = subVehicle;
	}

	public synchronized void setRank(int rankNum) {
		this.rank = rankNum;
	}

	public int getAuthLevel() {
		return authLevel;
	}
	
	@Override
	public String toString() {
		return name.trim();
	}

}
