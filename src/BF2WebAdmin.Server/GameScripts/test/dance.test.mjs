import assert from 'node:assert/strict';
import test from 'node:test';
import { player, start } from './harness.mjs';

const dancer = (id, overrides = {}) => player(id, { position: [10 * id, 150, -5], rotation: [40, 12, -7], ...overrides });
const teleports = run => run.calls.filter(c => c.call === 'teleport');

test('a dancer stays in place and turns smoothly, one turn of yaw a second', () => {
    const run = start('dance', { players: [1], seconds: 5 }, [dancer(1)]);
    run.ticks(30);

    const calls = teleports(run);
    assert.equal(calls.length, 30);
    assert.ok(calls.every(c => c.id === 1));
    assert.ok(calls.every(c => c.position[0] === 10 && c.position[1] === 150 && c.position[2] === -5));

    // 12 degrees of yaw a tick from the yaw it had, and a wobble that stays gentle
    for (let i = 0; i < calls.length; i++) {
        const turned = ((calls[i].rotation[0] - 40 - 12 * (i + 1)) % 360 + 360) % 360;
        assert.ok(Math.min(turned, 360 - turned) < 1e-6, `tick ${i + 1}: yaw ${calls[i].rotation[0]}`);
        assert.ok(calls[i].rotation[0] >= -180 && calls[i].rotation[0] <= 180);
        assert.ok(Math.abs(calls[i].rotation[1]) <= 15 && Math.abs(calls[i].rotation[2]) <= 15);
    }

    // No jumps: pitch and roll move a few degrees a tick at most
    for (let i = 1; i < calls.length; i++) {
        assert.ok(Math.abs(calls[i].rotation[1] - calls[i - 1].rotation[1]) < 5);
        assert.ok(Math.abs(calls[i].rotation[2] - calls[i - 1].rotation[2]) < 5);
    }
});

test('at the end everyone is put back level with the yaw each had, and the script finishes', () => {
    const run = start('dance', { players: [1, 2], seconds: 1 }, [dancer(1), dancer(2, { rotation: [-90, 0, 0] })]);
    run.ticks(100);

    assert.equal(run.ended, 'finished');
    assert.equal(run.tick, 30);
    assert.deepEqual(run.result, { players: 2 });
    assert.deepEqual(teleports(run).slice(-2), [
        { call: 'teleport', id: 1, position: [10, 150, -5], rotation: [40, 0, 0] },
        { call: 'teleport', id: 2, position: [20, 150, -5], rotation: [-90, 0, 0] },
    ]);
    assert.deepEqual(run.world.get(2).rotation, [-90, 0, 0]);
});

test('a stop puts everyone back too', () => {
    const run = start('dance', { players: [1], seconds: 5 }, [dancer(1)]);
    run.ticks(7);
    run.stop();

    assert.equal(run.ended, 'stopped');
    assert.deepEqual(teleports(run).at(-1), { call: 'teleport', id: 1, position: [10, 150, -5], rotation: [40, 0, 0] });
});

test('a player that dies or leaves is dropped and not put back', () => {
    const run = start('dance', { players: [1, 2, 3], seconds: 1 }, [dancer(1), dancer(2), dancer(3)]);
    run.ticks(5);
    run.world.get(2).alive = false;
    run.world.delete(3);
    run.ticks(100);

    assert.deepEqual(run.result, { players: 1 });
    assert.equal(teleports(run).filter(c => c.id !== 1).length, 10);
    assert.equal(teleports(run).at(-1).id, 1);
});

test('the crew of one vehicle dances once, and players that are dead or unknown do not', () => {
    const heli = { seated: true, rootId: 77, vehicle: 'ahe_z10' };
    const run = start('dance', { players: [1, 2, 3, 9], seconds: 1 },
        [dancer(1, { ...heli, seat: 0 }), dancer(2, { ...heli, seat: 1 }), dancer(3, { alive: false, position: null, rotation: null, velocity: null })]);
    run.ticks();
    assert.deepEqual(teleports(run).map(c => c.id), [1]);
});

test('with nobody to dance it finishes on the first tick', () => {
    const run = start('dance', { players: [], seconds: 5 });
    run.ticks(3);
    assert.equal(run.tick, 1);
    assert.deepEqual(run.result, { players: 0 });
    assert.deepEqual(run.calls, []);
});

test('bad arguments are an error', () => {
    assert.throws(() => start('dance', { players: 3, seconds: 5 }), { name: 'TypeError' });
    assert.throws(() => start('dance', { players: [1] }), { name: 'TypeError' });
});
