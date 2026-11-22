import { useQuery } from '@tanstack/react-query'
import { ChevronRight } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { HealthRound, PlayerConnection, ServerConnections, ServerHealthStatus, ServerHealthWindow } from '@/api/client'
import { type Band, type Series, StatusStrip, TimeChart, type TimeRange } from '@/components/server/health-charts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { healthConnectionsQuery, healthMinutes, healthQuery, healthRoundsQuery } from '@/hooks/use-server-live'
import { clock, connectionReasonsText, currentConnections, ms, overallHealth, reasonsText, sentence, statusColors, statusHelp, statusLabels } from '@/lib/health'
import { nearestIndex } from '@/lib/nearest-index'
import { cn } from '@/lib/utils'

// A tick is 1/30 s
const tickUs = 1_000_000 / 30

/** The parts of the history the panel shows; WebAdmin keeps six hours. */
const ranges = [
  { minutes: 60, label: 'Last hour' },
  { minutes: 360, label: 'Last 6 hours' },
]

const and = new Intl.ListFormat('en', { type: 'conjunction' })

/** What a reason of a window means for the server, for the verdict; reasonText has the figure. */
const reasonPhrases: Record<string, string> = {
  lateTicks: 'ticks are running late',
  shortTicks: 'ticks are bunching up',
  lostTicks: 'ticks are being lost',
  cpu: 'ticks take too much CPU',
  pageFaults: 'the server waits for memory from disk',
  steal: 'the host is short of CPU',
}

/** The same reasons as what the player has, for the verdict; loss either way reads the same there. */
const connectionPhrases: Record<string, string> = {
  lossToPlayer: 'is losing packets',
  lossFromPlayer: 'is losing packets',
  pingSpread: 'has an unsteady ping',
  lowSendRate: 'sends at the low rate',
}

const connectionRank: Record<PlayerConnection['status'], number> = { Bad: 0, Warn: 1, Good: 2 }

/** How a figure or a state reads: info is a loading level, none is nothing to rate (no data, offline, stepped time). */
type Level = 'good' | 'warn' | 'bad' | 'info' | 'none'

const levelRank: Record<Level, number> = { none: 0, info: 0, good: 1, warn: 2, bad: 3 }
const worse = (a: Level, b: Level) => (levelRank[b] > levelRank[a] ? b : a)
const statusLevel = (status: string): Level => (status === 'Bad' ? 'bad' : status === 'Warn' ? 'warn' : 'good')
const limitLevel = (value: number, limit: { warn: number; bad: number }): Level =>
  value > limit.bad ? 'bad' : value > limit.warn ? 'warn' : 'good'

const levelStyles: Record<Level, { text: string; dot: string; box: string; accent: string }> = {
  good: { text: 'text-emerald-700 dark:text-emerald-400', dot: 'bg-emerald-500', box: 'border-emerald-500/30 bg-emerald-500/5', accent: 'border-l-emerald-500' },
  warn: { text: 'text-amber-700 dark:text-amber-400', dot: 'bg-amber-500', box: 'border-amber-500/40 bg-amber-500/5', accent: 'border-l-amber-500' },
  bad: { text: 'text-red-700 dark:text-red-400', dot: 'bg-red-500', box: 'border-red-500/40 bg-red-500/5', accent: 'border-l-red-500' },
  info: { text: 'text-sky-700 dark:text-sky-400', dot: 'bg-sky-500', box: 'border-sky-500/30 bg-sky-500/5', accent: 'border-l-sky-500' },
  none: { text: 'text-foreground', dot: 'bg-muted-foreground/40', box: 'bg-card', accent: 'border-l-border' },
}

/**
 * When the tiles' figures are a warning or bad. The window and the connections have a status of their own, from the
 * server's rules; a tile takes the worse of that (when its reasons are about the tile's figure) and these limits.
 * - Tick interval p99: the server runs frames of 28.3 ms against ticks of 33.3 ms, so an interval of about 28 ms, or
 *   about 57 ms when a tick waits a frame, is normal. Above 60 ms is more than that wait; above 66.7 ms a tick is late,
 *   and a p99 there means more than one tick in a hundred is.
 * - CPU per tick p99, of the 33.3 ms a tick has: above half leaves little room for a busy moment, above 80 % is close to
 *   a late tick.
 * - Ping: above 150 ms a helicopter duel feels it, above 250 ms it is hard to play.
 * - Loss, the worse direction: the loss chart's bad line is 5 %; above 1 % it already shows in game.
 */
const limits = {
  tickP99Ms: { warn: 60, bad: 1000 / 15 },
  cpuPercent: { warn: 50, bad: 80 },
  pingMs: { warn: 150, bad: 250 },
  lossPercent: { warn: 1, bad: 5 },
}

const tickReasons = ['lateTicks', 'shortTicks', 'lostTicks']
const lossReasons = ['lossToPlayer', 'lossFromPlayer']

const playerName = (c: PlayerConnection) => c.name ?? `Player ${c.playerId} (left)`
const lossOf = (c: PlayerConnection) => Math.max(c.lossToPlayerPercent, c.lossFromPlayerPercent)

/**
 * While another part of the history loads, the part shown before stays: of the same server only. The health queries'
 * keys hold the server id in their params.
 */
const sameServer =
  (serverId: string) =>
  <T,>(previous: T | undefined, query: { queryKey: readonly unknown[] } | undefined) =>
    (query?.queryKey[2] as { params?: { path?: { serverId?: string } } } | undefined)?.params?.path?.serverId === serverId ? previous : undefined

/**
 * The server's health now as a small chip for the server header, the worse of the latest window and its players'
 * connections, with the reasons in its title: nothing while the server is offline or has sent no window. Reads the
 * query that useServerLive keeps current.
 */
export function HealthChip({ serverId, connected }: { serverId: string; connected: boolean }) {
  const health = overallHealth(useQuery({ ...healthQuery(serverId), staleTime: Infinity }).data)
  if (!connected || !health) return null

  return (
    <span className="inline-flex h-5 shrink-0 items-center gap-1.5 rounded-full border px-2 text-xs font-medium whitespace-nowrap" title={health.title}>
      <span className={cn('size-2 shrink-0 rounded-full', statusColors[health.status])} aria-hidden />
      {health.label}
      <span className="sr-only">. {health.reason}</span>
    </span>
  )
}

/**
 * How the server process runs, from the ServerHealth windows its module sends every 10 s: a verdict on the latest
 * window, its main figures as tiles (the rest on demand), the players' connections, and the last hour or six hours as
 * charts. WebAdmin rates the windows; this only shows the rating. A server whose module sends none has no data.
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

  const connections = connected && health.data ? currentConnections(health.data) : null
  const players = useMemo(
    () =>
      connections
        ? [...connections.players].sort(
            (a, b) => connectionRank[a.status] - connectionRank[b.status] || (a.name ?? '').localeCompare(b.name ?? '') || a.playerId - b.playerId,
          )
        : null,
    [connections],
  )
  const lastHour = useMemo(
    () => (latest ? lastHourVerdict(history ?? [], connectionsHistory.data ?? [], Date.parse(latest.time)) : null),
    [latest, history, connectionsHistory.data],
  )

  return (
    <div className="space-y-4">
      <Verdict server={serverVerdict(latest, connected)} players={players && playersVerdict(players)} lastHour={connected ? lastHour : null} />

      {latest && (
        <>
          {/* A window of a server that has gone offline is not its state now: its tiles are not rated */}
          <Tiles latest={latest} rated={connected && isRated(latest)} players={players} />
          <Details latest={latest} />
        </>
      )}

      {players && <Connections players={players} />}

      {latest && (
        <Card>
          <CardHeader>
            <CardTitle>History</CardTitle>
            <CardAction>
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
            </CardAction>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
              {summary.windows === 0 && <span className="text-muted-foreground">No windows</span>}
              {(['Good', 'Warn', 'Bad', 'Loading', 'Stepped'] as const).map(
                (s) =>
                  (summary.counts[s] > 0 || (summary.windows > 0 && s !== 'Loading' && s !== 'Stepped')) && (
                    <span key={s} className="flex items-center gap-1.5">
                      <span className={cn('inline-block size-2 shrink-0 rounded-full', statusColors[s])} />
                      {summary.counts[s]} {statusLabels[s].toLowerCase()}
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

            <p className="text-xs text-muted-foreground">
              The server runs frames of 28.3 ms against ticks of 33.3 ms, so a median interval of about 28 ms and a longest interval of about 57 ms
              are normal. Late ticks (an interval over 66.7 ms) and short ticks (two ticks in one frame) are not.
              {history &&
                history.length > 0 &&
                ' One status cell per 10 s window; loading and stepped windows are left out of the figures and the tick charts. Shaded: the 2v2 rounds (WebAdmin knows those since it started).'}
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

type VerdictLine = { level: Level; title: string; detail?: string; time?: string }

/** The latest window in a sentence: running smoothly, or what is wrong. */
function serverVerdict(latest: ServerHealthWindow | null, connected: boolean): VerdictLine {
  if (!connected)
    return {
      level: 'none',
      title: 'The server is offline',
      detail: latest ? `Its last health window is from ${new Date(latest.time).toLocaleString()}.` : 'It has sent no health window.',
    }
  if (!latest)
    return {
      level: 'none',
      title: 'No health data',
      detail: 'The module of this server has sent no health window. A module that has them sends one every 10 seconds.',
    }

  const time = `Window of ${(latest.wallUs / 1_000_000).toFixed(1)} s received at ${clock(latest.time)}`
  switch (latest.status) {
    case 'Good':
      return { level: 'good', title: 'The server runs smoothly', time }
    case 'Loading':
      return { level: 'info', title: 'A level is loading', detail: statusHelp.Loading, time }
    case 'Stepped':
      return { level: 'none', title: 'Stepped time is on', detail: statusHelp.Stepped, time }
    default: {
      const phrases = [...new Set(latest.reasons.map((r) => reasonPhrases[r] ?? r))]
      return {
        level: statusLevel(latest.status),
        title: phrases.length > 0 ? sentence(and.format(phrases)) : 'Not running smoothly',
        detail: latest.reasons.length > 0 ? `${sentence(reasonsText(latest))} in the last window.` : undefined,
        time,
      }
    }
  }
}

/** The players whose connection is not good, in a sentence; null when all are. A crowd is only counted. */
function playersVerdict(players: PlayerConnection[]): VerdictLine | null {
  const troubled = players.filter((p) => p.status !== 'Good')
  if (troubled.length === 0) return null
  const level = troubled.reduce<Level>((l, p) => worse(l, statusLevel(p.status)), 'good')
  if (troubled.length > 3) return { level, title: `${troubled.length} players have connection problems.` }
  const lines = troubled.map((p) => {
    const phrases = [...new Set(p.reasons.map((r) => connectionPhrases[r] ?? r))]
    return `${playerName(p)} ${phrases.length > 0 ? and.format(phrases) : 'has connection problems'}`
  })
  return { level, title: `${lines.join('; ')}.` }
}

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`

/**
 * The hour up to the latest window in a sentence: the windows that were degraded or warned, and a player's packet loss
 * or ping when it went over the tiles' limits. Null before there is a window in it.
 */
function lastHourVerdict(history: ServerHealthWindow[], connections: ServerConnections[], latestTime: number): VerdictLine | null {
  const from = latestTime - 60 * 60_000
  const windows = history.filter((w) => Date.parse(w.time) > from)
  if (windows.length === 0) return null
  const bad = windows.filter((w) => w.status === 'Bad').length
  const warn = windows.filter((w) => w.status === 'Warn').length
  // Players who joined during a window have too low figures, as in the charts
  const readings = connections.filter((c) => Date.parse(c.time) > from).flatMap((c) => c.players.filter((p) => !p.partial))
  const lossWorst = readings.reduce<PlayerConnection | null>((w, c) => (!w || lossOf(c) > lossOf(w) ? c : w), null)
  const pingWorst = readings.reduce<PlayerConnection | null>((w, c) => (!w || c.ping > w.ping ? c : w), null)

  let level: Level = bad > 0 ? 'bad' : warn > 0 ? 'warn' : 'good'
  const parts: string[] = []
  if (bad > 0) parts.push(plural(bad, 'degraded window'))
  if (warn > 0) parts.push(`${plural(warn, 'window')} with warnings`)
  if (lossWorst && lossOf(lossWorst) > limits.lossPercent.warn) {
    const direction = lossWorst.lossToPlayerPercent >= lossWorst.lossFromPlayerPercent ? 'to' : 'from'
    parts.push(`packet loss ${direction} ${playerName(lossWorst)} up to ${lossOf(lossWorst).toFixed(1)} %`)
    level = worse(level, limitLevel(lossOf(lossWorst), limits.lossPercent))
  }
  if (pingWorst && pingWorst.ping > limits.pingMs.warn) {
    parts.push(`ping of ${playerName(pingWorst)} up to ${pingWorst.ping} ms`)
    level = worse(level, limitLevel(pingWorst.ping, limits.pingMs))
  }
  return parts.length > 0
    ? { level, title: `${sentence(and.format(parts))}.`, detail: `Of ${plural(windows.length, 'window')}; the history below shows when.` }
    : { level, title: 'No problems.', detail: `All ${plural(windows.length, 'window')} ran smoothly, and no player's connection went over the limits.` }
}

/**
 * The verdict, split in now (the latest window and its connections) and the last hour, so that a problem a while ago
 * does not read as one now, nor the other way round. The box takes the colour of now.
 */
function Verdict({ server, players, lastHour }: { server: VerdictLine; players: VerdictLine | null; lastHour: VerdictLine | null }) {
  const now = players ? worse(server.level, players.level) : server.level
  const style = levelStyles[now]
  return (
    <div className={cn('grid gap-4 rounded-xl border p-4 md:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]', style.box)}>
      <div className="flex min-w-0 items-start gap-3">
        <span className={cn('mt-6 size-3 shrink-0 rounded-full', style.dot)} aria-hidden />
        <div className="min-w-0 space-y-1">
          <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Now</p>
          <p className={cn('text-xl leading-tight font-semibold', levelStyles[server.level].text)}>{server.title}</p>
          {server.detail && <p className="text-sm">{server.detail}</p>}
          {players && <p className={cn('text-sm font-medium', levelStyles[players.level].text)}>{players.title}</p>}
          {server.time && <p className="text-xs text-muted-foreground">{server.time}</p>}
        </div>
      </div>
      {lastHour && (
        <div className="flex min-w-0 items-start gap-3 border-t border-foreground/10 pt-4 md:border-t-0 md:border-l md:pt-0 md:pl-4">
          <span className={cn('mt-6 size-2.5 shrink-0 rounded-full', levelStyles[lastHour.level].dot)} aria-hidden />
          <div className="min-w-0 space-y-1">
            <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Last hour</p>
            <p className={cn('font-medium', levelStyles[lastHour.level].text)}>{lastHour.title}</p>
            {lastHour.detail && <p className="text-xs text-muted-foreground">{lastHour.detail}</p>}
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * The four figures that say most about the latest window: how late the ticks run, how much of a tick the CPU takes,
 * and the worst ping and loss of the players. Not rated (no colour) for a loading level, stepped time or an offline
 * server.
 */
function Tiles({ latest, rated, players }: { latest: ServerHealthWindow; rated: boolean; players: PlayerConnection[] | null }) {
  // The window's own status, when one of <reasons> made it
  const windowLevel = (reasons: string[]) => (latest.reasons.some((r) => reasons.includes(r)) ? statusLevel(latest.status) : 'good')
  const tickMs = latest.intervalP99Us / 1000
  const cpuPercent = (latest.cpuP99Us / tickUs) * 100

  const connected = players ?? []
  const pingWorst = connected.reduce<PlayerConnection | null>((w, c) => (!w || c.ping > w.ping ? c : w), null)
  const lossWorst = connected.reduce<PlayerConnection | null>((w, c) => (!w || lossOf(c) > lossOf(w) ? c : w), null)
  const noPlayers = players === null ? 'No current connection figures' : 'No players connected'
  // A connection's own status, when one of <reasons> made it
  const connectionLevel = (c: PlayerConnection, reasons: string[]) => (c.reasons.some((r) => reasons.includes(r)) ? statusLevel(c.status) : 'good')

  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <Tile
        label="Tick interval p99"
        value={`${tickMs.toFixed(1)} ms`}
        detail={`median ${ms(latest.intervalMedianUs, 1)} · longest ${ms(latest.intervalMaxUs, 1)} ms`}
        level={rated ? worse(limitLevel(tickMs, limits.tickP99Ms), windowLevel(tickReasons)) : 'none'}
        hint="99 % of the ticks of the window came within this time of the one before. About 28 ms (median) and 57 ms (longest) are normal; over 66.7 ms a tick is late."
      />
      <Tile
        label="CPU per tick p99"
        value={`${cpuPercent.toFixed(cpuPercent < 10 ? 1 : 0)} %`}
        detail={`${ms(latest.cpuP99Us, 2)} ms of a 33.3 ms tick`}
        level={rated ? worse(limitLevel(cpuPercent, limits.cpuPercent), windowLevel(['cpu'])) : 'none'}
        hint="The CPU time 99 % of the ticks of the window took at most, as a part of the 33.3 ms a tick has."
      />
      <Tile
        label="Worst ping"
        value={pingWorst ? `${pingWorst.ping} ms` : '—'}
        detail={pingWorst ? `${playerName(pingWorst)} · ${pingWorst.pingMin}–${pingWorst.pingMax} ms` : noPlayers}
        level={pingWorst ? worse(limitLevel(pingWorst.ping, limits.pingMs), connectionLevel(pingWorst, ['pingSpread'])) : 'none'}
        hint="The highest last ping of the players, with the lowest and highest of their window."
      />
      <Tile
        label="Worst loss"
        value={lossWorst ? `${lossOf(lossWorst).toFixed(1)} %` : '—'}
        detail={
          lossWorst
            ? `${playerName(lossWorst)} · to ${lossWorst.lossToPlayerPercent.toFixed(1)} / from ${lossWorst.lossFromPlayerPercent.toFixed(1)} %`
            : noPlayers
        }
        level={lossWorst ? worse(limitLevel(lossOf(lossWorst), limits.lossPercent), connectionLevel(lossWorst, lossReasons)) : 'none'}
        hint="The highest packet loss of the players over the window, in the worse direction (to or from the player)."
      />
    </div>
  )
}

const levelNames: Partial<Record<Level, string>> = { good: 'good', warn: 'warning', bad: 'bad' }

function Tile({ label, value, detail, level, hint }: { label: string; value: string; detail: string; level: Level; hint: string }) {
  const style = levelStyles[level]
  return (
    <div className={cn('min-w-0 rounded-xl border-l-4 bg-card p-3 ring-1 ring-foreground/10', style.accent)} title={hint}>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={cn('mt-0.5 text-2xl font-semibold tabular-nums', (level === 'warn' || level === 'bad') && style.text)}>
        {value}
        {levelNames[level] && <span className="sr-only"> ({levelNames[level]})</span>}
      </div>
      <div className="text-xs break-words text-muted-foreground">{detail}</div>
    </div>
  )
}

/** Every figure of the latest window, closed at first: the tiles and the charts have what most people look for. */
function Details({ latest }: { latest: ServerHealthWindow }) {
  return (
    <Collapsible className="rounded-xl bg-card ring-1 ring-foreground/10">
      <CollapsibleTrigger className="flex w-full items-center gap-2 rounded-xl px-4 py-3 text-left text-sm font-medium outline-none hover:bg-muted/50 focus-visible:ring-[3px] focus-visible:ring-ring/50">
        <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform in-data-[panel-open]:rotate-90" />
        All figures of the latest window
      </CollapsibleTrigger>
      <CollapsibleContent className="px-4 pb-4">
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 pt-1 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
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
      </CollapsibleContent>
    </Collapsible>
  )
}

function ConnectionStatus({ connection: c }: { connection: PlayerConnection }) {
  return (
    <Badge variant={c.status === 'Bad' ? 'destructive' : 'secondary'}>
      <span className={cn('inline-block size-2 shrink-0 rounded-full', statusColors[c.status])} />
      {statusLabels[c.status]}
    </Badge>
  )
}

function PartialBadge() {
  return (
    <Badge variant="outline" className="shrink-0" title="The player joined during this window, so the figures are too low">
      partial
    </Badge>
  )
}

/**
 * The players' connections over the latest window, the worst first: ping, the packets a second in both directions
 * and what was lost of them. Bots have no connection and no row. A phone gets a card per player with the main figures
 * instead of the wide table.
 */
function Connections({ players }: { players: PlayerConnection[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Connections</CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {players.length === 0 && <p className="text-muted-foreground">No players connected</p>}

        {players.length > 0 && (
          <ul className="space-y-2 md:hidden">
            {players.map((c) => (
              <li key={c.playerId} className="space-y-2 rounded-lg border p-3">
                <div className="flex items-center gap-2">
                  <span className={cn('min-w-0 flex-1 truncate font-medium', c.name === null && 'text-muted-foreground')}>{playerName(c)}</span>
                  {c.partial && <PartialBadge />}
                  <ConnectionStatus connection={c} />
                </div>
                {c.reasons.length > 0 && <p className="text-xs text-muted-foreground">{connectionReasonsText(c)}</p>}
                <dl className="grid grid-cols-2 gap-2">
                  <div>
                    <dt className="text-xs text-muted-foreground">Ping</dt>
                    <dd className="font-mono tabular-nums">
                      {c.ping} ms{' '}
                      <span className="text-xs text-muted-foreground">
                        {c.pingMin}–{c.pingMax}
                      </span>
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs text-muted-foreground">Loss to / from</dt>
                    <dd className="font-mono tabular-nums">
                      {c.lossToPlayerPercent.toFixed(2)} / {c.lossFromPlayerPercent.toFixed(2)} %
                    </dd>
                  </div>
                </dl>
              </li>
            ))}
          </ul>
        )}

        {players.length > 0 && (
          <div className="hidden rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right" title="The last ping, and the lowest and highest of the window">
                    Ping
                  </TableHead>
                  <TableHead className="text-right" title="Packets a second sent to the player / received from the player">
                    Packets/s to / from
                  </TableHead>
                  <TableHead className="text-right" title="Packets lost on the way to the player / from the player, of the packets of that direction">
                    Loss to / from
                  </TableHead>
                  <TableHead className="text-right" title="Kilobytes a second sent to the player / received from the player">
                    kB/s to / from
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {players.map((c) => (
                  <TableRow key={c.playerId}>
                    <TableCell className="max-w-64">
                      <div className="flex items-center gap-2">
                        <span className={cn('truncate', c.name === null && 'text-muted-foreground')}>{playerName(c)}</span>
                        {c.partial && <PartialBadge />}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                        <ConnectionStatus connection={c} />
                        {c.reasons.length > 0 && (
                          <span className="text-xs whitespace-normal text-muted-foreground">
                            {connectionReasonsText(c)}
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
                    <TableCell className="text-right font-mono tabular-nums">
                      {c.packetsToPlayer.toFixed(1)} / {c.packetsFromPlayer.toFixed(1)}
                    </TableCell>
                    <TableCell className="text-right font-mono tabular-nums">
                      {c.lossToPlayerPercent.toFixed(2)} / {c.lossFromPlayerPercent.toFixed(2)} %
                      <div className="text-xs text-muted-foreground" title="Packets a second lost in the worse reading of the window, to / from the player">
                        peak {c.lostToPlayerPeak.toFixed(1)} / {c.lostFromPlayerPeak.toFixed(1)} /s
                      </div>
                    </TableCell>
                    <TableCell className="text-right font-mono tabular-nums">
                      {(c.bytesToPlayer / 1000).toFixed(1)} / {(c.bytesFromPlayer / 1000).toFixed(1)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        <p className="text-xs text-muted-foreground">
          A client sends 10 or 30 packets a second and is sent 4 to 20, depending on its connection setting; loss is what did not arrive, in each
          direction.
        </p>
      </CardContent>
    </Card>
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
