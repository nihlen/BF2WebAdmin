import { Quaternion, Vector3 } from 'three'
import type { ViewerFrame } from '@/api/events'
import { rotToQuaternion, vecToWorld } from './coords'

/** The module ticks at 30 Hz; viewer time is in ticks */
export const TICK_RATE = 30

/** How much of the live stream the page keeps: ten minutes */
const MAX_TICKS = 10 * 60 * TICK_RATE

/** Samples further apart than this are not interpolated (a dropped frame or two is) */
const MAX_LERP_TICKS = 4

/** Faster than this between two samples is a teleport or a respawn, not a move */
const MAX_SPEED = 400

/** A vehicle that has had no sample for this long is gone: unchanged vehicles are still sent once a second */
const VEHICLE_TIMEOUT_TICKS = 40

type Sample = { tick: number }

export type PlayerSample = Sample & {
  team: number
  life: number
  flags: number
  root: number
  seat: number
  view: number
  lag: number
  ping: number
  p: number[]
  r: number[]
  v: number[]
}

export type VehicleSample = Sample & { template: string; team: number; health: number; p: number[]; r: number[] }

export type AimSample = Sample & { flags: number; cp: number[]; cr: number[]; wp: number[]; wr: number[] }

export type ProjectileSample = Sample & { template: string; owner: number; p: number[]; r: number[]; v: number[] }

/** A time series of one entity, in tick order */
export class Series<T extends Sample> {
  readonly samples: T[] = []

  get first() {
    return this.samples[0]
  }

  get last() {
    return this.samples[this.samples.length - 1]
  }

  push(sample: T) {
    const last = this.last
    if (!last || sample.tick > last.tick) this.samples.push(sample)
    else if (sample.tick === last.tick) this.samples[this.samples.length - 1] = sample
    // Older ticks (a frame that overtook another) are dropped
  }

  /** Index of the last sample at or before tick, -1 if none */
  index(tick: number) {
    const s = this.samples
    let lo = 0
    let hi = s.length - 1
    if (hi < 0 || tick < s[0].tick) return -1
    if (tick >= s[hi].tick) return hi
    while (hi - lo > 1) {
      const mid = (lo + hi) >> 1
      if (s[mid].tick <= tick) lo = mid
      else hi = mid
    }
    return lo
  }

  /** The samples around tick: a at or before it, b after it (when close enough to interpolate) and the blend 0..1 */
  around(tick: number): { a: T; b: T | null; u: number } | null {
    const i = this.index(tick)
    if (i < 0) return null
    const a = this.samples[i]
    const b = this.samples[i + 1]
    if (!b || b.tick - a.tick > MAX_LERP_TICKS) return { a, b: null, u: 0 }
    return { a, b, u: (tick - a.tick) / (b.tick - a.tick) }
  }

  prune(minTick: number) {
    let n = 0
    while (n < this.samples.length - 1 && this.samples[n].tick < minTick) n++
    if (n > 0) this.samples.splice(0, n)
  }
}

const _b = new Vector3()
const _qb = new Quaternion()

/** Position (world) between two samples' BF2 vectors; a teleport between them holds the first */
export function lerpPosition(a: number[], b: number[] | null, u: number, ticks: number, out: Vector3) {
  vecToWorld(a, out)
  if (!b || u <= 0) return out
  vecToWorld(b, _b)
  if (out.distanceTo(_b) > (MAX_SPEED * Math.max(ticks, 1)) / TICK_RATE) return out
  return out.lerp(_b, u)
}

/** Rotation (world) between two samples' BF2 rotations */
export function slerpRotation(a: number[], b: number[] | null, u: number, out: Quaternion) {
  rotToQuaternion(a, out)
  if (!b || u <= 0) return out
  return out.slerp(rotToQuaternion(b, _qb), u)
}

/** A sample's position and rotation at tick, interpolated; null before its first sample */
export function poseAt<T extends Sample>(
  series: Series<T>,
  tick: number,
  pos: (s: T) => number[],
  rot: (s: T) => number[],
  outPosition: Vector3,
  outRotation: Quaternion,
) {
  const at = series.around(tick)
  if (!at) return null
  const ticks = at.b ? at.b.tick - at.a.tick : 0
  lerpPosition(pos(at.a), at.b && pos(at.b), at.u, ticks, outPosition)
  slerpRotation(rot(at.a), at.b && rot(at.b), at.u, outRotation)
  return at.a
}

/**
 * The live stream as it arrives from the hub (ViewerFrame events), kept for ten minutes so the timeline can go back.
 * Frames of one tick come from separate streams (players, vehicles, aim, projectiles); each entity has its own series.
 */
export class LiveBuffer {
  readonly players = new Map<number, Series<PlayerSample>>()
  readonly vehicles = new Map<number, Series<VehicleSample>>()
  readonly aims = new Map<number, Series<AimSample>>()
  readonly projectiles = new Map<number, Series<ProjectileSample>>()
  firstTick = -1
  lastTick = -1
  /** performance.now() when the last frame arrived */
  lastArrival = 0
  private pruneAt = 0

  get isEmpty() {
    return this.lastTick < 0
  }

  clear() {
    this.players.clear()
    this.vehicles.clear()
    this.aims.clear()
    this.projectiles.clear()
    this.firstTick = this.lastTick = -1
  }

  push(frame: ViewerFrame) {
    const tick = frame.tick
    // A new admin channel link starts its ticks over: start over too
    if (this.lastTick >= 0 && tick < this.lastTick - 10 * TICK_RATE) this.clear()

    if (frame.players) {
      for (const p of frame.players) {
        series(this.players, p.id).push({
          tick,
          team: p.team,
          life: p.life,
          flags: p.flags,
          root: p.root,
          seat: p.seat,
          view: p.view,
          lag: p.lag,
          ping: p.ping,
          p: p.p,
          r: p.r,
          v: p.v,
        })
      }
    }
    if (frame.vehicles) {
      for (const v of frame.vehicles) {
        series(this.vehicles, v.id).push({ tick, template: v.template, team: v.team, health: v.health, p: v.p, r: v.r })
      }
    }
    if (frame.aims) {
      for (const a of frame.aims) series(this.aims, a.id).push({ tick, flags: a.flags, cp: a.cp, cr: a.cr, wp: a.wp, wr: a.wr })
    }
    if (frame.projectiles) {
      for (const p of frame.projectiles) {
        series(this.projectiles, p.id).push({ tick, template: p.template, owner: p.owner, p: p.p, r: p.r, v: p.v })
      }
    }

    if (this.firstTick < 0) this.firstTick = tick
    this.lastTick = Math.max(this.lastTick, tick)
    this.lastArrival = performance.now()

    if (tick >= this.pruneAt) {
      this.pruneAt = tick + 10 * TICK_RATE
      this.prune(this.lastTick - MAX_TICKS)
    }
  }

  private prune(minTick: number) {
    if (minTick <= this.firstTick) return
    this.firstTick = minTick
    for (const map of [this.players, this.vehicles, this.aims, this.projectiles] as Map<number, Series<Sample>>[]) {
      for (const [id, s] of map) {
        s.prune(minTick)
        if (s.last.tick < minTick) map.delete(id)
      }
    }
  }

  /** The player's state at tick (the sample at or before it, no older than a second) */
  playerAt(id: number, tick: number) {
    const s = this.players.get(id)
    const i = s ? s.index(tick) : -1
    const sample = i >= 0 ? s!.samples[i] : undefined
    return sample && tick - sample.tick <= TICK_RATE ? sample : undefined
  }

  /** Whether the vehicle exists at tick: it has a sample at or before it that is recent enough */
  vehicleVisible(series: Series<VehicleSample>, tick: number) {
    const i = series.index(tick)
    if (i < 0) return false
    const a = series.samples[i]
    return tick - a.tick <= VEHICLE_TIMEOUT_TICKS || i + 1 < series.samples.length
  }

  /** Whether a projectile is in flight at tick: projectile frames are sent every tick while it lives */
  projectileVisible(series: Series<ProjectileSample>, tick: number) {
    const i = series.index(tick)
    return i >= 0 && (i + 1 < series.samples.length || tick - series.samples[i].tick <= MAX_LERP_TICKS)
  }
}

function series<T extends Sample>(map: Map<number, Series<T>>, id: number) {
  let s = map.get(id)
  if (!s) {
    s = new Series<T>()
    map.set(id, s)
  }
  return s
}

/** Position helpers for poseAt */
export const samplePos = (s: { p: number[] }) => s.p
export const sampleRot = (s: { r: number[] }) => s.r
