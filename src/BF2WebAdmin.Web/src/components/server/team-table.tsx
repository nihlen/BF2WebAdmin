import { MoreVertical, ShieldCheck, Skull } from 'lucide-react'
import { useEffect, useState } from 'react'
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
import type { PlayerPosition } from '@/hooks/use-server-live'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevelName, authLevels, defaultAuthLevel } from '@/lib/auth-levels'
import { connectionReasonsText, pingClass, statusColors } from '@/lib/health'
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
}) {
  // Pings arrive with the positions, which do not re-render: read them once a second
  const [, setTick] = useState(0)
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 1000)
    return () => clearInterval(timer)
  }, [])

  return (
    <Card className="py-0">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className={cn('w-10', teamTextColor(team.id))}>{team.name}</TableHead>
            <TableHead>Name</TableHead>
            <TableHead className="text-center" title="Score">
              S
            </TableHead>
            <TableHead className="hidden text-center sm:table-cell" title="Kills">
              K
            </TableHead>
            <TableHead className="hidden text-center sm:table-cell" title="Deaths">
              D
            </TableHead>
            <TableHead className="text-center" title="Ping">
              P
            </TableHead>
            <TableHead className="w-px">
              <span className="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {players.length === 0 && (
            <TableRow>
              <TableCell colSpan={7} className="text-center text-muted-foreground">
                No players
              </TableCell>
            </TableRow>
          )}
          {[...players]
            .sort((a, b) => b.score.total - a.score.total)
            .map((player) => {
              const level = player.hash ? levels.get(player.hash) : undefined
              return (
                <TableRow key={player.index}>
                  <TableCell>{player.index}</TableCell>
                  <TableCell className="max-w-0 w-full">
                    <button
                      type="button"
                      className="flex max-w-full items-center gap-2 text-left hover:underline"
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
                  </TableCell>
                  <TableCell className="text-center">{player.score.total}</TableCell>
                  <TableCell className="hidden text-center sm:table-cell">{player.score.kills}</TableCell>
                  <TableCell className="hidden text-center sm:table-cell">{player.score.deaths}</TableCell>
                  <TableCell className="text-center">
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
    </Card>
  )
}

/**
 * The ping, coloured when high, and a dot when the latest health window rated the connection Warn or Bad (loss, an
 * unsteady ping), with the reasons in its title.
 */
function Ping({ ping, connection }: { ping: number; connection: PlayerConnection | undefined }) {
  const troubled = connection && connection.status !== 'Good'
  const title = [`Ping ${ping} ms`, troubled && `Connection ${connection.status}: ${connectionReasonsText(connection)}`].filter(Boolean).join('. ')
  return (
    <span className={cn('inline-flex items-center gap-1 tabular-nums', pingClass(ping))} title={title}>
      {troubled && <span className={cn('size-1.5 shrink-0 rounded-full', statusColors[connection.status])} aria-hidden />}
      {ping}
      {troubled && <span className="sr-only">, connection {connection.status}</span>}
    </span>
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
        <DropdownMenuItem onClick={() => void actions.switchTeam(player)}>Switch team</DropdownMenuItem>
        <DropdownMenuItem onClick={() => void actions.killPlayer(player)}>Kill player</DropdownMenuItem>
        <DropdownMenuItem onClick={() => onModerate('warn', player)}>Warn player</DropdownMenuItem>
        <DropdownMenuItem onClick={() => onModerate('kick', player)}>Kick player</DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onClick={() => onModerate('ban', player)}>
          Ban player
        </DropdownMenuItem>
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
