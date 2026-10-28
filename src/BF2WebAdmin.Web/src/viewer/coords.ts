import { Euler, MathUtils, Quaternion, Vector3 } from 'three'

// Ported from BF2v2's round replay (app/src/components/pages/Match/Replay/track.ts).

/**
 * BF2 is left-handed Y-up with X east and Z north. three.js is right-handed Y-up, so Z is mirrored:
 * BF2 (x, height, z) -> three.js (x, height, -z). Seen from above, east is still right and north is up.
 * The map GLB is built with the same mapping (BF2v2 tools/replay-map/build_map.py).
 */
export function bf2ToWorld(x: number, height: number, z: number, out = new Vector3()) {
  return out.set(x, height, -z)
}

/** A wire vector [x, y, z] in BF2 coordinates as a world position */
export function vecToWorld(v: ArrayLike<number>, out = new Vector3()) {
  return out.set(v[0], v[1], -v[2])
}

const _euler = new Euler(0, 0, 0, 'YXZ')

/**
 * The admin channel's rotation [yaw, pitch, roll] in degrees (Python's getRotation convention) as a model rotation.
 * Models face -Z with +Y up. Yaw 0 = north, 90 = east; positive pitch = nose down; positive roll = bank left
 * (measured from recorded rounds in BF2v2).
 */
export function rotToQuaternion(r: ArrayLike<number>, out = new Quaternion()) {
  _euler.set(MathUtils.degToRad(-r[1]), -MathUtils.degToRad(r[0]), MathUtils.degToRad(r[2]), 'YXZ')
  return out.setFromEuler(_euler)
}

/** A point or direction in an object's BF2 frame (x right, y up, z forward) rotated into the world by the object's rotation */
export function localToWorld(x: number, y: number, z: number, rotation: Quaternion, out = new Vector3()) {
  return out.set(x, y, -z).applyQuaternion(rotation)
}

/** The object's forward (BF2 local +z) in the world */
export const forwardOf = (rotation: Quaternion, out = new Vector3()) => out.set(0, 0, -1).applyQuaternion(rotation)

/** The object's up (BF2 local +y) in the world */
export const upOf = (rotation: Quaternion, out = new Vector3()) => out.set(0, 1, 0).applyQuaternion(rotation)
