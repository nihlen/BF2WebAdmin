package net.nihlen.bf2.objects;

/**
 * A JSON Message to be sent via WebSocket.
 *
 * @author Alex
 */
public class JsonMessage {

	public String status;
	public String message;
	public Object data;

	public JsonMessage(GameServer server) {
		this.data = new JsonServerData(server);
	}

	public JsonMessage(String serverId, String eventType, Object eventMessage) {
		this.data = new JsonEventData(serverId, eventType, eventMessage);
	}

	public abstract class JsonData {
		public String server_id;
		public String type;
	}

	public class JsonServerData extends JsonData {
		public GameServer server;	// change varable name from server to message?
		public JsonServerData(GameServer server) {
			this.server_id = server.getServerId();
			this.type = "serverdata";
			this.server = server;
		}
	}

	public class JsonEventData extends JsonData {
		public Object message;
		public JsonEventData(String server_id, String type, Object message) {
			this.server_id = server_id;
			this.type = type;
			this.message = message;
		}
	}

	/*
	public class JsonPlayer {
		public int index;
		public String key;
		public int pid;
		public String name;
		public String ip;
		public JsonCountry country;
		public boolean alive;
		public int team_id;
		public int squad_id;
		public String kit_type;
		public int vehicle_id;
		public int rank_num;
		public JsonScore score;
		public Position position;
		public Rotation rotation;
	}

	public class JsonCountry {
		public String name;
		public String code;
	}

	public class JsonScore {
		public int total;
		public int team;
		public int kills;
		public int deaths;
		public int ping;
	}

	public class JsonVehicle {
	}
	*/
}

