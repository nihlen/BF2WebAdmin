import { Loader2, Map as MapIcon, Search, SkipForward } from 'lucide-react'
import { useState } from 'react'
import type { GameMap, ServerSnapshot } from '@/api/client'
import type { ConfirmRequest } from '@/components/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { ServerActions } from '@/hooks/use-server-actions'
import { usePendingActions } from '@/lib/pending-actions'
import { gameModeName } from '@/lib/teams'
import { cn } from '@/lib/utils'

/** The map rotation. A click asks first (there may be players), then follows the change until the new round starts. */
export function MapList({
  serverId,
  snapshot,
  actions,
  confirm,
}: {
  serverId: string
  snapshot: ServerSnapshot
  actions: ServerActions
  confirm: (request: ConfirmRequest) => void
}) {
  const [filter, setFilter] = useState('')
  const pending = usePendingActions(serverId)
  const changing = [...pending].some((key) => key.startsWith('map:') || key === 'next-map' || key === 'restart-map')
  const players = snapshot.players.length
  const { maps, server } = snapshot

  const shown = maps.filter((m) => !filter || m.name.toLowerCase().includes(filter.toLowerCase()))
  const playersText = players === 0 ? 'No players are online.' : `${players} ${players === 1 ? 'player is' : 'players are'} online and will be moved to the new map.`

  const change = (map: GameMap) =>
    confirm({
      title: `Change map to ${map.name}?`,
      description: `${gameModeName(map.gameMode)}, size ${map.size}. The current round ends now. ${playersText}`,
      confirmLabel: 'Change map',
      onConfirm: () => void actions.changeMap(map),
    })

  return (
    <div className="flex h-[550px] flex-col">
      <div className="flex gap-2 border-b p-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input className="pl-8" placeholder="Search maps" aria-label="Search maps" value={filter} onChange={(e) => setFilter(e.target.value)} />
        </div>
        <Button
          variant="outline"
          disabled={changing}
          onClick={() =>
            confirm({
              title: 'Run the next map?',
              description: `The current round ends now. ${playersText}`,
              confirmLabel: 'Run next map',
              onConfirm: () => void actions.nextMap(),
            })
          }
        >
          <SkipForward data-icon="inline-start" />
          Next map
        </Button>
      </div>
      <ul className="flex-1 overflow-y-auto p-2">
        {shown.length === 0 && <li className="p-3 text-sm text-muted-foreground">{maps.length === 0 ? 'The server sent no maps' : 'No map matches'}</li>}
        {shown.map((map) => {
          const isCurrent = map.name.toLowerCase() === server.map?.toLowerCase()
          const isPending = pending.has(`map:${map.name}`)
          return (
            <li key={`${map.index}-${map.name}-${map.gameMode}-${map.size}`}>
              <button
                type="button"
                disabled={changing}
                className={cn(
                  'flex w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60',
                  isCurrent && 'bg-muted font-medium',
                  isPending && 'opacity-100',
                )}
                onClick={() => change(map)}
              >
                {isPending ? <Loader2 className="size-4 animate-spin text-primary" /> : <MapIcon className="size-4 text-muted-foreground" />}
                <span className="w-6 text-muted-foreground">{map.index}</span>
                <span className="flex-1 truncate">{map.name}</span>
                <span className="hidden text-muted-foreground sm:inline">{gameModeName(map.gameMode)}</span>
                <span className="w-6 text-right text-muted-foreground">{map.size}</span>
                {isCurrent && <span className="text-xs text-muted-foreground">current</span>}
                {isPending && <span className="text-xs text-primary">changing…</span>}
              </button>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
