import { Euler, MathUtils, Quaternion, Vector3 } from 'three'

// BF2 positions are [x, height, z] (z north) and rotations [yaw, pitch, roll] in degrees, as the console's
// object.absolutePosition and object.rotation take them. three.js mirrors z: BF2 (x, h, z) is (x, h, -z), as in the
// 3D viewer (viewer/coords.ts) and the map and template GLBs (tools/builder-assets/README.md).

export type Vec = number[]

export function toWorld(p: Vec, out = new Vector3()) {
  return out.set(p[0], p[1], -p[2])
}

export function fromWorld(v: Vector3): Vec {
  return [round(v.x, 3), round(v.y, 3), round(-v.z, 3)]
}

const _euler = new Euler(0, 0, 0, 'YXZ')

/**
 * A BF2 rotation as the rotation of a template's model. BF2 applies yaw about the vertical axis, then pitch, then roll
 * (Euler order YXZ in its own left-handed frame); mirroring z turns yaw and pitch around.
 */
export function toQuaternion(r: Vec, out = new Quaternion()) {
  _euler.set(MathUtils.degToRad(-r[1]), MathUtils.degToRad(-r[0]), MathUtils.degToRad(r[2]), 'YXZ')
  return out.setFromEuler(_euler)
}

export function fromQuaternion(q: Quaternion): Vec {
  _euler.setFromQuaternion(q, 'YXZ')
  return [normalizeDegrees(-MathUtils.radToDeg(_euler.y)), normalizeDegrees(-MathUtils.radToDeg(_euler.x)), normalizeDegrees(MathUtils.radToDeg(_euler.z))]
}

/** -180 to 180, rounded to a hundredth, and -0 as 0 */
export function normalizeDegrees(d: number) {
  let v = ((((d + 180) % 360) + 360) % 360) - 180
  v = round(v, 2)
  return v === 0 ? 0 : v
}

function round(v: number, digits: number) {
  const f = 10 ** digits
  const r = Math.round(v * f) / f
  return r === 0 ? 0 : r
}
