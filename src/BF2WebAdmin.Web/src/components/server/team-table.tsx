import { ChevronDown, MoreVertical, ShieldCheck, Skull } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import type { Player, PlayerConnection, Team } from '@/api/client'
import type { ModerationKind } from '@/components/server/moderation-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import type { PlayerPosition } from '@/hooks/use-server-live'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevelName, authLevels, defaultAuthLevel } from '@/lib/auth-levels'
import { connectionReasonsText, pingClass, statusColors, statusLabels } from '@/lib/health'
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'

export type PlayerAuthLevels = ReadonlyMap<string, number>

export function TeamTable({
  team,
  players,
  positions,
  authLevels: levels,
  serverGroup,
  connections,
  actions,
  onModerate,
  onDetails,
  fill,
  className,
}: {
  team: Team
  players: Player[]
  positions: React.RefObject<Map<number, PlayerPosition>>
  authLevels: PlayerAuthLevels
  serverGroup: string
  /** The players' connections in the latest health window, by player index, when the server sends them */
  connections: ReadonlyMap<number, PlayerConnection>
  actions: ServerActions
  onModerate: (kind: ModerationKind, player: Player) => void
  onDetails: (player: Player) => void
  /**
   * From lg up the card is given a height (Live's grid) instead of taking its own: the rows are compact there, and a
   * team longer than the height scrolls in the card, with a fade and a count of the players below it.
   */
  fill?: boolean
  className?: string
}) {
  // Pings arrive with the positions, which do not re-render: read them once a second
  const [, setTick] = useState(0)
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 1000)
    return () => clearInterval(timer)
  }, [])
  const { ref: scroller, below, toEnd } = useRowsBelow(players.length)

  const table = (
    <Table>
      <TableHeader>
        <TableRow>
          {/* The team heads its players' names; the slot number is in the player sheet */}
          <TableHead className={cn('pl-3 font-semibold', teamTextColor(team.id))}>
            {team.name}
            <span className="ml-1.5 font-normal text-muted-foreground tabular-nums">{players.length}</span>
          </TableHead>
          <ColumnHead label="Score" short="S" />
          <ColumnHead label="Kills" short="K" className="hidden sm:table-cell" />
          <ColumnHead label="Deaths" short="D" className="hidden sm:table-cell" />
          <ColumnHead label="Ping" short="P" />
          <TableHead className="w-px">
            <span className="sr-only">Actions</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {players.length === 0 && (
          <TableRow>
            <TableCell colSpan={6} className="text-center text-muted-foreground">
              No players on this team.
            </TableCell>
          </TableRow>
        )}
        {[...players]
          .sort((a, b) => b.score.total - a.score.total)
          .map((player) => {
            const level = player.hash ? levels.get(player.hash) : undefined
            return (
              <TableRow key={player.index} className="group/row">
                <TableCell className="w-full max-w-0 pl-3">
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      className="flex min-w-0 items-center gap-2 text-left hover:underline"
                      onClick={() => onDetails(player)}
                      title="Player details"
                    >
                      {player.country && <span className={`fi fi-${player.country.toLowerCase()} shrink-0`} title={player.country} />}
                      <span className="truncate">{player.name}</span>
                      {/* Dead is a moment of the round, not a reason to grey out the name the admin is looking for */}
                      {!player.isAlive && (
                        <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground" title="Dead, waiting to respawn">
                          <Skull className="size-3.5" />
                          dead
                        </span>
                      )}
                      {level !== undefined && level > defaultAuthLevel && (
                        <Badge variant="outline" className="shrink-0" title={`Auth level ${level}`}>
                          <ShieldCheck data-icon="inline-start" />
                          {authLevelName(level)}
                        </Badge>
                      )}
                    </button>
                    <QuickActions player={player} onModerate={onModerate} />
                  </div>
                </TableCell>
                <TableCell className="text-right tabular-nums">{player.score.total}</TableCell>
                <TableCell className="hidden text-right tabular-nums sm:table-cell">{player.score.kills}</TableCell>
                <TableCell className="hidden text-right tabular-nums sm:table-cell">{player.score.deaths}</TableCell>
                <TableCell className="text-right">
                  <Ping ping={positions.current.get(player.index)?.ping ?? player.score.ping} connection={connections.get(player.index)} />
                </TableCell>
                <TableCell>
                  <PlayerMenu player={player} level={level} serverGroup={serverGroup} actions={actions} onModerate={onModerate} onDetails={onDetails} />
                </TableCell>
              </TableRow>
            )
          })}
      </TableBody>
    </Table>
  )

  if (!fill) return <Card className={cn('py-0', className)}>{table}</Card>
  return (
    <Card className={cn('relative gap-0 py-0 lg:min-h-0 lg:[&_td]:py-1 lg:[&_th]:h-9', className)}>
      <div ref={scroller} className="lg:min-h-0 lg:flex-1 lg:overflow-y-auto">
        {table}
      </div>
      {below > 0 && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 flex h-14 items-end justify-center bg-linear-to-t from-card from-40% to-transparent pb-2">
          <button
            type="button"
            onClick={toEnd}
            className="pointer-events-auto inline-flex items-center gap-1 rounded-full border bg-card px-2.5 py-0.5 text-xs font-medium shadow-sm hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            +{below} more
            <ChevronDown aria-hidden className="size-3.5" />
          </button>
        </div>
      )}
    </Card>
  )
}

/**
 * How many rows of a scrolling table are below its visible part, whole or in part, kept current as it scrolls, resizes
 * or gets rows; and a way to scroll to its end.
 */
function useRowsBelow(rows: number) {
  const ref = useRef<HTMLDivElement>(null)
  const [below, setBelow] = useState(0)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const count = () => {
      const bottom = el.getBoundingClientRect().bottom
      setBelow([...el.querySelectorAll('tbody tr')].filter((row) => row.getBoundingClientRect().bottom > bottom + 1).length)
    }
    count()
    const observer = new ResizeObserver(count)
    observer.observe(el)
    el.addEventListener('scroll', count, { passive: true })
    return () => {
      observer.disconnect()
      el.removeEventListener('scroll', count)
    }
  }, [rows])

  const toEnd = () => ref.current?.scrollTo({ top: ref.current.scrollHeight, behavior: 'smooth' })
  return { ref, below, toEnd }
}

/** A column's name in full, or its first letter on a phone where four words do not fit; the title and screen readers have it whole. */
function ColumnHead({ label, short, className }: { label: string; short: string; className?: string }) {
  return (
    <TableHead className={cn('text-right', className)} title={label}>
      <span className="sm:hidden" aria-hidden>
        {short}
      </span>
      <span className="max-sm:sr-only">{label}</span>
    </TableHead>
  )
}

/**
 * The ping, coloured when high. When the latest health window rated the connection Warn or Bad (loss, an unsteady
 * ping), a dot before it opens a tooltip with the reasons, which a touch screen can show too.
 */
function Ping({ ping, connection }: { ping: number; connection: PlayerConnection | undefined }) {
  const value = <span className={cn('tabular-nums', pingClass(ping))}>{ping}</span>
  if (!connection || connection.status === 'Good') return value

  return (
    <span className="inline-flex items-center gap-1">
      <Tooltip>
        <TooltipTrigger
          render={<button type="button" />}
          aria-label={`Connection ${statusLabels[connection.status]}: ${connectionReasonsText(connection)}`}
          className="flex size-4 items-center justify-center rounded-full focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
        >
          <span className={cn('size-1.5 rounded-full', statusColors[connection.status])} />
        </TooltipTrigger>
        <TooltipContent side="top">
          Connection {statusLabels[connection.status].toLowerCase()}: {connectionReasonsText(connection)}. Ping {connection.pingMin}–{connection.pingMax}{' '}
          ms, loss to / from {connection.lossToPlayerPercent.toFixed(1)} / {connection.lossFromPlayerPercent.toFixed(1)} % in the last 10 s.
        </TooltipContent>
      </Tooltip>
      {value}
    </span>
  )
}

/**
 * Warn, Kick and Ban on a scoreboard row, from md up while the pointer is over the row or the keyboard is in it: the
 * moderation dialog opens with the player and the action chosen. A phone has them first in the row's menu.
 */
function QuickActions({ player, onModerate }: { player: Player; onModerate: (kind: ModerationKind, player: Player) => void }) {
  return (
    <div className="ml-auto hidden shrink-0 gap-1 md:group-focus-within/row:flex md:group-hover/row:flex">
      <Button variant="outline" size="xs" aria-label={`Warn ${player.name}`} onClick={() => onModerate('warn', player)}>
        Warn
      </Button>
      <Button variant="outline" size="xs" aria-label={`Kick ${player.name}`} onClick={() => onModerate('kick', player)}>
        Kick
      </Button>
      <Button variant="destructive" size="xs" aria-label={`Ban ${player.name}`} onClick={() => onModerate('ban', player)}>
        Ban
      </Button>
    </div>
  )
}

function PlayerMenu({
  player,
  level,
  serverGroup,
  actions,
  onModerate,
  onDetails,
}: {
  player: Player
  level: number | undefined
  serverGroup: string
  actions: ServerActions
  onModerate: (kind: ModerationKind, player: Player) => void
  onDetails: (player: Player) => void
}) {

  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`Actions for ${player.name}`} />}>
        <MoreVertical />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => onDetails(player)}>Details</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => onModerate('warn', player)}>Warn player</DropdownMenuItem>
        <DropdownMenuItem onClick={() => onModerate('kick', player)}>Kick player</DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onClick={() => onModerate('ban', player)}>
          Ban player
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => void actions.switchTeam(player)}>Switch team</DropdownMenuItem>
        <DropdownMenuItem onClick={() => void actions.killPlayer(player)}>Kill player</DropdownMenuItem>
        <DropdownMenuSeparator />
        <AuthLevelMenu player={player} level={level} serverGroup={serverGroup} actions={actions} />
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/** The player's auth level in the server group, as a submenu. Needs the player's hash (or a test server's name key). */
export function AuthLevelMenu({
  player,
  level,
  serverGroup,
  actions,
}: {
  player: Player
  level: number | undefined
  serverGroup: string
  actions: ServerActions
}) {
  if (!player.hash) {
    return (
      <DropdownMenuItem disabled title="The server sent no CD-key hash for this player">
        Auth level (no hash)
      </DropdownMenuItem>
    )
  }

  const current = level ?? defaultAuthLevel
  return (
    <DropdownMenuSub>
      <DropdownMenuSubTrigger>Auth level: {authLevelName(current)}</DropdownMenuSubTrigger>
      <DropdownMenuSubContent>
        <DropdownMenuGroup>
          <DropdownMenuLabel>In group {serverGroup}</DropdownMenuLabel>
          <DropdownMenuRadioGroup
            value={String(current)}
            onValueChange={(value) => void actions.setAuthLevel(serverGroup, player.hash, Number(value), player.name)}
          >
            {authLevels.map((l) => (
              <DropdownMenuRadioItem key={l.value} value={String(l.value)} title={l.description}>
                {l.name}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuSubContent>
    </DropdownMenuSub>
  )
}
