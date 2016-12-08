
Separator: \t
End: \n

## We can send these messages to the BF2 server:

rcon <string cmd>																Run RCON command
rconresponse <int? code> <string cmd>											Run RCON command and return with a response code (test)
pm <int playerid> <string msg>													Send player a message that will appear in their console
position <int playerid> <float x> <float h> <float y>							Teleport player
rotation <int playerid> <float yaw> <float pitch> <float roll>					Rotate player
damage <int playerid> <int damage>												Set player health (damage)
rank <int playerid> <int ranknum> <int rankevent>								Set player rank (ranknum 0-21) (rankevent 0-1 to show or not)
medal <int playerid <int medalnum> <int medalval>								Give player a medal award
gameevent <int playerid> <int event> <int data>									Send player game event
hudevent <int playerid> <int event> <int data>									Send player HUD event
score <int playerid> <int totalScore> <int teamScore> <int kills> <int deaths>	Set player score
team <int playerid> <int teamid>												Set player team

## We can receive these messages from the BF2 server:

### Connection event
serverInfo (string serverName, string mapsStr, int gamePort, int queryPort, int maxPlayers)

### Game status events
gameStatePreGame			-
gameStatePlaying			(string team1Name, string team2Name, string mapName, int maxPlayers)
gameStateEndGame			(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
gameStatePaused				-
gameStateRestart			-
gameStateNotConnected		-

### Game events
controlPointCapture			(int teamId, string cpName)
controlPointNeutralised		(string cpName)

### Timer events
ticketStatus				(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
playerPositionUpdate		(int playerIndex, Position position, Rotation rotation, int ping)
projectilePositionUpdate	(int id, string templateName, Position position, Rotation rotation)

### Player events
playerConnect				(int index, string name, int pid, string ipAddress, string hash, int teamId)
playerSpawn					(int index, Position pos)
playerChangeTeam			(int index, int teamId)
playerScore					(int index, int score, int teamScore, int kills, int deaths)
playerRevived				(int medicIndex, int medicScore, int medicKills, int medicDeaths, int reviveeIndex, int reviveeScore, int reviveeKills, int reviveeDeaths)
playerKilledSelf			(int index, Position pos, int score, int kills, int deaths)
playerTeamkilled			(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths)
playerKilled				(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths, string weapon)
playerDeath					(int index, Position pos, int score, int kills, int deaths)
playerDisconnect			(int index)

### Vehicle events
enterVehicle				(int index, int vehicleId, string rootVehicleName, string vehicleName)
exitVehicle					(int index, int vehicleId, string rootVehicleName, string vehicleName)
vehicleDestroyed			(int vehicleId, string vehicleName)

### Chat events
chatServer					(string channel, string flags, string text)
chatPlayer					(string channel, string flags, int index, string text)

## Notes

* Player id is not the same as player index?
