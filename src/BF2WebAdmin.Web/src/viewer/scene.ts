import {
  ACESFilmicToneMapping,
  BoxGeometry,
  BufferGeometry,
  CapsuleGeometry,
  ClampToEdgeWrapping,
  CylinderGeometry,
  Color,
  DirectionalLight,
  DoubleSide,
  Float32BufferAttribute,
  FogExp2,
  Group,
  HemisphereLight,
  type InterleavedBufferAttribute,
  type Material,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  MeshPhongMaterial,
  type Object3D,
  PerspectiveCamera,
  PlaneGeometry,
  Quaternion,
  RepeatWrapping,
  Scene,
  SphereGeometry,
  SRGBColorSpace,
  TextureLoader,
  Vector3,
  WebGLRenderer,
} from 'three'
import { OrbitControls } from 'three/addons/controls/OrbitControls.js'
import { Line2 } from 'three/addons/lines/Line2.js'
import { LineGeometry } from 'three/addons/lines/LineGeometry.js'
import { LineMaterial } from 'three/addons/lines/LineMaterial.js'
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js'
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { CSS2DObject, CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js'
import type { MissileReplay } from '@/api/client'
import { type LiveBuffer, poseAt, samplePos, sampleRot, TICK_RATE } from './buffer'
import {
  adjustedFovDegrees,
  type CameraPose,
  type CameraTemplate,
  ChaseState,
  chaseCamera,
  FlyByState,
  flyByCamera,
  isHelicopter,
  lookAt,
  MISSILE_CAMERA,
  noseCamera,
  seatCamera,
  TV_AIM_CAMERA,
  VIEW_MODES,
} from './cameras'
import { rotToQuaternion, vecToWorld } from './coords'
import { HeliModel } from './helis'
import { findPart, type HitboxSet, HitboxModel, loadHitboxes, facesNear, partMesh } from './hitboxes'
import { Explosion, MissileModel } from './models'
import { SpectatorControls } from './spectator'

// The 3D viewer's scene: the live stream (LiveBuffer) drawn at any tick, a player's own view, and a selected missile's
// hit/miss replay with its targets where they were ("red") and where the server tested them ("blue"). Ported from
// BF2v2's round replay (app/src/components/pages/Match/Replay/scene.ts); its samples come 30 times a second, so it
// interpolates linearly between ticks instead of smoothing.

const ASSETS = '/viewer'
const DEFAULT_SEA_LEVEL = 141.4
const TRAIL_TICKS = 8 * TICK_RATE
const TRAIL_POINTS = 80
/** Live playback stays this many ticks behind the newest frame, so there is always a next sample to interpolate to */
const LIVE_DELAY_TICKS = 2
/** Ticks shown before and after a missile's flight */
const MISSILE_PADDING_TICKS = TICK_RATE

const FOG_DENSITY = 0.0005
const DEFAULT_HORIZON_COLOR = '#b1bad0'
const SKY_RADIUS = 20000
const FOV_FREE = 55

/**
 * The hitbox ghosts: red the visual model where the target is now (what the gunner saw), blue the hull's collision mesh
 * where the server tested it (rewound), orange the child parts' collision meshes, tested where they are now
 */
export const CURRENT_COLOR = '#ff3b3b'
export const TESTED_COLOR = '#3b82ff'
export const PARTS_COLOR = '#f97316'
const PATH_COLOR = '#f5e9c8'
export const SWEEP_COLOR = '#facc15'
/** The impact marker: a direct hit on a vehicle, or anything else the missile stopped on */
export const HIT_COLOR = '#22c55e'
export const IMPACT_COLOR = '#d4d4d4'

export const teamColor = (team: number) => (team === 2 ? '#60a5fa' : team === 1 ? '#f87171' : '#d4d4d4')

export type CameraMode = 'free' | 'orbit' | 'player' | 'missile'

interface MapMeta {
  seaLevel: number
  sky?: SkyMeta
}

interface SkyMeta {
  texture: string
  uOffset: number
  horizonColor: string
  sunDirection?: [number, number, number]
}

/** What the timeline shows */
export type PlaybackState = {
  tick: number
  start: number
  end: number
  playing: boolean
  live: boolean
  speed: number
  /** The selected missile's first and last tick, when one is selected, and the tick it stopped on (-1 if none) */
  missile: { first: number; last: number; impact: number; impactKind: ImpactKind } | null
}

/** What the missile stopped on: a vehicle it targeted (hull, part or crew), another object, the terrain, or nothing */
export type ImpactKind = 'vehicle' | 'object' | 'terrain' | null

/** What the player view shows, for the page's overlay */
export type ViewInfo = {
  playerId: number
  name: string
  alive: boolean
  view: number
  viewName: string
  camera: string
  fovDegrees: number
  tv: boolean
  lagMs: number
  ping: number
} | null

type VehicleView = {
  model: HeliModel | Mesh
  label: CSS2DObject
  labelEl: HTMLDivElement
  trail: Line2
  template: string
  team: number
  /** The collision meshes at the live pose, with the Hitboxes toggle */
  hitbox: HitboxModel | null
}

type ProjectileView = { model: MissileModel }

type SoldierView = { model: Mesh; label: CSS2DObject; labelEl: HTMLDivElement; team: number }

type TargetView = {
  /** The visual model at the current pose (red) */
  current: HeliModel | Mesh
  /** The hull's collision mesh at the tested pose (blue), or the visual model without rotors when there is none */
  tested: Object3D
  /** The child parts' collision meshes at the current pose (orange) */
  parts: Object3D | null
  lagLine: Line2
  label: CSS2DObject
  labelEl: HTMLDivElement
}

type MissileView = {
  replay: MissileReplay
  first: number
  positions: Vector3[]
  path: Line2
  flown: Line2
  sweep: Line2
  model: MissileModel
  explosion: Explosion
  /** Per target in the replay; null for one that did not move */
  targets: (TargetView | null)[]
  markers: Object3D[]
  /** Tick of the direct hit or terrain impact, -1 if none */
  impactTick: number
  impactKind: ImpactKind
  /** The big marker at the impact point: faint before the impact tick, bright from it */
  impactMarker: Group | null
}

export type PlayerName = { name: string; team: number }

export class ViewerScene {
  private readonly renderer: WebGLRenderer
  private readonly labelRenderer: CSS2DRenderer
  private readonly scene = new Scene()
  private readonly camera = new PerspectiveCamera(FOV_FREE, 1, 0.5, 30000)
  private readonly controls: OrbitControls
  private readonly spectator: SpectatorControls
  private readonly resizeObserver: ResizeObserver
  private readonly world = new Group()
  private readonly live = new Group()
  private readonly overlay = new Group()
  private readonly sun = new DirectionalLight('#fff3e0', 2.2)
  private readonly heliTemplates = new Map<string, Object3D>()
  private hitboxes: HitboxSet = new Map()
  private sky: Mesh | null = null
  private seaLevel = DEFAULT_SEA_LEVEL
  private frame = 0
  private lastFrameTime = 0
  private lastNotify = 0
  private disposed = false
  private loadedMap = ''

  private buffer: LiveBuffer | null = null
  private names = new Map<number, PlayerName>()
  private readonly vehicles = new Map<number, VehicleView>()
  private readonly projectiles = new Map<number, ProjectileView>()
  private readonly soldiers = new Map<number, SoldierView>()
  private missile: MissileView | null = null

  // Playback, in ticks
  private tick = 0
  private lastRenderedTick = 0
  private playing = true
  private following = true
  private speed = 1

  private cameraMode: CameraMode = 'free'
  private subject = -1
  private orbitInitialized = false
  private readonly orbitTarget = new Vector3()
  private readonly chase = new ChaseState()
  private readonly flyBy = new FlyByState()
  private readonly pose: CameraPose = { position: new Vector3(), quaternion: new Quaternion(), fov: 1.1 }
  private showTrails = true
  private showLabels = true
  private showEffects = true
  private showHitboxes = false
  /** The player view's subject, vehicle, view mode and camera: a change snaps the chase and fly-by cameras */
  private viewKey = ''
  private viewInfo: ViewInfo = null
  /** The vehicle the player view looks out of from inside: not drawn, its model has no cockpit */
  private ownVehicle = 0

  /** About 10 times a second, and on every seek or state change */
  onPlayback: (state: PlaybackState) => void = () => {}
  onViewInfo: (info: ViewInfo) => void = () => {}

  private readonly container: HTMLElement

  constructor(container: HTMLElement) {
    this.container = container
    this.renderer = new WebGLRenderer({ antialias: true, powerPreference: 'high-performance' })
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    this.renderer.outputColorSpace = SRGBColorSpace
    this.renderer.toneMapping = ACESFilmicToneMapping
    this.renderer.toneMappingExposure = 1.1
    this.renderer.domElement.style.display = 'block'
    container.appendChild(this.renderer.domElement)

    this.labelRenderer = new CSS2DRenderer()
    Object.assign(this.labelRenderer.domElement.style, { position: 'absolute', top: '0', left: '0', pointerEvents: 'none' })
    container.appendChild(this.labelRenderer.domElement)

    const horizon = new Color(DEFAULT_HORIZON_COLOR)
    this.scene.background = horizon
    this.scene.fog = new FogExp2(horizon, FOG_DENSITY)
    this.scene.add(new HemisphereLight('#dfe8f2', '#5a5444', 1.6))
    this.sun.position.set(-600, 900, 400)
    this.scene.add(this.sun, this.world, this.live, this.overlay)

    this.controls = new OrbitControls(this.camera, this.renderer.domElement)
    this.controls.enableDamping = true
    this.controls.dampingFactor = 0.08
    this.controls.minDistance = 3
    this.controls.maxDistance = 6000
    this.camera.position.set(0, 900, 900)
    this.controls.target.set(0, DEFAULT_SEA_LEVEL, 0)
    this.camera.lookAt(this.controls.target)

    this.spectator = new SpectatorControls(this.camera, this.renderer.domElement)
    this.spectator.minHeight = DEFAULT_SEA_LEVEL + 2
    this.spectator.syncFromCamera()
    this.setCameraMode('free')

    this.resizeObserver = new ResizeObserver(() => this.resize())
    this.resizeObserver.observe(container)
    this.resize()

    this.lastFrameTime = performance.now()
    this.frame = requestAnimationFrame(this.animate)
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Map

  /** Loads the helicopter models and the map's 3D terrain (/viewer/maps/<map>/map.glb), or a flat ground without one */
  async loadMap(map: string) {
    if (!map || map === this.loadedMap) return
    this.loadedMap = map
    if (!this.heliTemplates.size) await this.loadHeliModels()
    disposeTree(this.world)
    this.world.clear()
    this.sky = null
    await this.loadTerrain(map.toLowerCase())
  }

  /** The AH-1Z and Z-10 from the game meshes (BF2v2 tools/replay-map/build_helis.py) */
  private async loadHeliModels() {
    const hitboxes = loadHitboxes(`${ASSETS}/models/heli-hitboxes.json`)
      .then((set) => (this.hitboxes = set))
      .catch((e: unknown) => console.warn('[viewer] helicopter hitboxes not available, showing the visual hull instead', e))
    try {
      const draco = new DRACOLoader().setDecoderPath(`${ASSETS}/draco/`)
      const gltf = await new GLTFLoader().setDRACOLoader(draco).loadAsync(`${ASSETS}/models/helis.glb`)
      draco.dispose()
      for (const name of ['ahe_ah1z', 'ahe_z10']) {
        const node = gltf.scene.getObjectByName(name)
        if (node) this.heliTemplates.set(name, node)
      }
    } catch (e) {
      console.warn('[viewer] helicopter models not available, using simple models', e)
    }
    await hitboxes
  }

  private async loadTerrain(map: string) {
    const base = `${ASSETS}/maps/${map}`
    let meta: MapMeta | null = null
    try {
      const response = await fetch(`${base}/map.json`)
      if (response.ok && response.headers.get('content-type')?.includes('json')) meta = (await response.json()) as MapMeta
    } catch {
      meta = null
    }

    if (meta) {
      this.seaLevel = meta.seaLevel
      if (meta.sky) await this.loadSky(`${base}/${meta.sky.texture}`, meta.sky)
      if (this.disposed) return
      this.spectator.minHeight = meta.seaLevel + 2
      const draco = new DRACOLoader().setDecoderPath(`${ASSETS}/draco/`)
      const gltf = await new GLTFLoader().setDRACOLoader(draco).loadAsync(`${base}/map.glb`)
      draco.dispose()
      gltf.scene.traverse((o) => {
        const mesh = o as Mesh
        if (!mesh.isMesh) return
        const old = mesh.material as Material & { map?: MeshLambertMaterial['map'] }
        if (mesh.name.startsWith('terrain')) {
          mesh.material = new MeshLambertMaterial({ map: old.map ?? null })
        } else if (mesh.name.startsWith('outer_terrain')) {
          mesh.material = new MeshLambertMaterial({ color: '#6f6f5a' })
        } else if (mesh.name.startsWith('see_through')) {
          // Wire fences and grates: the holes are in textures the map doesn't carry, so show them translucent
          mesh.material = new MeshLambertMaterial({ vertexColors: true, transparent: true, opacity: 0.35, side: DoubleSide, depthWrite: false })
        } else if (mesh.name.startsWith('objects') || mesh.name.startsWith('trees')) {
          mesh.material = mesh.geometry.hasAttribute('color')
            ? new MeshLambertMaterial({ vertexColors: true })
            : new MeshLambertMaterial({ color: '#a29d94', flatShading: true })
        }
        old.dispose()
      })
      if (this.disposed) return
      this.world.add(gltf.scene)
    } else {
      // No 3D map for this level: a plain ground at the default sea level, with a grid of 100 m squares
      const ground = new Mesh(new PlaneGeometry(4096, 4096, 41, 41), new MeshLambertMaterial({ color: '#6b6b55', wireframe: false }))
      ground.rotation.x = -Math.PI / 2
      ground.position.y = this.seaLevel + 0.5
      const grid = new Mesh(new PlaneGeometry(4096, 4096, 41, 41), new MeshBasicMaterial({ color: '#8a8a70', wireframe: true }))
      grid.rotation.x = -Math.PI / 2
      grid.position.y = this.seaLevel + 0.6
      this.world.add(ground, grid)
    }

    const water = new Mesh(
      new PlaneGeometry(40000, 40000),
      new MeshPhongMaterial({ color: '#2f4d5e', transparent: true, opacity: 0.88, shininess: 40, specular: '#46555e' }),
    )
    water.rotation.x = -Math.PI / 2
    water.position.y = this.seaLevel
    water.renderOrder = 1
    this.world.add(water)
  }

  private async loadSky(url: string, sky: SkyMeta) {
    const texture = await new TextureLoader().loadAsync(url).catch(() => null)
    if (!texture || this.disposed) return
    texture.colorSpace = SRGBColorSpace
    texture.wrapS = RepeatWrapping
    texture.wrapT = ClampToEdgeWrapping
    this.sky = new Mesh(skyDomeGeometry(sky.uOffset), new MeshBasicMaterial({ map: texture, fog: false, depthWrite: false, side: DoubleSide }))
    this.sky.renderOrder = -1
    this.sky.frustumCulled = false
    this.world.add(this.sky)

    const horizon = new Color(sky.horizonColor)
    this.scene.background = horizon
    ;(this.scene.fog as FogExp2).color.copy(horizon)
    if (sky.sunDirection) this.sun.position.fromArray(sky.sunDirection).multiplyScalar(1000)
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Data

  setBuffer(buffer: LiveBuffer) {
    this.buffer = buffer
  }

  setNames(names: Map<number, PlayerName>) {
    this.names = names
  }

  /** Shows a missile's replay (null clears it) and moves the timeline to its flight */
  setMissile(replay: MissileReplay | null) {
    this.clearMissile()
    if (!replay) {
      if (this.cameraMode === 'missile') this.setCameraMode('orbit')
      this.goLive()
      return
    }

    const first = replay.summary.firstTick
    const positions = replay.samples.map((s) => vecToWorld(s.p))
    const path = makeLine(PATH_COLOR, positions.length, 0.35, 2)
    const flown = makeLine(PATH_COLOR, positions.length, 0.95, 3)
    const sweep = makeLine(SWEEP_COLOR, 2, 1, 5)
    setLinePoints(path, positions)
    // True size, its centre on the traced position: the point the engine tests
    const model = new MissileModel(teamColor(replay.summary.shooterTeam))
    model.addTestPoint(SWEEP_COLOR)
    model.setEffects(this.showEffects)
    const explosion = new Explosion(5)
    this.overlay.add(path, flown, sweep, model.root, explosion.root)

    // Parked, empty vehicles near the path are targets too; they are drawn by the live view
    const targets = replay.summary.targets.map((target) => {
      if (!target.moved) return null
      const current = this.makeVehicleModel(target.template, CURRENT_COLOR, true, 0.22)
      const hitbox = this.hitboxes.get(target.template.toLowerCase())
      const boxes = hitbox ? new HitboxModel(hitbox, TESTED_COLOR, PARTS_COLOR) : null
      const tested = boxes ? boxes.hull : rootOf(this.makeVehicleModel(target.template, TESTED_COLOR, false))
      const parts = boxes ? boxes.parts : null
      if (parts) this.overlay.add(parts)
      const lagLine = makeLine('#ffffff', 2, 0.8, 1.5)
      const labelEl = document.createElement('div')
      labelEl.style.cssText =
        'font:600 11px/1.2 system-ui,sans-serif;color:#fff;text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap;transform:translateY(-26px)'
      const label = new CSS2DObject(labelEl)
      this.overlay.add(rootOf(current), tested, lagLine, label)
      return { current, tested, parts, lagLine, label, labelEl }
    })

    // The parts the missile touched: where it hit, and for a child part where that part was (tested where it is now)
    const targetIds = new Set(replay.summary.targets.map((t) => t.netId))
    const markers: Object3D[] = []
    let impactTick = -1
    let impactKind: ImpactKind = null
    let impactMarker: Group | null = null
    for (const c of replay.collisions) {
      const onTarget = c.kind !== 0 && targetIds.has(c.root)
      const hit = new Mesh(
        new SphereGeometry(0.35, 12, 8),
        new MeshBasicMaterial({ color: c.kind === 0 ? '#a3a3a3' : SWEEP_COLOR, depthTest: false }),
      )
      hit.position.copy(vecToWorld(c.p))
      hit.renderOrder = 5
      const labelEl = document.createElement('div')
      labelEl.style.cssText = `font:600 11px/1.2 system-ui,sans-serif;color:${onTarget ? HIT_COLOR : SWEEP_COLOR};text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap`
      // CSS2DRenderer owns the element's transform: move the text right of the marker with an inner element
      const text = labelEl.appendChild(document.createElement('span'))
      text.style.cssText = 'display:inline-block;transform:translateX(calc(50% + 18px))'
      text.textContent = `${describeCollision(c, onTarget)} · tick +${c.tick - first}`
      const label = new CSS2DObject(labelEl)
      label.position.copy(hit.position)
      this.overlay.add(hit, label)
      hit.userData.tick = label.userData.tick = c.tick
      markers.push(hit, label)
      // The part touched, where the engine tested it (rewound for the hull, now for a child part; a rotor at the blade
      // angle of the hit)
      const hitbox = onTarget ? this.hitboxes.get(c.rootTemplate.toLowerCase()) : undefined
      const part = hitbox && !(c.flags & 2) ? findPart(hitbox, c.rootTemplate, c.partTemplate) : undefined
      if (part) {
        // A small part lights up whole; on the hull, the patch of faces around the point
        const isHull = part === hitbox!.hull
        const touched = new Group()
        touched.position.copy(vecToWorld(c.partP))
        touched.quaternion.copy(rotToQuaternion(c.partR))
        const local = hit.position.clone().sub(touched.position).applyQuaternion(touched.quaternion.clone().invert())
        const face = isHull ? facesNear(part, local, 0.6, HIT_COLOR) : null
        if (!isHull) touched.add(partMesh(part, HIT_COLOR, 0.8))
        if (face) touched.add(face)
        touched.traverse((o) => (o.renderOrder = 4))
        if (face) face.renderOrder = 7
        touched.userData.tick = c.tick
        this.overlay.add(touched)
        markers.push(touched)
      }
      if (impactTick < 0) {
        impactTick = c.tick
        impactKind = c.kind === 0 ? 'terrain' : onTarget ? 'vehicle' : 'object'
        impactMarker = makeImpactMarker(impactKind === 'vehicle' ? HIT_COLOR : IMPACT_COLOR)
        impactMarker.position.copy(hit.position)
        this.overlay.add(impactMarker)
      }
    }

    this.missile = { replay, first, positions, path, flown, sweep, model, explosion, targets, markers, impactTick, impactKind, impactMarker }
    this.following = false
    this.seek(first - 5)
    this.playing = true
    this.notify()
  }

  private clearMissile() {
    if (!this.missile) return
    for (const t of this.missile.targets) t?.labelEl.remove()
    for (const o of this.missile.markers) if (o instanceof CSS2DObject) o.element.remove()
    disposeTree(this.overlay)
    this.overlay.clear()
    this.missile = null
  }

  private makeVehicleModel(template: string, color: string, rotors: boolean, opacity = rotors ? 0.4 : 0.5): HeliModel | Mesh {
    const name = template.toLowerCase()
    if (isHelicopter(name)) {
      const model = new HeliModel(color, name.includes('z10'), this.heliTemplates.get(name))
      model.makeGhost(color, opacity, rotors)
      return model
    }
    return new Mesh(
      new BoxGeometry(3, 2, 6),
      new MeshLambertMaterial({ color, emissive: color, emissiveIntensity: 0.45, transparent: true, opacity: 0.45, depthWrite: false }),
    )
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Playback

  /** The timeline's range: the selected missile's flight, or the live buffer */
  private range() {
    if (this.missile) {
      const last = this.missile.first + this.missile.replay.samples.length - 1
      return { start: this.missile.first - MISSILE_PADDING_TICKS, end: last + MISSILE_PADDING_TICKS }
    }
    const b = this.buffer
    if (!b || b.isEmpty) return { start: 0, end: 0 }
    return { start: b.firstTick, end: Math.max(b.firstTick, b.lastTick - LIVE_DELAY_TICKS) }
  }

  get state(): PlaybackState {
    const { start, end } = this.range()
    const m = this.missile
    return {
      tick: this.tick,
      start,
      end,
      playing: this.playing,
      live: this.following && !m,
      speed: this.speed,
      missile: m ? { first: m.first, last: m.first + m.replay.samples.length - 1, impact: m.impactTick, impactKind: m.impactKind } : null,
    }
  }

  play() {
    const { end } = this.range()
    if (this.tick >= end && this.missile) this.seek(this.range().start)
    this.playing = true
    this.notify()
  }

  pause() {
    this.playing = false
    this.following = false
    this.notify()
  }

  togglePlay() {
    if (this.playing) this.pause()
    else this.play()
  }

  setSpeed(speed: number) {
    this.speed = speed
    this.notify()
  }

  /** Back to the newest frame, playing (leaves a selected missile) */
  goLive() {
    if (this.missile) {
      this.setMissile(null)
      return
    }
    this.following = true
    this.playing = true
    this.speed = 1
    this.notify()
  }

  seek(tick: number) {
    const { start, end } = this.range()
    this.tick = Math.min(Math.max(tick, start), end)
    if (!this.missile && this.tick < end - 1) this.following = false
    this.notify()
  }

  /** One tick forward or back, landing on a whole tick, and pauses */
  step(ticks: number) {
    this.playing = false
    this.following = false
    this.seek(ticks > 0 ? Math.floor(this.tick + 1e-6) + ticks : Math.ceil(this.tick - 1e-6) + ticks)
  }

  /** Jumps to the tick the selected missile stopped on (its sweep that tick is the one that hit) and pauses */
  seekImpact() {
    if (!this.missile || this.missile.impactTick < 0) return false
    this.playing = false
    this.following = false
    this.seek(this.missile.impactTick)
    return true
  }

  private advance(dtMs: number) {
    const { start, end } = this.range()
    if (this.following && !this.missile && this.buffer && !this.buffer.isEmpty) {
      // Live: run at real time, a couple of ticks behind the newest frame, and catch up when frames come early or late
      const sinceFrame = Math.min((performance.now() - this.buffer.lastArrival) / 1000, 1) * TICK_RATE
      const target = Math.min(this.buffer.lastTick - LIVE_DELAY_TICKS + sinceFrame, this.buffer.lastTick)
      this.tick += (dtMs / 1000) * TICK_RATE
      const error = target - this.tick
      if (Math.abs(error) > 10) this.tick = target
      else this.tick += error * Math.min(1, dtMs / 250)
      this.tick = Math.max(this.tick, start)
      return
    }
    if (!this.playing) return
    this.tick += (dtMs / 1000) * TICK_RATE * this.speed
    if (this.tick >= end) {
      this.tick = end
      // At the live end of the buffer, playing becomes following
      if (!this.missile) this.following = true
      else this.playing = false
    }
  }

  private notify() {
    this.lastNotify = performance.now()
    this.onPlayback(this.state)
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Camera

  /**
   * free: fly around (WASD, drag to look). orbit: circle the subject (a player, or the selected missile). player: the
   * subject's own view, following its view mode. missile: the selected missile's TV camera
   */
  setCameraMode(mode: CameraMode, subject = this.subject) {
    if (mode === 'missile' && !this.missile) mode = 'orbit'
    this.cameraMode = mode
    this.subject = subject
    this.orbitInitialized = false
    this.chase.reset()
    this.flyBy.reset()
    this.viewKey = ''
    this.controls.enabled = mode === 'orbit'
    this.spectator.enabled = mode === 'free'
    if (mode === 'free') this.spectator.syncFromCamera()
    if (mode !== 'player') this.setViewInfo(null)
    this.ownVehicle = 0
  }

  setKey(code: string, down: boolean) {
    this.spectator.setKey(code, down)
  }

  clearKeys() {
    this.spectator.clearKeys()
  }

  setTrails(show: boolean) {
    this.showTrails = show
  }

  setLabels(show: boolean) {
    this.showLabels = show
  }

  /** The collision meshes on the live helicopters (hull blue, child parts orange, at the pose the server has now) */
  setHitboxes(show: boolean) {
    this.showHitboxes = show
  }

  /** The missiles' motor glow and the explosion */
  setEffects(show: boolean) {
    this.showEffects = show
    for (const view of this.projectiles.values()) view.model.setEffects(show)
    this.missile?.model.setEffects(show)
  }

  private setViewInfo(info: ViewInfo) {
    const old = this.viewInfo
    const same =
      old === info ||
      (old !== null &&
        info !== null &&
        old.playerId === info.playerId &&
        old.alive === info.alive &&
        old.view === info.view &&
        old.camera === info.camera &&
        old.name === info.name &&
        Math.round(old.fovDegrees) === Math.round(info.fovDegrees) &&
        old.lagMs === info.lagMs &&
        old.ping === info.ping)
    this.viewInfo = info
    if (!same) this.onViewInfo(info)
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Frame

  private animate = (now: number) => {
    if (this.disposed) return
    this.frame = requestAnimationFrame(this.animate)
    const dt = Math.min(now - this.lastFrameTime, 100)
    this.lastFrameTime = now

    this.advance(dt)
    const gameDt = (this.tick - this.lastRenderedTick) / TICK_RATE
    this.lastRenderedTick = this.tick
    this.updateLive()
    this.updateMissile()
    this.updateCamera(gameDt)

    if (this.cameraMode === 'free') this.spectator.update(dt)
    else if (this.cameraMode === 'orbit') this.controls.update()
    this.sky?.position.copy(this.camera.position)
    this.renderer.render(this.scene, this.camera)
    this.labelRenderer.render(this.scene, this.camera)

    if (now - this.lastNotify > 100) this.notify()
  }

  private readonly _p = new Vector3()
  private readonly _q = new Quaternion()
  private readonly _v = new Vector3()
  private readonly _w = new Vector3()
  private readonly _q2 = new Quaternion()

  /** The players in a vehicle at tick, pilot first */
  private crewOf(root: number, tick: number) {
    const crew: { id: number; seat: number }[] = []
    if (!this.buffer) return crew
    for (const id of this.buffer.players.keys()) {
      const p = this.buffer.playerAt(id, tick)
      if (p && p.life !== 0 && p.root === root && p.flags & 1) crew.push({ id, seat: p.seat })
    }
    return crew.sort((a, b) => a.seat - b.seat)
  }

  private updateLive() {
    const b = this.buffer
    const t = this.tick
    if (!b) return
    // During the selected missile's flight, the targets that moved are drawn by their ghosts
    const missileTargets = new Set(
      this.missile && this.missileInFlight() ? this.missile.replay.summary.targets.filter((x) => x.moved).map((x) => x.netId) : [],
    )

    // Vehicles
    const seenVehicles = new Set<number>()
    for (const [id, series] of b.vehicles) {
      if (!b.vehicleVisible(series, t)) continue
      const sample = poseAt(series, t, samplePos, sampleRot, this._p, this._q)
      if (!sample) continue
      seenVehicles.add(id)
      let view = this.vehicles.get(id)
      if (!view || view.template !== sample.template || view.team !== sample.team) {
        if (view) this.removeVehicle(id)
        view = this.addVehicle(id, sample.template, sample.team)
      }
      const root = rootOf(view.model)
      applyPose(root, this._p, this._q)
      // A missile's target is drawn by its ghosts
      root.visible = !missileTargets.has(id) && id !== this.ownVehicle
      if (view.model instanceof HeliModel) view.model.animate((t / TICK_RATE) * 1000, this.playing || this.following)

      const crew = this.crewOf(id, t)
      view.labelEl.textContent = crew.map((c) => this.names.get(c.id)?.name ?? `#${c.id}`).join(' · ')
      view.label.position.copy(this._p)
      view.label.visible = this.showLabels && crew.length > 0 && root.visible
      view.labelEl.style.display = view.label.visible ? '' : 'none'

      const hitbox = this.showHitboxes && root.visible ? this.hitboxes.get(sample.template.toLowerCase()) : undefined
      if (hitbox && !view.hitbox) {
        view.hitbox = new HitboxModel(hitbox, TESTED_COLOR, PARTS_COLOR, 0.5)
        this.live.add(view.hitbox.hull, view.hitbox.parts)
      }
      if (view.hitbox) {
        view.hitbox.hull.visible = view.hitbox.parts.visible = !!hitbox
        applyPose(view.hitbox.hull, this._p, this._q)
        applyPose(view.hitbox.parts, this._p, this._q)
      }

      view.trail.visible = this.showTrails && isHelicopter(sample.template)
      if (view.trail.visible) {
        updateTrail(view.trail, Math.max(series.first.tick, t - TRAIL_TICKS), t, (tick, out) => {
          poseAt(series, tick, samplePos, sampleRot, out, this._q2)
          return out
        })
      }
    }
    for (const id of [...this.vehicles.keys()]) if (!seenVehicles.has(id)) this.removeVehicle(id)

    // Soldiers on foot
    const seenSoldiers = new Set<number>()
    for (const [id, series] of b.players) {
      const p = b.playerAt(id, t)
      if (!p || p.life === 0 || p.flags & 1) continue
      if (!poseAt(series, t, samplePos, sampleRot, this._p, this._q)) continue
      seenSoldiers.add(id)
      let view = this.soldiers.get(id)
      if (!view || view.team !== p.team) {
        if (view) this.removeSoldier(id)
        view = this.addSoldier(id, p.team)
      }
      view.model.position.copy(this._p)
      view.model.position.y += 0.9
      view.label.position.copy(view.model.position)
      view.labelEl.textContent = this.names.get(id)?.name ?? `#${id}`
      view.label.visible = this.showLabels
      view.labelEl.style.display = view.label.visible ? '' : 'none'
    }
    for (const id of [...this.soldiers.keys()]) if (!seenSoldiers.has(id)) this.removeSoldier(id)

    // Projectiles (the subscribed ones: TV missiles). The selected missile is drawn by its replay
    const seenProjectiles = new Set<number>()
    const replayId = this.missileActive() ? this.missile!.replay.summary.projectileId : -1
    for (const [id, series] of b.projectiles) {
      if (id === replayId || !b.projectileVisible(series, t)) continue
      const sample = poseAt(series, t, samplePos, sampleRot, this._p, this._q)
      if (!sample) continue
      seenProjectiles.add(id)
      let view = this.projectiles.get(id)
      if (!view) {
        const owner = b.playerAt(sample.owner, t)
        view = { model: new MissileModel(teamColor(owner?.team ?? 0)) }
        view.model.setEffects(this.showEffects)
        this.live.add(view.model.root)
        this.projectiles.set(id, view)
      }
      applyPose(view.model.root, this._p, this._q)
    }
    for (const [id, view] of [...this.projectiles]) {
      if (seenProjectiles.has(id)) continue
      this.live.remove(view.model.root)
      disposeTree(view.model.root)
      this.projectiles.delete(id)
    }
  }

  private addVehicle(id: number, template: string, team: number): VehicleView {
    const color = teamColor(team)
    const name = template.toLowerCase()
    const model = isHelicopter(name)
      ? new HeliModel(color, name.includes('z10'), this.heliTemplates.get(name))
      : new Mesh(new BoxGeometry(2.5, 1.8, 5), new MeshLambertMaterial({ color: new Color('#8b8b7a').lerp(new Color(color), 0.4) }))
    const labelEl = document.createElement('div')
    labelEl.style.cssText = `color:${color};font:600 12px/1.2 system-ui,sans-serif;text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap;text-align:center;transform:translateY(-22px)`
    const label = new CSS2DObject(labelEl)
    const trail = makeLine(color, TRAIL_POINTS, 0.85, 2.5)
    this.live.add(rootOf(model), label, trail)
    const view = { model, label, labelEl, trail, template, team, hitbox: null }
    this.vehicles.set(id, view)
    return view
  }

  private removeVehicle(id: number) {
    const view = this.vehicles.get(id)
    if (!view) return
    const root = rootOf(view.model)
    view.labelEl.remove()
    this.live.remove(root, view.label, view.trail)
    disposeTree(root)
    disposeTree(view.trail)
    if (view.hitbox) {
      this.live.remove(view.hitbox.hull, view.hitbox.parts)
      disposeTree(view.hitbox.hull)
      disposeTree(view.hitbox.parts)
    }
    this.vehicles.delete(id)
  }

  private addSoldier(id: number, team: number): SoldierView {
    const color = teamColor(team)
    const model = new Mesh(new CapsuleGeometry(0.35, 1.1, 4, 8), new MeshLambertMaterial({ color }))
    const labelEl = document.createElement('div')
    labelEl.style.cssText = `color:${color};font:600 11px/1.2 system-ui,sans-serif;text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap;transform:translateY(-18px)`
    const label = new CSS2DObject(labelEl)
    this.live.add(model, label)
    const view = { model, label, labelEl, team }
    this.soldiers.set(id, view)
    return view
  }

  private removeSoldier(id: number) {
    const view = this.soldiers.get(id)
    if (!view) return
    view.labelEl.remove()
    this.live.remove(view.model, view.label)
    disposeTree(view.model)
    this.soldiers.delete(id)
  }

  /** Whether the timeline is inside the selected missile's padded range */
  private missileActive() {
    return this.missile !== null
  }

  /** Whether the timeline is within the selected missile's flight (its first to last sample) */
  private missileInFlight() {
    const { x, n } = this.missileSample()
    return x >= 0 && x <= n - 1
  }

  /** The selected missile's sample index and blend at the current tick */
  private missileSample() {
    const m = this.missile!
    const n = m.positions.length
    const x = this.tick - m.first
    const k = Math.min(Math.max(Math.floor(x), 0), n - 1)
    const u = k < n - 1 ? Math.min(Math.max(x - k, 0), 1) : 0
    return { k, u, x, n }
  }

  private updateMissile() {
    const m = this.missile
    if (!m) return
    const { k, u, x, n } = this.missileSample()
    const inFlight = x >= 0 && x <= n - 1
    const samples = m.replay.samples

    // The missile and the path flown so far
    this._p.copy(m.positions[k])
    if (u > 0) this._p.lerp(m.positions[k + 1], u)
    m.model.root.visible = x >= 0 && x <= n - 1 + MISSILE_PADDING_TICKS && !(m.impactTick >= 0 && this.tick > m.impactTick + 1)
    m.model.root.position.copy(this._p)
    vecToWorld(samples[k].v, this._v)
    if (this._v.lengthSq() > 1) m.model.root.lookAt(this._w.copy(this._p).sub(this._v))
    const flown = m.positions.slice(0, k + 1)
    if (x > 0) flown.push(this._p.clone())
    setLinePoints(m.flown, x >= 0 ? flown : [])

    // The sweep the engine tests this tick, against each target's blue pose and the orange parts: the missile's point
    // from end - v/30 to end (PointResponsePhysics.cpp:112-145), where end is the next sample. v is the trace's
    // velocity (getPositionalSpeed, not necessarily the integration velocity the engine uses; within a millimetre of
    // the step after launch). On the hit tick the missile stops at the contact point with v 0: the tested segment
    // then starts at this sample and runs v/30 ahead, past the contact point
    m.sweep.visible = inFlight && k < n - 1
    if (m.sweep.visible) {
      const end = m.positions[k + 1]
      vecToWorld(samples[k + 1].v, this._v)
      if (this._v.lengthSq() > 1) setLinePoints(m.sweep, [end.clone().addScaledVector(this._v, -1 / TICK_RATE), end])
      else {
        vecToWorld(samples[k].v, this._v)
        setLinePoints(m.sweep, [m.positions[k], m.positions[k].clone().addScaledVector(this._v, 1 / TICK_RATE)])
      }
    }

    // A part is marked from the tick it was hit
    for (const marker of m.markers) {
      marker.visible = this.tick >= marker.userData.tick
      if (marker instanceof CSS2DObject) marker.element.style.display = marker.visible && this.showLabels ? '' : 'none'
    }

    const impact = m.impactTick >= 0 ? m.impactTick : -1
    if (m.impactMarker) setImpactMarker(m.impactMarker, this.tick >= impact)
    if (impact >= 0 && this.showEffects) {
      m.explosion.root.position.copy(m.replay.collisions.length ? vecToWorld(m.replay.collisions[0].p, this._w) : m.positions[n - 1])
      m.explosion.update(((this.tick - impact) / TICK_RATE) * 1000)
    } else m.explosion.update(-1)

    // Targets: red where they are now, blue (the hull) where this tick's sweep tests them. Between ticks both move on
    // smoothly; on a whole tick they are exactly the server's values
    m.replay.summary.targets.forEach((target, i) => {
      const view = m.targets[i]
      if (!view) return
      const a = samples[k].t[i]
      const b = u > 0 ? samples[k + 1].t[i] : null
      const visible = inFlight
      const currentRoot = rootOf(view.current)
      const testedRoot = view.tested
      currentRoot.visible = testedRoot.visible = visible
      if (view.parts) view.parts.visible = visible
      view.lagLine.visible = view.label.visible = visible
      if (!visible) {
        view.labelEl.style.display = 'none'
        return
      }

      ghostPose(a, b, u, 0, this._p, this._q)
      applyPose(currentRoot, this._p, this._q)
      if (view.parts) applyPose(view.parts, this._p, this._q)
      if (view.current instanceof HeliModel) view.current.animate((this.tick / TICK_RATE) * 1000, this.playing)
      ghostPose(a, b, u, 6, this._w, this._q2)
      applyPose(testedRoot, this._w, this._q2)
      setLinePoints(view.lagLine, [this._p, this._w])

      const lagMs = Math.round(samples[k].lag * 1000)
      const crew = target.crew.length ? target.crew.join(' · ') : target.template
      view.labelEl.textContent = `${crew} · ${lagMs} ms → ${this._p.distanceTo(this._w).toFixed(1)} m`
      view.label.position.copy(this._p)
      view.labelEl.style.display = this.showLabels ? '' : 'none'
    })
  }

  private updateCamera(gameDt: number) {
    if (gameDt < 0 || gameDt > 0.5) {
      // A seek: the chase camera snaps like after a respawn
      this.chase.reset()
      this.flyBy.reset()
    }

    if (this.cameraMode === 'free') {
      this.setFov(FOV_FREE)
      return
    }

    if (this.cameraMode === 'orbit') {
      this.setFov(FOV_FREE)
      if (!this.subjectPosition(this._p)) return
      if (!this.orbitInitialized) {
        this.orbitTarget.copy(this._p)
        this.camera.position.copy(this._p).add(this._v.set(0, 12, 35))
        this.controls.target.copy(this._p)
        this.orbitInitialized = true
      }
      this._v.copy(this.camera.position).sub(this.controls.target)
      this.orbitTarget.copy(this._p)
      this.controls.target.copy(this.orbitTarget)
      this.camera.position.copy(this.orbitTarget).add(this._v)
      return
    }

    if (this.cameraMode === 'missile') {
      if (this.missileCamera(gameDt)) this.applyCameraPose()
      return
    }

    if (this.playerCamera(gameDt)) this.applyCameraPose()
  }

  /** Where the orbit camera looks: the selected missile in its replay, else the subject player */
  private subjectPosition(out: Vector3) {
    if (this.missile && (this.subject < 0 || this.missileActive())) {
      const { k, u } = this.missileSample()
      out.copy(this.missile.positions[k])
      if (u > 0) out.lerp(this.missile.positions[k + 1], u)
      return true
    }
    const b = this.buffer
    if (!b || this.subject < 0) return false
    const p = b.playerAt(this.subject, this.tick)
    if (!p) return false
    if (p.flags & 1) {
      const series = b.vehicles.get(p.root)
      if (series && poseAt(series, this.tick, samplePos, sampleRot, out, this._q)) return true
    }
    const series = b.players.get(this.subject)
    return !!series && !!poseAt(series, this.tick, samplePos, sampleRot, out, this._q)
  }

  /** The selected missile's TV camera, from its trace: its chase view along the flight direction */
  private missileCamera(gameDt: number) {
    const m = this.missile!
    const { k, u } = this.missileSample()
    this._p.copy(m.positions[k])
    if (u > 0) this._p.lerp(m.positions[k + 1], u)
    // The trace has no rotation: the missile flies nose first. After a direct hit it sits still: keep its last heading
    for (let i = k; i >= 0; i--) {
      vecToWorld(m.replay.samples[i].v, this._v)
      if (this._v.lengthSq() > 1) break
    }
    // lookAt turns -Z (the forward of models and cameras) towards the target
    lookAt(this._w.set(0, 0, 0), this._v, this._q)
    m.model.root.visible = false
    chaseCamera(this.chase, `missile:${m.replay.summary.key}`, this._p, this._q, MISSILE_CAMERA, false, gameDt, this.seaLevel, this.pose)
    return true
  }

  /**
   * The subject player's own view: the server's camera object in the inside, nose and trace views, the chase and fly-by
   * views computed from the vehicle like the client does, the TV launcher's and the TV missile's cameras
   */
  private playerCamera(gameDt: number) {
    const b = this.buffer
    const id = this.subject
    const name = this.names.get(id)?.name ?? `#${id}`
    const p = b?.playerAt(id, this.tick)
    if (!b || !p) {
      this.ownVehicle = 0
      this.setViewInfo({ playerId: id, name, alive: false, view: 255, viewName: 'No data', camera: '', fovDegrees: 0, tv: false, lagMs: 0, ping: 0 })
      return false
    }
    const info = { playerId: id, name, alive: p.life !== 0, view: p.view, viewName: VIEW_MODES[p.view] ?? `${p.view}`, lagMs: p.lag, ping: p.ping }
    const aims = b.aims.get(id)
    const cameraPosition = new Vector3()
    const cameraRotation = new Quaternion()
    const aim = aims
      ? poseAt(
          aims,
          this.tick,
          (s) => s.cp,
          (s) => s.cr,
          cameraPosition,
          cameraRotation,
        )
      : null
    if (p.life === 0 || !aim || !(aim.flags & 1) || this.tick - aim.tick > TICK_RATE) {
      this.ownVehicle = 0
      this.setViewInfo({ ...info, camera: p.life === 0 ? 'Dead' : 'No camera', fovDegrees: 0, tv: false })
      return false
    }

    // The root object: the vehicle when seated, else the soldier
    const rootPosition = new Vector3()
    const rootRotation = new Quaternion()
    let template = ''
    const seated = (p.flags & 1) !== 0
    const vehicle = seated ? b.vehicles.get(p.root) : undefined
    const vehicleSample = vehicle ? poseAt(vehicle, this.tick, samplePos, sampleRot, rootPosition, rootRotation) : null
    if (vehicleSample) template = vehicleSample.template
    else poseAt(b.players.get(id)!, this.tick, samplePos, sampleRot, rootPosition, rootRotation)

    // A TV missile camera: the camera object is far from the vehicle, on one of the player's own missiles
    let missilePose: { position: Vector3; rotation: Quaternion } | null = null
    if (cameraPosition.distanceTo(rootPosition) > 6) {
      for (const series of b.projectiles.values()) {
        if (series.last.owner !== id || !b.projectileVisible(series, this.tick)) continue
        const position = new Vector3()
        const rotation = new Quaternion()
        if (poseAt(series, this.tick, samplePos, sampleRot, position, rotation) && position.distanceTo(cameraPosition) < 5) {
          missilePose = { position, rotation }
          break
        }
      }
    }

    // Switching view (or vehicle, or onto the TV missile) cuts straight to the new view, as the game does: the chase
    // camera only swings after the vehicle within one view
    const viewKey = `${id}:${p.root}:${p.view}:${missilePose ? 'tv' : ''}`
    if (viewKey !== this.viewKey) {
      this.viewKey = viewKey
      this.chase.reset()
      this.flyBy.reset()
    }

    const front = p.view === 4
    let camera = ''
    let cameraTemplate: CameraTemplate
    if (missilePose) {
      cameraTemplate = MISSILE_CAMERA
      camera = 'TV missile'
      if (p.view === 3 || p.view === 4)
        chaseCamera(this.chase, `missile:${id}:${p.view}`, missilePose.position, missilePose.rotation, MISSILE_CAMERA, front, gameDt, this.seaLevel, this.pose)
      else this.cameraObject(cameraPosition, cameraRotation, MISSILE_CAMERA.insideFov)
    } else if (template && p.seat === 1 && isHelicopter(template) && localX(cameraPosition, rootPosition, rootRotation) > 0.9) {
      // The TV launcher's camera sits low on the side of the nose, unlike the gunner's eye on the centre line
      cameraTemplate = TV_AIM_CAMERA
      camera = 'TV launcher'
      this.cameraObject(cameraPosition, cameraRotation, TV_AIM_CAMERA.insideFov)
    } else {
      cameraTemplate = seatCamera(template, seated ? p.seat : 0)
      const subjectKey = `${id}:${p.root}:${p.view}`
      switch (p.view) {
        case 2:
          camera = 'Nose'
          noseCamera(cameraPosition, cameraRotation, rootRotation, cameraTemplate, p.seat !== 0, this.pose)
          break
        case 3:
        case 4:
          camera = front ? 'Front chase' : 'Chase'
          chaseCamera(this.chase, subjectKey, rootPosition, rootRotation, cameraTemplate, front, gameDt, this.seaLevel, this.pose)
          break
        case 5: {
          camera = 'Fly-by'
          vecToWorld(p.v, this._v)
          flyByCamera(this.flyBy, subjectKey, cameraPosition, rootRotation, this._v, this.seaLevel, this.pose, cameraTemplate.worldFov)
          break
        }
        default:
          camera = p.view === 1 ? (seated ? 'Seat camera' : 'Soldier camera') : 'Camera object'
          this.cameraObject(cameraPosition, cameraRotation, p.view === 1 ? cameraTemplate.insideFov : cameraTemplate.worldFov)
      }
    }

    // Looking out of the vehicle (its camera object, not an external view): hide it
    this.ownVehicle = seated && !missilePose && (p.view === 1 || cameraTemplate === TV_AIM_CAMERA) ? p.root : 0
    const tv = cameraTemplate === MISSILE_CAMERA || cameraTemplate === TV_AIM_CAMERA
    this.setViewInfo({ ...info, camera, fovDegrees: this.fovDegrees(this.pose.fov), tv })
    return true
  }

  /** The server's camera object as it is: the eye, with free look */
  private cameraObject(position: Vector3, rotation: Quaternion, fov: number) {
    this.pose.position.copy(position)
    // The camera object faces BF2's +z like any object; a three.js camera looks down -Z, which is the same direction
    // after the mirror (models face -Z too)
    this.pose.quaternion.copy(rotation)
    this.pose.fov = fov
  }

  private fovDegrees(fov: number) {
    return adjustedFovDegrees(fov, this.container.clientWidth, this.container.clientHeight)
  }

  private applyCameraPose() {
    this.camera.position.copy(this.pose.position)
    this.camera.quaternion.copy(this.pose.quaternion)
    this.setFov(this.fovDegrees(this.pose.fov))
  }

  private setFov(fov: number) {
    if (Math.abs(this.camera.fov - fov) < 1e-4) return
    this.camera.fov = fov
    this.camera.updateProjectionMatrix()
  }

  private resize() {
    const width = this.container.clientWidth
    const height = this.container.clientHeight
    if (!width || !height) return
    this.renderer.setSize(width, height)
    this.labelRenderer.setSize(width, height)
    this.camera.aspect = width / height
    this.camera.updateProjectionMatrix()
  }

  dispose() {
    this.disposed = true
    cancelAnimationFrame(this.frame)
    this.resizeObserver.disconnect()
    this.controls.dispose()
    this.spectator.dispose()
    for (const id of [...this.vehicles.keys()]) this.removeVehicle(id)
    for (const id of [...this.soldiers.keys()]) this.removeSoldier(id)
    this.clearMissile()
    disposeTree(this.live)
    disposeTree(this.world)
    this.renderer.dispose()
    this.renderer.domElement.remove()
    this.labelRenderer.domElement.remove()
  }
}

// -------------------------------------------------------------------------------------------------------------------

const rootOf = (model: HeliModel | Mesh): Object3D => (model instanceof HeliModel ? model.root : model)

function applyPose(object: Object3D, position: Vector3, rotation: Quaternion) {
  object.position.copy(position)
  object.quaternion.copy(rotation)
}

/** A target pose from a trace sample's 12 values: current at offset 0, tested at offset 6 */
function ghostPose(a: number[], b: number[] | null, u: number, offset: number, outPosition: Vector3, outRotation: Quaternion) {
  vecToWorld(a.slice(offset, offset + 3), outPosition)
  rotToQuaternion(a.slice(offset + 3, offset + 6), outRotation)
  if (b && u > 0) {
    outPosition.lerp(vecToWorld(b.slice(offset, offset + 3)), u)
    outRotation.slerp(rotToQuaternion(b.slice(offset + 3, offset + 6)), u)
  }
}

/** How far right of the vehicle's centre line a point is, metres */
function localX(point: Vector3, rootPosition: Vector3, rootRotation: Quaternion) {
  const local = point.clone().sub(rootPosition).applyQuaternion(rootRotation.clone().invert())
  return Math.abs(local.x)
}

/** A collision's label. onTarget: its root is one of the missile's targets (a vehicle), not a building or a tree */
function describeCollision(c: MissileReplay['collisions'][number], onTarget: boolean) {
  if (c.kind === 0) return 'Ground / water'
  const flags = c.flags
  const rewound = (flags & 4) !== 0 ? 'rewound' : 'tested now'
  if (flags & 2) return `Hit soldier ${c.partPlayer >= 0 ? `#${c.partPlayer}` : ''} (${rewound})`
  if (!onTarget) return `Object ${c.partTemplate || c.rootTemplate}`
  if (flags & 1) return `Hit hull ${c.rootTemplate} (${rewound})`
  return `Hit ${c.partTemplate} on ${c.rootTemplate} (${rewound})`
}

/** A wire sphere and a pole over the impact point, drawn over everything so it can be found from afar */
function makeImpactMarker(color: string) {
  const group = new Group()
  const sphere = new Mesh(
    new SphereGeometry(0.9, 14, 8),
    new MeshBasicMaterial({ color, wireframe: true, transparent: true, depthTest: false, depthWrite: false }),
  )
  const pole = new Mesh(
    new CylinderGeometry(0.06, 0.06, 30, 6, 1, true),
    new MeshBasicMaterial({ color, transparent: true, depthTest: false, depthWrite: false }),
  )
  pole.position.y = 15
  sphere.renderOrder = pole.renderOrder = 6
  group.add(sphere, pole)
  setImpactMarker(group, false)
  return group
}

/** Faint before the impact tick, bright from it */
function setImpactMarker(group: Group, reached: boolean) {
  for (const o of group.children) ((o as Mesh).material as MeshBasicMaterial).opacity = reached ? 1 : 0.3
}

/** A screen-space wide polyline (WebGL lines are always 1 px) with room for `points` vertices */
function makeLine(color: string, points: number, opacity: number, width: number) {
  const geometry = new LineGeometry()
  geometry.setPositions(new Float32Array(Math.max(points, 2) * 3))
  geometry.instanceCount = 0
  const material = new LineMaterial({ color, linewidth: width, worldUnits: false, transparent: true, opacity, depthWrite: false })
  const line = new Line2(geometry, material)
  line.frustumCulled = false
  return line
}

/** Sets a line's vertices (at most its capacity) */
function setLinePoints(line: Line2, points: Vector3[]) {
  const start = line.geometry.getAttribute('instanceStart') as InterleavedBufferAttribute
  const buffer = start.data
  const array = buffer.array as Float32Array
  const segments = Math.min(Math.max(points.length - 1, 0), buffer.count)
  for (let s = 0; s < segments; s++) {
    const a = points[s]
    const b = points[s + 1]
    array.set([a.x, a.y, a.z, b.x, b.y, b.z], s * 6)
  }
  buffer.needsUpdate = true
  line.geometry.instanceCount = segments
}

const _trailPoints: Vector3[] = Array.from({ length: TRAIL_POINTS }, () => new Vector3())

/** Fills a trail with positions sampled evenly from tick `from` to `to` */
function updateTrail(line: Line2, from: number, to: number, positionAt: (tick: number, out: Vector3) => Vector3) {
  if (to <= from) {
    setLinePoints(line, [])
    return
  }
  for (let i = 0; i < TRAIL_POINTS; i++) positionAt(from + ((to - from) * i) / (TRAIL_POINTS - 1), _trailPoints[i])
  setLinePoints(line, _trailPoints)
}

/**
 * Sphere around the camera with the panorama UVs of BF2's skydome. u keeps increasing past 1 across the seam
 * (the texture repeats) so no triangle spans the whole texture. Below the horizon it shows the bottom row (haze)
 */
function skyDomeGeometry(uOffset: number, columns = 64, rows = 32) {
  const positions: number[] = []
  const uvs: number[] = []
  const indices: number[] = []
  for (let r = 0; r <= rows; r++) {
    const elevation = -Math.PI / 2 + (r / rows) * Math.PI
    for (let c = 0; c <= columns; c++) {
      const azimuth = (c / columns) * Math.PI * 2
      // BF2 direction (cos el cos az, sin el, cos el sin az) -> world (x, y, -z)
      positions.push(
        SKY_RADIUS * Math.cos(elevation) * Math.cos(azimuth),
        SKY_RADIUS * Math.sin(elevation),
        -SKY_RADIUS * Math.cos(elevation) * Math.sin(azimuth),
      )
      uvs.push(uOffset + c / columns, Math.min(Math.max(elevation / (Math.PI / 2), 0.002), 1))
    }
  }
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < columns; c++) {
      const a = r * (columns + 1) + c
      const b = a + columns + 1
      indices.push(a, b, a + 1, a + 1, b, b + 1)
    }
  }
  const geometry = new BufferGeometry()
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3))
  geometry.setAttribute('uv', new Float32BufferAttribute(uvs, 2))
  geometry.setIndex(indices)
  return geometry
}

function disposeTree(root: Object3D) {
  root.traverse((o) => {
    const mesh = o as Mesh
    if (mesh.geometry && !mesh.userData.sharedGeometry) mesh.geometry.dispose()
    const materials = mesh.material ? (Array.isArray(mesh.material) ? mesh.material : [mesh.material]) : []
    for (const m of materials) {
      ;(m as Material & { map?: { dispose(): void } }).map?.dispose()
      m.dispose()
    }
  })
}
