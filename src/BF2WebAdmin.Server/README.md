
cmdPrefix .
public BF2v2Module(IPlayerRepository playerRepository) {} inject IoC

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












            switch (eventType)
            {
                case "serverInfo":
                    eh.OnServerInfo(p[1], p[2], Int(p[3]), Int(p[4]), Int(p[5]));
                    break;

                // Game status events
                case "gameStatePreGame":
                    eh.OnGameStatePreGame();
                    break;
                case "gameStatePlaying":
                    eh.OnGameStatePlaying(p[1], p[2], p[3], Int(p[4]));
                    break;
                case "gameStateEndGame":
                    eh.OnGameStateEndGame(p[1], Int(p[2]), p[3], Int(p[4]), p[5]);
                    break;
                case "gameStatePaused":
                    eh.OnGameStatePaused();
                    break;
                case "gameStateRestart":
                    eh.OnGameStateRestart();
                    break;
                case "gameStateNotConnected":
                    eh.OnGameStateNotConnected();
                    break;

                // Game events
                case "controlPointCapture":
                    eh.OnControlPointCapture(Int(p[1]), p[2]);
                    break;
                case "controlPointNeutralised":
                    eh.OnControlPointNeutralised(p[1]);
                    break;

                // Timer events
                case "ticketStatus":
                    eh.OnTicketStatus(p[1], Int(p[2]), p[3], Int(p[4]), p[5]);
                    break;
                case "playerPositionUpdate":
                    eh.OnPlayerPositionUpdate(Int(p[1]), Pos(p[2]), Rot(p[3]), Int(p[4]));
                    break;
                case "projectilePositionUpdate":
                    eh.OnProjectilePositionUpdate(Int(p[1]), p[2], Pos(p[3]), Rot(p[4]));
                    break;

                // Player events
                case "playerConnect":
                    eh.OnPlayerConnect(Int(p[1]), p[2], Int(p[3]), p[4], p[5], Int(p[6]));
                    break;
                case "playerSpawn":
                    eh.OnPlayerSpawn(Int(p[1]), Pos(p[2]));
                    break;
                case "playerChangeTeam":
                    eh.OnPlayerChangeTeam(Int(p[1]), Int(p[2]));
                    break;
                case "playerScore":
                    eh.OnPlayerScore(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]), Int(p[5]));
                    break;
                case "playerRevived":
                    eh.OnPlayerRevived(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]),
                        Int(p[5]), Int(p[6]), Int(p[7]), Int(p[8]));
                    break;
                case "playerKilledSelf":
                    eh.OnPlayerKilledSelf(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]));
                    break;
                case "playerTeamkilled":
                    eh.OnPlayerTeamkilled(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]),
                        Int(p[6]), Pos(p[7]), Int(p[8]), Int(p[9]), Int(p[10]));
                    break;
                case "playerKilled":
                    eh.OnPlayerKilled(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]),
                        Int(p[6]), Pos(p[7]), Int(p[8]), Int(p[9]), Int(p[10]), p[11]);
                    break;
                case "playerDeath":
                    eh.OnPlayerDeath(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]));
                    break;
                case "playerDisconnect":
                    eh.OnPlayerDisconnect(Int(p[1]));
                    break;

                // Vehicle events
                case "enterVehicle":
                    eh.OnEnterVehicle(Int(p[1]), Int(p[2]), p[3], p[4]);
                    break;
                case "exitVehicle":
                    eh.OnExitVehicle(Int(p[1]), Int(p[2]), p[3], p[4]);
                    break;
                case "vehicleDestroyed":
                    eh.OnVehicleDestroyed(Int(p[1]), p[2]);
                    break;

                // Chat events
                case "chatServer":
                    eh.OnChatServer(p[1], p[2], p[3]);
                    break;
                case "chatPlayer":
                    eh.OnChatPlayer(p[1], p[2], Int(p[3]), p[4]);
                    break;

                default:
                    //Log.Warn($"Unknown event type: {eventType}");
                    break;
            }






	    /*
    public class GameCommand
    {
        public TYPE Type1 { get; set; }
    }

    public class RankCommand : GameCommand
    {
        public string PlayerName { get; set; }
        public int RankNumber { get; set; }
    }

    public class Command
    {

        private List<Param> _parameters;

        public Command()
        {
            //Event.Type("serverInfo").Args;

            // r|rank

            // Fluent command syntax?
            Command.Create("r|rank", "Set player rank")
                .Args("playerName", "rankNumber")
                .Callback<string, int>(ExecRankCommand);

            Command.Create("tp|teleport", "Teleport a player")
                .Callback<string, Position>((playerName, position) =>
                {

                })
                .Callback<string, string>((playerName, checkpoint) =>
                {

                });

            //.Args("playerName", "rankNumber").Callback<string, int>((playerName, rankNumber) => ExecCommand(playerName, rankNumber));
            //.Args(Arg<string>("name"), Arg<int>("rankNumber")).Callback<string, int>(ExecCommand);
            //.Args().Arg<string>("name").Arg<int>("rankNumber").Callback(ExecCommand)
            //.Args().Arg<string>("name").Arg<string>("rankName");
        }

        public static Command Create(string name, string description)
        {
            var command = new Command();
            return command;
        }

        public Command Args<T>(params T[] p)
        {
            return this;
        }

        public static Param Arg<T>(string name)
        {
            return new Param { Type = typeof(T), Name = name };
        }

        public Command Callback<T1, T2>(Action<T1, T2> action)
        {
            return this;
        }

        public void ExecRankCommand(string name, int rank)
        {

        }

        public class Param
        {
            public Type Type { get; set; }
            public string Name { get; set; }

            //public static Param Is<T>(string name)
            //{
            //    return new Param { Type = typeof(T), Name = name };
            //}
        }
    }

    public static class EventType
    {
        // Server
        public const string ServerInfo = "serverInfo";

        // Game Status

    }*/
