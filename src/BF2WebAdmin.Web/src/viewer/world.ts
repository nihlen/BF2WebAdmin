import {
  BufferGeometry,
  ClampToEdgeWrapping,
  Color,
  type DirectionalLight,
  DoubleSide,
  Float32BufferAttribute,
  type FogExp2,
  type Group,
  type Material,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  MeshPhongMaterial,
  type Object3D,
  PlaneGeometry,
  RepeatWrapping,
  type Scene,
  SRGBColorSpace,
  TextureLoader,
} from 'three'
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js'
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { viewerAssets } from '@/lib/static-assets'

// The map's world as the 3D viewer and the map builder draw it: the terrain and static objects (map.glb under viewerAssets(), built by
// BF2v2 tools/replay-map/build_map.py), the sky dome and the sea. A map without a 3D build gets a flat ground.

/** The Draco decoder (Google's, Apache 2.0) is served by WebAdmin itself */
export const DRACO = '/viewer/draco/'
export const DEFAULT_SEA_LEVEL = 141.4
export const FOG_DENSITY = 0.0005
export const DEFAULT_HORIZON_COLOR = '#b1bad0'
const SKY_RADIUS = 20000

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

export type LoadedWorld = {
  seaLevel: number
  /** The sky dome, to keep centred on the camera */
  sky: Mesh | null
  /** The terrain's meshes (and the flat ground without a 3D map), for picking a point on the ground */
  ground: Object3D[]
  /** The map's own static objects, for picking a point on them */
  objects: Object3D[]
  /** The sea surface */
  water: Mesh
  /** Whether the map has a 3D build; without one the ground is a flat plane at the default sea level */
  has3d: boolean
}

/** Loads the world of a map into the group (cleared by the caller) and sets the scene's horizon and sun from its sky */
export async function loadMapWorld(map: string, world: Group, scene: Scene, sun: DirectionalLight, isDisposed: () => boolean): Promise<LoadedWorld> {
  const base = `${viewerAssets()}/maps/${map.toLowerCase()}`
  let meta: MapMeta | null = null
  try {
    const response = await fetch(`${base}/map.json`)
    if (response.ok && response.headers.get('content-type')?.includes('json')) meta = (await response.json()) as MapMeta
  } catch {
    meta = null
  }

  let seaLevel = DEFAULT_SEA_LEVEL
  let sky: Mesh | null = null
  const ground: Object3D[] = []
  const objects: Object3D[] = []
  if (meta) {
    seaLevel = meta.seaLevel
    if (meta.sky) sky = await loadSky(`${base}/${meta.sky.texture}`, meta.sky, world, scene, sun, isDisposed)
    if (isDisposed()) return { seaLevel, sky, ground, objects, water: new Mesh(), has3d: true }
    const draco = new DRACOLoader().setDecoderPath(DRACO)
    const gltf = await new GLTFLoader().setDRACOLoader(draco).loadAsync(`${base}/map.glb`)
    draco.dispose()
    gltf.scene.traverse((o) => {
      const mesh = o as Mesh
      if (!mesh.isMesh) return
      const old = mesh.material as Material & { map?: MeshLambertMaterial['map'] }
      if (mesh.name.startsWith('terrain')) {
        mesh.material = new MeshLambertMaterial({ map: old.map ?? null })
        ground.push(mesh)
      } else if (mesh.name.startsWith('outer_terrain')) {
        mesh.material = new MeshLambertMaterial({ color: '#6f6f5a' })
        ground.push(mesh)
      } else if (mesh.name.startsWith('see_through')) {
        // Wire fences and grates: the holes are in textures the map doesn't carry, so show them translucent
        mesh.material = new MeshLambertMaterial({ vertexColors: true, transparent: true, opacity: 0.35, side: DoubleSide, depthWrite: false })
      } else if (mesh.name.startsWith('objects') || mesh.name.startsWith('trees')) {
        mesh.material = mesh.geometry.hasAttribute('color')
          ? new MeshLambertMaterial({ vertexColors: true })
          : new MeshLambertMaterial({ color: '#a29d94', flatShading: true })
        if (mesh.name.startsWith('objects')) objects.push(mesh)
      }
      old.dispose()
    })
    if (isDisposed()) return { seaLevel, sky, ground, objects, water: new Mesh(), has3d: true }
    world.add(gltf.scene)
  } else {
    // No 3D map for this level: a plain ground at the default sea level, with a grid of 100 m squares
    const plane = new Mesh(new PlaneGeometry(4096, 4096, 41, 41), new MeshLambertMaterial({ color: '#6b6b55', wireframe: false }))
    plane.rotation.x = -Math.PI / 2
    plane.position.y = seaLevel + 0.5
    const grid = new Mesh(new PlaneGeometry(4096, 4096, 41, 41), new MeshBasicMaterial({ color: '#8a8a70', wireframe: true }))
    grid.rotation.x = -Math.PI / 2
    grid.position.y = seaLevel + 0.6
    world.add(plane, grid)
    ground.push(plane)
  }

  const water = new Mesh(
    new PlaneGeometry(40000, 40000),
    new MeshPhongMaterial({ color: '#2f4d5e', transparent: true, opacity: 0.88, shininess: 40, specular: '#46555e' }),
  )
  water.rotation.x = -Math.PI / 2
  water.position.y = seaLevel
  // Drawn first among the transparent objects and writing depth: the lines and ghosts drawn after it are hidden
  // where the sea is in front of them and drawn over it where they are in front
  water.renderOrder = -1
  world.add(water)
  return { seaLevel, sky, ground, objects, water, has3d: meta !== null }
}

async function loadSky(url: string, meta: SkyMeta, world: Group, scene: Scene, sun: DirectionalLight, isDisposed: () => boolean) {
  const texture = await new TextureLoader().loadAsync(url).catch(() => null)
  if (!texture || isDisposed()) return null
  texture.colorSpace = SRGBColorSpace
  texture.wrapS = RepeatWrapping
  texture.wrapT = ClampToEdgeWrapping
  const sky = new Mesh(skyDomeGeometry(meta.uOffset), new MeshBasicMaterial({ map: texture, fog: false, depthWrite: false, side: DoubleSide }))
  sky.renderOrder = -1
  sky.frustumCulled = false
  world.add(sky)

  const horizon = new Color(meta.horizonColor)
  scene.background = horizon
  ;(scene.fog as FogExp2 | null)?.color.copy(horizon)
  if (meta.sunDirection) sun.position.fromArray(meta.sunDirection).multiplyScalar(1000)
  return sky
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

export function disposeTree(root: Object3D) {
  root.traverse((o) => {
    const mesh = o as Mesh
    if (mesh.geometry && !mesh.userData.sharedGeometry) mesh.geometry.dispose()
    // The map builder's template models: every clone uses the cached model's materials
    if (mesh.userData.sharedMaterial) return
    const materials = mesh.material ? (Array.isArray(mesh.material) ? mesh.material : [mesh.material]) : []
    for (const m of materials) {
      ;(m as Material & { map?: { dispose(): void } }).map?.dispose()
      m.dispose()
    }
  })
}
