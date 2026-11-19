// .replay: plays a recorded kill shot with objects WebAdmin spawned for it (the missile and, in a match, the
// helicopters) and holds everything at the impact. Needs API 2 (objects by networkable id).
//
// Arguments: { speed, preroll, hold, frames, objects: [{ id, missile?, f }] }. `frames` is the number of recorded frames,
// one game tick apart; each object's `f` has six numbers per frame: x, y, z, yaw, pitch, roll. The replay holds the first
// frame for `preroll` ticks (clients get the spawned objects and blend from there), plays the frames at `speed` (1 is
// real time), then holds the last one (the impact) for `hold` ticks. The missile's trail starts when it starts moving.
// It finishes with { played, activated }; WebAdmin removes the objects afterwards.

interface ReplayObject {
    id: number;
    missile?: boolean;
    f: number[];
}

interface ReplayArgs {
    speed: number;
    preroll: number;
    hold: number;
    frames: number;
    objects: ReplayObject[];
}

function readArgs(value: unknown): ReplayArgs {
    const a = value as Partial<ReplayArgs> | null;
    const number = (x: unknown, min: number, max: number, what: string): number => {
        if (typeof x !== "number" || !Number.isFinite(x) || x < min || x > max)
            throw new TypeError(`replay: ${what} must be a number from ${min} to ${max}`);
        return x;
    };
    const frames = Math.floor(number(a?.frames, 1, 1000, "frames"));
    const objects = a?.objects;
    if (!Array.isArray(objects) || objects.length === 0 || objects.length > 16)
        throw new TypeError("replay: objects must be 1 to 16 objects");
    for (const o of objects) {
        if (typeof o?.id !== "number" || !Array.isArray(o.f) || o.f.length !== frames * 6)
            throw new TypeError("replay: each object needs an id and six numbers per frame");
    }
    return {
        speed: number(a?.speed, 0.05, 4, "speed"),
        preroll: Math.floor(number(a?.preroll, 0, 300, "preroll")),
        hold: Math.floor(number(a?.hold, 0, 3000, "hold")),
        frames,
        objects,
    };
}

const args = readArgs(script.args);

// The angle from a to b the short way round, in degrees
function turn(a: number, b: number): number {
    return ((((b - a) % 360) + 540) % 360) - 180;
}

/** The pose of an object between two frames: position and pitch straight, yaw and roll the short way round. */
function pose(f: number[], frame: number): [Vec3, Rot] {
    const last = args.frames - 1;
    const i = Math.min(Math.max(Math.floor(frame), 0), last);
    const j = Math.min(i + 1, last);
    const u = i === j ? 0 : Math.min(Math.max(frame - i, 0), 1);
    const at = (k: number, n: number) => f[k * 6 + n] ?? 0;
    const lerp = (n: number) => at(i, n) + (at(j, n) - at(i, n)) * u;
    const angle = (n: number) => at(i, n) + turn(at(i, n), at(j, n)) * u;
    return [[lerp(0), lerp(1), lerp(2)], [angle(3), lerp(4), angle(5)]];
}

const live = new Set(args.objects.map(o => o.id));
let ticks = 0;
let started = false;
let activated = false;

/** Puts every object that still exists at the frame; `nudge` moves it up a millimetre (see the hold below). */
function place(frame: number, nudge: number) {
    for (const o of args.objects) {
        if (!live.has(o.id))
            continue;
        const [position, rotation] = pose(o.f, frame);
        position[1] += nudge;
        if (!game.teleportObject(o.id, position, rotation)) {
            live.delete(o.id);
            script.log("replay: object", o.id, "is gone");
        }
    }
}

script.onTick(() => {
    ticks++;
    if (ticks <= args.preroll) {
        place(0, 0);
        return;
    }

    const frame = (ticks - args.preroll) * args.speed;
    if (!started) {
        // The trail starts with the flight, not during the preroll's standstill
        started = true;
        for (const o of args.objects) {
            if (!o.missile || !live.has(o.id))
                continue;
            if (game.activateProjectile(o.id))
                activated = true;
            else
                script.log("replay: missile", o.id, "was not activated");
        }
    }

    if (frame < args.frames - 1) {
        place(frame, 0);
        return;
    }

    // The hold: a pose that does not change is not sent, and a client goes on moving the object at its last speed for a
    // moment; a millimetre up every other tick keeps the updates coming with no speed in them
    const held = ticks - args.preroll - Math.ceil((args.frames - 1) / args.speed);
    if (held > args.hold) {
        script.finish({ played: args.frames, activated });
        return;
    }
    place(args.frames - 1, held % 2 === 0 ? 0 : 0.001);
});
