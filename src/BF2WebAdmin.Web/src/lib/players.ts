import type { Player, PlayerRef } from '@/api/client'

/**
 * The player a chat line or an event names, on the server now: by hash when it has one (it stays theirs, while an index
 * goes to the next player who joins), else by index and name.
 */
export const findOnline = (players: Player[], target: PlayerRef) =>
  target.hash ? players.find((p) => p.hash === target.hash) : players.find((p) => p.index === target.index && p.name === target.name)
