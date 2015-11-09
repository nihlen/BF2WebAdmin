package net.nihlen.bf2;

import net.nihlen.bf2.objects.*;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.util.ArrayList;
import java.util.Arrays;

/**
 * Handles the events received from the Battlefield 2 game server.
 * 
 * @author Alex
 */
public class GameServerEventHandler /*implements GameStatusEventListener, ChatEventListener*/ {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;
	
	public GameServerEventHandler(GameServer server) {
		this.server = server;
	}
	
	public void processMessage(String msg) {
		
		// Only handle events, not rcon responses and other stuff lacking tabs (\t)
		if (!msg.contains("\t"))
			return;
		
		String[] args = msg.split("\t");
		String eventType = args[0];
		
		try {
			
			switch (eventType) {

				// Connection event
				case "serverInfo":
					serverInfo(args[1], args[2], Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]));
					break;

				// Game status events
				case "gameStatePreGame":
					gameStatePreGame();
					break;
				case "gameStatePlaying":
					gameStatePlaying(args[1], args[2], args[3], Integer.parseInt(args[4]));
					break;
				case "gameStateEndGame":
					gameStateEndGame(args[1], Integer.parseInt(args[2]), args[3], Integer.parseInt(args[4]), args[5]);
					break;
				case "gameStatePaused":
					gameStatePaused();
					break;
				case "gameStateRestart":
					gameStateRestart();
					break;
				case "gameStateNotConnected":
					gameStateNotConnected();
					break;
					
				// Game events
				case "controlPointCapture":
					controlPointCapture(Integer.parseInt(args[1]), args[2]);
					break;
				case "controlPointNeutralised":
					controlPointNeutralised(args[1]);
					break;
					
				// Timer events
				case "ticketStatus":
					ticketStatus(args[1], Integer.parseInt(args[2]), args[3], Integer.parseInt(args[4]), args[5]);
					break;
				case "playerPositionUpdate":
					playerPositionUpdate(Integer.parseInt(args[1]), Position.createPosition(args[2]), Rotation.createRotation(args[3]), Integer.parseInt(args[4]));
					break;
				case "projectilePositionUpdate":
					projectilePositionUpdate(Integer.parseInt(args[1]), args[2], Position.createPosition(args[3]), Rotation.createRotation(args[4]));
					break;

				// Player events
				case "playerConnect":
					playerConnect(Integer.parseInt(args[1]), args[2], Integer.parseInt(args[3]), args[4], args[5], Integer.parseInt(args[6]));
					break;
				case "playerSpawn":
					playerSpawn(Integer.parseInt(args[1]), Position.createPosition(args[2]));
					break;
				case "playerChangeTeam":
					playerChangeTeam(Integer.parseInt(args[1]), Integer.parseInt(args[2]));
					break;
				case "playerScore":
					//playerScore(Integer.parseInt(args[1]), Integer.parseInt(args[2]));
					playerScore(Integer.parseInt(args[1]), Integer.parseInt(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]));
					break;
				case "playerRevived":
					playerRevived(Integer.parseInt(args[1]), Integer.parseInt(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), 
							Integer.parseInt(args[5]), Integer.parseInt(args[6]), Integer.parseInt(args[7]), Integer.parseInt(args[8]));
					break;
				case "playerKilledSelf":
					playerKilledSelf(Integer.parseInt(args[1]), Position.createPosition(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]));
					break;
				case "playerTeamkilled":
					playerTeamkilled(Integer.parseInt(args[1]), Position.createPosition(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]), 
							Integer.parseInt(args[6]), Position.createPosition(args[7]), Integer.parseInt(args[8]), Integer.parseInt(args[9]), Integer.parseInt(args[10]));
					break;
				case "playerKilled":
					playerKilled(Integer.parseInt(args[1]), Position.createPosition(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]), 
							Integer.parseInt(args[6]), Position.createPosition(args[7]), Integer.parseInt(args[8]), Integer.parseInt(args[9]), Integer.parseInt(args[10]), args[11]);
					break;
				case "playerDeath":
					playerDeath(Integer.parseInt(args[1]), Position.createPosition(args[2]), Integer.parseInt(args[3]), Integer.parseInt(args[4]), Integer.parseInt(args[5]));
					break;
				case "playerDisconnect":
					playerDisconnect(Integer.parseInt(args[1]));
					break;
						
				// Vehicle events
				case "enterVehicle":
					enterVehicle(Integer.parseInt(args[1]), Integer.parseInt(args[2]), args[3], args[4]);
					break;
				case "exitVehicle":
					exitVehicle(Integer.parseInt(args[1]), Integer.parseInt(args[2]), args[3], args[4]);
					break;
				case "vehicleDestroyed":
					vehicleDestroyed(Integer.parseInt(args[1]), args[2]);
					break;
					
				// Chat events
				case "chatServer":
					chatServer(args[1], args[2], args[3]);
					break;
				case "chatPlayer":
					chatPlayer(args[1], args[2], Integer.parseInt(args[3]), args[4]);
					break;
					
				default:
					log.error("Unknown event type: [{}] in message: [{}]", eventType, msg);
					break;
			}

			
		} catch (Exception e) {
			//Log.error("GameServerEventHandler: " + e.getMessage() + "\n" + e.getStackTrace().toString());
			log.error(e);
		}
		
	}

	/*
	 * Connection event
	 */
	private void serverInfo(String serverName, String mapsStr, int gamePort, int queryPort, int maxPlayers) {
		ArrayList<String> mapList = new ArrayList<String>(Arrays.asList(mapsStr.split(",")));
		server.setInfo(serverName, mapList, gamePort, queryPort, maxPlayers);
	}

	/*
	 * Game state events
	 */
	private void gameStatePreGame() {
		server.setGameState(GameState.PRE_GAME);
	}

	private void gameStatePlaying(String team1Name, String team2Name, String mapName, int maxPlayers) {
		server.setGameState(GameState.PLAYING);
		server.setMapName(mapName.toLowerCase());
	}

	private void gameStateEndGame(String team1Name, int team1Tickets, String team2Name, int team2Tickets, String mapName) {
		server.setGameState(GameState.END_GAME);
	}

	private void gameStatePaused() {
		server.setGameState(GameState.PAUSED);
	}

	private void gameStateRestart() {
		server.setGameState(GameState.RESTART);
	}

	private void gameStateNotConnected() {
		server.setGameState(GameState.NOT_CONNECTED);
	}
	
	/*
	 * Game events
	 */
	private void controlPointCapture(int teamId, String cpName) {
	}
	
	private void controlPointNeutralised(String cpName) {
	}
	
	/*
	 * Timer events
	 */
	private void ticketStatus(String team1Name, int team1Tickets, String team2Name, int team2Tickets, String mapName) {
	}

	private void playerPositionUpdate(int playerIndex, Position position, Rotation rotation, int ping) {
		server.updatePlayerPosition(playerIndex, position, rotation);
		server.getPlayer(playerIndex).ping = ping;
	}

	private void projectilePositionUpdate(int id, String templateName, Position position, Rotation rotation) {
		server.updateProjectilePosition(id, templateName, position, rotation);
	}

	/*
	 * Player events
	 */
	private void playerConnect(int index, String name, int pid, String ipAddress, String hash, int teamId) {
		server.addPlayer(index, name, pid, ipAddress, hash, teamId);
	}
	
	private void playerSpawn(int index, Position pos) {
		Player player = server.getPlayer(index);
		if (player != null) {
			player.setAlive(true);
			player.setPosition(pos);
			server.onPlayerSpawn(player);
		}
	}
	
	private void playerChangeTeam(int index, int teamId) {
		Player player = server.getPlayer(index);
		if (player != null) {
			player.setTeam(teamId);
			server.onPlayerChangeTeam(player, teamId);
		}
	}

	//private void playerScore(int index, int difference) {
	private void playerScore(int index, int score, int teamScore, int kills, int deaths) {
		server.updatePlayerScore(index, score, teamScore, kills, deaths);
	}

	private void playerRevived(int medicIndex, int medicScore, int medicKills, int medicDeaths, int reviveeIndex, int reviveeScore, int reviveeKills, int reviveeDeaths) {
		// Don't care
	}
	
	private void playerKilledSelf(int index, Position pos, int score, int kills, int deaths) {
		//server.updatePlayerScore(index, score, kills, deaths);
		server.onPlayerDeath(index);
	}
	
	private void playerTeamkilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths) {
		//server.updatePlayerScore(attackerIndex, attackerScore, attackerKills, attackerDeaths);
		//server.updatePlayerScore(victimIndex, victimScore, victimKills, victimDeaths);
		server.onPlayerKilled(attackerIndex, victimIndex, "TK");
	}

	private void playerKilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths, String weapon) {
		//server.updatePlayerScore(attackerIndex, attackerScore, attackerKills, attackerDeaths);
		//server.updatePlayerScore(victimIndex, victimScore, victimKills, victimDeaths);
		server.onPlayerKilled(attackerIndex, victimIndex, weapon);
	}
	
	private void playerDeath(int index, Position pos, int score, int kills, int deaths) {
		//server.updatePlayerScore(index, score, kills, deaths);
		server.onPlayerDeath(index);
	}

	private void playerDisconnect(int index) {
		server.removePlayer(index);
	}
	
	/*
	 * Vehicle events
	 */
	private void enterVehicle(int index, int vehicleId, String rootVehicleName, String vehicleName) {
		server.updatePlayerVehicle(index, vehicleId, rootVehicleName, vehicleName);
	}
	
	private void exitVehicle(int index, int vehicleId, String rootVehicleName, String vehicleName) {
		server.updatePlayerVehicle(index, -1, null, null);
	}
	
	private void vehicleDestroyed(int vehicleId, String vehicleName) {
		// Don't care
	}
	
	/*
	 * Chat events	
	 */
	private void chatServer(String channel, String flags, String text) {
		ChatEntry c = new ChatEntry(channel, flags, text);
		server.addChatEntry(c);
	}

	private void chatPlayer(String channel, String flags, int index, String text) {
		Player player = server.getPlayer(index);
		ChatEntry msg = new ChatEntry(channel, flags, player, text);
		server.addChatEntry(msg);
	}
	
}
