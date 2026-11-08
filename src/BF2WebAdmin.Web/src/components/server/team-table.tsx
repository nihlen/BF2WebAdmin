import { MoreVertical, ShieldCheck } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { Player, Team } from '@/api/client'
import type { InputRequest } from '@/components/input-dialog'
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
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'
import { playerInputs } from './player-inputs'

export type PlayerAuthLevels = ReadonlyMap<string, number>

export function TeamTable({
  team,
  players,
  positions,
  authLevels: levels,
  serverGroup,
  actions,
  onInput,
  onDetails,
}: {
  team: Team
  players: Player[]
  positions: React.RefObject<Map<number, PlayerPosition>>
  authLevels: PlayerAuthLevels
  serverGroup: string
  actions: ServerActions
  onInput: (request: InputRequest) => void
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
            <TableHead className="w-px" />
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
                      <span className={cn('truncate', !player.isAlive && 'text-muted-foreground/50')}>{player.name}</span>
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
                  <TableCell className="text-center">{positions.current.get(player.index)?.ping ?? player.score.ping}</TableCell>
                  <TableCell>
                    <PlayerMenu player={player} level={level} serverGroup={serverGroup} actions={actions} onInput={onInput} onDetails={onDetails} />
                  </TableCell>
                </TableRow>
              )
            })}
        </TableBody>
      </Table>
    </Card>
  )
}

function PlayerMenu({
  player,
  level,
  serverGroup,
  actions,
  onInput,
  onDetails,
}: {
  player: Player
  level: number | undefined
  serverGroup: string
  actions: ServerActions
  onInput: (request: InputRequest) => void
  onDetails: (player: Player) => void
}) {
  const inputs = playerInputs(actions, onInput)

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
        <DropdownMenuItem onClick={() => inputs.warn(player)}>Warn player</DropdownMenuItem>
        <DropdownMenuItem onClick={() => inputs.kick(player)}>Kick player</DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onClick={() => inputs.ban(player)}>
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
