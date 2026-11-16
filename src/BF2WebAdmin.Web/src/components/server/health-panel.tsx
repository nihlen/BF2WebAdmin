import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import type { HealthRound, PlayerConnection, ServerConnections, ServerHealthStatus, ServerHealthWindow } from '@/api/client'
import { type Band, type Series, StatusStrip, TimeChart, type TimeRange } from '@/components/server/health-charts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { healthConnectionsQuery, healthMinutes, healthQuery, healthRoundsQuery } from '@/hooks/use-server-live'
import { nearestIndex } from '@/lib/nearest-index'
import { cn } from '@/lib/utils'

// A tick is 1/30 s
const tickUs = 1_000_000 / 30

/** The parts of the history the panel shows; WebAdmin keeps six hours. */
const ranges = [
  { minutes: 60, label: 'Last hour' },
  { minutes: 360, label: 'Last 6 hours' },
]

const statusColors: Record<ServerHealthStatus, string> = {
  Good: 'bg-emerald-500',
  Warn: 'bg-amber-500',
  Bad: 'bg-red-500',
  Loading: 'bg-sky-500',
  Stepped: 'bg-muted-foreground/40',
}

const statusHelp: Partial<Record<ServerHealthStatus, string>> = {
  Loading: 'A level was loading in this window, so its figures say nothing about how the server runs.',
  Stepped: 'Stepped time is on, so the tick figures do not describe a real-time server.',
}

const ms = (microseconds: number, digits: number) => (microseconds / 1000).toFixed(digits)
const count = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`
const clock = (value: string) => new Date(value).toLocaleTimeString()

/** A reason of a Warn or Bad window (ServerHealthRules on the server) in words, with the figure that caused it. */
function reasonText(reason: string, w: ServerHealthWindow) {
  switch (reason) {
    case 'lateTicks':
      return count(w.lateTicks, 'late tick')
    case 'shortTicks':
      return count(w.shortTicks, 'short tick')
    case 'lostTicks':
      return count(w.lostTicks, 'lost tick')
    case 'cpu':
      return `CPU per tick p99 ${ms(w.cpuP99Us, 2)} ms`
    case 'pageFaults':
      return count(w.majorFaults, 'major page fault')
    case 'steal':
      return `host steal ${w.hostStealPercent.toFixed(2)} %`
    default:
      return reason
  }
}

const reasonsText = (w: ServerHealthWindow) => w.reasons.map((r) => reasonText(r, w)).join(', ')

/** A reason of a Warn or Bad connection (PlayerConnectionRules on the server) in words; the row has the figures. */
const connectionReasons: Record<string, string> = {
  lossToPlayer: 'loss to the player',
  lossFromPlayer: 'loss from the player',
  pingSpread: 'unsteady ping',
  lowSendRate: 'sends at the low rate',
}

const connectionRank: Record<PlayerConnection['status'], number> = { Bad: 0, Warn: 1, Good: 2 }

// The connections come right after their window; older ones are of a module that stopped sending them
const connectionsMaxAgeMs = 15_000

/**
 * While another part of the history loads, the part shown before stays: of the same server only. The health queries'
 * keys hold the server id in their params.
 */
const sameServer =
  (serverId: string) =>
  <T,>(previous: T | undefined, query: { queryKey: readonly unknown[] } | undefined) =>
    (query?.queryKey[2] as { params?: { path?: { serverId?: string } } } | undefined)?.params?.path?.serverId === serverId ? previous : undefined

/**
 * How the server process runs, from the ServerHealth windows its module sends every 10 s: the latest window's status
 * and figures, and the last hour or six hours as charts. WebAdmin rates the windows; this only shows the rating. A
 * server whose module sends none has no data.
 */
export function HealthPanel({ serverId, connected }: { serverId: string; connected: boolean }) {
  const [minutes, setMinutes] = useState(healthMinutes)
  const keep = sameServer(serverId)
  // Kept current by useServerLive's hub events, whichever part of the history it is
  const health = useQuery({ ...healthQuery(serverId, minutes), staleTime: Infinity, placeholderData: keep })
  const connectionsHistory = useQuery({ ...healthConnectionsQuery(serverId, minutes), staleTime: Infinity, placeholderData: keep })
  // No event brings a round: fetched again now and then
  const rounds = useQuery({ ...healthRoundsQuery(serverId, minutes), refetchInterval: 15_000, placeholderData: keep })
  const latest = health.data?.latest ?? null
  const history = health.data?.history
  const summary = useMemo(() => summarize(history ?? []), [history])

  // A window of a server that has gone offline is not its state now
  const status = connected && latest ? latest.status : null
  const connections = health.data?.connections ?? null
  const showConnections =
    connected && latest && connections && Date.parse(latest.time) - Date.parse(connections.time) < connectionsMaxAgeMs

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          Server health
          <Badge variant={status === 'Bad' ? 'destructive' : 'secondary'}>
            <span className={cn('inline-block size-2 shrink-0 rounded-full', status ? statusColors[status] : 'bg-muted-foreground/40')} />
            {status ?? 'No data'}
          </Badge>
        </CardTitle>
        <CardDescription>
          {!latest && 'The module of this server has sent no health window. A module that has them sends one every 10 seconds.'}
          {latest && !connected && `The server is offline. Its last window is from ${new Date(latest.time).toLocaleString()}.`}
          {latest && connected && (
            <>
              {latest.reasons.length > 0 && `${reasonsText(latest)}. `}
              {statusHelp[latest.status] && `${statusHelp[latest.status]} `}
              Window of {(latest.wallUs / 1_000_000).toFixed(1)} s received at {clock(latest.time)}.
            </>
          )}
        </CardDescription>
      </CardHeader>

      {latest && (
        <CardContent className="space-y-4">
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3 xl:grid-cols-6">
            <Figure label="Tick interval" hint="median / p99 / longest">
              {ms(latest.intervalMedianUs, 1)} / {ms(latest.intervalP99Us, 1)} / {ms(latest.intervalMaxUs, 1)} ms
            </Figure>
            <Figure label="Late ticks">{latest.lateTicks}</Figure>
            <Figure label="Short ticks">{latest.shortTicks}</Figure>
            <Figure label="Lost ticks">{latest.lostTicks}</Figure>
            <Figure label="CPU per tick" hint={`median / p99 / largest; p99 is ${((latest.cpuP99Us / tickUs) * 100).toFixed(1)} % of a tick`}>
              {ms(latest.cpuMedianUs, 2)} / {ms(latest.cpuP99Us, 2)} / {ms(latest.cpuMaxUs, 2)} ms
            </Figure>
            <Figure label="Module handler" hint="p99">
              {ms(latest.handlerP99Us, 2)} ms
            </Figure>
            <Figure label="Context switches" hint="involuntary">
              {latest.involuntarySwitches}
            </Figure>
            <Figure label="Major page faults">{latest.majorFaults}</Figure>
            <Figure label="Host CPUs">{latest.hostCpus > 0 ? latest.hostCpus : 'n/a'}</Figure>
            <Figure label="Host CPU" hint="busy / steal / I/O wait">
              {latest.hostValid
                ? `${latest.hostBusyPercent.toFixed(2)} / ${latest.hostStealPercent.toFixed(2)} / ${latest.hostIoWaitPercent.toFixed(2)} %`
                : 'n/a'}
            </Figure>
            <Figure label="Resident memory">{Math.round(latest.residentKib / 1024)} MiB</Figure>
          </dl>

          {showConnections && <Connections connections={connections} />}

          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
              <div className="flex items-center gap-1" role="group" aria-label="Part of the history">
                {ranges.map((r) => (
                  <Button
                    key={r.minutes}
                    size="xs"
                    variant={minutes === r.minutes ? 'secondary' : 'ghost'}
                    aria-pressed={minutes === r.minutes}
                    onClick={() => setMinutes(r.minutes)}
                  >
                    {r.label}
                  </Button>
                ))}
              </div>
              {summary.windows === 0 && <span className="text-muted-foreground">No windows</span>}
              {(['Good', 'Warn', 'Bad', 'Loading', 'Stepped'] as const).map(
                (s) =>
                  (summary.counts[s] > 0 || (summary.windows > 0 && s !== 'Loading' && s !== 'Stepped')) && (
                    <span key={s} className="flex items-center gap-1.5">
                      <span className={cn('inline-block size-2 shrink-0 rounded-full', statusColors[s])} />
                      {summary.counts[s]} {s.toLowerCase()}
                    </span>
                  ),
              )}
              {summary.rated > 0 && (
                <span className="text-muted-foreground">
                  Worst tick interval {ms(summary.intervalMaxUs, 1)} ms, highest CPU p99 {ms(summary.cpuP99Us, 2)} ms, highest steal{' '}
                  {summary.stealPercent === null ? 'n/a' : `${summary.stealPercent.toFixed(2)} %`}
                </span>
              )}
            </div>

            {history && history.length > 0 && (
              <HealthCharts
                history={history}
                connections={connectionsHistory.data ?? []}
                rounds={rounds.data ?? []}
                minutes={minutes}
                latestTime={Date.parse(latest.time)}
              />
            )}
          </div>

          <p className="text-xs text-muted-foreground">
            The server runs frames of 28.3 ms against ticks of 33.3 ms, so a median interval of about 28 ms and a longest interval of about 57 ms
            are normal. Late ticks (an interval over 66.7 ms) and short ticks (two ticks in one frame) are not.
            {history &&
              history.length > 0 &&
              ' One status cell per 10 s window; loading and stepped windows are left out of the figures and the tick charts. Shaded: the 2v2 rounds (WebAdmin knows those since it started).'}
          </p>
        </CardContent>
      )}
    </Card>
  )
}

/**
 * The players' connections over the latest window, the worst first: ping, the packets a second in both directions
 * and what was lost of them. Bots have no connection and no row.
 */
function Connections({ connections }: { connections: ServerConnections }) {
  const players = useMemo(
    () =>
      [...connections.players].sort(
        (a, b) => connectionRank[a.status] - connectionRank[b.status] || (a.name ?? '').localeCompare(b.name ?? '') || a.playerId - b.playerId,
      ),
    [connections],
  )

  return (
    <div className="space-y-2">
      <div className="font-medium">Connections</div>
      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right" title="The last ping, and the lowest and highest of the window">
                Ping
              </TableHead>
              <TableHead className="hidden text-right md:table-cell" title="Packets a second sent to the player / received from the player">
                Packets/s to / from
              </TableHead>
              <TableHead className="text-right" title="Packets lost on the way to the player / from the player, of the packets of that direction">
                Loss to / from
              </TableHead>
              <TableHead className="hidden text-right md:table-cell" title="Kilobytes a second sent to the player / received from the player">
                kB/s to / from
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {players.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-center text-muted-foreground">
                  No players connected
                </TableCell>
              </TableRow>
            )}
            {players.map((c) => (
              <TableRow key={c.playerId}>
                <TableCell className="max-w-48">
                  <div className="flex items-center gap-2">
                    <span className={cn('truncate', c.name === null && 'text-muted-foreground')}>{c.name ?? `Player ${c.playerId} (left)`}</span>
                    {c.partial && (
                      <Badge variant="outline" className="shrink-0" title="The player joined during this window, so the figures are too low">
                        partial
                      </Badge>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <Badge variant={c.status === 'Bad' ? 'destructive' : 'secondary'}>
                      <span className={cn('inline-block size-2 shrink-0 rounded-full', statusColors[c.status])} />
                      {c.status}
                    </Badge>
                    {c.reasons.length > 0 && (
                      <span className="text-xs whitespace-normal text-muted-foreground">
                        {c.reasons.map((r) => connectionReasons[r] ?? r).join(', ')}
                      </span>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-right font-mono tabular-nums">
                  {c.ping} ms
                  <div className="text-xs text-muted-foreground">
                    {c.pingMin}–{c.pingMax}
                  </div>
                </TableCell>
                <TableCell className="hidden text-right font-mono tabular-nums md:table-cell">
                  {c.packetsToPlayer.toFixed(1)} / {c.packetsFromPlayer.toFixed(1)}
                </TableCell>
                <TableCell className="text-right font-mono tabular-nums">
                  {c.lossToPlayerPercent.toFixed(2)} / {c.lossFromPlayerPercent.toFixed(2)} %
                  <div className="text-xs text-muted-foreground" title="Packets a second lost in the worse reading of the window, to / from the player">
                    peak {c.lostToPlayerPeak.toFixed(1)} / {c.lostFromPlayerPeak.toFixed(1)} /s
                  </div>
                </TableCell>
                <TableCell className="hidden text-right font-mono tabular-nums md:table-cell">
                  {(c.bytesToPlayer / 1000).toFixed(1)} / {(c.bytesFromPlayer / 1000).toFixed(1)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <p className="text-xs text-muted-foreground">
        A client sends 10 or 30 packets a second and is sent 4 to 20, depending on its connection setting; loss is what did not arrive, in each
        direction.
      </p>
    </div>
  )
}

function Figure({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="font-mono tabular-nums">{children}</dd>
      {hint && <dd className="text-xs text-muted-foreground">{hint}</dd>}
    </div>
  )
}

/** The windows by status, and the worst figures of the rated ones (Loading and Stepped windows are left out). */
function summarize(history: ServerHealthWindow[]) {
  const counts: Record<ServerHealthStatus, number> = { Good: 0, Warn: 0, Bad: 0, Loading: 0, Stepped: 0 }
  let intervalMaxUs = 0
  let cpuP99Us = 0
  let stealPercent: number | null = null
  for (const w of history) {
    counts[w.status]++
    if (w.status === 'Loading' || w.status === 'Stepped') continue
    intervalMaxUs = Math.max(intervalMaxUs, w.intervalMaxUs)
    cpuP99Us = Math.max(cpuP99Us, w.cpuP99Us)
    if (w.hostValid) stealPercent = Math.max(stealPercent ?? 0, w.hostStealPercent)
  }
  return { counts, windows: history.length, rated: counts.Good + counts.Warn + counts.Bad, intervalMaxUs, cpuP99Us, stealPercent }
}

function windowTitle(w: ServerHealthWindow) {
  const lines = [`${clock(w.time)}  ${w.status}${w.reasons.length > 0 ? `: ${reasonsText(w)}` : ''}`]
  lines.push(`Longest tick interval ${ms(w.intervalMaxUs, 1)} ms, CPU p99 ${ms(w.cpuP99Us, 2)} ms`)
  lines.push(`Late ${w.lateTicks}, short ${w.shortTicks}, lost ${w.lostTicks}, steal ${w.hostValid ? `${w.hostStealPercent.toFixed(2)} %` : 'n/a'}`)
  return lines.join('\n')
}

const isRated = (w: ServerHealthWindow) => w.status !== 'Loading' && w.status !== 'Stepped'
const fixed = (digits: number) => (v: number) => v.toFixed(digits)
const auto = (v: number) => v.toFixed(v !== 0 && v < 10 ? 1 : 0)

/** A median of numbers; null when there are none. */
function median(values: number[]) {
  if (values.length === 0) return null
  const sorted = [...values].sort((a, b) => a - b)
  const middle = sorted.length >> 1
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2
}

/**
 * The history as charts on one time axis: the windows' statuses, the tick timings, the host's CPU, the memory, and the
 * players' ping and loss, with the 2v2 rounds shaded behind. Pointing at a time shows that window in every chart.
 */
function HealthCharts({
  history,
  connections,
  rounds,
  minutes,
  latestTime,
}: {
  history: ServerHealthWindow[]
  connections: ServerConnections[]
  rounds: HealthRound[]
  minutes: number
  latestTime: number
}) {
  const [hover, setHover] = useState<number | null>(null)
  const range: TimeRange = { from: latestTime - minutes * 60_000, to: latestTime }

  const windows = useMemo(() => {
    const times = history.map((w) => Date.parse(w.time))
    const rated = (f: (w: ServerHealthWindow) => number) => history.map((w) => (isRated(w) ? f(w) : null))
    const host = (f: (w: ServerHealthWindow) => number) => history.map((w) => (w.hostValid ? f(w) : null))
    return {
      times,
      statuses: history.map((w) => w.status),
      interval: [
        { name: 'longest', stroke: 'stroke-red-500', dot: 'bg-red-500', values: rated((w) => w.intervalMaxUs / 1000) },
        { name: 'p99', stroke: 'stroke-amber-500', dot: 'bg-amber-500', values: rated((w) => w.intervalP99Us / 1000) },
        { name: 'median', stroke: 'stroke-sky-500', dot: 'bg-sky-500', values: rated((w) => w.intervalMedianUs / 1000) },
      ] satisfies Series[],
      cpu: [
        { name: 'largest', stroke: 'stroke-red-500', dot: 'bg-red-500', values: rated((w) => w.cpuMaxUs / 1000) },
        { name: 'p99', stroke: 'stroke-amber-500', dot: 'bg-amber-500', values: rated((w) => w.cpuP99Us / 1000) },
        { name: 'median', stroke: 'stroke-sky-500', dot: 'bg-sky-500', values: rated((w) => w.cpuMedianUs / 1000) },
      ] satisfies Series[],
      host: [
        { name: 'busy', stroke: 'stroke-sky-500', dot: 'bg-sky-500', values: host((w) => w.hostBusyPercent) },
        { name: 'steal', stroke: 'stroke-red-500', dot: 'bg-red-500', values: host((w) => w.hostStealPercent) },
        { name: 'I/O wait', stroke: 'stroke-violet-500', dot: 'bg-violet-500', values: host((w) => w.hostIoWaitPercent) },
      ] satisfies Series[],
      memory: [
        { name: 'resident', stroke: 'stroke-emerald-500', dot: 'bg-emerald-500', values: history.map((w) => w.residentKib / 1024) },
      ] satisfies Series[],
    }
  }, [history])

  // Players who joined during a window have too low figures and are left out
  const players = useMemo(() => {
    const full = connections.map((c) => c.players.filter((p) => !p.partial))
    const of = (f: (players: PlayerConnection[]) => number | null) => full.map((p) => (p.length > 0 ? f(p) : null))
    return {
      times: connections.map((c) => Date.parse(c.time)),
      ping: [
        { name: 'highest', stroke: 'stroke-red-500', dot: 'bg-red-500', values: of((p) => Math.max(...p.map((x) => x.pingMax))) },
        { name: 'median', stroke: 'stroke-sky-500', dot: 'bg-sky-500', values: of((p) => median(p.map((x) => x.ping))) },
      ] satisfies Series[],
      loss: [
        { name: 'worst to a player', stroke: 'stroke-red-500', dot: 'bg-red-500', values: of((p) => Math.max(...p.map((x) => x.lossToPlayerPercent))) },
        { name: 'worst from a player', stroke: 'stroke-amber-500', dot: 'bg-amber-500', values: of((p) => Math.max(...p.map((x) => x.lossFromPlayerPercent))) },
      ] satisfies Series[],
    }
  }, [connections])

  // A match's rounds have one shade, the next match's the other
  const bands = useMemo(() => {
    const result: Band[] = []
    let alternate = false
    rounds.forEach((r, i) => {
      if (i > 0 && r.matchId !== rounds[i - 1].matchId) alternate = !alternate
      result.push({ key: r.roundId, from: Date.parse(r.start), to: r.end ? Date.parse(r.end) : latestTime, label: `R${r.number}`, alternate })
    })
    return result
  }, [rounds, latestTime])

  const shared = { range, hover, onHover: setHover, bands }
  const index = hover !== null ? nearestIndex(windows.times, hover) : -1
  const pointed = index >= 0 && Math.abs(windows.times[index] - hover!) <= 25_000 ? history[index] : null
  const round = pointed ? bands.find((b) => b.from <= Date.parse(pointed.time) - 10_000 && Date.parse(pointed.time) - 10_000 <= b.to) : undefined

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <StatusStrip times={windows.times} statuses={windows.statuses} {...shared} />
        <div className="min-h-10 text-xs whitespace-pre-line text-muted-foreground">
          {pointed
            ? `${windowTitle(pointed)}${round ? `\nIn round ${round.label.slice(1)}` : ''}`
            : 'Point at a time to see its window. The legends show the latest window, or the one pointed at.'}
        </div>
      </div>
      <div className="grid gap-x-6 gap-y-4 lg:grid-cols-2">
        <TimeChart title="Tick interval, ms" format={auto} times={windows.times} series={windows.interval} minMax={70} cap={250} limits={[{ value: 1000 / 15, label: 'late' }]} {...shared} />
        <TimeChart title="CPU per tick, ms" format={auto} times={windows.times} series={windows.cpu} minMax={5} cap={100} limits={[{ value: 1000 / 30, label: 'one tick' }]} {...shared} />
        <TimeChart title="Host CPU, %" format={auto} times={windows.times} series={windows.host} minMax={5} {...shared} empty="The host's figures are not valid" />
        <TimeChart title="Resident memory, MiB" format={fixed(0)} times={windows.times} series={windows.memory} minMax={100} {...shared} />
        <TimeChart title="Players' ping, ms" format={fixed(0)} times={players.times} series={players.ping} minMax={100} cap={500} {...shared} empty="No players' connections" />
        <TimeChart title="Packet loss, %" format={auto} times={players.times} series={players.loss} minMax={2} cap={50} limits={[{ value: 5, label: 'bad' }]} {...shared} empty="No players' connections" />
      </div>
    </div>
  )
}
