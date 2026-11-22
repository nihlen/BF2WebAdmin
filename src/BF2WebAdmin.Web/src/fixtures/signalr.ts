// The fixture mode's stand-in for @microsoft/signalr (fixtures-plugin.ts points the import here). It connects at once,
// and when a page subscribes to a server it sends that server's player positions, so the minimap has players on it.
// No other live events by itself: every page shows its snapshot, the same on every run. The screenshot script sends
// events for a shot of their effect (new chat while another tab is open) through window.__fixtureHub.

import { fixtureSnapshot, scenarioFromCookie } from './data'

export const HubConnectionState = {
  Disconnected: 'Disconnected',
  Connecting: 'Connecting',
  Connected: 'Connected',
  Disconnecting: 'Disconnecting',
  Reconnecting: 'Reconnecting',
} as const
export type HubConnectionState = (typeof HubConnectionState)[keyof typeof HubConnectionState]

export const LogLevel = { Trace: 0, Debug: 1, Information: 2, Warning: 3, Error: 4, Critical: 5, None: 6 } as const

type Handler = (...args: unknown[]) => void

class FixtureHubConnection {
  state: HubConnectionState = HubConnectionState.Disconnected
  private handlers = new Map<string, Set<Handler>>()

  constructor() {
    ;(window as unknown as { __fixtureHub: unknown }).__fixtureHub = { emit: (name: string, event: unknown) => this.emit(name, event) }
  }

  start() {
    this.state = HubConnectionState.Connected
    return Promise.resolve()
  }

  stop() {
    this.state = HubConnectionState.Disconnected
    return Promise.resolve()
  }

  on(name: string, handler: Handler) {
    if (!this.handlers.has(name)) this.handlers.set(name, new Set())
    this.handlers.get(name)!.add(handler)
  }

  off(name: string, handler: Handler) {
    this.handlers.get(name)?.delete(handler)
  }

  onreconnecting() {}
  onreconnected() {}
  onclose() {}

  invoke(method: string, ...args: unknown[]) {
    if (method === 'Subscribe') this.sendPositions(String(args[0]))
    return Promise.resolve()
  }

  private emit(name: string, event: unknown) {
    this.handlers.get(name)?.forEach((handler) => handler(event))
  }

  /** Two position events per player, after the page has fetched its snapshot */
  private sendPositions(serverId: string) {
    const snapshot = fixtureSnapshot(serverId, scenarioFromCookie(document.cookie))
    if (!snapshot) return
    for (const delay of [400, 700]) {
      setTimeout(() => {
        for (const player of snapshot.players) {
          if (!player.isAlive || !player.position) continue
          this.emit('PlayerPositionEvent', {
            playerId: player.index,
            position: player.position,
            rotation: player.rotation ?? { x: 0, y: 0, z: 0 },
            ping: player.score.ping,
            timeStamp: new Date().toISOString(),
          })
        }
      }, delay)
    }
  }
}

export class HubConnectionBuilder {
  withUrl() {
    return this
  }
  withAutomaticReconnect() {
    return this
  }
  configureLogging() {
    return this
  }
  build() {
    return new FixtureHubConnection()
  }
}
