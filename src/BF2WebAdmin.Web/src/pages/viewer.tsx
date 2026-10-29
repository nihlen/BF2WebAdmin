import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowLeft, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Crosshair, Pause, Play, Radio } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, errorMessage, fetchClient, type MissileSummary } from '@/api/client'
import { useHubEvent, useViewerSubscription } from '@/api/hub'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useServerLive } from '@/hooks/use-server-live'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'
import { viewerRoute } from '@/router'
import { LiveBuffer, TICK_RATE } from '@/viewer/buffer'
import { isHelicopter } from '@/viewer/cameras'
import { isSpectatorMoveKey, SPECTATOR_KEYS } from '@/viewer/keys'
import {
  type CameraMode,
  CURRENT_COLOR,
  HIT_COLOR,
  IMPACT_COLOR,
  PARTS_COLOR,
  type PlaybackState,
  type PlayerName,
  SWEEP_COLOR,
  TESTED_COLOR,
  teamColor,
  ViewerScene,
  type ViewInfo,
} from '@/viewer/scene'

const speeds = [0.1, 0.25, 0.5, 1, 2]

const missilesQuery = (serverId: string) => api.queryOptions('get', '/api/servers/{serverId}/missiles', { params: { path: { serverId } } })

/**
 * The 3D viewer: the server's players, vehicles and TV missiles at 30 Hz, a player's own view, and the last 20 TV
 * missiles' hit/miss replays (the target where the gunner saw it in red, where the server tested it in blue).
 */
export function ViewerPage() {
  const serverId = decodeServerId(viewerRoute.useParams().serverId)
  const { snapshot } = useServerLive(serverId)
  useViewerSubscription(serverId)
  const queryClient = useQueryClient()
  const missiles = useQuery(missilesQuery(serverId))

  const buffer = useMemo(() => new LiveBuffer(), [])
  const [scene, setScene] = useState<ViewerScene | null>(null)
  const [playback, setPlayback] = useState<PlaybackState | null>(null)
  const [viewInfo, setViewInfo] = useState<ViewInfo>(null)
  const [cameraMode, setCameraMode] = useState<CameraMode>('free')
  const [subject, setSubject] = useState(-1)
  const [selectedMissile, setSelectedMissile] = useState<number | null>(null)
  const [missileError, setMissileError] = useState<string | null>(null)
  const [labels, setLabels] = useState(true)
  const [trails, setTrails] = useState(true)
  // The missile's motor glow and explosion: off while a missile's replay is shown, where they hide the hit
  const [effects, setEffects] = useState(true)
  const [hitboxes, setHitboxes] = useState(false)
  const missileRequest = useRef<number | null>(null)

  const map = snapshot.data?.server.map ?? ''
  const players = useMemo(() => snapshot.data?.players ?? [], [snapshot.data])

  useHubEvent('ViewerFrame', (frame) => buffer.push(frame))
  useHubEvent('MissileEvent', () => void queryClient.invalidateQueries({ queryKey: missilesQuery(serverId).queryKey }))

  // The scene lives as long as its container. The page is loaded on demand (router.tsx), so three.js is only loaded here
  const containerRef = useCallback(
    (container: HTMLDivElement | null) => {
      if (!container) return
      const created = new ViewerScene(container)
      created.setBuffer(buffer)
      created.onPlayback = setPlayback
      created.onViewInfo = setViewInfo
      setScene(created)
      return () => {
        created.dispose()
        setScene(null)
      }
    },
    [buffer],
  )

  useEffect(() => {
    if (scene && map) void scene.loadMap(map)
  }, [scene, map])

  useEffect(() => {
    scene?.setNames(new Map<number, PlayerName>(players.map((p) => [p.index, { name: p.name, team: p.team }])))
  }, [scene, players])

  useEffect(() => scene?.setLabels(labels), [scene, labels])
  useEffect(() => scene?.setTrails(trails), [scene, trails])
  useEffect(() => scene?.setEffects(effects), [scene, effects])
  useEffect(() => scene?.setHitboxes(hitboxes), [scene, hitboxes])
  useEffect(() => scene?.setCameraMode(cameraMode, subject), [scene, cameraMode, subject])

  /** Shows a missile's whole flight, or goes back to live (null) */
  const selectMissile = (key: number | null) => {
    setSelectedMissile(key)
    setMissileError(null)
    setEffects(key === null)
    missileRequest.current = key
    if (!scene) return
    if (key === null) {
      scene.setMissile(null)
      return
    }
    fetchClient
      .GET('/api/servers/{serverId}/missiles/{key}', { params: { path: { serverId, key } } })
      .then(({ data }) => {
        if (data && missileRequest.current === key) scene.setMissile(data)
      })
      .catch((error: unknown) => {
        if (missileRequest.current === key) setMissileError(errorMessage(error))
      })
  }

  // Keys: WASD/QE fly the free camera (Shift faster, Alt slower; slow while a missile is shown), space plays, , and .
  // step one tick, H jumps to the hit, L goes live
  useEffect(() => {
    if (!scene) return
    // Only text entry keeps the keys; a focused checkbox, button or the timeline does not
    const isTyping = (e: KeyboardEvent) =>
      e.target instanceof HTMLTextAreaElement ||
      (e.target instanceof HTMLInputElement && !['checkbox', 'radio', 'range', 'button'].includes(e.target.type)) ||
      (e.target instanceof HTMLElement && e.target.isContentEditable)
    const down = (e: KeyboardEvent) => {
      if (isTyping(e) || e.ctrlKey || e.metaKey) return
      // Alt is the free camera's slow modifier: only flying keys work with it, and the browser's Alt shortcuts are held off
      if (e.altKey) {
        if (SPECTATOR_KEYS.has(e.code)) {
          if (isSpectatorMoveKey(e.code) && cameraMode !== 'free') setCameraMode('free')
          scene.setKey(e.code, true)
          e.preventDefault()
        }
        return
      }
      if (e.code === 'Space') {
        scene.togglePlay()
        e.preventDefault()
      } else if (e.code === 'Comma') scene.step(-1)
      else if (e.code === 'Period') scene.step(1)
      else if (e.code === 'KeyH') scene.seekImpact()
      else if (e.code === 'KeyL') {
        missileRequest.current = null
        setSelectedMissile(null)
        setEffects(true)
        scene.goLive()
      } else if (SPECTATOR_KEYS.has(e.code)) {
        if (isSpectatorMoveKey(e.code) && cameraMode !== 'free') setCameraMode('free')
        scene.setKey(e.code, true)
        e.preventDefault()
      }
    }
    const up = (e: KeyboardEvent) => {
      scene.setKey(e.code, false)
      // Releasing Alt would otherwise move the focus to the browser's menu
      if (e.code === 'AltLeft' || e.code === 'AltRight') e.preventDefault()
    }
    const blur = () => scene.clearKeys()
    window.addEventListener('keydown', down)
    window.addEventListener('keyup', up)
    window.addEventListener('blur', blur)
    return () => {
      window.removeEventListener('keydown', down)
      window.removeEventListener('keyup', up)
      window.removeEventListener('blur', blur)
    }
  }, [scene, cameraMode])

  if (snapshot.isError) return <p className="text-destructive">{errorMessage(snapshot.error)}</p>
  if (!snapshot.data) return <Skeleton className="h-[600px]" />

  const { server } = snapshot.data
  const selected = missiles.data?.find((m) => m.key === selectedMissile)
  const teams = new Map(players.map((p) => [p.name, p.team]))
  const playerNames = new Map(players.map((p) => [p.index, p.name]))
  const outcomeOf = (m: MissileSummary) => missileOutcome(m, teams, playerNames)

  return (
    <div className="flex h-[calc(100svh-6rem)] min-h-[520px] flex-col gap-3">
      <title>{`BF2 Web Admin - ${server.name} - 3D viewer`}</title>

      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          nativeButton={false}
          render={<Link to="/server/$serverId" params={{ serverId: encodeServerId(serverId) }} />}
        >
          <ArrowLeft />
        </Button>
        <h1 className="truncate text-xl font-semibold">{server.name}</h1>
        {server.map && <Badge variant="secondary">{server.map}</Badge>}
        <Badge variant="secondary">{server.socketState}</Badge>
        <FrameRate buffer={buffer} />
      </div>

      <div className="flex min-h-0 flex-1 gap-3">
        <div className="relative min-w-0 flex-1 overflow-hidden rounded-lg border bg-black">
          <div ref={containerRef} className={cn('absolute inset-0', viewInfo?.tv && cameraMode === 'player' && 'grayscale')} />
          {(cameraMode === 'missile' || (viewInfo?.tv && cameraMode === 'player')) && <TvCrosshair />}
          {cameraMode === 'player' && viewInfo && <ViewOverlay info={viewInfo} />}
          {selected && <MissileOverlay missile={selected} outcome={outcomeOf(selected)} playback={playback} onJump={() => scene?.seekImpact()} />}
          {selected && <Legend />}
          {!scene && <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">Loading the 3D view…</div>}
        </div>

        <aside className="flex w-80 shrink-0 flex-col gap-3 overflow-hidden">
          <Card className="gap-2 p-3">
            <div className="text-sm font-medium">Camera</div>
            <div className="grid grid-cols-2 gap-1">
              <CameraButton mode="free" current={cameraMode} onSelect={setCameraMode} label="Free" title="Fly freely: drag to look, WASD/QE to move (Shift faster, Alt slower)" />
              <CameraButton
                mode="orbit"
                current={cameraMode}
                onSelect={setCameraMode}
                label="Orbit"
                title="Circle the selected player, or the selected missile: drag to turn, wheel to zoom"
                disabled={subject < 0 && !selected}
              />
              <CameraButton
                mode="player"
                current={cameraMode}
                onSelect={setCameraMode}
                label="Player view"
                title="What the selected player's camera shows, live or at the timeline's tick: his view mode and camera as the server reports them every tick (the TV launcher, his TV missile; inferred when the server keeps him in the launcher)"
                disabled={subject < 0}
              />
              <CameraButton
                mode="missile"
                current={cameraMode}
                onSelect={setCameraMode}
                label="Missile TV"
                title="The selected missile's own TV camera, rebuilt from its recorded flight (a replay; no player needed)"
                disabled={!selected}
              />
            </div>
            <div className="max-h-48 space-y-0.5 overflow-y-auto">
              {players.length === 0 && <p className="text-xs text-muted-foreground">No players</p>}
              {players.map((p) => (
                <button
                  key={p.index}
                  type="button"
                  className={cn(
                    'flex w-full items-center gap-2 rounded px-2 py-1 text-left text-sm hover:bg-muted',
                    subject === p.index && 'bg-muted',
                  )}
                  onClick={() => {
                    setSubject(p.index)
                    if (cameraMode === 'free' || cameraMode === 'missile') setCameraMode('player')
                  }}
                >
                  <span className="size-2 shrink-0 rounded-full" style={{ background: teamColor(p.team) }} />
                  <span className="truncate">{p.name}</span>
                  {!p.isAlive && <span className="ml-auto text-xs text-muted-foreground">dead</span>}
                </button>
              ))}
            </div>
            <div className="flex gap-3 text-xs text-muted-foreground">
              <label className="flex items-center gap-1" title="Names and labels in the 3D view">
                <input type="checkbox" checked={labels} onChange={(e) => setLabels(e.target.checked)} /> Names
              </label>
              <label className="flex items-center gap-1">
                <input type="checkbox" checked={trails} onChange={(e) => setTrails(e.target.checked)} /> Trails
              </label>
              <label className="flex items-center gap-1" title="The missiles' motor glow and the explosion. Off while a missile is selected">
                <input type="checkbox" checked={effects} onChange={(e) => setEffects(e.target.checked)} /> Exhaust &amp; explosion
              </label>
              <label className="flex items-center gap-1" title="The helicopters' collision meshes on the live view: hull blue, parts orange">
                <input type="checkbox" checked={hitboxes} onChange={(e) => setHitboxes(e.target.checked)} /> Hitboxes
              </label>
            </div>
          </Card>

          <Card className="min-h-0 flex-1 gap-2 p-3">
            <div className="flex items-center justify-between">
              <div className="text-sm font-medium">TV missiles</div>
              {selectedMissile !== null && (
                <Button variant="ghost" size="xs" onClick={() => selectMissile(null)}>
                  Clear
                </Button>
              )}
            </div>
            {missileError && <p className="text-xs text-destructive">{missileError}</p>}
            <div className="min-h-0 flex-1 space-y-1 overflow-y-auto">
              {missiles.isError && <p className="text-xs text-muted-foreground">No missiles: the server is not connected</p>}
              {missiles.data?.length === 0 && <p className="text-xs text-muted-foreground">No TV missiles yet. The last 20 are kept.</p>}
              {missiles.data?.map((m) => (
                <MissileItem
                  key={m.key}
                  missile={m}
                  outcome={outcomeOf(m)}
                  selected={m.key === selectedMissile}
                  onSelect={() => selectMissile(m.key === selectedMissile ? null : m.key)}
                />
              ))}
            </div>
          </Card>
        </aside>
      </div>

      <Timeline scene={scene} playback={playback} onLive={() => selectMissile(null)} />
    </div>
  )
}

function CameraButton({
  mode,
  current,
  onSelect,
  label,
  title,
  disabled,
}: {
  mode: CameraMode
  current: CameraMode
  onSelect: (mode: CameraMode) => void
  label: string
  title: string
  disabled?: boolean
}) {
  return (
    <Button variant={mode === current ? 'default' : 'outline'} size="sm" title={title} disabled={disabled} onClick={() => onSelect(mode)}>
      {label}
    </Button>
  )
}

/** Frames a second from the hub: 30 per stream while the server streams for the viewer */
function FrameRate({ buffer }: { buffer: LiveBuffer }) {
  const [rate, setRate] = useState(0)
  useEffect(() => {
    let lastTick = buffer.lastTick
    const timer = setInterval(() => {
      setRate(lastTick >= 0 && buffer.lastTick >= lastTick ? buffer.lastTick - lastTick : 0)
      lastTick = buffer.lastTick
    }, 1000)
    return () => clearInterval(timer)
  }, [buffer])
  return (
    <Badge variant={rate > 0 ? 'secondary' : 'outline'} title="Server ticks received in the last second">
      {rate > 0 ? `${rate} ticks/s` : 'No live frames'}
    </Badge>
  )
}

type Outcome = { text: string; /** A hit on an enemy helicopter: its hull, a part or its crew */ enemyHit: boolean }

const isHeliTemplate = (template: string) => isHelicopter(template) || /^(ahe|the|she)_/i.test(template)

/**
 * What the missile did. Only a hit on an enemy helicopter is a hit: the hit's root must be one of the missile's targets
 * (the vehicles near its path; buildings and trees are not) and a helicopter, with no crew on the shooter's team. The
 * summary carries the root's template, not its id, so a target of the same template stands for it.
 */
function missileOutcome(m: MissileSummary, teams: Map<string, number>, playerNames: Map<number, string>): Outcome {
  if (m.outcome === 'hit') {
    const root = m.hitRoot ?? ''
    const part = m.hitPart ?? root
    const target = m.targets.find((t) => t.template.toLowerCase() === root.toLowerCase())
    // The engine rewinds only the hull and soldiers, by the shooter's lag; parts and a hull at lag under a tick are not
    const rewound = m.hitRewound ? ' (rewind)' : ' (no rewind)'
    if (target && isHeliTemplate(root)) {
      const what =
        m.hitPlayer >= 0 ? `crew${playerNames.has(m.hitPlayer) ? ` ${playerNames.get(m.hitPlayer)}` : ''}` : part.toLowerCase() !== root.toLowerCase() ? part : 'hull'
      if (target.crew.length === 0) return { text: `Hit empty ${root}: ${what}`, enemyHit: false }
      if (m.shooterTeam > 0 && target.crew.some((name) => teams.get(name) === m.shooterTeam))
        return { text: `Hit friendly ${root}: ${what}`, enemyHit: false }
      return { text: `Hit ${what}${rewound}`, enemyHit: true }
    }
    if (target) return { text: `Hit vehicle ${root}`, enemyHit: false }
    if (m.hitPlayer >= 0) return { text: `Hit soldier${playerNames.has(m.hitPlayer) ? ` ${playerNames.get(m.hitPlayer)}` : ''}`, enemyHit: false }
    return { text: `Hit object ${part || 'unknown'}`, enemyHit: false }
  }
  if (m.outcome === 'terrain') return { text: 'Hit ground / water', enemyHit: false }
  // The trace says it ended at a collision (flag bit 1) but no ProjectileCollision came with it: WebAdmin lost it
  // before the fix in ViewerFeed (reused projectile ids evicted the newest collisions)
  // (the summary has the closest approach, not where it stopped: the replay shows that)
  if (m.flags & 2) return { text: 'Contact, part not recorded', enemyHit: false }
  // Parked, empty vehicles near the path are targets too: name the nearest one that moved
  const moved = m.targets.filter((t) => t.moved)
  const nearest = [...(moved.length ? moved : m.targets)].sort((a, b) => a.closestTested - b.closestTested)[0]
  const text = nearest && nearest.closestTested >= 0 ? `Miss, ${nearest.closestTested.toFixed(1)} m from ${nearest.crew[0] ?? nearest.template}` : 'Miss'
  return { text, enemyHit: false }
}

function MissileItem({
  missile,
  outcome,
  selected,
  onSelect,
}: {
  missile: MissileSummary
  outcome: Outcome
  selected: boolean
  onSelect: () => void
}) {
  const time = new Date(missile.time).toLocaleTimeString()
  return (
    <button
      type="button"
      className={cn('w-full rounded-md border px-2 py-1.5 text-left text-xs hover:bg-muted', selected && 'border-primary bg-muted')}
      onClick={onSelect}
    >
      <div className="flex items-center gap-2">
        <span className="size-2 shrink-0 rounded-full" style={{ background: teamColor(missile.shooterTeam) }} />
        <span className="truncate font-medium">{missile.shooterName || 'Unknown'}</span>
        <span className="ml-auto shrink-0 text-muted-foreground">{time}</span>
      </div>
      <div className="mt-0.5 flex items-center gap-2">
        <span className={cn('truncate', outcome.enemyHit ? 'text-emerald-500' : 'text-muted-foreground')}>{outcome.text}</span>
        <span className="ml-auto shrink-0 text-muted-foreground">lag {missile.lagMs} ms</span>
      </div>
    </button>
  )
}

/** The replay's colours, over the 3D view while a missile is selected */
const LEGEND_KEY = 'viewer.legend.collapsed'

/** The replay's colours, over the 3D view while a missile is selected; collapsible, remembered in localStorage */
function Legend() {
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(LEGEND_KEY) === '1')
  const toggle = () => {
    setCollapsed(!collapsed)
    localStorage.setItem(LEGEND_KEY, collapsed ? '0' : '1')
  }
  const rows: [string, string, string][] = [
    [CURRENT_COLOR, 'Red: where you saw it (= server now)', "the gunner's client draws missile and target with the same delay (a model)"],
    [TESTED_COLOR, 'Blue: hull hitbox (rewind)', "the hull's collision mesh, moved back by the shooter's lag"],
    [PARTS_COLOR, 'Orange: parts hitbox (no rewind)', 'rotors (blade sweep), wing missiles, turret, sensors, gear'],
    [SWEEP_COLOR, 'Yellow: tested point and sweep', 'the missile is one point at its centre, no radius; green: the part hit'],
    [HIT_COLOR, 'Pole: impact point', 'green on a helicopter, grey on ground or an object; bright from the hit tick (H)'],
  ]
  return (
    <div className="absolute bottom-2 left-2 max-w-md space-y-0.5 rounded bg-black/60 px-2 py-1 text-xs text-white">
      <button
        type="button"
        className="flex items-center gap-1 font-medium text-white/80 hover:text-white"
        title={collapsed ? 'Show the legend' : 'Hide the legend'}
        onClick={toggle}
      >
        {collapsed ? <ChevronUp className="size-3" /> : <ChevronDown className="size-3" />}
        Legend
        {collapsed && (
          <span className="ml-1 flex gap-1">
            {rows.map(([color, name]) => (
              <span key={name} className="size-2.5 rounded-sm" style={{ background: color }} />
            ))}
          </span>
        )}
      </button>
      {!collapsed && rows.map(([color, name, text]) => (
        <div key={name} className="flex items-baseline gap-2">
          <span className="size-2.5 shrink-0 translate-y-0.5 rounded-sm" style={{ background: color }} />
          <span>
            <span className="font-medium">{name}</span> <span className="text-white/70">{text}</span>
          </span>
        </div>
      ))}
    </div>
  )
}

function ViewOverlay({ info }: { info: NonNullable<ViewInfo> }) {
  return (
    <div className="pointer-events-none absolute top-2 left-2 rounded bg-black/60 px-2 py-1 text-xs text-white">
      <div className="font-medium">{info.name}</div>
      <div>
        {info.alive
          ? `${info.viewName} (${info.view}) · ${info.camera}${info.fovDegrees ? ` · FOV ${info.fovDegrees.toFixed(0)}°` : ''}`
          : info.camera || 'Dead'}
      </div>
      <div className="text-white/70">
        ping {info.ping} ms · lag {info.lagMs} ms
      </div>
    </div>
  )
}

function MissileOverlay({
  missile,
  outcome,
  playback,
  onJump,
}: {
  missile: MissileSummary
  outcome: Outcome
  playback: PlaybackState | null
  onJump: () => void
}) {
  const m = playback?.missile
  const impact = m && m.impact >= 0 ? m.impact : -1
  const current = playback ? Math.round(playback.tick) : -1
  return (
    <div className="absolute top-2 right-2 max-w-80 rounded bg-black/60 px-2 py-1 text-xs text-white">
      <div className="font-medium">
        {missile.shooterName} · <span className={cn(outcome.enemyHit && 'text-emerald-400')}>{outcome.text}</span>
      </div>
      <div className="text-white/70">
        lag {missile.lagMs} ms · {missile.samples} ticks
      </div>
      {m && impact >= 0 && (
        <button
          type="button"
          className={cn(
            'mt-0.5 flex items-center gap-1 rounded px-1 font-medium hover:bg-white/15',
            current === impact ? 'bg-white/20' : '',
          )}
          style={{ color: m.impactKind === 'vehicle' ? HIT_COLOR : IMPACT_COLOR }}
          title="Jump to the tick of the impact (H)"
          onClick={onJump}
        >
          <Crosshair className="size-3" />
          {m.impactKind === 'vehicle' ? 'Hit' : 'Impact'} at tick +{impact - m.first} (server tick {impact})
          {current === impact && ' · shown'}
        </button>
      )}
      {missile.targets
        .filter((t) => t.moved)
        .map((t) => (
          <div key={t.netId} className="text-white/70">
            {t.crew.join(' · ') || t.template}: closest {t.closestTested.toFixed(1)} m from blue, {t.closestCurrent.toFixed(1)} m from red
          </div>
        ))}
    </div>
  )
}

function TvCrosshair() {
  return (
    <svg
      className="pointer-events-none absolute inset-0 m-auto size-40 text-white/80"
      viewBox="0 0 100 100"
      fill="none"
      stroke="currentColor"
      strokeWidth="0.8"
    >
      <path d="M50 30 V44 M50 56 V70 M30 50 H44 M56 50 H70" />
      <path d="M20 20 h8 M20 20 v8 M80 20 h-8 M80 20 v8 M20 80 h8 M20 80 v-8 M80 80 h-8 M80 80 v-8" />
    </svg>
  )
}

function formatTicks(ticks: number) {
  const seconds = Math.max(ticks, 0) / TICK_RATE
  return `${Math.floor(seconds / 60)}:${(seconds % 60).toFixed(1).padStart(4, '0')}`
}

/**
 * The timeline's track: press and drag anywhere on it to scrub (it pauses), while playing too. Not a focusable input, so
 * the viewer's keys keep working after using it
 */
function TimelineTrack({ scene, playback }: { scene: ViewerScene | null; playback: PlaybackState }) {
  const { tick, start, end, missile } = playback
  const span = Math.max(end - start, 1)
  const percent = (t: number) => `${Math.min(Math.max((t - start) / span, 0), 1) * 100}%`
  const track = useRef<HTMLDivElement>(null)
  const dragging = useRef(false)
  const disabled = end <= start || !scene

  const seekAt = (clientX: number) => {
    const rect = track.current?.getBoundingClientRect()
    if (!rect || !scene) return
    const f = Math.min(Math.max((clientX - rect.left) / Math.max(rect.width, 1), 0), 1)
    scene.seek(start + f * span)
  }

  return (
    <div
      ref={track}
      className={cn('relative h-6 flex-1 touch-none select-none', disabled ? 'opacity-50' : 'cursor-pointer')}
      onPointerDown={(e) => {
        if (disabled || e.button !== 0) return
        e.preventDefault()
        e.currentTarget.setPointerCapture(e.pointerId)
        dragging.current = true
        scene?.pause()
        seekAt(e.clientX)
      }}
      onPointerMove={(e) => {
        if (dragging.current) seekAt(e.clientX)
      }}
      onPointerUp={(e) => {
        dragging.current = false
        if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
      }}
      onPointerCancel={() => (dragging.current = false)}
    >
      <div className="absolute inset-x-0 top-1/2 h-1.5 -translate-y-1/2 rounded-full bg-muted" />
      <div className="absolute top-1/2 left-0 h-1.5 -translate-y-1/2 rounded-full bg-primary/70" style={{ width: percent(tick) }} />
      {missile && (
        <div
          className="absolute top-1/2 h-2 -translate-y-1/2 rounded bg-yellow-400/40"
          style={{ left: percent(missile.first), width: `${((missile.last - missile.first) / span) * 100}%` }}
        />
      )}
      {missile && missile.impact >= 0 && (
        <div
          className="absolute top-1/2 h-5 w-1 -translate-x-1/2 -translate-y-1/2 rounded-sm"
          style={{ left: percent(missile.impact), background: missile.impactKind === 'vehicle' ? HIT_COLOR : IMPACT_COLOR }}
          title="Hit tick"
        />
      )}
      <div
        className="absolute top-1/2 size-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-background bg-primary shadow"
        style={{ left: percent(tick) }}
      />
    </div>
  )
}

function Timeline({ scene, playback, onLive }: { scene: ViewerScene | null; playback: PlaybackState | null; onLive: () => void }) {
  if (!playback) return <div className="h-10" />
  const { tick, start, end, playing, live, speed, missile } = playback

  return (
    <div className="flex items-center gap-2">
      <Button variant="outline" size="icon" title="Play / pause (space)" onClick={() => scene?.togglePlay()}>
        {playing ? <Pause /> : <Play />}
      </Button>
      <Button variant="outline" size="icon" title="One tick back (,)" onClick={() => scene?.step(-1)}>
        <ChevronLeft />
      </Button>
      <Button variant="outline" size="icon" title="One tick forward (.)" onClick={() => scene?.step(1)}>
        <ChevronRight />
      </Button>
      {missile && (
        <Button variant="outline" size="sm" title="Jump to the hit / impact tick (H)" disabled={missile.impact < 0} onClick={() => scene?.seekImpact()}>
          <Crosshair data-icon="inline-start" />
          {missile.impactKind === 'vehicle' ? 'Hit' : 'Impact'}
        </Button>
      )}
      <Button
        variant={live ? 'default' : 'outline'}
        size="sm"
        title="Back to live (L)"
        onClick={() => {
          onLive()
          scene?.goLive()
        }}
      >
        <Radio data-icon="inline-start" />
        Live
      </Button>
      <select
        className="h-8 rounded-md border bg-background px-1 text-sm"
        value={speed}
        title="Playback speed"
        onChange={(e) => scene?.setSpeed(Number(e.target.value))}
      >
        {speeds.map((s) => (
          <option key={s} value={s}>
            {s}x
          </option>
        ))}
      </select>
      <TimelineTrack scene={scene} playback={playback} />
      <div className="w-44 shrink-0 text-right font-mono text-xs text-muted-foreground">
        {missile
          ? `${formatTicks(tick - missile.first)} · tick ${Math.round(tick - missile.first)}/${missile.last - missile.first}`
          : `${formatTicks(tick - start)} / ${formatTicks(end - start)}`}
        <div>
          tick {Math.floor(tick)}
          {missile && missile.impact >= 0 && (
            <span style={{ color: missile.impactKind === 'vehicle' ? HIT_COLOR : undefined }}> · hit +{missile.impact - missile.first}</span>
          )}
        </div>
      </div>
    </div>
  )
}
