// .nasa: a soft altitude ceiling. Whatever is above the altitude is made to sink back towards it, faster the higher
// it is, and keeps its horizontal speed. Nothing is teleported, so a helicopter is not stopped dead at the ceiling.
//
// Arguments and messages: { altitude: number } (the world height in metres; a message changes it).

interface NasaArgs {
    altitude: number;
}

// Sink speed per metre above the ceiling (1/s), and the fastest sink speed (m/s)
const pull = 2;
const maxSinkSpeed = 30;

function readAltitude(value: unknown): number {
    const altitude = (value as Partial<NasaArgs> | null)?.altitude;
    if (typeof altitude !== "number" || !Number.isFinite(altitude))
        throw new TypeError("nasa: altitude must be a number");
    return altitude;
}

let altitude = readAltitude(script.args);

// The vehicles handled in this tick: its crew share one root object
const handled = new Set<number>();

script.onMessage(message => {
    altitude = readAltitude(message);
});

script.onTick(() => {
    handled.clear();
    for (const id of game.players()) {
        const player = game.player(id);
        if (!player || !player.alive || !player.position || !player.velocity)
            continue;

        const above = player.position[1] - altitude;
        if (above <= 0)
            continue;

        if (player.rootId !== 0) {
            if (handled.has(player.rootId))
                continue;
            handled.add(player.rootId);
        }

        const sink = Math.max(-above * pull, -maxSinkSpeed);
        if (player.velocity[1] > sink)
            game.setVelocity(id, [player.velocity[0], sink, player.velocity[2]]);
    }
});
