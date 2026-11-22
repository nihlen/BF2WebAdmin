import type { PlayerConnection, ServerConnections, ServerHealth, ServerHealthStatus, ServerHealthWindow } from '@/api/client'

// The health windows' and connections' ratings (WebAdmin's ServerHealthRules and PlayerConnectionRules) in words and
// colours, the same on the Health tab, the server header's chip and the dashboard's and sidebar's dots

export const statusColors: Record<ServerHealthStatus, string> = {
  Good: 'bg-emerald-500',
  Warn: 'bg-amber-500',
  Bad: 'bg-red-500',
  Loading: 'bg-sky-500',
  Stepped: 'bg-muted-foreground/40',
}

export const statusHelp: Partial<Record<ServerHealthStatus, string>> = {
  Loading: 'A level was loading in this window, so its figures say nothing about how the server runs.',
  Stepped: 'Stepped time is on, so the tick figures do not describe a real-time server.',
}

/** A status in a word, the same for a window, a connection and a server's overall health, wherever it shows. */
export const statusLabels: Record<ServerHealthStatus, string> = {
  Good: 'Healthy',
  Warn: 'Warning',
  Bad: 'Degraded',
  Loading: 'Loading level',
  Stepped: 'Stepped time',
}

export const ms = (microseconds: number, digits: number) => (microseconds / 1000).toFixed(digits)
const count = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`
export const clock = (value: string) => new Date(value).toLocaleTimeString()
export const sentence = (text: string) => text.charAt(0).toUpperCase() + text.slice(1)

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

export const reasonsText = (w: ServerHealthWindow) => w.reasons.map((r) => reasonText(r, w)).join(', ')

/** A reason of a Warn or Bad connection (PlayerConnectionRules on the server) in words; the row has the figures. */
const connectionReasons: Record<string, string> = {
  lossToPlayer: 'loss to the player',
  lossFromPlayer: 'loss from the player',
  pingSpread: 'unsteady ping',
  lowSendRate: 'sends at the low rate',
}

/** A player's connection problems in words, such as "unsteady ping"; empty for a good connection. */
export const connectionReasonsText = (c: PlayerConnection) => c.reasons.map((r) => connectionReasons[r] ?? r).join(', ')

// The connections come right after their window; older ones are of a module that stopped sending them
const connectionsMaxAgeMs = 15_000

/** The players' connections of the latest window, or null when there are none or they are older than the window. */
export function currentConnections(health: Pick<ServerHealth, 'latest' | 'connections'>): ServerConnections | null {
  const { latest, connections } = health
  if (!latest || !connections) return null
  return Date.parse(latest.time) - Date.parse(connections.time) < connectionsMaxAgeMs ? connections : null
}

/** What is wrong with a player's connection, with its figure and the player's name ("packet loss to GhostRotor 5.5 %"). */
function connectionProblem(c: PlayerConnection) {
  const name = c.name ?? `player ${c.playerId}`
  return c.reasons
    .map((r) => {
      switch (r) {
        case 'lossToPlayer':
          return `packet loss to ${name} ${c.lossToPlayerPercent.toFixed(1)} %`
        case 'lossFromPlayer':
          return `packet loss from ${name} ${c.lossFromPlayerPercent.toFixed(1)} %`
        case 'pingSpread':
          return `unsteady ping of ${name} (${c.pingMin}–${c.pingMax} ms)`
        case 'lowSendRate':
          return `${name} sends at the low rate`
        default:
          return `${name}: ${r}`
      }
    })
    .join(', ')
}

const severity: Record<ServerHealthStatus, number> = { Bad: 3, Warn: 2, Good: 1, Loading: 0, Stepped: 0 }

/**
 * A server's health now in one status: the worse of the latest window and its players' connections, so that it is
 * amber as soon as anything warns, and its reasons in a sentence for a tooltip. A window that is loading a level or in
 * stepped time keeps its own status unless a connection is worse. Null without a window.
 */
export function overallHealth(health: Pick<ServerHealth, 'latest' | 'connections'> | undefined) {
  const latest = health?.latest
  if (!latest) return null
  const troubled = (currentConnections(health)?.players ?? []).filter((p) => p.status !== 'Good')
  const status = troubled.reduce<ServerHealthStatus>((s, p) => (severity[p.status] > severity[s] ? p.status : s), latest.status)
  const problems = [...(latest.reasons.length > 0 ? [reasonsText(latest)] : []), ...troubled.map(connectionProblem)]
  const reason = problems.length > 0 ? `${sentence(problems.join('; '))}.` : (statusHelp[latest.status] ?? 'Running smoothly.')
  // Where a line has room for one problem only (the dashboard), the first and how many more; the title has them all
  const summary = problems.length > 1 ? `${sentence(problems[0])}, and ${problems.length - 1} more.` : reason
  return { status, label: statusLabels[status], reason, summary, title: `${statusLabels[status]}: ${reason} Window received at ${clock(latest.time)}.` }
}

/** A ping worth a look: amber over 100 ms, red over 200 ms. */
export const pingClass = (ping: number) =>
  ping > 200 ? 'font-medium text-red-600 dark:text-red-400' : ping > 100 ? 'font-medium text-amber-600 dark:text-amber-400' : undefined
