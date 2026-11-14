// Dance: holds each player where it is and turns it smoothly, one full turn of yaw a second with a gentle pitch and
// roll wobble, then puts it back level with the yaw it had. A player that dies or leaves is dropped.
//
// Arguments: { players: number[], seconds: number }. Finishes with { players: n }, the number still dancing at the end.

interface DanceArgs {
    players: number[];
    seconds: number;
}

interface Dancer {
    id: number;
    position: Vec3;
    yaw: number;
}

const turnsPerSecond = 1;
const wobbleDegrees = 15;

function readArgs(value: unknown): DanceArgs {
    const args = value as Partial<DanceArgs> | null;
    if (!args || !Array.isArray(args.players) || typeof args.seconds !== "number" || !Number.isFinite(args.seconds))
        throw new TypeError("dance: the arguments are { players: number[], seconds: number }");
    return { players: args.players, seconds: args.seconds };
}

/** An angle in degrees as -180 to 180. */
function wrap(degrees: number): number {
    return ((degrees + 180) % 360 + 360) % 360 - 180;
}

const args = readArgs(script.args);
const ticks = Math.max(1, Math.round(args.seconds * game.tickRate));
const dancers: Dancer[] = [];
const roots = new Set<number>();

for (const id of args.players) {
    const player = game.player(id);
    if (!player || !player.alive || !player.position || !player.rotation)
        continue;

    // The crew of one vehicle shares its root object: it dances once
    if (player.rootId !== 0) {
        if (roots.has(player.rootId))
            continue;
        roots.add(player.rootId);
    }

    dancers.push({ id, position: player.position, yaw: player.rotation[0] });
}

let elapsed = 0;

script.onTick(() => {
    elapsed++;
    const seconds = elapsed / game.tickRate;
    const yaw = 360 * turnsPerSecond * seconds;
    const pitch = wobbleDegrees * Math.sin(2 * Math.PI * seconds);
    const roll = wobbleDegrees * Math.sin(3 * Math.PI * seconds);

    for (let i = dancers.length - 1; i >= 0; i--) {
        const dancer = dancers[i]!;
        const player = game.player(dancer.id);
        if (!player || !player.alive || !game.teleport(dancer.id, dancer.position, [wrap(dancer.yaw + yaw), pitch, roll]))
            dancers.splice(i, 1);
    }

    if (elapsed >= ticks || dancers.length === 0)
        script.finish({ players: dancers.length });
});

// At the end, and when stopped: back in place, level, with the yaw each had
script.onStop(() => {
    for (const dancer of dancers)
        game.teleport(dancer.id, dancer.position, [dancer.yaw, 0, 0]);
});
