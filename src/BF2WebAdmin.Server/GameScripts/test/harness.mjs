// Runs a built script (dist/<name>.js) against a fake `script` and `game`, as the game server's module would: strict
// global code, the handlers it registers called tick by tick, and the stop handlers before it ends. The globals the
// module's engine does not have are removed, so a script that uses one fails here too.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const missing = ['Date', 'RegExp', 'eval', 'Function', 'Proxy', 'WeakRef', 'FinalizationRegistry', 'BigInt', 'Atomics',
    'SharedArrayBuffer', 'WebAssembly', 'console', 'setTimeout', 'setInterval', 'queueMicrotask'];

/** A living player on foot at the origin; override what the test needs. */
export function player(id, overrides = {}) {
    return {
        id, name: `player${id}`, team: 1, alive: true, bot: false, seated: false, seat: -1, rootId: 1000 + id, vehicle: 'us_light_soldier',
        position: [0, 0, 0], rotation: [0, 0, 0], velocity: [0, 0, 0], ...overrides,
    };
}

/**
 * Starts dist/<name>.js. `players` is the world: the test changes it between ticks, and the script's teleports and
 * speed changes are applied to it and recorded in `calls`.
 */
export function start(name, args, players = []) {
    const world = new Map(players.map(p => [p.id, p]));
    const handlers = { tick: [], message: [], stop: [] };
    const run = { world, calls: [], emitted: [], logs: [], ended: null, result: undefined, tick: 0 };

    const end = reason => {
        if (run.ended)
            return;
        for (const handler of handlers.stop)
            handler(reason);
        run.ended = reason;
    };

    let finishing = false;
    const call = fn => {
        if (run.ended)
            throw new Error(`the script has ended (${run.ended})`);
        fn();
        if (finishing)
            end('finished');
    };

    const copy = p => ({ ...p, position: p.position && [...p.position], rotation: p.rotation && [...p.rotation], velocity: p.velocity && [...p.velocity] });
    const root = id => {
        const p = world.get(id);
        return p && p.alive ? p : null;
    };
    // The script's values belong to its own realm: recorded as plain data of this one, for deepEqual
    const record = entry => { run.calls.push(JSON.parse(JSON.stringify(entry))); };
    // The crew of a vehicle shares its root object
    const sharing = p => [...world.values()].filter(other => other === p || (other.alive && p.rootId !== 0 && other.rootId === p.rootId));

    const script = Object.freeze({
        api: 1,
        name,
        args: args === undefined ? null : JSON.parse(JSON.stringify(args)),
        onTick: fn => { handlers.tick.push(fn); },
        onMessage: fn => { handlers.message.push(fn); },
        onStop: fn => { handlers.stop.push(fn); },
        finish: result => { finishing = true; run.result = result === undefined ? undefined : JSON.parse(JSON.stringify(result)); },
        emit: data => { run.emitted.push(JSON.parse(JSON.stringify(data))); },
        log: (...parts) => { run.logs.push(parts.join(' ')); },
    });

    const game = Object.freeze({
        get tick() { return run.tick; },
        tickRate: 30,
        get time() { return run.tick / 30; },
        map: 'dalian_2_v_2',
        players: () => [...world.keys()],
        player: id => (world.has(id) ? copy(world.get(id)) : null),
        teleport: (id, position, rotation) => {
            const p = root(id);
            if (!p)
                return false;
            record({ call: 'teleport', id, position, rotation });
            for (const other of sharing(p)) {
                if (position) { other.position = [...position]; other.velocity = [0, 0, 0]; }
                if (rotation) other.rotation = [...rotation];
            }
            return true;
        },
        setVelocity: (id, velocity) => {
            const p = root(id);
            if (!p)
                return false;
            record({ call: 'setVelocity', id, velocity });
            for (const other of sharing(p))
                other.velocity = [...velocity];
            return true;
        },
        setAngularVelocity: (id, velocity) => {
            if (!root(id))
                return false;
            record({ call: 'setAngularVelocity', id, velocity });
            return true;
        },
        say: (text, to) => { record({ call: 'say', text, player: to }); },
    });

    const context = vm.createContext({ script, game });
    vm.runInContext(`for (const name of ${JSON.stringify(missing)}) delete globalThis[name];`, context);

    const file = new URL(`../dist/${name}.js`, import.meta.url);
    // On one line with the source, so line numbers stay the script's
    call(() => vm.runInContext(`"use strict";${readFileSync(file, 'utf8')}`, context, { filename: `${name}.ts` }));

    /** Runs n ticks (fewer when the script ends). */
    run.ticks = (n = 1) => {
        for (let i = 0; i < n && !run.ended; i++) {
            run.tick++;
            call(() => { for (const handler of handlers.tick) handler(run.tick); });
        }
    };
    run.message = message => call(() => { for (const handler of handlers.message) handler(JSON.parse(JSON.stringify(message))); });
    run.stop = (reason = 'stopped') => end(reason);
    return run;
}
