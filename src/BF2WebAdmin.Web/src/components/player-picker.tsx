import { keepPreviousData } from '@tanstack/react-query'
import { Search, X } from 'lucide-react'
import { useDeferredValue, useId, useState } from 'react'
import { api, type PlayerSearchResult } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useServers } from '@/hooks/use-servers'
import { formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

export type PickedPlayer = Pick<PlayerSearchResult, 'hash' | 'name'>

const shortHash = (value: string) => (value.length > 14 ? `${value.slice(0, 8)}…${value.slice(-4)}` : value)

// The server answers from two characters on
const minQuery = 2
const maxShown = 6

/**
 * A player of a server group, found by name: those online on the group's servers first, then those seen on them before
 * (GET /api/server-groups/{group}/players). The matches are a list under the field, in the page's flow (so a card or a
 * dialog does not cut them off); the arrow keys and Enter pick one, which then shows as a chip with its hash. What is
 * typed without picking stays the field's text, for a raw CD-key hash or address.
 */
export function PlayerPicker({
  id,
  serverGroup,
  text,
  onTextChange,
  picked,
  onPick,
  placeholder,
  invalid,
}: {
  id: string
  serverGroup: string
  text: string
  onTextChange: (text: string) => void
  picked: PickedPlayer | null
  onPick: (player: PickedPlayer | null) => void
  placeholder?: string
  invalid?: boolean
}) {
  const listId = useId()
  const servers = useServers().data ?? []
  const [active, setActive] = useState(0)
  const [dismissed, setDismissed] = useState(false)
  const query = useDeferredValue(text.trim())
  const results = api.useQuery(
    'get',
    '/api/server-groups/{serverGroup}/players',
    { params: { path: { serverGroup }, query: { query } } },
    { enabled: query.length >= minQuery && !picked, placeholderData: keepPreviousData, staleTime: 30_000 },
  )
  const shown = query.length >= minQuery && !dismissed ? (results.data ?? []).slice(0, maxShown) : []
  const current = Math.min(active, shown.length - 1)
  const optionId = (i: number) => `${listId}-${i}`

  const pick = (player: PlayerSearchResult) => {
    onPick({ hash: player.hash, name: player.name })
    setActive(0)
  }

  if (picked)
    return (
      <div className="flex h-8 min-w-0 items-center gap-2 rounded-lg border border-input bg-muted/40 pr-1 pl-2.5 text-sm">
        <span className="min-w-0 truncate font-medium">{picked.name}</span>
        <span className="shrink-0 font-mono text-xs text-muted-foreground" title={picked.hash}>
          {shortHash(picked.hash)}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon-xs"
          className="ml-auto"
          aria-label={`Clear ${picked.name}`}
          onClick={() => {
            onPick(null)
            requestAnimationFrame(() => document.getElementById(id)?.focus())
          }}
        >
          <X />
        </Button>
      </div>
    )

  return (
    <div className="space-y-1.5">
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
        <Input
          id={id}
          role="combobox"
          aria-expanded={shown.length > 0}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={shown.length > 0 ? optionId(current) : undefined}
          aria-invalid={invalid || undefined}
          autoComplete="off"
          spellCheck={false}
          className="pl-8"
          placeholder={placeholder}
          value={text}
          onChange={(e) => {
            onTextChange(e.target.value)
            setActive(0)
            setDismissed(false)
          }}
          onKeyDown={(e) => {
            if (shown.length === 0) return
            if (e.key === 'ArrowDown') {
              e.preventDefault()
              setActive((current + 1) % shown.length)
            } else if (e.key === 'ArrowUp') {
              e.preventDefault()
              setActive((current - 1 + shown.length) % shown.length)
            } else if (e.key === 'Enter') {
              e.preventDefault()
              pick(shown[current])
            } else if (e.key === 'Escape') {
              e.preventDefault()
              setDismissed(true)
            }
          }}
        />
      </div>
      {shown.length > 0 && (
        <ul id={listId} role="listbox" aria-label="Players found" className="overflow-hidden rounded-md border text-sm">
          {shown.map((player, i) => {
            const server = player.serverId ? servers.find((s) => s.id === player.serverId) : undefined
            return (
              <li
                key={player.hash}
                id={optionId(i)}
                role="option"
                aria-selected={i === current}
                className={cn('flex cursor-pointer items-center gap-2 px-3 py-1.5', i === current && 'bg-accent text-accent-foreground')}
                onMouseMove={() => i !== current && setActive(i)}
                // Before the field loses focus
                onMouseDown={(e) => {
                  e.preventDefault()
                  pick(player)
                }}
              >
                <span className="min-w-0 truncate font-medium">{player.name}</span>
                {player.serverId ? (
                  <Badge variant="secondary" className="shrink-0" title={server?.name}>
                    online
                  </Badge>
                ) : (
                  player.lastSeen && <span className="shrink-0 text-xs text-muted-foreground">seen {formatRelative(player.lastSeen)}</span>
                )}
                <span className="ml-auto shrink-0 font-mono text-xs text-muted-foreground max-sm:hidden" title={player.hash}>
                  {shortHash(player.hash)}
                </span>
              </li>
            )
          })}
        </ul>
      )}
      {query.length >= minQuery && !dismissed && results.data?.length === 0 && !results.isPlaceholderData && (
        <p className="text-xs text-muted-foreground">No player of this group by that name. A CD-key hash can be used as it is.</p>
      )}
    </div>
  )
}
