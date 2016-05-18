package net.nihlen.bf2.modules;

import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.CommandExecutor;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.objects.GameServer;
import net.nihlen.bf2.objects.Player;
import net.nihlen.bf2.objects.Position;
import net.nihlen.bf2.objects.Rotation;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

/**
 * Load/Save custom maps and spawn vehicles and static objects.
 *
 * @author Alex
 */
public class MapBuilder implements BF2Module, CommandExecutor {

	private static final Logger log = LogManager.getLogger();

	private final GameServer server;

	public MapBuilder(GameServer server) {
		this.server = server;
	}

	public void load(ModManager mm) {

		// Commands
		mm.addCommand("spawn", "<object>", 0, this);

	}

	public void executeCommand(String command, String[] args, Player sender) {
		switch (command) {

			case "spawn":
				executeSpawnCommand(args, sender);
				break;

			default:
				log.error("Unknown command: {}", command);
				break;

		}
	}

	private void executeSpawnCommand(String[] args, Player sender) {
		if (args.length == 1) {
			String object = args[0].trim();
			spawnObject(object, sender.position, sender.rotation);
		}
	}


	private void spawnObject(String objName, Position position, Rotation rotation) {

		String cmd = "rcon objecttemplate.active " + objName + "\n";
		cmd += "rcon objecttemplate.setNetworkableInfo BasicInfo\n";

		cmd += "rcon objecttemplate.create ObjectSpawner tmp_spawner_" + objName + "\n";
		cmd += "rcon objecttemplate.activeSafe ObjectSpawner tmp_spawner_" + objName + "\n";
		cmd += "rcon objecttemplate.isNotSaveable 1\n";
		cmd += "rcon objecttemplate.hasMobilePhysics 0\n";
		cmd += "rcon objecttemplate.setObjectTemplate 0 " + objName + "\n";
		cmd += "rcon objecttemplate.setObjectTemplate 1 " + objName + "\n";
		cmd += "rcon objecttemplate.setObjectTemplate 2 " + objName + "\n";
		cmd += "rcon objecttemplate.setNetworkableInfo BasicInfo\n";

		cmd += "rcon object.create tmp_spawner_" + objName + "\n";
		cmd += "rcon object.absolutePosition " + position.toString() + "\n";
		cmd += "rcon object.rotation " + rotation.toString() + "\n";
		cmd += "rcon object.name obj_" + objName + "\n";
		cmd += "rcon object.team 1\n";
		cmd += "rcon object.layer 1\n";
		cmd += "rcon object.delete";

		BF2SocketServer.getInstance().send(server.getServerId(), cmd);
		log.info("Spawned {} at {}", objName, position);

	}

}
