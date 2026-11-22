import { Loader2, Search, SkipForward } from 'lucide-react'
import { useState } from 'react'
import type { GameMap, ServerSnapshot } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import type { ServerActions } from '@/hooks/use-server-actions'
import { usePendingActions } from '@/lib/pending-actions'
import { gameModeName } from '@/lib/teams'
import { cn } from '@/lib/utils'

/** A map of the rotation to load, or the rotation's next map */
type Target = GameMap | 'next'

const mapKey = (map: GameMap) => `${map.index}-${map.name}-${map.gameMode}-${map.size}`

/**
 * "Change map…" of the header's Round menu: the rotation, searchable, with the current map marked and a Load per row.
 * Loading asks first in the dialog itself (a second dialog over this one would hide the list it is about), then the
 * dialog closes and the notification follows the change until the new round starts.
 */
export function ChangeMapDialog({
  open,
  onOpenChange,
  serverId,
  snapshot,
  actions,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  serverId: string
  snapshot: ServerSnapshot
  actions: ServerActions
}) {
  const [filter, setFilter] = useState('')
  const [target, setTarget] = useState<Target | null>(null)
  const pending = usePendingActions(serverId)
  const changing = [...pending].some((key) => key.startsWith('map:') || key === 'next-map' || key === 'restart-map')
  const { maps, server, players } = snapshot

  const shown = maps.filter((m) => !filter || m.name.toLowerCase().includes(filter.toLowerCase()) || gameModeName(m.gameMode).toLowerCase().includes(filter.toLowerCase()))
  const playersText =
    players.length === 0 ? 'No players are online.' : `${players.length} ${players.length === 1 ? 'player is' : 'players are'} online and will be moved to the new map.`

  const close = (next: boolean) => {
    onOpenChange(next)
    if (!next) {
      setTarget(null)
      setFilter('')
    }
  }
  const run = () => {
    if (target === 'next') void actions.nextMap()
    else if (target) void actions.changeMap(target)
    close(false)
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="gap-0 p-0 sm:max-w-lg">
        <DialogHeader className="p-4 pb-3">
          <DialogTitle>Change map</DialogTitle>
          <DialogDescription>
            {server.map ? (
              <>
                Now playing <span className="font-medium text-foreground">{server.map}</span>. The round ends when the new map loads.
              </>
            ) : (
              'The round ends when the new map loads.'
            )}
          </DialogDescription>
        </DialogHeader>
        <div className="flex gap-2 border-y px-4 py-2">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input className="pl-8" placeholder="Search maps" aria-label="Search maps" value={filter} onChange={(e) => setFilter(e.target.value)} />
          </div>
          <Button variant="outline" disabled={changing} onClick={() => setTarget('next')}>
            <SkipForward data-icon="inline-start" />
            Next map
          </Button>
        </div>
        <ul className="max-h-[min(24rem,50svh)] overflow-y-auto p-2" aria-label="Map rotation">
          {shown.length === 0 && <li className="p-3 text-sm text-muted-foreground">{maps.length === 0 ? 'The server sent no maps.' : `No map matches “${filter.trim()}”.`}</li>}
          {shown.map((map) => {
            const isCurrent = map.name.toLowerCase() === server.map?.toLowerCase()
            const isPending = pending.has(`map:${map.name}`)
            const isTarget = target !== 'next' && target !== null && mapKey(target) === mapKey(map)
            return (
              <li key={mapKey(map)} className={cn('flex items-center gap-3 rounded-md px-3 py-1.5 text-sm', isCurrent && 'bg-muted', isTarget && 'ring-2 ring-primary/60')}>
                <span className="w-5 shrink-0 text-right text-xs text-muted-foreground tabular-nums">{map.index}</span>
                <span className="min-w-0 flex-1">
                  <span className={cn('block truncate', isCurrent && 'font-medium')}>{map.name}</span>
                  <span className="block text-xs text-muted-foreground">
                    {gameModeName(map.gameMode)}, size {map.size}
                  </span>
                </span>
                {isCurrent && <Badge variant="secondary">Current</Badge>}
                {isPending ? (
                  <span className="inline-flex items-center gap-1.5 text-xs text-primary">
                    <Loader2 className="size-3.5 animate-spin" />
                    Loading…
                  </span>
                ) : (
                  <Button size="sm" variant="outline" disabled={changing} aria-label={`Load ${map.name}`} onClick={() => setTarget(map)}>
                    Load
                  </Button>
                )}
              </li>
            )
          })}
        </ul>
        {/* The confirmation in place of a second dialog: what happens, and the one button that does it */}
        {target && (
          <div role="alertdialog" aria-label="Confirm the map change" className="flex flex-col gap-3 rounded-b-xl border-t bg-muted/50 p-4 sm:flex-row sm:items-center">
            <p className="flex-1 text-sm">
              <span className="font-medium">{target === 'next' ? 'Run the next map?' : `Load ${target.name}?`}</span>{' '}
              <span className="text-muted-foreground">
                {target !== 'next' && `${gameModeName(target.gameMode)}, size ${target.size}. `}The current round ends now. {playersText}
              </span>
            </p>
            <div className="flex shrink-0 justify-end gap-2">
              <Button variant="outline" onClick={() => setTarget(null)}>
                Cancel
              </Button>
              <Button disabled={changing} onClick={run}>
                {target === 'next' ? 'Run next map' : 'Change map'}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
