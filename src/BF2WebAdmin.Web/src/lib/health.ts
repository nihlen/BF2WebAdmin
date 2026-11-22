import type { PlayerConnection, ServerHealthStatus, ServerHealthWindow } from '@/api/client'

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

/** The latest window's status in a word, for the server header's chip and the dashboard's and sidebar's dots. */
export const chipLabels: Record<ServerHealthStatus, string> = {
  Good: 'Healthy',
  Warn: 'Warning',
  Bad: 'Problems',
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

/** What the latest window says, in a sentence: its reasons with their figures, or that it ran smoothly. */
export function healthTitle(latest: ServerHealthWindow) {
  return latest.reasons.length > 0
    ? `${sentence(reasonsText(latest))} in the window received at ${clock(latest.time)}`
    : (statusHelp[latest.status] ?? `Running smoothly in the window received at ${clock(latest.time)}`)
}

/** A player's connection problems in words, such as "unsteady ping"; empty for a good connection. */
export const connectionReasonsText = (c: PlayerConnection) => c.reasons.map((r) => connectionReasons[r] ?? r).join(', ')

/** A ping worth a look: amber over 100 ms, red over 200 ms. */
export const pingClass = (ping: number) =>
  ping > 200 ? 'font-medium text-red-600 dark:text-red-400' : ping > 100 ? 'font-medium text-amber-600 dark:text-amber-400' : undefined
