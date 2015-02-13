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
	
	public Player(int index, String name, int pid, String IpAddress, String hash, int teamId) {
		this.index = index;
		this.name = name;
		this.pid = pid;
		this.IpAddress = IpAddress;
		this.hash = hash;
		this.teamId = teamId;
		this.isAlive = false;
	}
	
	public void delete() {
		if (rootVehicle != null) {
			rootVehicle.removePlayer(this);
		}
	}
	
	public void updateScore(int totalScore, int kills, int deaths) {
		this.totalScore = totalScore;
		this.kills = kills;
		this.deaths = deaths;
	}
	
}
