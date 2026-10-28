import { Euler, Vector3, type PerspectiveCamera } from 'three'
import { BACK, BOOST, DOWN, FORWARD, LEFT, RIGHT, UP } from './keys'

const _euler = new Euler(0, 0, 0, 'YXZ')
const _dir = new Vector3()
const _move = new Vector3()

/**
 * Free flying camera like the BF2 spectator: drag to look around, WASD/arrows to fly where you look,
 * Q/E down/up, Shift to go faster and the mouse wheel to move forward and back.
 */
export class SpectatorControls {
  enabled = false
  /** m/s */
  speed = 60
  boostFactor = 4
  /** Lowest camera height (the sea level) */
  minHeight = -Infinity

  private readonly keys = new Set<string>()
  private readonly velocity = new Vector3()
  private yaw = 0
  private pitch = 0
  private dragging = false
  private lastX = 0
  private lastY = 0

  private readonly camera: PerspectiveCamera
  private readonly dom: HTMLElement

  constructor(camera: PerspectiveCamera, dom: HTMLElement) {
    this.camera = camera
    this.dom = dom
    dom.addEventListener('pointerdown', this.onPointerDown)
    dom.addEventListener('pointermove', this.onPointerMove)
    dom.addEventListener('pointerup', this.onPointerUp)
    dom.addEventListener('pointercancel', this.onPointerUp)
    dom.addEventListener('wheel', this.onWheel, { passive: false })
    dom.addEventListener('contextmenu', this.onContextMenu)
  }

  /** Takes over the camera's current view direction (after another mode or a scripted move) */
  syncFromCamera() {
    _euler.setFromQuaternion(this.camera.quaternion, 'YXZ')
    this.yaw = _euler.y
    this.pitch = _euler.x
    this.velocity.set(0, 0, 0)
  }

  setKey(code: string, down: boolean) {
    if (down) this.keys.add(code)
    else this.keys.delete(code)
  }

  clearKeys() {
    this.keys.clear()
  }

  update(dtMs: number) {
    if (!this.enabled) return
    const pressed = (codes: string[]) => (codes.some((c) => this.keys.has(c)) ? 1 : 0)

    _move.set(pressed(RIGHT) - pressed(LEFT), pressed(UP) - pressed(DOWN), pressed(BACK) - pressed(FORWARD))
    const speed = this.speed * (pressed(BOOST) ? this.boostFactor : 1)
    const target = new Vector3()
    if (_move.lengthSq() > 0) {
      // Forward/back and strafe follow the view (like BF2), up/down is always vertical
      _dir.set(_move.x, 0, _move.z).applyQuaternion(this.camera.quaternion)
      target
        .copy(_dir)
        .add(new Vector3(0, _move.y, 0))
        .normalize()
        .multiplyScalar(speed)
    }
    // Ease in and out instead of starting and stopping instantly
    this.velocity.lerp(target, 1 - Math.exp(-dtMs / 90))
    this.camera.position.addScaledVector(this.velocity, dtMs / 1000)
    this.camera.position.y = Math.max(this.camera.position.y, this.minHeight)

    _euler.set(this.pitch, this.yaw, 0, 'YXZ')
    this.camera.quaternion.setFromEuler(_euler)
  }

  private onPointerDown = (e: PointerEvent) => {
    if (!this.enabled || (e.button !== 0 && e.button !== 2)) return
    this.dragging = true
    this.lastX = e.clientX
    this.lastY = e.clientY
    this.dom.setPointerCapture(e.pointerId)
  }

  private onPointerMove = (e: PointerEvent) => {
    if (!this.enabled || !this.dragging) return
    const dx = e.clientX - this.lastX
    const dy = e.clientY - this.lastY
    this.lastX = e.clientX
    this.lastY = e.clientY
    this.yaw -= dx * 0.004
    this.pitch = Math.min(Math.max(this.pitch - dy * 0.004, -Math.PI / 2 + 0.01), Math.PI / 2 - 0.01)
  }

  private onPointerUp = (e: PointerEvent) => {
    if (!this.dragging) return
    this.dragging = false
    if (this.dom.hasPointerCapture(e.pointerId)) this.dom.releasePointerCapture(e.pointerId)
  }

  private onWheel = (e: WheelEvent) => {
    if (!this.enabled) return
    e.preventDefault()
    const boost = this.keys.has('ShiftLeft') || this.keys.has('ShiftRight') ? this.boostFactor : 1
    _dir.set(0, 0, -1).applyQuaternion(this.camera.quaternion)
    this.camera.position.addScaledVector(_dir, -Math.sign(e.deltaY) * 15 * boost)
    this.camera.position.y = Math.max(this.camera.position.y, this.minHeight)
  }

  private onContextMenu = (e: Event) => {
    if (this.enabled) e.preventDefault()
  }

  dispose() {
    this.dom.removeEventListener('pointerdown', this.onPointerDown)
    this.dom.removeEventListener('pointermove', this.onPointerMove)
    this.dom.removeEventListener('pointerup', this.onPointerUp)
    this.dom.removeEventListener('pointercancel', this.onPointerUp)
    this.dom.removeEventListener('wheel', this.onWheel)
    this.dom.removeEventListener('contextmenu', this.onContextMenu)
  }
}
