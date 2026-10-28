import { Euler, Matrix4, Quaternion, Vector3 } from 'three'
import { forwardOf, localToWorld, upOf } from './coords'

// BF2's cameras, rebuilt from what the server sends. The server's camera object (AimStream) is the seat's camera, with
// the player's free look, in every view mode, or the TV missile's camera while one flies. The external views are
// computed by the client every frame (Camera::getTransformation, OpenBF2 Code/BF2/Game/Objects/Base/Camera.cpp:648-667),
// so the viewer computes them the same way from the vehicle's pose.

/** The admin channel's view modes (PlayerState) */
export const VIEW_MODES: Record<number, string> = {
  0: 'None',
  1: 'Inside',
  2: 'Nose',
  3: 'Chase',
  4: 'Front chase',
  5: 'Fly-by',
  6: 'Trace',
  7: 'Extern trace',
  255: 'No camera',
}

/** A camera template's values (CameraTemplate; defaults from CameraTemplate.cpp:12-43) */
export type CameraTemplate = {
  /** Vertical field of view, radians: insideFOV in view 1, worldFOV in the others (Camera.cpp:1104-1110) */
  insideFov: number
  worldFov: number
  /** Nose view: added to the camera position, in the camera parent's frame */
  nosePos: [number, number, number]
  /** Chase: metres from the target; -1 = twice the vehicle's bounding radius */
  chaseDistance: number
  /** Chase: how far up the camera sits, as a share of the vehicle's up axis added to its back axis */
  chaseAngle: number
  /** Chase: the point looked at, in the root vehicle's frame */
  chaseOffset: [number, number, number]
  /** Chase: how fast the camera swings after the vehicle (blend = dt x stiffness) */
  followStiffness: number
}

const DEFAULT_CAMERA: CameraTemplate = {
  insideFov: 1.2,
  worldFov: 1.2,
  nosePos: [0, 0, 0],
  chaseDistance: -1,
  chaseAngle: 0.3,
  chaseOffset: [0, 0, 0],
  followStiffness: 1,
}

const heliCamera = (nosePos: [number, number, number], chaseDistance: number): CameraTemplate => ({
  insideFov: 1.1,
  worldFov: 1.1,
  nosePos,
  chaseDistance,
  chaseAngle: 0.25,
  chaseOffset: [0, 2.5, 6],
  followStiffness: 2,
})

/** The helicopter seat cameras (objects_server.zip ahe_ah1z.tweak, ahe_z10.tweak): seat 0 pilot, seat 1 gunner */
const SEAT_CAMERAS: Record<string, CameraTemplate[]> = {
  ahe_ah1z: [heliCamera([0, 0, 2.25], 23), heliCamera([0, 0, 1.5], 23)],
  ahe_z10: [heliCamera([0, 0, 3.25], 28), heliCamera([0, 0, 4.5], 28)],
}

/**
 * The gunner's TV launcher camera (CameraId 11: AHE_AH1Z_HellFireLauncherLaser_Camera, ahe_z10_HJ8LauncherCamera),
 * active while the TV missile is selected. It has no external views
 */
export const TV_AIM_CAMERA: CameraTemplate = { ...DEFAULT_CAMERA, insideFov: 0.6, worldFov: 0.6 }

/**
 * The TV missile's own camera (agm114_hellfire_tv_Camera, hellfire_tv.tweak). It has no inside view, so it starts in
 * the chase view: a metre behind a point 2 m ahead of the missile, looking along its smoothed heading
 */
export const MISSILE_CAMERA: CameraTemplate = {
  insideFov: 0.6,
  worldFov: 0.6,
  nosePos: [0, 0, 0],
  chaseDistance: 1,
  chaseAngle: 0,
  chaseOffset: [0, 0, 2],
  followStiffness: 6,
}

/** BF2 helicopters' rough physics bounding radius, for the fly-by view and a missing chase distance */
const HELI_RADIUS = 8

export function seatCamera(template: string, seat: number): CameraTemplate {
  const seats = SEAT_CAMERAS[template.toLowerCase()]
  return seats?.[seat] ?? seats?.[0] ?? DEFAULT_CAMERA
}

export function isHelicopter(template: string) {
  return template.toLowerCase() in SEAT_CAMERAS
}

/** The vertical field of view in degrees for a viewport: BF2 keeps it from 4:3 to 16:9 (RenderView::setAdjustedFov) */
export function adjustedFovDegrees(fov: number, width: number, height: number) {
  const aspect = height / Math.max(width, 1)
  let radians = fov
  if (aspect <= 0.5625) radians = fov * aspect * 1.7777778
  else if (aspect >= 0.8) radians = fov * aspect * 1.3333334
  return (radians * 180) / Math.PI
}

/** The camera's place and look: a three.js camera pose */
export type CameraPose = { position: Vector3; quaternion: Quaternion; fov: number }

const _target = new Vector3()
const _desired = new Vector3()
const _f = new Vector3()
const _u = new Vector3()
const _m = new Matrix4()
const _q = new Quaternion()
const _e = new Euler(0, 0, 0, 'YXZ')
const WORLD_UP = new Vector3(0, 1, 0)

/** Looks from eye at target with world up (no roll), as calcLookAtMatrix */
export function lookAt(eye: Vector3, target: Vector3, out: Quaternion) {
  return out.setFromRotationMatrix(_m.lookAt(eye, target, WORLD_UP))
}

/** The swinging state of a chase camera (Camera::m_chaseRotation, m_chaseDistance) */
export class ChaseState {
  readonly dir = new Vector3()
  distance = 0
  subject = ''
  valid = false

  reset() {
    this.valid = false
  }
}

/**
 * Chase and front chase (Camera::getTransformationCVMChase, Camera.cpp:744-857): the camera hangs chaseDistance from a
 * point chaseOffset in the root vehicle's frame, along its back axis tilted up by chaseAngle, swinging after it with
 * followStiffness, and looks at that point with world up. dt is game time in seconds; a new subject snaps.
 */
export function chaseCamera(
  state: ChaseState,
  subject: string,
  rootPosition: Vector3,
  rootRotation: Quaternion,
  template: CameraTemplate,
  front: boolean,
  dt: number,
  minHeight: number,
  out: CameraPose,
) {
  const [ox, oy, oz] = template.chaseOffset
  _target.copy(rootPosition)
  if (ox || oy || oz) _target.add(localToWorld(ox, oy, oz, rootRotation, _desired))

  const sign = front ? -1 : 1
  forwardOf(rootRotation, _f)
  upOf(rootRotation, _u)
  _desired.copy(_f).multiplyScalar(-sign).addScaledVector(_u, template.chaseAngle).normalize()

  const distance = template.chaseDistance === -1 ? HELI_RADIUS * 2 : template.chaseDistance
  if (!state.valid || state.subject !== subject) {
    state.dir.copy(_desired)
    state.distance = distance
    state.subject = subject
    state.valid = true
  } else if (dt > 0) {
    // The engine blends by dt x stiffness each rendered frame; this is the same at any step size
    const blend = 1 - Math.exp(-dt * template.followStiffness)
    state.dir.lerp(_desired, blend).normalize()
    state.distance += (distance - state.distance) * blend
  }

  out.position.copy(_target).addScaledVector(state.dir, state.distance)
  // The engine keeps the eye a metre above the terrain and the water; the viewer only knows the water level
  out.position.y = Math.max(out.position.y, minHeight + 1)
  lookAt(out.position, _target, out.quaternion)
  out.fov = template.worldFov
  return out
}

/**
 * The nose view (Camera::getTransformationCVMNose, Camera.cpp:723-739): the camera's own transform, moved by nosePos in
 * its parent's frame. A pilot's camera hangs on the vehicle; a gunner's on a camera mount that turns with the free-look
 * yaw, which is taken from the camera's rotation relative to the vehicle.
 */
export function noseCamera(
  cameraPosition: Vector3,
  cameraRotation: Quaternion,
  rootRotation: Quaternion,
  template: CameraTemplate,
  gunner: boolean,
  out: CameraPose,
) {
  _q.copy(rootRotation)
  if (gunner) {
    _e.setFromQuaternion(_q.clone().invert().multiply(cameraRotation), 'YXZ')
    _q.multiply(new Quaternion().setFromAxisAngle(WORLD_UP, _e.y))
  }
  const [x, y, z] = template.nosePos
  out.position.copy(cameraPosition).add(localToWorld(x, y, z, _q, _desired))
  out.quaternion.copy(cameraRotation)
  out.fov = template.worldFov
  return out
}

/** The fly-by camera's recorded point */
export class FlyByState {
  readonly point = new Vector3()
  distance = 0
  subject = ''
  valid = false

  reset() {
    this.valid = false
  }
}

/**
 * The fly-by view (Camera::getTransformationCVMFlyBy, Camera.cpp:951-1035), simplified: the engine records points
 * ahead of the vehicle (0.4 x 8 x (forward x 1.2 x radius + velocity x 0.3)) on a timer of its own and looks from them
 * at the vehicle; the viewer places one such point and moves on to the next once the vehicle has flown past it.
 */
export function flyByCamera(
  state: FlyByState,
  subject: string,
  cameraPosition: Vector3,
  rootRotation: Quaternion,
  velocity: Vector3,
  minHeight: number,
  out: CameraPose,
  fov: number,
) {
  const away = state.valid ? state.point.distanceTo(cameraPosition) : 0
  if (!state.valid || state.subject !== subject || away > Math.max(state.distance * 1.5, 40)) {
    forwardOf(rootRotation, _f)
    _desired
      .copy(_f)
      .multiplyScalar(1.2 * HELI_RADIUS)
      .addScaledVector(velocity, 0.3)
      .multiplyScalar(0.4 * 8)
    state.point.copy(cameraPosition).add(_desired)
    state.point.y = Math.max(state.point.y, minHeight + 1)
    state.distance = _desired.length()
    state.subject = subject
    state.valid = true
  }
  out.position.copy(state.point)
  lookAt(out.position, cameraPosition, out.quaternion)
  out.fov = fov
  return out
}
