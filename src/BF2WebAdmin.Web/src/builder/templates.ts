import {
  AmbientLight,
  Box3,
  BoxGeometry,
  DirectionalLight,
  DoubleSide,
  EdgesGeometry,
  Group,
  LineBasicMaterial,
  LineSegments,
  Mesh,
  MeshLambertMaterial,
  type Object3D,
  PerspectiveCamera,
  Scene,
  SRGBColorSpace,
  Vector3,
  WebGLRenderer,
} from 'three'
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js'
import { type GLTF, GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { ASSETS, DRACO } from '@/viewer/world'

// The static objects a builder can place: one GLB per object template (tools/builder-assets), listed in catalog.json
// with its bounds in its own frame (BF2 metres, [x, height, z]).

export type CatalogEntry = {
  name: string
  category: string
  /** [x, height, z] */
  min: number[]
  max: number[]
  size: number
}

const TEMPLATES = `${ASSETS}/templates`

let catalogPromise: Promise<CatalogEntry[]> | null = null

/** The palette's templates; empty when the catalog is not built */
export function loadCatalog(): Promise<CatalogEntry[]> {
  catalogPromise ??= fetch(`${TEMPLATES}/catalog.json`)
    .then(async (r) => {
      if (!r.ok || !r.headers.get('content-type')?.includes('json')) return []
      const body = (await r.json()) as { templates?: CatalogEntry[] } | CatalogEntry[]
      const list = Array.isArray(body) ? body : (body.templates ?? [])
      return list.map((t) => ({ ...t, name: t.name.toLowerCase() }))
    })
    .catch(() => [])
  return catalogPromise
}

let loader: GLTFLoader | null = null
const models = new Map<string, Promise<Object3D | null>>()

function gltfLoader() {
  if (!loader) {
    const draco = new DRACOLoader().setDecoderPath(DRACO)
    loader = new GLTFLoader().setDRACOLoader(draco)
  }
  return loader
}

/**
 * A template's model in its own frame (BF2 (x, h, z) as three.js (x, h, -z)), shared: clone it. Null when there is
 * no GLB for it; use a box of its bounds then.
 */
export function loadTemplateModel(template: string): Promise<Object3D | null> {
  const key = template.toLowerCase()
  let model = models.get(key)
  if (!model) {
    model = gltfLoader()
      .loadAsync(`${TEMPLATES}/${encodeURIComponent(key)}.glb`)
      .then((gltf: GLTF) => {
        // Mesh names (tools/builder-assets/README.md): objects opaque, see_through (fences, grates, panes)
        // translucent, leaves (leaf cards) opaque and double sided; all vertex coloured
        gltf.scene.traverse((o) => {
          const mesh = o as Mesh
          if (!mesh.isMesh) return
          const old = mesh.material as MeshLambertMaterial
          const colors = mesh.geometry.hasAttribute('color')
          const base = colors ? { vertexColors: true } : { color: old.color ?? '#a29d94' }
          const name = `${mesh.name} ${mesh.parent?.name ?? ''}`
          mesh.material = name.includes('see_through')
            ? new MeshLambertMaterial({ ...base, transparent: true, opacity: 0.35, side: DoubleSide, depthWrite: false })
            : name.includes('leaves')
              ? new MeshLambertMaterial({ ...base, side: DoubleSide })
              : new MeshLambertMaterial(base)
          old.dispose?.()
          mesh.userData.sharedGeometry = true
          mesh.userData.sharedMaterial = true
        })
        return gltf.scene as Object3D
      })
      .catch(() => null)
    models.set(key, model)
  }
  return model
}

/** A box of the bounds, for a template without a model, a spawn point, or while the model loads */
export function boundsBox(min: number[], max: number[], color: string) {
  const size = new Vector3(max[0] - min[0], max[1] - min[1], max[2] - min[2]).max(new Vector3(0.2, 0.2, 0.2))
  // BF2 z is three.js -z
  const centre = new Vector3((min[0] + max[0]) / 2, (min[1] + max[1]) / 2, -(min[2] + max[2]) / 2)
  const group = new Group()
  const box = new Mesh(new BoxGeometry(size.x, size.y, size.z), new MeshLambertMaterial({ color, transparent: true, opacity: 0.55 }))
  box.position.copy(centre)
  const edges = new LineSegments(new EdgesGeometry(box.geometry), new LineBasicMaterial({ color }))
  edges.position.copy(centre)
  group.add(box, edges)
  return group
}

// ---------------------------------------------------------------------------------------------------------------
// Thumbnails: rendered one at a time in a small offscreen renderer, kept as data URLs

const THUMB_SIZE = 96
let thumbRenderer: WebGLRenderer | null = null
let thumbScene: Scene | null = null
let thumbCamera: PerspectiveCamera | null = null
const thumbs = new Map<string, Promise<string | null>>()
let queue: Promise<unknown> = Promise.resolve()

function thumbSetup() {
  if (!thumbRenderer) {
    thumbRenderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true })
    thumbRenderer.setSize(THUMB_SIZE, THUMB_SIZE)
    thumbRenderer.outputColorSpace = SRGBColorSpace
    thumbScene = new Scene()
    thumbScene.add(new AmbientLight('#ffffff', 1.4))
    const sun = new DirectionalLight('#fff3e0', 2.2)
    sun.position.set(-3, 5, 2)
    thumbScene.add(sun)
    thumbCamera = new PerspectiveCamera(30, 1, 0.1, 5000)
  }
  return { renderer: thumbRenderer, scene: thumbScene!, camera: thumbCamera! }
}

/** A picture of the template from above at an angle, or null without a model */
export function templateThumbnail(template: string): Promise<string | null> {
  const key = template.toLowerCase()
  let thumb = thumbs.get(key)
  if (!thumb) {
    // One at a time: they share the renderer
    thumb = queue.then(async () => {
      const model = await loadTemplateModel(key)
      if (!model) return null
      const { renderer, scene, camera } = thumbSetup()
      const object = model.clone()
      scene.add(object)
      const box = new Box3().setFromObject(object)
      const centre = box.getCenter(new Vector3())
      const radius = Math.max(box.getSize(new Vector3()).length() / 2, 0.5)
      const distance = radius / Math.sin((camera.fov * Math.PI) / 360)
      camera.position.copy(centre).add(new Vector3(0.8, 0.6, 1).normalize().multiplyScalar(distance))
      camera.near = distance / 100
      camera.far = distance * 4
      camera.updateProjectionMatrix()
      camera.lookAt(centre)
      renderer.render(scene, camera)
      const url = renderer.domElement.toDataURL('image/png')
      scene.remove(object)
      return url
    })
    queue = thumb.catch(() => null)
    thumbs.set(key, thumb)
  }
  return thumb
}
