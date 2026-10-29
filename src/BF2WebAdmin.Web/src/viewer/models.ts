import { AdditiveBlending, Color, ConeGeometry, CylinderGeometry, Group, Mesh, MeshBasicMaterial, MeshLambertMaterial, SphereGeometry } from 'three'

/** The Hellfire's length and its origin: the centre (collision mesh agm114_hellfire: z -0.875 to 0.871, radius 0.113) */
export const MISSILE_LENGTH = 1.75

/**
 * A TV-guided missile at true size with a glowing motor. Faces -Z, origin at its centre: the point the engine sweeps
 * (physicsType Point, no radius; OpenBF2 PointResponsePhysics.cpp:112-145), so the nose is 0.87 m ahead of it.
 */
export class MissileModel {
  readonly root = new Group()
  private readonly glow: Mesh
  private testPoint: Mesh | null = null

  constructor(teamColor: string) {
    const half = MISSILE_LENGTH / 2
    const noseLength = 0.3
    const body = new Mesh(new CylinderGeometry(0.09, 0.09, MISSILE_LENGTH - noseLength, 8), new MeshLambertMaterial({ color: '#d9d9d0' }))
    body.rotation.x = Math.PI / 2
    body.position.z = noseLength / 2
    this.root.add(body)

    const nose = new Mesh(new ConeGeometry(0.09, noseLength, 8), new MeshLambertMaterial({ color: '#d9d9d0' }))
    nose.rotation.x = -Math.PI / 2
    nose.position.z = -half + noseLength / 2
    this.root.add(nose)

    const glow = (this.glow = new Mesh(
      new SphereGeometry(0.4, 10, 8),
      new MeshBasicMaterial({
        color: new Color(teamColor).lerp(new Color('#ffd27a'), 0.6),
        transparent: true,
        opacity: 0.85,
        blending: AdditiveBlending,
        depthWrite: false,
      }),
    ))
    glow.position.z = half + 0.3
    glow.scale.set(1, 1, 2.2)
    this.root.add(glow)
  }

  /** A small bright dot on the origin, drawn over everything: the point the engine tests */
  addTestPoint(color: string) {
    const dot = new Mesh(new SphereGeometry(0.12, 12, 8), new MeshBasicMaterial({ color, depthTest: false, transparent: true }))
    dot.renderOrder = 8
    this.root.add(dot)
    this.testPoint = dot
  }

  /** Hidden when the camera is this missile's own camera, where the dot would sit in front of the lens */
  setTestPointVisible(show: boolean) {
    if (this.testPoint) this.testPoint.visible = show
  }

  /** Shows or hides the motor's glow (the viewer's effects toggle) */
  setEffects(show: boolean) {
    this.glow.visible = show
  }
}

/** Fireball + smoke, driven by the time since the explosion so it scrubs correctly */
export class Explosion {
  readonly root = new Group()
  private readonly fire: Mesh<SphereGeometry, MeshBasicMaterial>
  private readonly smoke: Mesh<SphereGeometry, MeshBasicMaterial>

  private readonly size: number

  constructor(size: number) {
    this.size = size
    this.fire = new Mesh(
      new SphereGeometry(1, 16, 12),
      new MeshBasicMaterial({ color: '#ffb347', transparent: true, blending: AdditiveBlending, depthWrite: false }),
    )
    this.smoke = new Mesh(new SphereGeometry(1, 16, 12), new MeshBasicMaterial({ color: '#3a3734', transparent: true, depthWrite: false }))
    this.root.add(this.smoke, this.fire)
    this.root.visible = false
  }

  /** elapsedMs < 0 hides it */
  update(elapsedMs: number) {
    const fireMs = 900
    const smokeMs = 4000
    if (elapsedMs < 0 || elapsedMs > smokeMs) {
      this.root.visible = false
      return
    }
    this.root.visible = true

    const f = Math.min(elapsedMs / fireMs, 1)
    this.fire.visible = f < 1
    this.fire.scale.setScalar(this.size * (0.3 + 0.9 * Math.sqrt(f)))
    this.fire.material.opacity = 1 - f
    this.fire.material.color.setHSL(0.09 - 0.06 * f, 1, 0.6 - 0.25 * f)

    const s = elapsedMs / smokeMs
    this.smoke.scale.setScalar(this.size * (0.4 + 1.4 * Math.sqrt(s)))
    this.smoke.position.y = s * this.size * 1.5
    this.smoke.material.opacity = 0.55 * (1 - s)
  }
}
