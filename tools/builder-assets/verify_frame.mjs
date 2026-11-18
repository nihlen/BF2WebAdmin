// Second half of the frame check (README.md, "Verification"): places each template GLB's vertices with three.js the
// way the README says a frontend does, and measures how far each lands from the nearest vertex of the same object
// in the level's map.glb (BF2v2 build_map.py: world = R(yaw, pitch, roll) @ local + position in BF2 space).
//
//     node tools/builder-assets/verify_frame.mjs <scratch>/frame-check.json
//
// Uses the web UI's three.js (src/BF2WebAdmin.Web/node_modules, after npm ci there).
import { readFileSync } from 'node:fs'
import { Euler, MathUtils, Object3D, Vector3 } from '../../src/BF2WebAdmin.Web/node_modules/three/build/three.module.js'

const objects = JSON.parse(readFileSync(process.argv[2], 'utf8'))

/** The README's transform: BF2 position (x, h, z) -> (x, h, -z); rotation (yaw, pitch, roll) -> Euler(-pitch, -yaw, roll, 'YXZ') */
function place(pos, rot, signs = [1, 1, 1]) {
  const o = new Object3D()
  o.position.set(pos[0], pos[1], -pos[2])
  o.rotation.copy(new Euler(
    -signs[1] * MathUtils.degToRad(rot[1]),
    -signs[0] * MathUtils.degToRad(rot[0]),
    signs[2] * MathUtils.degToRad(rot[2]),
    'YXZ',
  ))
  o.updateMatrixWorld(true)
  return o.matrixWorld
}

/** Nearest-vertex distances with a uniform grid over the map's vertices */
function nearest(points, cloud, cell = 1) {
  const grid = new Map()
  const key = (x, y, z) => `${x},${y},${z}`
  for (let i = 0; i < cloud.length; i += 3) {
    const k = key(Math.floor(cloud[i] / cell), Math.floor(cloud[i + 1] / cell), Math.floor(cloud[i + 2] / cell))
    if (!grid.has(k)) grid.set(k, [])
    grid.get(k).push(i)
  }
  return points.map((p) => {
    const cx = Math.floor(p.x / cell), cy = Math.floor(p.y / cell), cz = Math.floor(p.z / cell)
    let best = Infinity
    for (let r = 0; r < 64 && (best === Infinity || best > (r - 1) * cell); r++) {
      for (let dx = -r; dx <= r; dx++) for (let dy = -r; dy <= r; dy++) for (let dz = -r; dz <= r; dz++) {
        if (Math.max(Math.abs(dx), Math.abs(dy), Math.abs(dz)) !== r) continue
        for (const i of grid.get(key(cx + dx, cy + dy, cz + dz)) ?? []) {
          best = Math.min(best, Math.hypot(cloud[i] - p.x, cloud[i + 1] - p.y, cloud[i + 2] - p.z))
        }
      }
    }
    return best
  })
}

function stats(d) {
  const s = [...d].sort((a, b) => a - b)
  return { mean: s.reduce((a, b) => a + b, 0) / s.length, p95: s[Math.floor(s.length * 0.95)], max: s[s.length - 1] }
}

function bounds(points) {
  const min = new Vector3(Infinity, Infinity, Infinity), max = new Vector3(-Infinity, -Infinity, -Infinity)
  for (const p of points) { min.min(p); max.max(p) }
  return { min, max }
}

const cm = (v) => (v * 100).toFixed(1)
let worst = 0
const rows = []
for (const o of objects) {
  const local = []
  for (let i = 0; i < o.local.length; i += 3) local.push(new Vector3(o.local[i], o.local[i + 1], o.local[i + 2]))
  const placed = local.map((v) => v.clone().applyMatrix4(place(o.pos, o.rot)))
  const d = stats(nearest(placed, o.map))
  worst = Math.max(worst, d.max)
  // The map's vertices that matched, as bounds, against the placed GLB's bounds
  const b = bounds(placed)
  const near = []
  for (let i = 0; i < o.map.length; i += 3) {
    const p = new Vector3(o.map[i], o.map[i + 1], o.map[i + 2])
    if (p.x >= b.min.x - 0.1 && p.y >= b.min.y - 0.1 && p.z >= b.min.z - 0.1 && p.x <= b.max.x + 0.1 && p.y <= b.max.y + 0.1 && p.z <= b.max.z + 0.1) near.push(p)
  }
  const mb = bounds(near)
  const boundsDiff = Math.max(...['x', 'y', 'z'].flatMap((a) => [Math.abs(mb.min[a] - b.min[a]), Math.abs(mb.max[a] - b.max[a])]))
  // Controls: the same with one angle's sign flipped must miss
  const flips = [[-1, 1, 1], [1, -1, 1], [1, 1, -1]].map((s, k) => (o.rot[k] === 0 ? null : stats(nearest(local.map((v) => v.clone().applyMatrix4(place(o.pos, o.rot, s))), o.map)).max))
  rows.push({ name: o.name, rot: o.rot.join('/'), vertices: local.length, mean_cm: cm(d.mean), p95_cm: cm(d.p95), max_cm: cm(d.max), bounds_cm: cm(boundsDiff), flipped_yaw_m: flips[0]?.toFixed(2) ?? '-', flipped_pitch_m: flips[1]?.toFixed(2) ?? '-', flipped_roll_m: flips[2]?.toFixed(2) ?? '-' })
}
console.table(rows)
console.log(`worst vertex distance ${cm(worst)} cm over ${objects.length} objects`)
