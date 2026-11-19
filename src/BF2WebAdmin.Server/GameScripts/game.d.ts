// The host API a game script sees on the game server, versions 1 and 2: the two global objects `script` and `game`.
// The definition is the "Scripts" section of openbf2-server-ext docs/admin-channel.md; docs/scripts.md explains it.
//
// A script is one file of strict global code. The engine has the base objects, JSON, Map/Set, typed arrays and
// promises. It has no Date, no regular expressions, no eval or Function constructor, no Proxy, no timers, no console
// and no modules (no import or export): count ticks, and use script.log.

/** x, y (up) and z in metres; metres per second for a velocity; radians per second about the world axes for an angular velocity. */
type Vec3 = [x: number, y: number, z: number];

/** Yaw, pitch and roll in degrees. */
type Rot = [yaw: number, pitch: number, roll: number];

/**
 * A player as the game server sees it now. Its root object is its vehicle when seated, else its soldier; a dead
 * player has none.
 */
interface GamePlayer {
    /** The player id, 0-255. */
    readonly id: number;
    readonly name: string;
    readonly team: number;
    readonly alive: boolean;
    /** The AI flag: any bot. */
    readonly bot: boolean;
    readonly seated: boolean;
    /** The seat's id in its vehicle, or -1 on foot or dead. */
    readonly seat: number;
    /** The root object's networkable id, 0 without one. The players of one vehicle share it. */
    readonly rootId: number;
    /** The root object's template name, empty when dead. */
    readonly vehicle: string;
    /** The root object's position, null when dead. */
    readonly position: Vec3 | null;
    /** The root object's rotation, null when dead. */
    readonly rotation: Rot | null;
    /** The root object's velocity, null when dead. */
    readonly velocity: Vec3 | null;
}

interface Script {
    /** The host API version of the game server's module: 1, or 2 with the object members of Game. */
    readonly api: number;
    readonly name: string;
    /** The arguments WebAdmin started the script with, null without any. */
    readonly args: unknown;

    /** Calls the handler every tick of the game server (30 a second), with the tick number. */
    onTick(handler: (tick: number) => void): void;
    /** Calls the handler with every message WebAdmin sends to this script. */
    onMessage(handler: (message: unknown) => void): void;
    /**
     * Calls the handler before the script ends by finish, a stop from WebAdmin, a level unload or a closing link
     * ("finished", "stopped", "level unload", "link closed"), so it can undo what it did. Not called after an error.
     */
    onStop(handler: (reason: string) => void): void;

    /** Ends the script when the current handler returns. The result must be JSON-serialisable. */
    finish(result?: unknown): void;
    /** Sends data to WebAdmin (at most 8192 bytes of JSON, at most 16 a tick; more throws a RangeError). */
    emit(data: unknown): void;
    /** Writes a line to the game server's log and to WebAdmin's. */
    log(...parts: unknown[]): void;
}

interface Game {
    /** The game server module's tick number. */
    readonly tick: number;
    /** Ticks per second, 30. */
    readonly tickRate: number;
    /** The engine's time in seconds. */
    readonly time: number;
    /** The current map's name. */
    readonly map: string;

    /** The ids of all players. */
    players(): number[];
    /** A player, or null when there is none with that id. */
    player(id: number): GamePlayer | null;

    /**
     * Moves and turns the player's root object; null keeps the position or the rotation. A position resets the
     * object's speeds. False when the player has no root object.
     */
    teleport(id: number, position: Vec3 | null, rotation: Rot | null): boolean;
    /** Sets the speed of the player's root object. False without a root object or a physics body. */
    setVelocity(id: number, velocity: Vec3): boolean;
    /** Sets the rotational speed of the player's root object. False without a root object or a physics body. */
    setAngularVelocity(id: number, velocity: Vec3): boolean;
    /** A chat line from the server to everyone, or to one player. */
    say(text: string, player?: number): void;

    // API 2 (a script started with api 2; see ScriptRunner.StartAsync). Objects by networkable id (1-65535), each
    // acting on the object's root: for objects WebAdmin spawned, which no player can name.

    /** An object, or null when there is none with that networkable id. */
    object(id: number): GameObject | null;
    /** As teleport, on an object. False when there is no such object. */
    teleportObject(id: number, position: Vec3 | null, rotation: Rot | null): boolean;
    /** As setVelocity, on an object. False without the object or a physics body. */
    setObjectVelocity(id: number, velocity: Vec3): boolean;
    /** As setAngularVelocity, on an object. False without the object or a physics body. */
    setObjectAngularVelocity(id: number, velocity: Vec3): boolean;
    /**
     * Starts a projectile's trail and looping sound on every client, not on the server, where it stays inert (no flight,
     * collision, damage or time to live): for a projectile WebAdmin spawned and the script moves. False when the id names
     * no projectile.
     */
    activateProjectile(id: number): boolean;
}

/** An object as the game server sees it now (API 2): its root object. */
interface GameObject {
    /** The root object's networkable id. */
    readonly id: number;
    readonly template: string;
    readonly position: Vec3;
    readonly rotation: Rot;
    readonly velocity: Vec3;
}

declare const script: Script;
declare const game: Game;
