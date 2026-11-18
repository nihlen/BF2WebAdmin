import {
  ACESFilmicToneMapping,
  Box3,
  BoxHelper,
  CapsuleGeometry,
  Color,
  ConeGeometry,
  CylinderGeometry,
  DirectionalLight,
  FogExp2,
  Group,
  HemisphereLight,
  LineBasicMaterial,
  type Material,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  type Object3D,
  PerspectiveCamera,
  Plane,
  Raycaster,
  RingGeometry,
  Scene,
  SRGBColorSpace,
  Vector2,
  Vector3,
  WebGLRenderer,
} from 'three'
import { TransformControls } from 'three/addons/controls/TransformControls.js'
import { CSS2DObject, CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js'
import { SpectatorControls } from '@/viewer/spectator'
import { DEFAULT_HORIZON_COLOR, DEFAULT_SEA_LEVEL, disposeTree, FOG_DENSITY, type LoadedWorld, loadMapWorld } from '@/viewer/world'
import { fromQuaternion, fromWorld, toQuaternion, toWorld, type Vec } from './coords'
import { BUILDER_KEY_MAP, CTRL, FLY_WHILE_LOOKING } from './keys'
import { boundsBox, type CatalogEntry, loadTemplateModel } from './templates'

// The map builder's 3D view: the map, what is built, the drafts on top of it, and the players. The camera flies like
// HeliGame's build mode: it looks while the right mouse button is held, and flies with WASD and Q/E only then; the
// left button selects and drags the gizmo. Items are dragged in from the page's lists. The page owns the drafts; the
// scene shows what it is given and reports clicks and drags.

export type ItemKind = 'Object' | 'SpawnPoint' | 'Helipad'

/** What the scene shows of one built item or draft */
export type SceneItem = {
  key: string
  kind: ItemKind
  template: string
  team: number
  position: Vec
  rotation: Vec
  /** A change not applied yet: drawn with an amber outline */
  draft: boolean
  /** Removed in a draft: drawn as a red outline only */
  removed: boolean
  /** In the builder but not in the game (its spawn failed) */
  missing: boolean
  /** False for a helipad whose spawn point into the helicopter players can only pick from the next round restart */
  spawnReady: boolean
}

export type PlaceSpec = { kind: ItemKind; template: string; team: number }

export type ScenePlayer = { index: number; name: string; team: number; alive: boolean; position: Vec | null }

export type GizmoTool = 'translate' | 'rotate'

const NOT_READY = ' · spawn here from next round'
const helipadLabel = (item: SceneItem) => `Helipad ${item.team} · ${item.template}${item.spawnReady ? '' : NOT_READY}`
const spawnLabel = (item: SceneItem) => `Spawn ${item.team}${item.spawnReady ? '' : NOT_READY}`

export const teamColor = (team: number) => (team === 2 ? '#60a5fa' : team === 1 ? '#f87171' : '#d4d4d4')
const DRAFT_COLOR = '#f59e0b'
const REMOVED_COLOR = '#ef4444'
const MISSING_COLOR = '#a855f7'
const SELECTED_COLOR = '#ffffff'
/** The concrete pad under a helipad's helicopter (BuilderSpawning.HelipadTemplate) */
export const HELIPAD_TEMPLATE = 'concreteblock_helipad'

type Entry = {
  item: SceneItem
  /** What identifies the model: rebuilt when it changes */
  modelKey: string
  root: Group
  outline: BoxHelper | null
  label: CSS2DObject | null
}

export class BuilderScene {
  readonly camera = new PerspectiveCamera(60, 1, 0.5, 30000)
  private readonly renderer: WebGLRenderer
  private readonly labelRenderer: CSS2DRenderer
  private readonly scene = new Scene()
  private readonly world = new Group()
  private readonly items = new Group()
  private readonly playersGroup = new Group()
  private readonly sun = new DirectionalLight('#fff3e0', 2.2)
  private readonly spectator: SpectatorControls
  private readonly gizmo: TransformControls
  private readonly resizeObserver: ResizeObserver
  private readonly raycaster = new Raycaster()
  private readonly container: HTMLElement
  private readonly entries = new Map<string, Entry>()
  private readonly playerViews = new Map<number, { root: Group; label: CSS2DObject; labelEl: HTMLDivElement }>()
  private catalog = new Map<string, CatalogEntry>()
  private loaded: LoadedWorld | null = null
  private loadedMap = ''
  private seaLevel = DEFAULT_SEA_LEVEL
  private frame = 0
  private lastFrameTime = 0
  private disposed = false

  private selected: string | null = null
  /** What is being dragged in from a list, drawn as a ghost on the surface under the pointer */
  private placing: PlaceSpec | null = null
  private placeGhost: Group | null = null
  private placeGhostKey = ''
  private pointerDown: { x: number; y: number; button: number } | null = null
  private gizmoDragging = false
  private snap = false
  private ctrlHeld = false
  private players: () => ScenePlayer[] = () => []

  private onSelect: (key: string | null) => void = () => {}
  private onTransform: (key: string, position: Vec, rotation: Vec) => void = () => {}

  /** What the page hears: a click selected an item or nothing; a gizmo drag ended with the item somewhere */
  setHandlers(handlers: { select: (key: string | null) => void; transform: (key: string, position: Vec, rotation: Vec) => void }) {
    this.onSelect = handlers.select
    this.onTransform = handlers.transform
  }

  constructor(container: HTMLElement) {
    this.container = container
    this.renderer = new WebGLRenderer({ antialias: true, powerPreference: 'high-performance' })
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    this.renderer.outputColorSpace = SRGBColorSpace
    this.renderer.toneMapping = ACESFilmicToneMapping
    this.renderer.toneMappingExposure = 1.1
    this.renderer.domElement.style.display = 'block'
    this.renderer.domElement.tabIndex = 0
    container.appendChild(this.renderer.domElement)

    this.labelRenderer = new CSS2DRenderer()
    Object.assign(this.labelRenderer.domElement.style, { position: 'absolute', top: '0', left: '0', pointerEvents: 'none' })
    container.appendChild(this.labelRenderer.domElement)

    const horizon = new Color(DEFAULT_HORIZON_COLOR)
    this.scene.background = horizon
    this.scene.fog = new FogExp2(horizon, FOG_DENSITY)
    this.scene.add(new HemisphereLight('#dfe8f2', '#5a5444', 1.6))
    this.sun.position.set(-600, 900, 400)
    this.scene.add(this.sun, this.world, this.items, this.playersGroup)

    this.camera.position.set(0, DEFAULT_SEA_LEVEL + 250, 400)
    this.camera.lookAt(0, DEFAULT_SEA_LEVEL, 0)

    this.spectator = new SpectatorControls(this.camera, this.renderer.domElement)
    this.spectator.lookButtons = [2]
    this.spectator.keyMap = BUILDER_KEY_MAP
    this.spectator.whileLooking = FLY_WHILE_LOOKING
    this.spectator.enabled = true
    this.spectator.minHeight = DEFAULT_SEA_LEVEL + 2
    this.spectator.syncFromCamera()

    this.gizmo = new TransformControls(this.camera, this.renderer.domElement)
    this.gizmo.setSize(0.9)
    this.gizmo.addEventListener('dragging-changed', (e) => {
      this.gizmoDragging = Boolean(e.value)
      // Ctrl snaps a drag instead of flying down
      if (this.gizmoDragging) for (const code of CTRL) this.spectator.setKey(code, false)
      else this.emitTransform()
    })
    this.scene.add(this.gizmo.getHelper())

    const dom = this.renderer.domElement
    dom.addEventListener('pointerdown', this.onPointerDown)
    dom.addEventListener('pointerup', this.onPointerUp)

    this.resizeObserver = new ResizeObserver(() => this.resize())
    this.resizeObserver.observe(container)
    this.resize()

    this.lastFrameTime = performance.now()
    this.frame = requestAnimationFrame(this.animate)
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Map

  async loadMap(map: string) {
    if (!map || map === this.loadedMap) return
    this.loadedMap = map
    this.gizmo.detach()
    disposeTree(this.world)
    this.world.clear()
    this.loaded = await loadMapWorld(map, this.world, this.scene, this.sun, () => this.disposed)
    if (this.disposed) return
    this.seaLevel = this.loaded.seaLevel
    this.spectator.minHeight = this.seaLevel + 2
  }

  /** The map's playable centre, to start the camera above */
  lookAtCentre() {
    this.camera.position.set(0, this.seaLevel + 250, 400)
    this.camera.lookAt(0, this.seaLevel, 0)
    this.spectator.syncFromCamera()
  }

  setCatalog(catalog: CatalogEntry[]) {
    this.catalog = new Map(catalog.map((c) => [c.name, c]))
    // Items drawn as boxes before the catalog came get their bounds
    for (const entry of this.entries.values()) entry.modelKey = ''
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Items

  /** Shows these items: entries are kept, moved, rebuilt or removed by key */
  setItems(items: SceneItem[]) {
    const keys = new Set(items.map((i) => i.key))
    for (const [key, entry] of this.entries) {
      if (keys.has(key)) continue
      if (this.gizmo.object === entry.root) this.gizmo.detach()
      this.removeEntry(entry)
      this.entries.delete(key)
    }

    for (const item of items) {
      let entry = this.entries.get(item.key)
      const modelKey = `${item.kind}:${item.template}:${item.team}`
      if (!entry || entry.modelKey !== modelKey) {
        const wasSelected = entry && this.gizmo.object === entry.root
        if (entry) this.removeEntry(entry)
        entry = { item, modelKey, root: new Group(), outline: null, label: null }
        entry.root.userData.builderKey = item.key
        this.items.add(entry.root)
        this.entries.set(item.key, entry)
        this.buildModel(entry)
        if (wasSelected) this.gizmo.attach(entry.root)
      }
      entry.item = item
      // A drag in progress keeps its own transform
      if (!(this.gizmoDragging && this.gizmo.object === entry.root)) {
        toWorld(item.position, entry.root.position)
        toQuaternion(item.rotation, entry.root.quaternion)
      }
      this.updateLook(entry)
    }
    this.updateGizmo()
  }

  private buildModel(entry: Entry) {
    const { item, root } = entry
    const color = teamColor(item.team)
    if (item.kind === 'SpawnPoint') {
      const post = new Mesh(new CylinderGeometry(0.25, 0.25, 2.2, 10), new MeshLambertMaterial({ color }))
      post.position.y = 1.1
      const head = new Mesh(new ConeGeometry(0.7, 1.2, 12), new MeshLambertMaterial({ color }))
      head.rotation.x = Math.PI
      head.position.y = 2.9
      // Which way the player faces: an arrow on the ground (models face -Z)
      const arrow = new Mesh(new ConeGeometry(0.35, 1.2, 8), new MeshBasicMaterial({ color }))
      arrow.rotation.x = -Math.PI / 2
      arrow.position.set(0, 0.15, -1)
      root.add(post, head, arrow)
      entry.label = this.makeLabel(spawnLabel(item), color, 4)
      root.add(entry.label)
      return
    }

    if (item.kind === 'Helipad') {
      const ring = new Mesh(new RingGeometry(5.2, 6, 40), new MeshBasicMaterial({ color, transparent: true, opacity: 0.85, depthWrite: false }))
      ring.rotation.x = -Math.PI / 2
      ring.position.y = 0.25
      root.add(ring)
      const heli = new Mesh(new CapsuleGeometry(0.9, 5, 4, 10), new MeshLambertMaterial({ color, transparent: true, opacity: 0.5 }))
      heli.rotation.x = Math.PI / 2
      heli.position.y = 2
      root.add(heli)
      this.addTemplateModel(root, HELIPAD_TEMPLATE, color)
      entry.label = this.makeLabel(helipadLabel(item), color, 6)
      root.add(entry.label)
      return
    }

    this.addTemplateModel(root, item.template, '#a29d94')
  }

  /** The template's GLB, or a box of its catalog bounds until it loads (or for good without one) */
  private addTemplateModel(root: Group, template: string, color: string) {
    const entryOf = this.catalog.get(template.toLowerCase())
    const placeholder = boundsBox(entryOf?.min ?? [-2, 0, -2], entryOf?.max ?? [2, 4, 2], color)
    placeholder.userData.placeholder = true
    root.add(placeholder)
    void loadTemplateModel(template).then((model) => {
      if (!model || this.disposed || !root.parent) return
      root.remove(placeholder)
      disposeTree(placeholder)
      root.add(model.clone())
      const entry = this.entries.get(root.userData.builderKey as string)
      if (entry) this.updateLook(entry)
    })
  }

  private makeLabel(text: string, color: string, height: number) {
    const el = document.createElement('div')
    el.textContent = text
    el.style.cssText = `font:600 11px/1.2 system-ui,sans-serif;color:${color};text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap`
    const label = new CSS2DObject(el)
    label.position.y = height
    return label
  }

  /** The outline: amber for a draft, red for a removal, purple when not in the game, white when selected */
  private updateLook(entry: Entry) {
    const { item } = entry
    const selected = this.selected === item.key
    const color = item.removed ? REMOVED_COLOR : selected ? SELECTED_COLOR : item.draft ? DRAFT_COLOR : item.missing ? MISSING_COLOR : null
    for (const child of entry.root.children) {
      if (child === entry.label) continue
      child.visible = !item.removed
    }
    if (entry.label) entry.label.element.textContent = item.kind === 'Helipad' ? helipadLabel(item) : spawnLabel(item)
    if (color) {
      if (!entry.outline) {
        entry.outline = new BoxHelper(entry.root, color)
        ;(entry.outline.material as Material).depthTest = false
        entry.outline.renderOrder = 10
        this.items.add(entry.outline)
      }
      ;(entry.outline.material as LineBasicMaterial).color.set(color)
      entry.outline.update()
    } else if (entry.outline) {
      this.items.remove(entry.outline)
      entry.outline.geometry.dispose()
      ;(entry.outline.material as Material).dispose()
      entry.outline = null
    }
  }

  private removeEntry(entry: Entry) {
    this.items.remove(entry.root)
    if (entry.label) entry.label.element.remove()
    entry.root.traverse((o) => {
      if (o instanceof CSS2DObject) o.element.remove()
    })
    disposeTree(entry.root)
    if (entry.outline) {
      this.items.remove(entry.outline)
      entry.outline.geometry.dispose()
      ;(entry.outline.material as Material).dispose()
    }
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Selection and gizmo

  select(key: string | null) {
    this.selected = key
    for (const entry of this.entries.values()) this.updateLook(entry)
    this.updateGizmo()
  }

  setTool(tool: GizmoTool) {
    this.gizmo.setMode(tool)
    this.updateGizmo()
  }

  setLocalSpace(local: boolean) {
    this.gizmo.setSpace(local ? 'local' : 'world')
  }

  /** Snap moves to whole metres and turns to 15 degrees: always, or while Ctrl is held */
  setSnap(snap: boolean) {
    this.snap = snap
    this.applySnap()
  }

  private applySnap() {
    const on = this.snap || this.ctrlHeld
    this.gizmo.setTranslationSnap(on ? 1 : null)
    this.gizmo.setRotationSnap(on ? Math.PI / 12 : null)
  }

  private updateGizmo() {
    const entry = this.selected ? this.entries.get(this.selected) : undefined
    if (!entry || entry.item.removed) {
      this.gizmo.detach()
      return
    }
    if (this.gizmo.object !== entry.root) this.gizmo.attach(entry.root)
    // Spawn points and helipads only turn about the vertical axis
    const yawOnly = entry.item.kind !== 'Object' && this.gizmo.mode === 'rotate'
    this.gizmo.showX = !yawOnly
    this.gizmo.showZ = !yawOnly
  }

  private emitTransform() {
    const root = this.gizmo.object
    const key = root?.userData.builderKey as string | undefined
    if (!root || !key) return
    const entry = this.entries.get(key)
    if (!entry) return
    const rotation = fromQuaternion(root.quaternion)
    if (entry.item.kind !== 'Object') {
      rotation[1] = 0
      rotation[2] = 0
    }
    this.onTransform(key, fromWorld(root.position), rotation)
  }

  /** Flies the camera to look at the item from a little above */
  focus(key: string) {
    const entry = this.entries.get(key)
    if (!entry) return
    const box = new Box3().setFromObject(entry.root)
    const centre = box.isEmpty() ? entry.root.position.clone() : box.getCenter(new Vector3())
    const radius = box.isEmpty() ? 10 : Math.max(box.getSize(new Vector3()).length() / 2, 5)
    const back = new Vector3(0, 0, 1).applyQuaternion(this.camera.quaternion).setY(0)
    if (back.lengthSq() < 1e-6) back.set(0, 0, 1)
    back.normalize().multiplyScalar(radius * 2.2)
    this.camera.position.copy(centre).add(back).add(new Vector3(0, radius * 1.2, 0))
    this.camera.lookAt(centre)
    this.spectator.syncFromCamera()
  }

  /** Where the item would stand on what is under it: the ground or an object below its origin */
  groundBelow(key: string): Vec | null {
    const entry = this.entries.get(key)
    if (!entry) return null
    const origin = entry.root.position.clone().add(new Vector3(0, 2000, 0))
    this.raycaster.set(origin, new Vector3(0, -1, 0))
    const hit = this.surfaceHit(entry.root)
    if (!hit) return null
    hit.y += this.baseLift(entry.item.kind, entry.item.template)
    return fromWorld(hit)
  }

  /**
   * How far above the surface a template's pivot goes for its base to stand on it: most meshes are centred on their
   * bounds (the catalog's min height is how far the base is below the pivot)
   */
  private baseLift(kind: ItemKind, template: string) {
    if (kind !== 'Object') return 0
    const entry = this.catalog.get(template.toLowerCase())
    return entry ? Math.max(0, -entry.min[1]) : 0
  }

  /** The point in the middle of the view on the ground, for placing without aiming */
  viewCentre(): Vec | null {
    this.raycaster.setFromCamera(new Vector2(0, 0), this.camera)
    const hit = this.surfaceHit(null)
    return hit ? fromWorld(hit) : null
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Placing: an item dragged in from a list goes on the surface under the pointer; a click on the list puts it ahead

  /** A drag from a list began: a ghost of the item follows the pointer over the 3D view */
  beginDragPlace(spec: PlaceSpec) {
    this.cancelDragPlace()
    this.placing = spec
  }

  /** The pointer moved during the drag (page coordinates); the ghost shows only over the 3D view */
  dragPlaceAt(clientX: number, clientY: number) {
    if (!this.placing) return
    this.updatePlaceGhost(clientX, clientY)
    if (this.placeGhost && !this.isOverView(clientX, clientY)) this.placeGhost.visible = false
  }

  /** The drag ended: where the item goes, or null when it was dropped outside the 3D view or on nothing */
  endDragPlace(clientX: number, clientY: number): Vec | null {
    const spec = this.placing
    let position: Vec | null = null
    if (spec && this.isOverView(clientX, clientY)) {
      const point = this.pickSurface(clientX, clientY, this.placeGhost)
      if (point) position = fromWorld(point.setY(point.y + this.baseLift(spec.kind, spec.template)))
    }
    this.cancelDragPlace()
    return position
  }

  cancelDragPlace() {
    if (this.placeGhost) {
      this.scene.remove(this.placeGhost)
      this.placeGhost.traverse((o) => {
        if (o instanceof CSS2DObject) o.element.remove()
      })
      disposeTree(this.placeGhost)
      this.placeGhost = null
      this.placeGhostKey = ''
    }
    this.placing = null
  }

  /** Where an item clicked in a list goes: on the surface in the middle of the view, or 50 m ahead above the sea */
  placeAhead(spec: PlaceSpec): Vec {
    this.raycaster.setFromCamera(new Vector2(0, 0), this.camera)
    const point = this.surfaceHit(null) ?? this.camera.position.clone().addScaledVector(this.raycaster.ray.direction, 50)
    point.y = Math.max(point.y, this.seaLevel)
    point.y += this.baseLift(spec.kind, spec.template)
    return fromWorld(point)
  }

  /** Whether a page point is over the 3D view */
  isOverView(clientX: number, clientY: number) {
    const rect = this.renderer.domElement.getBoundingClientRect()
    return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom
  }

  /** The keys go to the 3D view (after a click in a list) */
  focusView() {
    this.renderer.domElement.focus()
  }

  /** Whether the right mouse button is held to look and fly: W and E fly then instead of picking a tool */
  get looking() {
    return this.spectator.looking
  }

  private updatePlaceGhost(clientX: number, clientY: number) {
    if (!this.placing) return
    const key = `${this.placing.kind}:${this.placing.template}:${this.placing.team}`
    if (this.placeGhostKey !== key) {
      if (this.placeGhost) {
        this.scene.remove(this.placeGhost)
        disposeTree(this.placeGhost)
      }
      const ghostEntry: Entry = {
        item: { key: 'ghost', ...this.placing, position: [0, 0, 0], rotation: [0, 0, 0], draft: true, removed: false, missing: false, spawnReady: false },
        modelKey: key,
        root: new Group(),
        outline: null,
        label: null,
      }
      this.buildModel(ghostEntry)
      if (ghostEntry.label) {
        ghostEntry.root.remove(ghostEntry.label)
        ghostEntry.label.element.remove()
      }
      this.placeGhost = ghostEntry.root
      this.placeGhostKey = key
      this.scene.add(this.placeGhost)
    }
    const point = this.pickSurface(clientX, clientY, this.placeGhost)
    this.placeGhost!.visible = point !== null
    if (point) this.placeGhost!.position.copy(point).setY(point.y + this.baseLift(this.placing.kind, this.placing.template))
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Players

  /** Called every frame for the players to draw (the page keeps them in a ref) */
  setPlayerSource(source: () => ScenePlayer[]) {
    this.players = source
  }

  private updatePlayers() {
    const seen = new Set<number>()
    for (const p of this.players()) {
      if (!p.position || !p.alive) continue
      seen.add(p.index)
      let view = this.playerViews.get(p.index)
      if (!view) {
        const root = new Group()
        const body = new Mesh(new CapsuleGeometry(0.45, 1.0, 4, 8), new MeshLambertMaterial({ color: teamColor(p.team) }))
        body.position.y = 0.95
        root.add(body)
        const labelEl = document.createElement('div')
        labelEl.style.cssText = 'font:600 11px/1.2 system-ui,sans-serif;color:#fff;text-shadow:0 1px 2px #000,0 0 6px #000;white-space:nowrap'
        const label = new CSS2DObject(labelEl)
        label.position.y = 2.6
        root.add(label)
        this.playersGroup.add(root)
        view = { root, label, labelEl }
        this.playerViews.set(p.index, view)
      }
      view.labelEl.textContent = p.name
      view.labelEl.style.color = teamColor(p.team)
      ;((view.root.children[0] as Mesh).material as MeshLambertMaterial).color.set(teamColor(p.team))
      toWorld(p.position, view.root.position)
    }
    for (const [index, view] of this.playerViews) {
      if (seen.has(index)) continue
      this.playersGroup.remove(view.root)
      view.label.element.remove()
      disposeTree(view.root)
      this.playerViews.delete(index)
    }
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Input

  setKey(code: string, down: boolean) {
    if (CTRL.includes(code)) {
      this.ctrlHeld = down
      this.applySnap()
      // While the gizmo is dragged Ctrl snaps; otherwise it flies down
      if (down && this.gizmoDragging) return
    }
    this.spectator.setKey(code, down)
  }

  clearKeys() {
    this.spectator.clearKeys()
    this.ctrlHeld = false
    this.applySnap()
  }

  private setRay(clientX: number, clientY: number) {
    const rect = this.renderer.domElement.getBoundingClientRect()
    const ndc = new Vector2(((clientX - rect.left) / rect.width) * 2 - 1, -((clientY - rect.top) / rect.height) * 2 + 1)
    this.raycaster.setFromCamera(ndc, this.camera)
  }

  /** Where the ray first meets the ground, a map object, a built item or the sea */
  private pickSurface(clientX: number, clientY: number, ignore: Object3D | null) {
    this.setRay(clientX, clientY)
    return this.surfaceHit(ignore)
  }

  private surfaceHit(ignore: Object3D | null): Vector3 | null {
    const targets: Object3D[] = [...(this.loaded?.ground ?? []), ...(this.loaded?.objects ?? [])]
    for (const entry of this.entries.values()) {
      if (entry.root !== ignore && !entry.item.removed && entry.item.kind === 'Object') targets.push(entry.root)
    }
    const hit = this.raycaster.intersectObjects(targets, true).find((h) => !(h.object as Mesh).userData?.placeholder)
    const sea = this.raycaster.ray.intersectPlane(new Plane(new Vector3(0, 1, 0), -this.seaLevel), new Vector3())
    if (hit && (!sea || hit.distance <= this.raycaster.ray.origin.distanceTo(sea))) return hit.point.clone()
    return sea
  }

  /** The item under the pointer */
  private pickItem(clientX: number, clientY: number): string | null {
    this.setRay(clientX, clientY)
    const roots = [...this.entries.values()].filter((e) => !e.item.removed).map((e) => e.root)
    for (const hit of this.raycaster.intersectObjects(roots, true)) {
      let o: Object3D | null = hit.object
      while (o && o.userData.builderKey === undefined) o = o.parent
      if (o) return o.userData.builderKey as string
    }
    return null
  }

  private onPointerDown = (e: PointerEvent) => {
    this.renderer.domElement.focus()
    this.pointerDown = { x: e.clientX, y: e.clientY, button: e.button }
  }

  private onPointerUp = (e: PointerEvent) => {
    const down = this.pointerDown
    this.pointerDown = null
    if (!down || down.button !== 0 || e.button !== 0) return
    // A drag (of the gizmo or anything else) is not a click
    if (Math.hypot(e.clientX - down.x, e.clientY - down.y) > 4 || this.gizmoDragging || this.gizmo.dragging) return

    // The gizmo's own handles take the click
    if (this.gizmo.axis !== null) return
    this.onSelect(this.pickItem(e.clientX, e.clientY))
  }

  // ---------------------------------------------------------------------------------------------------------------

  private resize() {
    const width = this.container.clientWidth
    const height = this.container.clientHeight
    if (width === 0 || height === 0) return
    this.renderer.setSize(width, height)
    this.labelRenderer.setSize(width, height)
    this.camera.aspect = width / height
    this.camera.updateProjectionMatrix()
  }

  private animate = (time: number) => {
    if (this.disposed) return
    this.frame = requestAnimationFrame(this.animate)
    const dt = Math.min(time - this.lastFrameTime, 100)
    this.lastFrameTime = time
    this.spectator.update(dt)
    this.loaded?.sky?.position.copy(this.camera.position)
    this.updatePlayers()
    for (const entry of this.entries.values()) entry.outline?.update()
    this.renderer.render(this.scene, this.camera)
    this.labelRenderer.render(this.scene, this.camera)
  }

  dispose() {
    this.disposed = true
    cancelAnimationFrame(this.frame)
    this.resizeObserver.disconnect()
    const dom = this.renderer.domElement
    dom.removeEventListener('pointerdown', this.onPointerDown)
    dom.removeEventListener('pointerup', this.onPointerUp)
    this.gizmo.detach()
    this.gizmo.dispose()
    this.spectator.dispose()
    this.cancelDragPlace()
    for (const entry of this.entries.values()) this.removeEntry(entry)
    this.entries.clear()
    disposeTree(this.world)
    disposeTree(this.playersGroup)
    this.renderer.dispose()
    dom.remove()
    this.labelRenderer.domElement.remove()
  }
}
