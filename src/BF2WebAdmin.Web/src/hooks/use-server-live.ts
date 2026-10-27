import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { api, type Player, type ServerSnapshot, type Vector3 } from '@/api/client'
import { onHubReconnected, useHubEvent, useServerSubscription } from '@/api/hub'

const maxLogEntries = 500

/** The last two positions of a player, for the map to interpolate between. Updated without re-rendering. */
export type PlayerPosition = {
  previous: Vector3
  current: Vector3
  rotation: Vector3
  /** performance.now() when current arrived */
  receivedAt: number
  ping: number
}

export const snapshotQuery = (serverId: string) => api.queryOptions('get', '/api/servers/{serverId}', { params: { path: { serverId } } })

/**
 * A server's state: the snapshot from the API, kept current by the hub's game events. Positions arrive several times
 * a second per player, so they go to the positions ref (for the map) instead of the query cache.
 */
export function useServerLive(serverId: string) {
  const queryClient = useQueryClient()
  const subscribed = useServerSubscription(serverId)
  const query = snapshotQuery(serverId)
  const snapshot = useQuery({ ...query, enabled: subscribed, staleTime: Infinity })
  const positions = useRef(new Map<number, PlayerPosition>())

  // Events that arrive before the snapshot are already in it
  const update = (recipe: (snapshot: ServerSnapshot) => ServerSnapshot) =>
    queryClient.setQueryData<ServerSnapshot>(query.queryKey, (current) => (current ? recipe(current) : current))

  const updatePlayer = (playerId: number, recipe: (player: Player) => Player) =>
    update((s) => ({ ...s, players: s.players.map((p) => (p.index === playerId ? recipe(p) : p)) }))

  const log = (message: string, timestamp: string) =>
    update((s) => ({ ...s, eventLog: [...s.eventLog, { id: crypto.randomUUID(), message, timestamp }].slice(-maxLogEntries) }))

  const playerName = (playerId: number) =>
    queryClient.getQueryData<ServerSnapshot>(query.queryKey)?.players.find((p) => p.index === playerId)?.name ?? `#${playerId}`

  // The snapshot is the start point for the positions too
  useEffect(() => {
    positions.current.clear()
  }, [serverId])

  useEffect(
    () =>
      onHubReconnected(() => {
        positions.current.clear()
        void queryClient.invalidateQueries({ queryKey: snapshotQuery(serverId).queryKey })
      }),
    [queryClient, serverId],
  )

  useHubEvent('ServerUpdateEvent', (e) => {
    if (e.id !== serverId) return
    const { timeStamp: _, ...server } = e
    update((s) => ({ ...s, server }))
  })

  useHubEvent('SocketStateEvent', (e) => {
    update((s) => ({ ...s, server: { ...s.server, socketState: e.state } }))
    log(`Socket ${e.state}`, e.timeStamp)
  })

  useHubEvent('GameStateEvent', (e) => {
    update((s) => ({ ...s, server: { ...s.server, gameState: e.state } }))
    log(`Game state ${e.state}`, e.timeStamp)
  })

  useHubEvent('MapChangeEvent', (e) => {
    update((s) => ({ ...s, server: { ...s.server, map: e.map }, teams: e.teams }))
    log(`Map ${e.map} (${e.size})`, e.timeStamp)
  })

  useHubEvent('ChatEvent', (e) => {
    update((s) => ({ ...s, chatLog: [...s.chatLog, { message: e.message, timestamp: e.timeStamp }].slice(-maxLogEntries) }))
  })

  useHubEvent('PlayerJoinEvent', (e) => {
    update((s) => ({ ...s, players: [...s.players.filter((p) => p.index !== e.player.index), e.player] }))
    log(`${e.player.name} joined`, e.timeStamp)
  })

  useHubEvent('PlayerLeftEvent', (e) => {
    log(`${playerName(e.playerId)} left`, e.timeStamp)
    update((s) => ({ ...s, players: s.players.filter((p) => p.index !== e.playerId) }))
    positions.current.delete(e.playerId)
  })

  useHubEvent('PlayerTeamEvent', (e) => {
    updatePlayer(e.playerId, (p) => ({ ...p, team: e.teamId }))
    log(`${playerName(e.playerId)} changed to team ${e.teamId}`, e.timeStamp)
  })

  useHubEvent('PlayerScoreEvent', (e) => {
    updatePlayer(e.playerId, (p) => ({
      ...p,
      score: { ...p.score, kills: e.kills, deaths: e.deaths, team: e.teamScore, total: e.totalScore },
    }))
  })

  useHubEvent('PlayerSpawnEvent', (e) => {
    updatePlayer(e.playerId, (p) => ({ ...p, isAlive: true }))
    positions.current.set(e.playerId, {
      previous: e.position,
      current: e.position,
      rotation: e.rotation,
      receivedAt: performance.now(),
      ping: positions.current.get(e.playerId)?.ping ?? 0,
    })
    log(`${playerName(e.playerId)} spawned`, e.timeStamp)
  })

  useHubEvent('PlayerDeathEvent', (e) => {
    updatePlayer(e.playerId, (p) => ({ ...p, isAlive: false }))
    log(`${playerName(e.playerId)} died`, e.timeStamp)
  })

  useHubEvent('PlayerKillEvent', (e) => {
    log(`${playerName(e.attackerId)} killed ${playerName(e.victimId)} (${e.weapon})`, e.timeStamp)
  })

  useHubEvent('PlayerVehicleEvent', (e) => {
    log(`${playerName(e.playerId)} vehicle ${e.vehicle?.rootVehicleTemplate ?? 'none'}`, e.timeStamp)
  })

  useHubEvent('PlayerPositionEvent', (e) => {
    const previous = positions.current.get(e.playerId)
    positions.current.set(e.playerId, {
      previous: previous?.current ?? e.position,
      current: e.position,
      rotation: e.rotation,
      receivedAt: performance.now(),
      ping: e.ping,
    })
  })

  return { snapshot, positions, subscribed }
}
