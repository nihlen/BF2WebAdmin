import assert from 'node:assert/strict';
import test from 'node:test';
import { start } from './harness.mjs';

// A missile flying 10 m east per frame, level, and a helicopter turning from yaw 170 to -170 (through 180)
const frames = 4;
const missileFrames = [0, 100, 0, 90, 0, 0, 10, 100, 0, 90, 0, 0, 20, 100, 0, 90, 0, 0, 30, 100, 0, 90, 0, 0];
const heliFrames = [50, 120, 50, 170, 0, 0, 50, 120, 50, 180, 0, 0, 50, 120, 50, -170, 0, 0, 50, 120, 50, -170, 0, 0];

const objects = () => [
    { id: 1852, template: 'agm114_hellfire_tv', position: [0, 0, 0], rotation: [0, 0, 0], projectile: true },
    { id: 2065, template: 'ahe_ah1z', position: [0, 0, 0], rotation: [0, 0, 0] },
];
const args = (overrides = {}) => ({
    speed: 1, preroll: 2, hold: 4, frames,
    objects: [{ id: 1852, missile: true, f: missileFrames }, { id: 2065, f: heliFrames }],
    ...overrides,
});

test('holds the first frame for the preroll, then plays a frame a tick and starts the trail with the flight', () => {
    const run = start('replay', args(), [], objects(), 2);
    run.ticks(2);
    const missile = run.objects.get(1852);
    assert.deepEqual(missile.moves.map(m => m.position[0]), [0, 0]);
    assert.deepEqual(run.activated, []);

    run.ticks(3);
    assert.deepEqual(missile.moves.map(m => m.position[0]), [0, 0, 10, 20, 30]);
    assert.deepEqual(run.activated, [1852]);
    assert.deepEqual(missile.moves[3].rotation, [90, 0, 0]);
});

test('at a quarter speed the poses are blended between frames, yaw the short way round', () => {
    const run = start('replay', args({ speed: 0.25, preroll: 0 }), [], objects(), 2);
    run.ticks(4);
    assert.deepEqual(run.objects.get(1852).moves.map(m => m.position[0]), [2.5, 5, 7.5, 10]);
    run.ticks(2);
    const heli = run.objects.get(2065).moves;
    // Frame 1.5: halfway from 180 to -170, the short way: 185, which is -175
    assert.ok(Math.abs(((heli[5].rotation[0] % 360) + 360) % 360 - 185) < 1e-9, `yaw ${heli[5].rotation[0]}`);
});

test('holds the impact for the hold ticks, a millimetre up every other tick, then finishes', () => {
    const run = start('replay', args(), [], objects(), 2);
    run.ticks(30);
    assert.equal(run.ended, 'finished');
    assert.deepEqual(run.result, { played: frames, activated: true });
    const held = run.objects.get(1852).moves.slice(5).map(m => m.position);
    assert.equal(held.length, 4);
    assert.ok(held.every(p => p[0] === 30));
    assert.deepEqual(held.map(p => Math.round((p[1] - 100) * 1000)), [1, 0, 1, 0]);
});

test('an object that is gone is left out; a missile that cannot be activated is logged', () => {
    const things = objects();
    things[0].projectile = false;
    const run = start('replay', args(), [], things, 2);
    run.ticks(3);
    assert.equal(run.logs.length, 1);
    run.objects.delete(2065);
    run.ticks(30);
    assert.ok(run.logs.some(l => l.includes('2065 is gone')));
    assert.deepEqual(run.result, { played: frames, activated: false });
});

test('bad arguments are refused at the start', () => {
    assert.throws(() => start('replay', args({ speed: 0 }), [], objects(), 2), /speed/);
    assert.throws(() => start('replay', args({ objects: [{ id: 1852, f: [1, 2, 3] }] }), [], objects(), 2), /six numbers/);
    assert.throws(() => start('replay', null, [], objects(), 2), /frames/);
});
