import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowLeft, ChevronLeft, ChevronRight, Pause, Play, Radio } from 'lucide-react'
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
import { isSpectatorMoveKey, SPECTATOR_KEYS } from '@/viewer/keys'
import {
  type CameraMode,
  CURRENT_COLOR,
  type PlaybackState,
  type PlayerName,
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
  useEffect(() => scene?.setCameraMode(cameraMode, subject), [scene, cameraMode, subject])

  /** Shows a missile's whole flight, or goes back to live (null) */
  const selectMissile = (key: number | null) => {
    setSelectedMissile(key)
    setMissileError(null)
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

  // Keys: WASD/QE fly the free camera, space plays, , and . step one tick, L goes live
  useEffect(() => {
    if (!scene) return
    const isTyping = (e: KeyboardEvent) => e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement
    const down = (e: KeyboardEvent) => {
      if (isTyping(e) || e.ctrlKey || e.metaKey || e.altKey) return
      if (e.code === 'Space') {
        scene.togglePlay()
        e.preventDefault()
      } else if (e.code === 'Comma') scene.step(-1)
      else if (e.code === 'Period') scene.step(1)
      else if (e.code === 'KeyL') {
        missileRequest.current = null
        setSelectedMissile(null)
        scene.goLive()
      } else if (SPECTATOR_KEYS.has(e.code)) {
        if (isSpectatorMoveKey(e.code) && cameraMode !== 'free') setCameraMode('free')
        scene.setKey(e.code, true)
        e.preventDefault()
      }
    }
    const up = (e: KeyboardEvent) => scene.setKey(e.code, false)
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
          {selected && <MissileOverlay missile={selected} />}
          {!scene && <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">Loading the 3D view…</div>}
        </div>

        <aside className="flex w-80 shrink-0 flex-col gap-3 overflow-hidden">
          <Card className="gap-2 p-3">
            <div className="text-sm font-medium">Camera</div>
            <div className="grid grid-cols-2 gap-1">
              <CameraButton mode="free" current={cameraMode} onSelect={setCameraMode} label="Free" />
              <CameraButton mode="orbit" current={cameraMode} onSelect={setCameraMode} label="Orbit" disabled={subject < 0 && !selected} />
              <CameraButton mode="player" current={cameraMode} onSelect={setCameraMode} label="Player view" disabled={subject < 0} />
              <CameraButton mode="missile" current={cameraMode} onSelect={setCameraMode} label="Missile TV" disabled={!selected} />
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
              <label className="flex items-center gap-1">
                <input type="checkbox" checked={labels} onChange={(e) => setLabels(e.target.checked)} /> Names
              </label>
              <label className="flex items-center gap-1">
                <input type="checkbox" checked={trails} onChange={(e) => setTrails(e.target.checked)} /> Trails
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
                  selected={m.key === selectedMissile}
                  onSelect={() => selectMissile(m.key === selectedMissile ? null : m.key)}
                />
              ))}
            </div>
            <Legend />
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
  disabled,
}: {
  mode: CameraMode
  current: CameraMode
  onSelect: (mode: CameraMode) => void
  label: string
  disabled?: boolean
}) {
  return (
    <Button variant={mode === current ? 'default' : 'outline'} size="sm" disabled={disabled} onClick={() => onSelect(mode)}>
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

function outcomeText(m: MissileSummary) {
  if (m.outcome === 'hit') {
    const part = m.hitPart && m.hitRoot && m.hitPart.toLowerCase() !== m.hitRoot.toLowerCase() ? m.hitPart : m.hitPlayer >= 0 ? 'crew' : 'hull'
    return `Hit ${part}${m.hitRewound ? ' (rewound)' : ' (now)'}`
  }
  if (m.outcome === 'terrain') return 'Terrain'
  // Parked, empty vehicles near the path are targets too: name the nearest one that moved
  const moved = m.targets.filter((t) => t.moved)
  const nearest = [...(moved.length ? moved : m.targets)].sort((a, b) => a.closestTested - b.closestTested)[0]
  return nearest && nearest.closestTested >= 0 ? `Miss, ${nearest.closestTested.toFixed(1)} m from ${nearest.crew[0] ?? nearest.template}` : 'Miss'
}

function MissileItem({ missile, selected, onSelect }: { missile: MissileSummary; selected: boolean; onSelect: () => void }) {
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
        <span className={cn(missile.outcome === 'hit' ? 'text-emerald-500' : 'text-muted-foreground')}>{outcomeText(missile)}</span>
        <span className="ml-auto shrink-0 text-muted-foreground">lag {missile.lagMs} ms</span>
      </div>
    </button>
  )
}

function Legend() {
  return (
    <div className="space-y-1 border-t pt-2 text-xs text-muted-foreground">
      <div className="flex items-center gap-2">
        <span className="size-2.5 rounded-sm" style={{ background: CURRENT_COLOR }} />
        Where the target is now: what the gunner sees. Rotors and wing missiles are hit here
      </div>
      <div className="flex items-center gap-2">
        <span className="size-2.5 rounded-sm" style={{ background: TESTED_COLOR }} />
        The hull (and crew) where the server tests it: rewound by the shooter's lag
      </div>
      <div className="flex items-center gap-2">
        <span className="size-2.5 rounded-sm bg-yellow-400" />
        The sweep tested this tick, and the parts hit
      </div>
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

function MissileOverlay({ missile }: { missile: MissileSummary }) {
  return (
    <div className="pointer-events-none absolute top-2 right-2 max-w-72 rounded bg-black/60 px-2 py-1 text-xs text-white">
      <div className="font-medium">
        {missile.shooterName} · {outcomeText(missile)}
      </div>
      <div className="text-white/70">
        lag {missile.lagMs} ms · {missile.samples} ticks
      </div>
      {missile.targets
        .filter((t) => t.moved)
        .map((t) => (
          <div key={t.netId} className="text-white/70">
            {t.crew.join(' · ') || t.template}: {t.closestTested.toFixed(1)} m from blue, {t.closestCurrent.toFixed(1)} m from red
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

function Timeline({ scene, playback, onLive }: { scene: ViewerScene | null; playback: PlaybackState | null; onLive: () => void }) {
  if (!playback) return <div className="h-10" />
  const { tick, start, end, playing, live, speed, missile } = playback
  const span = Math.max(end - start, 1)
  const percent = (t: number) => `${((t - start) / span) * 100}%`

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
      <div className="relative flex-1">
        {missile && (
          <div
            className="pointer-events-none absolute top-1/2 h-2 -translate-y-1/2 rounded bg-yellow-400/40"
            style={{ left: percent(missile.first), width: `${((missile.last - missile.first) / span) * 100}%` }}
          />
        )}
        <input
          type="range"
          className="relative w-full accent-primary"
          min={start}
          max={Math.max(end, start + 1)}
          step="any"
          value={tick}
          disabled={end <= start}
          onChange={(e) => scene?.seek(Number(e.target.value))}
          onPointerDown={() => scene?.pause()}
        />
      </div>
      <div className="w-44 shrink-0 text-right font-mono text-xs text-muted-foreground">
        {missile
          ? `${formatTicks(tick - missile.first)} · tick ${Math.round(tick - missile.first)}/${missile.last - missile.first}`
          : `${formatTicks(tick - start)} / ${formatTicks(end - start)}`}
        <div>tick {Math.floor(tick)}</div>
      </div>
    </div>
  )
}
