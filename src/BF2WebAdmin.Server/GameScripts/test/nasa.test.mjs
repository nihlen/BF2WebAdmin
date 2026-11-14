import assert from 'node:assert/strict';
import test from 'node:test';
import { player, start } from './harness.mjs';

const heli = (id, height, velocity = [20, 5, -10], overrides = {}) =>
    player(id, { seated: true, seat: 0, rootId: 77, vehicle: 'ahe_ah1z', position: [100, height, 200], velocity, ...overrides });

test('a player below the ceiling is left alone', () => {
    const run = start('nasa', { altitude: 300 }, [heli(1, 299.9)]);
    run.ticks(30);
    assert.deepEqual(run.calls, []);
});

test('above the ceiling it sinks back, faster the higher it is, and keeps its horizontal speed', () => {
    const run = start('nasa', { altitude: 300 }, [heli(1, 305)]);
    run.ticks();
    assert.deepEqual(run.calls, [{ call: 'setVelocity', id: 1, velocity: [20, -10, -10] }]);

    const high = start('nasa', { altitude: 300 }, [heli(1, 400)]);
    high.ticks();
    assert.deepEqual(high.calls, [{ call: 'setVelocity', id: 1, velocity: [20, -30, -10] }]);
});

test('a player that already sinks fast enough keeps its speed, and nothing is ever teleported', () => {
    const run = start('nasa', { altitude: 300 }, [heli(1, 305, [0, -25, 0])]);
    run.ticks(10);
    assert.deepEqual(run.calls, []);

    const climbing = start('nasa', { altitude: 300 }, [heli(1, 301, [0, 10, 0])]);
    climbing.ticks(10);
    assert.ok(climbing.calls.length > 0);
    assert.ok(climbing.calls.every(c => c.call === 'setVelocity'));
});

test('the crew of one vehicle is handled once a tick', () => {
    const run = start('nasa', { altitude: 300 }, [heli(1, 310), heli(2, 310, undefined, { seat: 1 })]);
    run.ticks();
    assert.equal(run.calls.length, 1);
});

test('players without a networkable root are each handled', () => {
    const run = start('nasa', { altitude: 10 }, [player(1, { rootId: 0, position: [0, 20, 0] }), player(2, { rootId: 0, position: [0, 20, 0] })]);
    run.ticks();
    assert.deepEqual(run.calls.map(c => c.id), [1, 2]);
});

test('the dead are skipped', () => {
    const run = start('nasa', { altitude: 300 }, [player(1, { alive: false, position: null, rotation: null, velocity: null })]);
    run.ticks(3);
    assert.deepEqual(run.calls, []);
});

test('a message changes the altitude', () => {
    const run = start('nasa', { altitude: 300 }, [heli(1, 280)]);
    run.ticks();
    assert.deepEqual(run.calls, []);

    run.message({ altitude: 250 });
    run.ticks();
    assert.deepEqual(run.calls, [{ call: 'setVelocity', id: 1, velocity: [20, -30, -10] }]);
});

test('arguments or a message without an altitude are an error', () => {
    assert.throws(() => start('nasa', {}), { name: 'TypeError' });
    assert.throws(() => start('nasa', null), { name: 'TypeError' });
    const run = start('nasa', { altitude: 300 });
    assert.throws(() => run.message({ altitude: 'high' }), { name: 'TypeError' });
});
