package net.nihlen.bf2.objects;

public class Player {
	
	public final int index;
	public final String name;
	public final int pid;
	public final String IpAddress;
	public final String hash;
	
	public Position position;
	public Vehicle rootVehicle;
	public String subVehicle;
	
	public int teamId;
	public boolean isAlive;
	
	public int totalScore;
	public int teamScore;
	public int kills;
	public int deaths;
	public int ping;
	
	public Player(int index, String name, int pid, String IpAddress, String hash, int teamId) {
		this.index = index;
		this.name = name;
		this.pid = pid;
		this.IpAddress = IpAddress;
		this.hash = hash;
		this.teamId = teamId;
		this.isAlive = false;
	}
	
	/*public void delete() {
		if (rootVehicle != null) {
			rootVehicle.removePlayer(this);
		}
	}*/
	
	public synchronized void updateScore(int totalScore, int kills, int deaths) {
		this.totalScore = totalScore;
		this.kills = kills;
		this.deaths = deaths;
	}
	
	public synchronized void setPosition(Position pos) {
		this.position = pos;
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
	
	@Override
	public String toString() {
		return name.trim();
	}
	
	
	
}
