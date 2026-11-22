import { useQuery } from '@tanstack/react-query'
import { CircleAlert, Crosshair, Flag, MessageSquare, Radio, Terminal, UserRound } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import type { CommandLogEntry, EventLogEntry, EventLogKind, Player, PlayerRef } from '@/api/client'
import { PlayerText } from '@/components/server/player-link'
import { RelativeTime } from '@/components/relative-time'
import { Button } from '@/components/ui/button'
import { commandLogQuery } from '@/hooks/use-server-live'
import { formatTime } from '@/lib/format'
import { Chip } from '@/components/toggles'
import { cn } from '@/lib/utils'

type Category = 'web' | 'chat' | 'kills' | 'joins' | 'rounds' | 'other'
type Filter = Category | 'failures'

type Item =
  | { source: 'web'; id: string; time: string; category: Category; failed: boolean; entry: CommandLogEntry }
  | { source: 'game'; id: string; time: string; category: Category; failed: false; entry: EventLogEntry }

/**
 * The game events by their kind, from the server (the snapshot's event log) and from this page's hub events, which
 * useServerLive gives the same kinds. What fits none of the chips (spawns, vehicles, team changes, scores, control
 * points, the admin channel's state) is Other.
 */
const gameCategories: Partial<Record<EventLogKind, Category>> = {
  Kill: 'kills',
  Death: 'kills',
  Revive: 'kills',
  VehicleDestroyed: 'kills',
  Join: 'joins',
  Leave: 'joins',
  Kick: 'joins',
  Ban: 'joins',
  Bot: 'joins',
  RoundStart: 'rounds',
  RoundEnd: 'rounds',
  Map: 'rounds',
  GameState: 'rounds',
}

const gameCategory = (kind: EventLogKind): Category => gameCategories[kind] ?? 'other'

const filters: { id: Filter; label: string; hint: string }[] = [
  { id: 'web', label: 'Web actions', hint: 'Commands, console commands and actions sent from the web' },
  { id: 'chat', label: 'Chat', hint: 'Chat sent from the web' },
  { id: 'kills', label: 'Kills', hint: 'Kills and deaths' },
  { id: 'joins', label: 'Joins', hint: 'Players joining and leaving, kicks and bans' },
  { id: 'rounds', label: 'Rounds', hint: 'Maps, rounds and the game state' },
  { id: 'other', label: 'Other', hint: 'Spawns, vehicles, team changes and the rest' },
  { id: 'failures', label: 'Failures', hint: 'What the web sent and the server refused' },
]

const icons: Record<Category, { icon: typeof Terminal; className: string }> = {
  web: { icon: Terminal, className: 'text-sky-500' },
  chat: { icon: MessageSquare, className: 'text-muted-foreground' },
  kills: { icon: Crosshair, className: 'text-rose-500' },
  joins: { icon: UserRound, className: 'text-emerald-500' },
  rounds: { icon: Flag, className: 'text-violet-400' },
  other: { icon: Radio, className: 'text-muted-foreground/70' },
}

// The server logs the round restart under its old name; the UI calls it Restart round everywhere
const commandNames: Record<string, string> = { 'Restart map': 'Restart round' }

const verbs: Record<CommandLogEntry['kind'], string> = { Chat: 'said', Command: 'ran', Rcon: 'ran the console command', Action: 'ran' }

// A long log shows its newest part first; the rest is a click away
const pageSize = 100

/**
 * What web users sent to this server (from every browser) and the game's events, in one timeline, newest first, with
 * chips that narrow it to kinds of entries. The players an event names open their sheet, even after they left; in a
 * web user's command, the names of the players on the server do. WebAdmin keeps the last entries of both in memory,
 * so the timeline starts empty after WebAdmin restarts.
 */
export function ActivityLog({
  serverId,
  events,
  players,
  onPlayer,
}: {
  serverId: string
  events: EventLogEntry[]
  players: Player[]
  onPlayer: (player: PlayerRef) => void
}) {
  // Fetched and kept current by useServerLive
  const log = useQuery({ ...commandLogQuery(serverId), staleTime: Infinity })
  const [selected, setSelected] = useState<ReadonlySet<Filter>>(new Set())
  const [limit, setLimit] = useState(pageSize)
  useMinuteTick()

  const items = useMemo(() => {
    const web: Item[] = (log.data ?? []).map((entry) => ({
      source: 'web',
      id: entry.id,
      time: entry.time,
      category: entry.kind === 'Chat' ? 'chat' : 'web',
      failed: !entry.success,
      entry,
    }))
    const game: Item[] = events.map((entry) => ({ source: 'game', id: entry.id, time: entry.timestamp, category: gameCategory(entry.kind), failed: false, entry }))
    return [...web, ...game].sort((a, b) => Date.parse(b.time) - Date.parse(a.time))
  }, [log.data, events])

  const matches = (item: Item, filter: Filter) => (filter === 'failures' ? item.failed : item.category === filter)
  const shown = selected.size === 0 ? items : items.filter((item) => [...selected].some((f) => matches(item, f)))
  const counts = new Map(filters.map((f) => [f.id, items.filter((item) => matches(item, f.id)).length]))
  // A chip for a kind there is none of says nothing, unless it is on
  const chips = filters.filter((f) => counts.get(f.id) || selected.has(f.id) || (f.id !== 'other' && f.id !== 'failures'))

  const toggle = (filter: Filter) => {
    setSelected((current) => {
      const next = new Set(current)
      if (!next.delete(filter)) next.add(filter)
      return next
    })
    setLimit(pageSize)
  }

  return (
    <div className="space-y-3">
      <div role="group" aria-label="Show" className="flex flex-wrap gap-1.5 px-4">
        <FilterChip pressed={selected.size === 0} onClick={() => setSelected(new Set())} count={items.length}>
          All
        </FilterChip>
        {chips.map((f) => (
          <FilterChip key={f.id} pressed={selected.has(f.id)} onClick={() => toggle(f.id)} count={counts.get(f.id) ?? 0} title={f.hint} destructive={f.id === 'failures'}>
            {f.label}
          </FilterChip>
        ))}
      </div>

      {shown.length === 0 ? (
        <p className="border-t px-4 py-8 text-center text-sm text-muted-foreground">
          {items.length === 0
            ? 'Nothing yet. What web users send to this server and what happens in the game shows up here.'
            : 'Nothing of these kinds. WebAdmin keeps the latest entries since it started.'}
        </p>
      ) : (
        <ol className="divide-y border-t" aria-label="Activity, newest first">
          {shown.slice(0, limit).map((item) => (
            <Row key={`${item.source}:${item.id}`} item={item} players={players} onPlayer={onPlayer} />
          ))}
        </ol>
      )}
      {shown.length > limit && (
        <div className="flex justify-center pb-3">
          <Button variant="ghost" size="sm" onClick={() => setLimit((l) => l + pageSize)}>
            Show older ({shown.length - limit} more)
          </Button>
        </div>
      )}
    </div>
  )
}

/** Renders again every minute, so "2 minutes ago" does not stay at "now". */
function useMinuteTick() {
  const [, setTick] = useState(0)
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 60_000)
    return () => clearInterval(timer)
  }, [])
}

function FilterChip({
  pressed,
  onClick,
  count,
  title,
  destructive,
  children,
}: {
  pressed: boolean
  onClick: () => void
  count: number
  title?: string
  destructive?: boolean
  children: React.ReactNode
}) {
  return (
    <Chip pressed={pressed} title={title} onClick={onClick} className={cn(pressed && destructive && 'border-destructive bg-destructive text-white')}>
      {children}
      <span className={cn('tabular-nums', !pressed && 'text-muted-foreground')}>{count}</span>
    </Chip>
  )
}

/**
 * One entry: an icon for its kind, who did what (the user and the action in bold), the server's answer under it, and
 * when: the clock time and how long ago ("20:16 · 14 minutes ago"; on a phone one under the other), the full date on
 * hover. A failure has a red bar and says so.
 */
function Row({ item, players, onPlayer }: { item: Item; players: Player[]; onPlayer: (player: PlayerRef) => void }) {
  const { icon: Icon, className } = item.failed ? { icon: CircleAlert, className: 'text-destructive' } : icons[item.category]
  const link = 'font-semibold'

  return (
    <li className={cn('flex gap-3 px-4 py-2.5 text-sm', item.failed && 'shadow-[inset_3px_0_0_var(--color-destructive)] bg-destructive/5')}>
      <Icon className={cn('mt-0.5 size-4 shrink-0', className)} aria-hidden />
      <div className="min-w-0 flex-1 space-y-0.5">
        {item.source === 'web' ? (
          <>
            <p className="break-words">
              <span className="font-semibold">{item.entry.user}</span> <span className="text-muted-foreground">{verbs[item.entry.kind]}</span>{' '}
              {item.entry.kind === 'Chat' ? (
                <span>“{item.entry.command}”</span>
              ) : (
                <span className="font-semibold">
                  <PlayerText text={commandNames[item.entry.command] ?? item.entry.command} players={players} onOpen={onPlayer} linkClassName={link} />
                </span>
              )}
              {item.failed && <span className="ml-2 text-xs font-medium text-destructive uppercase">Failed</span>}
            </p>
            {item.entry.response && (
              <p className={cn('break-words whitespace-pre-wrap text-muted-foreground', item.failed && 'text-destructive')}>{item.entry.response}</p>
            )}
          </>
        ) : (
          <p className="break-words">
            <PlayerText text={item.entry.message} players={item.entry.players} onOpen={onPlayer} linkClassName={link} />
          </p>
        )}
      </div>
      <span className="flex shrink-0 flex-col items-end self-start pt-px text-xs whitespace-nowrap text-muted-foreground sm:flex-row sm:items-baseline sm:gap-1">
        <span className="font-medium text-foreground/80 tabular-nums">{formatTime(item.time)}</span>
        <span aria-hidden className="max-sm:hidden">
          ·
        </span>
        <RelativeTime value={item.time} />
      </span>
    </li>
  )
}
