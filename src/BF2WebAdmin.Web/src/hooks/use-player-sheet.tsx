import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import type { Player, PlayerRef } from '@/api/client'
import { ModerationDialog, type ModerationKind, type ModerationRequest } from '@/components/server/moderation-dialog'
import { PlayerSheet } from '@/components/server/player-sheet'
import { useServerPage } from '@/components/server/server-context'
import { playerAuthQuery, type ServerActions } from '@/hooks/use-server-actions'
import { findOnline } from '@/lib/players'

/**
 * A player's sheet and the moderation dialog, for any of a server's pages: the Live tab's scoreboard and chat, the
 * Health tab's connections and the Activity tab's events open the same sheet. The page renders `element` once and
 * calls `open` with a player of the snapshot or one a chat line or an event names (who may have left), or `moderate`
 * to go straight to warn, kick or ban.
 */
export function usePlayerSheet(actions: ServerActions) {
  const { serverId, snapshot, positions } = useServerPage()
  const [player, setPlayer] = useState<PlayerRef | null>(null)
  const [moderation, setModeration] = useState<ModerationRequest | null>(null)
  const moderate = (kind: ModerationKind, target: Player) => setModeration({ kind, player: target })

  const serverGroup = snapshot.data?.server.serverGroup ?? ''
  const auth = useQuery({ ...playerAuthQuery(serverGroup), enabled: !!serverGroup })
  const authLevels = useMemo(() => new Map((auth.data ?? []).map((a) => [a.playerHash, a.authLevel])), [auth.data])
  const players = snapshot.data?.players ?? []
  const current = player ? findOnline(players, player) : undefined

  const element = (
    <>
      <PlayerSheet
        serverId={serverId}
        player={player}
        current={current}
        teams={snapshot.data?.teams ?? []}
        ping={current ? positions.current.get(current.index)?.ping : undefined}
        authLevels={authLevels}
        serverGroup={serverGroup}
        actions={actions}
        onModerate={moderate}
        onClose={() => setPlayer(null)}
      />
      <ModerationDialog request={moderation} actions={actions} onClose={() => setModeration(null)} />
    </>
  )

  return { open: setPlayer, moderate, authLevels, serverGroup, element }
}
