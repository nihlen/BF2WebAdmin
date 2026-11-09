import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type GameMap, type Player, type ServerSnapshot } from '@/api/client'
import { authLevelName } from '@/lib/auth-levels'
import { trackAction } from '@/lib/pending-actions'

// Silent: trackAction shows the error in the action's own notification
const silent = { meta: { silent: true } }
const sameName = (a: string | null | undefined, b: string) => a?.toLowerCase() === b.toLowerCase()

/** Ban lengths (BanLength on the server): Round, Perm, or a number of seconds. */
export const banPeriods = [
  { value: 'Round', label: 'This round' },
  { value: String(60 * 60), label: '1 hour' },
  { value: String(24 * 60 * 60), label: '1 day' },
  { value: String(7 * 24 * 60 * 60), label: '1 week' },
  { value: String(30 * 24 * 60 * 60), label: '30 days' },
  { value: 'Perm', label: 'Permanent' },
]

/** The output of a "." command, or a fallback when it said nothing. */
const outputText = (output: readonly string[], fallback: string) => (output.length > 0 ? output.join('\n') : fallback)

/**
 * The actions of a server page. Each shows one notification that follows it from the request to the moment the game
 * shows the effect (a new round, the paused state, a player's new team), or says that it did not happen in time.
 */
export function useServerActions(serverId: string, snapshot: ServerSnapshot | undefined) {
  const queryClient = useQueryClient()
  const params = { path: { serverId } }
  const bansKey = api.queryOptions('get', '/api/servers/{serverId}/bans', { params }).queryKey
  const players = () => snapshot?.players ?? []
  const playerName = (index: number) => players().find((p) => p.index === index)?.name ?? `#${index}`

  const map = api.useMutation('post', '/api/servers/{serverId}/map', silent)
  const restartMapMutation = api.useMutation('post', '/api/servers/{serverId}/map/restart', silent)
  const nextMapMutation = api.useMutation('post', '/api/servers/{serverId}/map/next', silent)
  const pause = api.useMutation('post', '/api/servers/{serverId}/pause', silent)
  const command = api.useMutation('post', '/api/servers/{serverId}/commands', silent)
  const warnMutation = api.useMutation('post', '/api/servers/{serverId}/players/{index}/warn', silent)
  const kickMutation = api.useMutation('post', '/api/servers/{serverId}/players/{index}/kick', silent)
  const banMutation = api.useMutation('post', '/api/servers/{serverId}/players/{index}/ban', silent)
  const unbanMutation = api.useMutation('delete', '/api/servers/{serverId}/bans/{identifier}', silent)
  const chat = api.useMutation('post', '/api/servers/{serverId}/chat')
  const auth = api.useMutation('put', '/api/server-groups/{serverGroup}/auth', silent)

  const runCommand = async (text: string) => (await command.mutateAsync({ params, body: { command: text } })).output
  const playerParams = (player: Player) => ({ path: { serverId, index: player.index } })

  /** A "." command; the notification shows what it said in game. */
  const custom = (text: string, key = `command:${text}`) =>
    trackAction({ serverId, key, sending: `Running ${text}…`, run: () => runCommand(text), sent: (output) => outputText(output, `${text} sent`) })

  return {
    changeMap: (target: GameMap) =>
      trackAction({
        serverId,
        key: `map:${target.name}`,
        sending: `Changing the map to ${target.name}…`,
        run: () => map.mutateAsync({ params, body: { name: target.name, gameMode: target.gameMode, size: target.size } }),
        sent: () => `Loading ${target.name}…`,
        until: {
          check: ({ roundStart, server }) => sameName(roundStart?.map, target.name) || sameName(server?.map, target.name),
          success: `The map is now ${target.name}`,
          timeoutMs: 180_000,
          timeout: `No round on ${target.name} has started after 3 minutes. Check the server's console.`,
        },
      }),

    restartMap: () =>
      trackAction({
        serverId,
        key: 'restart-map',
        sending: 'Restarting the round…',
        run: () => restartMapMutation.mutateAsync({ params }),
        sent: () => 'Waiting for the new round…',
        until: {
          check: ({ roundStart }) => !!roundStart,
          success: 'The round has restarted',
          timeoutMs: 60_000,
          timeout: 'No new round has started after a minute',
        },
      }),

    nextMap: () =>
      trackAction({
        serverId,
        key: 'next-map',
        sending: 'Running the next map…',
        run: () => nextMapMutation.mutateAsync({ params }),
        sent: () => 'Loading the next map…',
        until: {
          check: ({ roundStart }) => !!roundStart,
          success: (update) => `The map is now ${update.roundStart?.map}`,
          timeoutMs: 180_000,
          timeout: 'No new round has started after 3 minutes',
        },
      }),

    togglePause: () => {
      const wasPaused = snapshot?.server.gameState === 'Paused'
      return trackAction({
        serverId,
        key: 'pause',
        sending: wasPaused ? 'Resuming the game…' : 'Pausing the game…',
        run: () => pause.mutateAsync({ params }),
        until: {
          check: ({ server }) => !!server && (server.gameState === 'Paused') !== wasPaused,
          success: wasPaused ? 'The game is running again' : 'The game is paused',
          timeoutMs: 15_000,
          timeout: wasPaused ? 'The game still shows as paused' : 'The game does not show as paused',
        },
      })
    },

    /** A clean quit (.restartserver), then wait for the server to go down and connect again. */
    restartServer: () => {
      let wentDown = false
      return trackAction({
        serverId,
        key: 'restart-server',
        sending: 'Restarting the server…',
        run: async () => {
          const output = await runCommand('.restartserver')
          const failure = output.find((line) => line.includes('Restart failed'))
          if (failure) throw new Error(failure)
          return output
        },
        sent: () => 'The server is quitting…',
        until: {
          check: ({ server }) => {
            if (server?.socketState === 'Disconnected') {
              wentDown = true
              return 'The server is down, waiting for it to start again…'
            }
            return wentDown && server?.socketState === 'Connected'
          },
          success: 'The server is back',
          timeoutMs: 240_000,
          timeout: wentDown
            ? 'The server has not come back after 4 minutes. Its container needs a restart policy (always or unless-stopped)'
            : 'The server has not disconnected after 4 minutes',
        },
      })
    },

    switchTeam: (player: Player) =>
      trackAction({
        serverId,
        key: `team:${player.index}`,
        sending: `Switching ${player.name}…`,
        run: () => runCommand(`.switchid ${player.index}`),
        until: {
          check: ({ players }) => !!players?.some((p) => p.index === player.index && p.team !== player.team),
          success: `${player.name} switched teams`,
          timeoutMs: 15_000,
          timeout: `${player.name} is still on the same team`,
        },
      }),

    killPlayer: (player: Player) => custom(`.killid ${player.index}`, `kill:${player.index}`),

    warn: (player: Player, reason: string) =>
      trackAction({
        serverId,
        key: `warn:${player.index}`,
        sending: `Warning ${player.name}…`,
        run: () => warnMutation.mutateAsync({ params: playerParams(player), body: { reason } }),
        sent: (response) => response.message,
      }),

    kick: (player: Player, reason: string) =>
      trackAction({
        serverId,
        key: `kick:${player.index}`,
        sending: `Kicking ${player.name}…`,
        run: () => kickMutation.mutateAsync({ params: playerParams(player), body: { reason } }),
        sent: (response) => response.message,
        until: {
          check: ({ players }) => !!players && !players.some((p) => p.index === player.index),
          success: `${player.name} has left the server`,
          timeoutMs: 20_000,
          timeout: `${player.name} is still on the server`,
        },
      }),

    /** The engine bans and kicks at once; the player list shows it when the player has left. */
    ban: (player: Player, reason: string, period: string) =>
      trackAction({
        serverId,
        key: `ban:${player.index}`,
        sending: `Banning ${player.name}…`,
        run: () => banMutation.mutateAsync({ params: playerParams(player), body: { reason, length: period } }),
        sent: (response) => response.message,
        until: {
          check: ({ players }) => {
            const gone = !!players && !players.some((p) => p.index === player.index)
            if (gone) void queryClient.invalidateQueries({ queryKey: bansKey })
            return gone
          },
          success: `${player.name} is banned and has left the server`,
          timeoutMs: 30_000,
          timeout: `${player.name} is still on the server. Check the Bans tab`,
        },
      }),

    unban: (identifier: string, nick: string | null) =>
      trackAction({
        serverId,
        key: `unban:${identifier}`,
        sending: `Unbanning ${nick ?? identifier}…`,
        run: async () => {
          const response = await unbanMutation.mutateAsync({ params: { path: { serverId, identifier } } })
          await queryClient.invalidateQueries({ queryKey: bansKey })
          return response
        },
        sent: (response) => response.message,
      }),

    /** Any "." command, such as from the command panel. */
    custom,

    setAuthLevel: (serverGroup: string, playerHash: string, level: number, name: string) =>
      trackAction({
        serverId,
        key: `auth:${playerHash}`,
        sending: `Setting the auth level of ${name}…`,
        run: async () => {
          await auth.mutateAsync({ params: { path: { serverGroup } }, body: { playerHash, authLevel: level } })
          await queryClient.invalidateQueries({ queryKey: playerAuthQuery(serverGroup).queryKey })
        },
        sent: () => `${name} is now ${authLevelName(level)}`,
      }),

    /** The chat box: "." and "!" answers are shown in a notification, anything else is said in game. */
    sendChat: (message: string) =>
      chat.mutate(
        { params, body: { message } },
        {
          onSuccess: (data) => {
            if (data.response) toast(data.response, { description: message })
          },
        },
      ),

    playerName,
  }
}

export type ServerActions = ReturnType<typeof useServerActions>

export const playerAuthQuery = (serverGroup: string) =>
  api.queryOptions('get', '/api/server-groups/{serverGroup}/auth', { params: { path: { serverGroup } } })
