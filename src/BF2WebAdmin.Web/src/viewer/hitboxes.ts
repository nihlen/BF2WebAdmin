import {
  BufferGeometry,
  CircleGeometry,
  DoubleSide,
  Float32BufferAttribute,
  Group,
  Matrix4,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  Quaternion,
  RingGeometry,
  Triangle,
  Vector3,
} from 'three'

// The helicopters' hitboxes: the collision meshes a TV missile is tested against (public/viewer/models/heli-hitboxes.json,
// built by tools/viewer/build_heli_hitboxes.py from the server's objects). The missile sweeps a single point against the
// projectile layer (coltype 0) of each object (OpenBF2 PointResponsePhysics.cpp:112-145). The hull (collision part 0)
// is tested at the shooter's rewound pose; the child parts (rotors, turret, sensors, wheels, dampers, the Z-10's tail,
// the AH-1Z's Hellfire dummies) where they are now. Child parts are drawn at rest: turret aim, spring travel and blade
// angle are not on the wire, so a rotor is drawn as the volume its blades sweep.

type HitboxJson = {
  meshes: Record<string, { v: number[]; i: number[] }>
  vehicles: Record<string, { parts: PartJson[] }>
}

type PartJson = {
  name: string
  part: number | null
  mesh: string
  rewound: boolean
  pos: [number, number, number]
  /** Row-major 3x3, BF2 frame */
  rot: number[]
  rotor?: { axis: 0 | 1 | 2; radius: number; min: number; max: number }
}

export type HitboxPart = {
  name: string
  geometry: BufferGeometry
  /** The rest transform relative to the vehicle, in the viewer's frame */
  position: Vector3
  quaternion: Quaternion
  rotor?: PartJson['rotor']
}

export type VehicleHitbox = { hull: HitboxPart; parts: HitboxPart[] }

export type HitboxSet = Map<string, VehicleHitbox>

/** BF2 is left-handed: the viewer mirrors z, so a BF2 rotation matrix M becomes S M S with S = diag(1, 1, -1) */
function rotationToQuaternion(m: number[]) {
  const s = [1, 1, -1]
  const e = (i: number, j: number) => m[i * 3 + j] * s[i] * s[j]
  const matrix = new Matrix4().set(e(0, 0), e(0, 1), e(0, 2), 0, e(1, 0), e(1, 1), e(1, 2), 0, e(2, 0), e(2, 1), e(2, 2), 0, 0, 0, 0, 1)
  return new Quaternion().setFromRotationMatrix(matrix)
}

function meshGeometry(mesh: { v: number[]; i: number[] }) {
  const positions = new Float32Array(mesh.v.length)
  for (let k = 0; k < mesh.v.length; k += 3) {
    positions[k] = mesh.v[k]
    positions[k + 1] = mesh.v[k + 1]
    positions[k + 2] = -mesh.v[k + 2]
  }
  const geometry = new BufferGeometry()
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3))
  geometry.setIndex(mesh.i)
  geometry.computeVertexNormals()
  return geometry
}

export async function loadHitboxes(url: string): Promise<HitboxSet> {
  const response = await fetch(url)
  if (!response.ok || !response.headers.get('content-type')?.includes('json')) throw new Error(`${url}: ${response.status}`)
  const json = (await response.json()) as HitboxJson
  const geometries = new Map<string, BufferGeometry>()
  const geometryOf = (key: string) => {
    let g = geometries.get(key)
    if (!g) geometries.set(key, (g = meshGeometry(json.meshes[key])))
    return g
  }
  const set: HitboxSet = new Map()
  for (const [template, vehicle] of Object.entries(json.vehicles)) {
    const parts = vehicle.parts.map(
      (p): HitboxPart => ({
        name: p.name,
        geometry: geometryOf(p.mesh),
        position: new Vector3(p.pos[0], p.pos[1], -p.pos[2]),
        quaternion: rotationToQuaternion(p.rot),
        rotor: p.rotor,
      }),
    )
    // The generator writes the hull (part 0, the only rewound part) first
    if (vehicle.parts[0]?.rewound) set.set(template.toLowerCase(), { hull: parts[0], parts: parts.slice(1) })
  }
  return set
}

function material(color: string, opacity: number) {
  return new MeshLambertMaterial({ color, emissive: color, emissiveIntensity: 0.5, transparent: true, opacity, depthWrite: false, side: DoubleSide })
}

/**
 * The disc a rotor's blades sweep, in the rotor's frame: thin and faint, with a clearer rim, at the middle of the blades'
 * extent along the spin axis
 */
function rotorSweep(rotor: NonNullable<HitboxPart['rotor']>, color: string, opacity: number) {
  const disc = new Group()
  disc.add(new Mesh(new CircleGeometry(rotor.radius, 64), material(color, opacity * 0.25)))
  disc.add(new Mesh(new RingGeometry(rotor.radius * 0.97, rotor.radius, 64), material(color, opacity * 0.9)))
  const centre = (rotor.min + rotor.max) / 2
  // The circle lies in its XY plane; the JSON axis is in BF2's frame (z mirrored)
  if (rotor.axis === 0) {
    disc.rotation.y = Math.PI / 2
    disc.position.x = centre
  } else if (rotor.axis === 1) {
    disc.rotation.x = -Math.PI / 2
    disc.position.y = centre
  } else disc.position.z = -centre
  return disc
}

/**
 * A vehicle's hitboxes as two groups, posed independently: `hull` (part 0, tested at the rewound pose) and `parts`
 * (the child parts at rest, tested at the current pose). Geometry is shared; dispose() frees the materials only.
 */
export class HitboxModel {
  readonly hull = new Group()
  readonly parts = new Group()

  constructor(hitbox: VehicleHitbox, hullColor: string | null, partsColor: string | null, opacity = 0.45) {
    if (hullColor) this.hull.add(partMesh(hitbox.hull, material(hullColor, opacity)))
    if (partsColor) {
      for (const part of hitbox.parts) {
        const holder = new Group()
        holder.position.copy(part.position)
        holder.quaternion.copy(part.quaternion)
        holder.add(part.rotor ? rotorSweep(part.rotor, partsColor, opacity) : partMesh(part, material(partsColor, opacity)))
        this.parts.add(holder)
      }
    }
    for (const group of [this.hull, this.parts]) group.traverse((o) => (o.renderOrder = 3))
  }
}

/** A part's own mesh (for a rotor: its blades) in the part's frame, to place at a collision's tested part pose */
export function partMesh(part: HitboxPart, mat: MeshLambertMaterial | string, opacity = 0.75) {
  const mesh = new Mesh(part.geometry, typeof mat === 'string' ? material(mat, opacity) : mat)
  mesh.userData.sharedGeometry = true
  return mesh
}

/**
 * The triangles of a part's mesh within `radius` of a point in the part's frame (the patch around the hit), as a mesh of
 * their own drawn over everything. Collision faces can be long slivers, so a single face says little
 */
export function facesNear(part: HitboxPart, local: Vector3, radius: number, color: string) {
  const position = part.geometry.getAttribute('position')
  const index = part.geometry.getIndex()
  if (!index) return null
  const tri = new Triangle()
  const closest = new Vector3()
  const corners: number[] = []
  for (let i = 0; i < index.count; i += 3) {
    tri.setFromAttributeAndIndices(position, index.getX(i), index.getX(i + 1), index.getX(i + 2))
    tri.closestPointToPoint(local, closest)
    if (closest.distanceTo(local) > radius) continue
    for (const v of [tri.a, tri.b, tri.c]) corners.push(v.x, v.y, v.z)
  }
  if (!corners.length) return null
  const geometry = new BufferGeometry()
  geometry.setAttribute('position', new Float32BufferAttribute(corners, 3))
  return new Mesh(geometry, new MeshBasicMaterial({ color, side: DoubleSide, depthTest: false, transparent: true, opacity: 0.85 }))
}

/** The vehicle's part a collision names (the hull for the vehicle's own template) */
export function findPart(hitbox: VehicleHitbox, template: string, partTemplate: string) {
  const name = partTemplate.toLowerCase()
  if (name === template.toLowerCase()) return hitbox.hull
  return hitbox.parts.find((p) => p.name.toLowerCase() === name)
}
