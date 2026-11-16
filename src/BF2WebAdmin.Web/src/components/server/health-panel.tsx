import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import type { ServerHealthStatus, ServerHealthWindow } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { healthQuery } from '@/hooks/use-server-live'
import { cn } from '@/lib/utils'

// A tick is 1/30 s
const tickUs = 1_000_000 / 30

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

/**
 * How the server process runs, from the ServerHealth windows its module sends every 10 s: the latest window's status
 * and figures, and the last hour. WebAdmin rates the windows; this only shows the rating. A server whose module sends
 * none has no data.
 */
export function HealthPanel({ serverId, connected }: { serverId: string; connected: boolean }) {
  // Fetched and kept current by useServerLive
  const health = useQuery({ ...healthQuery(serverId), staleTime: Infinity })
  const latest = health.data?.latest ?? null
  const history = health.data?.history
  const summary = useMemo(() => summarize(history ?? []), [history])

  // A window of a server that has gone offline is not its state now
  const status = connected && latest ? latest.status : null

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

          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
              <span className="font-medium">Last hour</span>
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
              <div
                role="img"
                aria-label={`The health windows of the last hour, oldest first: ${summary.counts.Good} good, ${summary.counts.Warn} warn, ${summary.counts.Bad} bad`}
                className="flex flex-wrap gap-px"
              >
                {history.map((w) => (
                  <span key={`${w.time} ${w.tick}`} title={windowTitle(w)} className={cn('h-5 w-1.5 rounded-[1px]', statusColors[w.status])} />
                ))}
              </div>
            )}
          </div>

          <p className="text-xs text-muted-foreground">
            The server runs frames of 28.3 ms against ticks of 33.3 ms, so a median interval of about 28 ms and a longest interval of about 57 ms
            are normal. Late ticks (an interval over 66.7 ms) and short ticks (two ticks in one frame) are not.
            {history && history.length > 0 && ' One cell per 10 s window, oldest first; loading and stepped windows are left out of the figures.'}
          </p>
        </CardContent>
      )}
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
