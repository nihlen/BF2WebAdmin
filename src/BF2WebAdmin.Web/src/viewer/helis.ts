import {
  BoxGeometry,
  BufferGeometry,
  CanvasTexture,
  CircleGeometry,
  Color,
  CylinderGeometry,
  DoubleSide,
  Float32BufferAttribute,
  Group,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  MeshPhongMaterial,
  MeshStandardMaterial,
  Matrix4,
  Object3D,
  Quaternion,
  SRGBColorSpace,
  Vector3,
  type Material,
} from 'three'

// Low-poly AH-1Z and Z-10 at real size (metres). Models face -Z with +Y up, and the origin is on the rotor mast
// axis at about fuselage centre height, which is roughly where BF2 puts the vehicle position.

/** A fuselage cross-section: z position, bottom and top y, and half width */
type Station = [z: number, bottom: number, top: number, halfWidth: number]

/**
 * Lofts a closed body through cross-sections. `squareness` > 1 makes the sections boxier (faceted Z-10),
 * 1 gives octagons/ellipses (AH-1Z).
 */
function loft(stations: Station[], sides = 8, squareness = 1, xOffset = 0) {
  const ring = (s: Station) => {
    const [z, bottom, top, hw] = s
    const cy = (bottom + top) / 2
    const hh = (top - bottom) / 2
    const points: Vector3[] = []
    for (let k = 0; k < sides; k++) {
      const a = (k / sides) * Math.PI * 2 + Math.PI / sides
      const c = Math.cos(a)
      const sn = Math.sin(a)
      const e = 1 / squareness
      points.push(new Vector3(xOffset + hw * Math.sign(c) * Math.abs(c) ** e, cy + hh * Math.sign(sn) * Math.abs(sn) ** e, z))
    }
    return points
  }

  const rings = stations.map(ring)
  const positions: number[] = []
  const tri = (a: Vector3, b: Vector3, c: Vector3) => positions.push(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z)

  for (let i = 0; i < rings.length - 1; i++) {
    const r0 = rings[i]
    const r1 = rings[i + 1]
    for (let k = 0; k < sides; k++) {
      const k1 = (k + 1) % sides
      // Stations go from front (-z) to back (+z); this winding faces outwards
      tri(r0[k], r0[k1], r1[k1])
      tri(r0[k], r1[k1], r1[k])
    }
  }
  // End caps
  const first = rings[0]
  const last = rings[rings.length - 1]
  const centre = (r: Vector3[]) => r.reduce((acc, p) => acc.add(p), new Vector3()).divideScalar(r.length)
  const c0 = centre(first)
  const c1 = centre(last)
  for (let k = 0; k < sides; k++) {
    const k1 = (k + 1) % sides
    tri(c0, first[k1], first[k])
    tri(c1, last[k], last[k1])
  }

  const geometry = new BufferGeometry()
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3))
  geometry.computeVertexNormals()
  return geometry
}

const _up = new Vector3(0, 1, 0)

/** A cylinder between two points (struts, skids, tubes) */
function tube(a: Vector3, b: Vector3, radius: number, material: Material, segments = 6) {
  const dir = new Vector3().subVectors(b, a)
  const mesh = new Mesh(new CylinderGeometry(radius, radius, dir.length(), segments), material)
  mesh.position.copy(a).addScaledVector(dir, 0.5)
  mesh.quaternion.copy(new Quaternion().setFromUnitVectors(_up, dir.normalize()))
  return mesh
}

function box(w: number, h: number, d: number, material: Material, x: number, y: number, z: number) {
  const mesh = new Mesh(new BoxGeometry(w, h, d), material)
  mesh.position.set(x, y, z)
  return mesh
}

interface Materials {
  body: Material
  dark: Material
  glass: Material
  accent: Material
  blade: Material
}

function materials(teamColor: string, base: string): Materials {
  const team = new Color(teamColor)
  return {
    body: new MeshLambertMaterial({ color: new Color(base).lerp(team, 0.4), flatShading: true }),
    dark: new MeshLambertMaterial({ color: '#2b2d2f', flatShading: true }),
    glass: new MeshPhongMaterial({ color: '#16212b', shininess: 90, specular: '#8fb0cc', flatShading: true }),
    accent: new MeshLambertMaterial({ color: team, emissive: team, emissiveIntensity: 0.3, flatShading: true }),
    blade: new MeshLambertMaterial({ color: '#1f2122' }),
  }
}

/** Main rotor: blades around +Y, plus a faint disc so it reads when the blades blur */
function mainRotor(blades: number, radius: number, chord: number, m: Materials) {
  const rotor = new Group()
  rotor.add(new Mesh(new CylinderGeometry(0.28, 0.28, 0.22, 8), m.dark))
  for (let i = 0; i < blades; i++) {
    const arm = new Group()
    arm.rotation.y = (i / blades) * Math.PI * 2
    arm.add(box(radius, 0.05, chord, m.blade, radius / 2, 0, 0))
    rotor.add(arm)
  }
  const disc = new Mesh(
    new CylinderGeometry(radius, radius, 0.02, 40),
    new MeshBasicMaterial({ color: '#000000', transparent: true, opacity: 0.1, depthWrite: false }),
  )
  rotor.add(disc)
  return rotor
}

/** Tail rotor: blades in the YZ plane, spinning around X */
function tailRotor(blades: number, radius: number, m: Materials, angles?: number[]) {
  const rotor = new Group()
  const list = angles ?? Array.from({ length: blades }, (_, i) => (i / blades) * Math.PI * 2)
  for (const a of list) {
    const arm = new Group()
    arm.rotation.x = a
    arm.add(box(0.04, radius, 0.2, m.blade, 0, radius / 2, 0))
    rotor.add(arm)
  }
  return rotor
}

const blurCanvases = new Map<number, HTMLCanvasElement>()

/**
 * Rotor blur texture like BF2's: a faint disc with a soft smear trailing each blade, fading out towards the hub
 * and with a slightly darker band near the tips
 */
function blurCanvas(blades: number) {
  const cached = blurCanvases.get(blades)
  if (cached) return cached
  const size = 256
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = size
  const ctx = canvas.getContext('2d')!
  const image = ctx.createImageData(size, size)
  const sector = (Math.PI * 2) / blades
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const dx = (x + 0.5) / (size / 2) - 1
      const dy = (y + 0.5) / (size / 2) - 1
      const r = Math.hypot(dx, dy)
      if (r > 1) continue
      // How far behind the nearest blade: on the main rotor disc the blades sweep towards decreasing canvas
      // angle, so the trail is at increasing angle. A sharp leading edge and a long fading smear
      const behind = (((Math.atan2(dy, dx) % sector) + sector) % sector) / sector
      const smear = Math.exp(-behind * 5) + 0.5 * Math.exp(-(1 - behind) * 40)
      const radial = smoothstep(0.08, 0.3, r) * (1 - smoothstep(0.95, 1, r)) * (0.75 + 0.35 * smoothstep(0.75, 0.92, r))
      const alpha = radial * (0.16 + 0.38 * smear)
      const i = (y * size + x) * 4
      image.data[i] = image.data[i + 1] = image.data[i + 2] = 28
      image.data[i + 3] = Math.round(Math.min(alpha, 1) * 255)
    }
  }
  ctx.putImageData(image, 0, 0)
  blurCanvases.set(blades, canvas)
  return canvas
}

function smoothstep(a: number, b: number, x: number) {
  const t = Math.min(Math.max((x - a) / (b - a), 0), 1)
  return t * t * (3 - 2 * t)
}

/** Furthest distance of the rotor's geometry from its spin axis ("y" or "x") */
function rotorRadius(rotor: Object3D, axis: 'x' | 'y') {
  rotor.updateWorldMatrix(true, true)
  const toRotor = new Matrix4().copy(rotor.matrixWorld).invert()
  const m = new Matrix4()
  const v = new Vector3()
  let radius = 0
  rotor.traverse((o) => {
    const mesh = o as Mesh
    const position = mesh.isMesh ? mesh.geometry.getAttribute('position') : undefined
    if (!position) return
    m.multiplyMatrices(toRotor, mesh.matrixWorld)
    for (let i = 0; i < position.count; i++) {
      v.fromBufferAttribute(position, i).applyMatrix4(m)
      radius = Math.max(radius, axis === 'y' ? Math.hypot(v.x, v.z) : Math.hypot(v.y, v.z))
    }
  })
  return radius
}

/** Translucent disc that stands in for a rotor while it spins, placed next to it so it can turn at its own pace */
function blurDisc(rotor: Object3D, axis: 'x' | 'y', blades: number) {
  const radius = rotorRadius(rotor, axis)
  const geometry = new CircleGeometry(radius, 48)
  if (axis === 'y') geometry.rotateX(-Math.PI / 2)
  else geometry.rotateY(Math.PI / 2)
  const texture = new CanvasTexture(blurCanvas(blades))
  texture.colorSpace = SRGBColorSpace
  const disc = new Mesh(geometry, new MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false, side: DoubleSide }))
  disc.position.copy(rotor.position)
  disc.quaternion.copy(rotor.quaternion)
  disc.visible = false
  rotor.parent?.add(disc)
  return disc
}

export class HeliModel {
  readonly root = new Group()
  private mainRotor: Object3D = new Group()
  private tailRotor: Object3D = new Group()
  private mainBlur?: Mesh
  private tailBlur?: Mesh

  /**
   * Uses the converted game model when `template` is given (a node from helis.glb with _mainrotor and
   * _tailrotor children), otherwise builds a simple procedural one.
   */
  /** False hides the rotors (a hull-only hitbox ghost) */
  private rotors = true

  constructor(teamColor: string, isZ10: boolean, template?: Object3D) {
    if (template) this.fromTemplate(template, teamColor)
    else if (isZ10) this.buildZ10(teamColor)
    else this.buildAh1z(teamColor)
    this.mainBlur = blurDisc(this.mainRotor, 'y', isZ10 ? 5 : 4)
    this.tailBlur = blurDisc(this.tailRotor, 'x', 4)
  }

  private fromTemplate(template: Object3D, teamColor: string) {
    const model = template.clone(true)
    model.position.set(0, 0, 0)
    // A light team tint, so the game textures still show
    const tint = new Color('#ffffff').lerp(new Color(teamColor), 0.12)
    model.traverse((o) => {
      const mesh = o as Mesh
      if (!mesh.isMesh) return
      // Geometry is shared between clones (and must survive clearing a round); materials are per team
      mesh.userData.sharedGeometry = true
      const material = (mesh.material as MeshStandardMaterial).clone()
      if (!o.name.includes('glass')) material.color.multiply(tint)
      material.metalness = 0
      mesh.material = material
    })
    this.mainRotor = model.getObjectByName(`${template.name}_mainrotor`) ?? new Group()
    this.tailRotor = model.getObjectByName(`${template.name}_tailrotor`) ?? new Group()
    this.root.add(model)
  }

  /**
   * Spins the rotors - driven by replay time so scrubbing and speed changes stay consistent. While playing, the
   * blades are swapped for blur discs like in BF2 (at a real rotor speed the blades would just strobe); paused, the
   * blades show
   */
  animate(timeMs: number, spinning: boolean) {
    this.mainRotor.rotation.y = timeMs * 0.03
    this.tailRotor.rotation.x = timeMs * 0.1
    this.mainRotor.visible = this.tailRotor.visible = !spinning && this.rotors
    if (this.mainBlur && this.tailBlur) {
      this.mainBlur.visible = this.tailBlur.visible = spinning && this.rotors
      // Turn the blur slowly so the smears shimmer a little instead of standing still
      this.mainBlur.rotation.y = timeMs * 0.004
      this.tailBlur.rotation.x = timeMs * 0.006
    }
  }

  /**
   * Draws the model as a translucent single-colour silhouette: the viewer's hitbox ghosts, red where the target is now
   * and blue where the server tested it. Without rotors the ghost is the hull only (rotors are tested where they are now)
   */
  makeGhost(color: string, opacity: number, rotors: boolean) {
    this.rotors = rotors
    if (!rotors) for (const o of [this.mainRotor, this.tailRotor, this.mainBlur, this.tailBlur]) if (o) o.visible = false
    this.root.traverse((o) => {
      const mesh = o as Mesh
      if (!mesh.isMesh) return
      const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material]
      materials.forEach((m) => m.dispose())
      mesh.material = new MeshLambertMaterial({ color, emissive: color, emissiveIntensity: 0.45, transparent: true, opacity, depthWrite: false })
      mesh.renderOrder = 2
    })
  }

  /** AH-1Z Viper: 17.8 m long overall, 14.6 m 4-blade rotor, narrow tandem fuselage, skids, tail rotor on the left */
  private buildAh1z(teamColor: string) {
    const m = materials(teamColor, '#4b5141')
    const add = (o: Object3D) => this.root.add(o)

    // Fuselage: pointed nose, slim tandem cockpit, engine hump behind the mast, tapering tail boom
    add(
      new Mesh(
        loft([
          [-6.4, -0.35, 0.05, 0.1],
          [-5.8, -0.75, 0.35, 0.38],
          [-4.8, -0.95, 0.7, 0.47],
          [-3.0, -1.05, 0.95, 0.5],
          [-1.2, -1.05, 1.1, 0.56],
          [0.0, -1.0, 1.2, 0.6],
          [1.5, -0.85, 1.05, 0.55],
          [2.6, -0.5, 0.75, 0.38],
          [5.0, -0.12, 0.6, 0.24],
          [7.7, 0.12, 0.55, 0.16],
        ]),
        m.body,
      ),
    )
    // Stepped tandem canopy (gunner in front, pilot behind and higher)
    add(
      new Mesh(
        loft(
          [
            [-5.1, 0.55, 0.65, 0.28],
            [-4.4, 0.6, 1.2, 0.4],
            [-3.5, 0.75, 1.3, 0.42],
            [-3.0, 0.85, 1.5, 0.42],
            [-2.1, 0.9, 1.55, 0.42],
            [-1.4, 0.95, 1.2, 0.38],
          ],
          8,
          1.2,
        ),
        m.glass,
      ),
    )
    // Engine nacelles either side of the mast
    for (const side of [-1, 1]) {
      add(
        new Mesh(
          loft(
            [
              [-1.0, 0.75, 1.15, 0.12],
              [-0.6, 0.65, 1.35, 0.26],
              [1.2, 0.65, 1.35, 0.26],
              [1.9, 0.75, 1.2, 0.18],
            ],
            8,
            1,
            side * 0.6,
          ),
          m.body,
        ),
      )
      add(box(0.3, 0.3, 0.15, m.dark, side * 0.6, 1.0, 1.95)) // exhaust
    }

    // Mast and 4-blade main rotor
    add(new Mesh(new CylinderGeometry(0.14, 0.2, 0.7, 8), m.dark).translateY(1.55))
    this.mainRotor = mainRotor(4, 7.3, 0.53, m)
    this.mainRotor.position.set(0, 1.95, 0)
    add(this.mainRotor)

    // Stub wings with Hellfire racks (outboard) and rocket pods (inboard), wingtip rails
    for (const side of [-1, 1]) {
      add(box(2.3, 0.12, 0.85, m.body, side * 1.65, -0.15, -0.55))
      add(box(0.15, 0.12, 0.9, m.dark, side * 2.85, -0.15, -0.55))
      const pod = new Mesh(new CylinderGeometry(0.2, 0.2, 1.7, 8), m.dark)
      pod.rotation.x = Math.PI / 2
      pod.position.set(side * 1.2, -0.5, -0.6)
      add(pod)
      for (const dx of [-0.13, 0.13]) {
        for (const dy of [-0.55, -0.8]) {
          const missile = new Mesh(new CylinderGeometry(0.09, 0.09, 1.6, 6), m.dark)
          missile.rotation.x = Math.PI / 2
          missile.position.set(side * 2.2 + dx, dy, -0.7)
          add(missile)
        }
      }
    }

    // Chin turret with the 20 mm gun
    add(new Mesh(new CylinderGeometry(0.28, 0.32, 0.35, 8), m.dark).translateZ(-5.0).translateY(-0.95))
    add(tube(new Vector3(0, -1.0, -5.0), new Vector3(0, -1.0, -6.4), 0.05, m.dark))

    // Tail: horizontal stabiliser mid-boom, swept fin, ventral fin, tail rotor on the left (port) side
    add(box(2.1, 0.08, 0.6, m.body, 0, 0.35, 5.6))
    const fin = new Mesh(
      loft(
        [
          [7.1, 0.4, 0.6, 0.06],
          [7.6, 0.45, 2.3, 0.08],
          [8.25, 1.2, 2.45, 0.06],
        ],
        4,
      ),
      m.accent,
    )
    add(fin)
    add(box(0.06, 0.6, 0.5, m.body, 0, -0.05, 7.6))
    this.tailRotor = tailRotor(4, 1.45, m)
    this.tailRotor.position.set(-0.2, 1.9, 7.85)
    add(this.tailRotor)

    // Skids and cross tubes
    for (const side of [-1, 1]) {
      add(tube(new Vector3(side * 1.05, -1.75, -3.3), new Vector3(side * 1.05, -1.75, 1.2), 0.05, m.dark))
      add(tube(new Vector3(side * 1.05, -1.75, -3.3), new Vector3(side * 1.05, -1.55, -3.75), 0.05, m.dark))
      for (const z of [-2.3, 0.4]) add(tube(new Vector3(side * 0.4, -0.95, z), new Vector3(side * 1.05, -1.75, z), 0.05, m.dark))
    }
  }

  /** Z-10: 14.2 m long, 13 m 5-blade rotor, angular faceted fuselage, wheels, scissor tail rotor on the right */
  private buildZ10(teamColor: string) {
    const m = materials(teamColor, '#56604f')
    const add = (o: Object3D) => this.root.add(o)

    // Faceted fuselage (boxier sections)
    add(
      new Mesh(
        loft(
          [
            [-6.6, -0.3, 0.1, 0.12],
            [-5.9, -0.75, 0.3, 0.42],
            [-4.8, -0.95, 0.62, 0.55],
            [-2.8, -1.0, 0.9, 0.62],
            [-0.8, -1.0, 1.1, 0.7],
            [0.8, -0.85, 1.1, 0.66],
            [2.0, -0.5, 0.82, 0.42],
            [4.5, -0.15, 0.6, 0.25],
            [6.9, 0.12, 0.55, 0.17],
          ],
          8,
          2.2,
        ),
        m.body,
      ),
    )
    // Stepped faceted canopy
    add(
      new Mesh(
        loft(
          [
            [-5.2, 0.45, 0.55, 0.3],
            [-4.5, 0.5, 1.1, 0.45],
            [-3.4, 0.7, 1.2, 0.48],
            [-2.9, 0.8, 1.45, 0.48],
            [-1.9, 0.85, 1.5, 0.46],
            [-1.2, 0.9, 1.15, 0.42],
          ],
          8,
          2.2,
        ),
        m.glass,
      ),
    )
    // Engines with angled exhausts
    for (const side of [-1, 1]) {
      add(
        new Mesh(
          loft(
            [
              [-0.9, 0.7, 1.05, 0.12],
              [-0.5, 0.6, 1.3, 0.28],
              [1.3, 0.6, 1.3, 0.28],
              [2.0, 0.7, 1.15, 0.2],
            ],
            8,
            2.2,
            side * 0.66,
          ),
          m.body,
        ),
      )
      add(box(0.32, 0.26, 0.2, m.dark, side * 0.8, 0.95, 2.05))
    }

    // Mast and 5-blade main rotor
    add(new Mesh(new CylinderGeometry(0.14, 0.2, 0.6, 8), m.dark).translateY(1.45))
    this.mainRotor = mainRotor(5, 6.5, 0.45, m)
    this.mainRotor.position.set(0, 1.8, 0)
    add(this.mainRotor)

    // Stub wings with slight anhedral and missile racks
    for (const side of [-1, 1]) {
      const wing = box(1.8, 0.12, 0.9, m.body, side * 1.4, -0.1, -0.6)
      wing.rotation.z = side * -0.08
      add(wing)
      for (const [x, n] of [
        [1.1, 1],
        [1.9, 2],
      ] as const) {
        for (let i = 0; i < n * 2; i++) {
          const missile = new Mesh(new CylinderGeometry(0.09, 0.09, 1.5, 6), m.dark)
          missile.rotation.x = Math.PI / 2
          missile.position.set(side * x + (i % 2 ? 0.12 : -0.12), -0.42 - Math.floor(i / 2) * 0.22, -0.7)
          add(missile)
        }
      }
    }

    // Chin turret
    add(new Mesh(new CylinderGeometry(0.26, 0.3, 0.35, 6), m.dark).translateZ(-5.2).translateY(-0.9))
    add(tube(new Vector3(0, -0.95, -5.2), new Vector3(0, -0.95, -6.5), 0.05, m.dark))

    // Tail: low horizontal stabiliser, swept fin, scissor (X) tail rotor on the right (starboard) side
    add(box(2.2, 0.08, 0.6, m.body, 0, 0.4, 6.1))
    add(
      new Mesh(
        loft(
          [
            [6.3, 0.4, 0.6, 0.07],
            [6.8, 0.45, 2.2, 0.09],
            [7.45, 1.15, 2.35, 0.07],
          ],
          4,
        ),
        m.accent,
      ),
    )
    this.tailRotor = tailRotor(4, 1.35, m, [0.95, 2.19, 0.95 + Math.PI, 2.19 + Math.PI])
    this.tailRotor.position.set(0.2, 1.7, 7.05)
    add(this.tailRotor)

    // Fixed tailwheel landing gear
    for (const side of [-1, 1]) {
      add(tube(new Vector3(side * 0.45, -0.9, -2.6), new Vector3(side * 0.95, -1.55, -2.8), 0.07, m.dark))
      const wheel = new Mesh(new CylinderGeometry(0.3, 0.3, 0.18, 10), m.dark)
      wheel.rotation.z = Math.PI / 2
      wheel.position.set(side * 1.0, -1.55, -2.8)
      add(wheel)
    }
    add(tube(new Vector3(0, 0.0, 5.6), new Vector3(0, -1.55 + 0.17, 5.9), 0.05, m.dark))
    const tailWheel = new Mesh(new CylinderGeometry(0.17, 0.17, 0.1, 8), m.dark)
    tailWheel.rotation.z = Math.PI / 2
    tailWheel.position.set(0, -1.68, 5.9)
    add(tailWheel)
  }
}
