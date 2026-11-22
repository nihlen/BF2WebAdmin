import type { GameState, Server } from '@/api/client'
import { formatRelative, formatTime } from '@/lib/format'

// The game's and the admin channel's states as the server sends them (enum names) in words, the same on every page

const gameStates: Record<GameState, string> = {
  PreGame: 'Warm-up',
  Playing: 'Playing',
  EndGame: 'Round over',
  Paused: 'Paused',
  Restart: 'Restarting',
  NotConnected: 'Not connected',
}

export const gameStateLabel = (state: GameState) => gameStates[state] ?? state

export type StatusTone = 'good' | 'warn' | 'bad'

/**
 * A server's status in one word and a colour: offline (red) when its admin channel is down, whatever the game state
 * was; otherwise the game state, amber while paused or restarting.
 */
export function serverStatus(server: Pick<Server, 'socketState' | 'gameState'>): { label: string; tone: StatusTone } {
  if (server.socketState !== 'Connected') return { label: 'Offline', tone: 'bad' }
  const tone = server.gameState === 'Paused' || server.gameState === 'Restart' || server.gameState === 'NotConnected' ? 'warn' : 'good'
  return { label: gameStateLabel(server.gameState), tone }
}

/**
 * The admin channel's connection in words, for a status's title: since when it is connected, when it was lost, or that
 * it has not connected since WebAdmin started (the server has no times then).
 */
export function connectionText(server: Pick<Server, 'socketState' | 'connectedAt' | 'disconnectedAt'>) {
  const at = (time: string) => `${formatTime(time)} (${formatRelative(time)})`
  if (server.socketState === 'Connected') return server.connectedAt ? `Connected since ${at(server.connectedAt)}` : 'The admin channel is connected'
  return server.disconnectedAt ? `Connection lost at ${at(server.disconnectedAt)}` : 'Not connected since WebAdmin started'
}

export const toneDot: Record<StatusTone, string> = {
  good: 'bg-emerald-500',
  warn: 'bg-amber-500',
  bad: 'bg-red-500',
}

export const toneText: Record<StatusTone, string> = {
  good: 'text-emerald-600 dark:text-emerald-400',
  warn: 'text-amber-600 dark:text-amber-400',
  bad: 'text-red-600 dark:text-red-400',
}
