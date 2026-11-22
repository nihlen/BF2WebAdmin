import { Copy } from 'lucide-react'
import { toast } from 'sonner'
import { api, type Player, type Team } from '@/api/client'
import type { ModerationKind } from '@/components/server/moderation-dialog'
import { LevelSelect } from '@/components/auth-level-select'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import type { ServerActions } from '@/hooks/use-server-actions'
import { defaultAuthLevel } from '@/lib/auth-levels'
import { banLength } from '@/lib/bans'
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'
import { formatTime } from '@/lib/format'
import { pingClass } from '@/lib/health'
import type { PlayerAuthLevels } from './team-table'

/**
 * A player's details and actions. Follows the player through the live snapshot by index; when the player leaves, the
 * last known details stay with a note.
 */
export function PlayerSheet({
  serverId,
  player,
  current,
  teams,
  ping,
  authLevels: levels,
  serverGroup,
  actions,
  onModerate,
  onClose,
}: {
  serverId: string
  /** The player as it was when the sheet opened */
  player: Player | null
  /** The same player in the current snapshot, if still on the server */
  current: Player | undefined
  teams: Team[]
  ping: number | undefined
  authLevels: PlayerAuthLevels
  serverGroup: string
  actions: ServerActions
  onModerate: (kind: ModerationKind, player: Player) => void
  onClose: () => void
}) {
  return (
    <Sheet open={!!player} onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="overflow-y-auto">
        {player && (
          <PlayerDetails
            serverId={serverId}
            player={current ?? player}
            left={!current}
            teams={teams}
            ping={ping}
            level={player.hash ? levels.get(player.hash) : undefined}
            serverGroup={serverGroup}
            actions={actions}
            onModerate={onModerate}
          />
        )}
      </SheetContent>
    </Sheet>
  )
}

function PlayerDetails({
  serverId,
  player,
  left,
  teams,
  ping,
  level,
  serverGroup,
  actions,
  onModerate,
}: {
  serverId: string
  player: Player
  left: boolean
  teams: Team[]
  ping: number | undefined
  level: number | undefined
  serverGroup: string
  actions: ServerActions
  onModerate: (kind: ModerationKind, player: Player) => void
}) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })
  const playerBans = (bans.data ?? []).filter(
    (b) => (!!player.hash && b.identifier === player.hash) || (!!player.ipAddress && b.identifier === player.ipAddress) || b.name === player.name,
  )
  const team = teams.find((t) => t.id === player.team)

  return (
    <>
      <SheetHeader>
        <SheetTitle className="flex items-center gap-2">
          {player.country && <span className={`fi fi-${player.country.toLowerCase()}`} title={player.country} />}
          {player.name}
        </SheetTitle>
        <SheetDescription>{left ? 'Has left the server' : `Player ${player.index}${player.isAlive ? '' : ', dead'}`}</SheetDescription>
      </SheetHeader>

      <div className="space-y-6 px-4 pb-6">
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-2">
          <dt className="text-muted-foreground">Team</dt>
          <dd className={teamTextColor(player.team)}>{team?.name ?? player.team}</dd>
          <dt className="text-muted-foreground">Score</dt>
          <dd>
            {player.score.total} ({player.score.kills} kills, {player.score.deaths} deaths)
          </dd>
          <dt className="text-muted-foreground">Ping</dt>
          <dd className={pingClass(ping ?? player.score.ping)}>{ping ?? player.score.ping} ms</dd>
          <dt className="text-muted-foreground">Country</dt>
          <dd>{player.country || '-'}</dd>
          <dt className="text-muted-foreground">IP address</dt>
          <dd>
            <Copyable value={player.ipAddress} />
          </dd>
          <dt className="text-muted-foreground">Hash</dt>
          <dd>{player.hash ? <Copyable value={player.hash} /> : <span className="text-muted-foreground">none sent</span>}</dd>
        </dl>

        <section className="space-y-2">
          <h3 className="font-medium">Auth level in {serverGroup}</h3>
          <LevelSelect
            label="Auth level"
            disabled={!player.hash}
            value={level ?? defaultAuthLevel}
            onChange={(next) => void actions.setAuthLevel(serverGroup, player.hash, next, player.name)}
          />
        </section>

        {!left && (
          <section className="space-y-2">
            <h3 className="font-medium">Actions</h3>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => void actions.switchTeam(player)}>
                Switch team
              </Button>
              <Button variant="outline" size="sm" onClick={() => void actions.killPlayer(player)}>
                Kill
              </Button>
              <Button variant="outline" size="sm" onClick={() => onModerate('warn', player)}>
                Warn
              </Button>
              <Button variant="outline" size="sm" onClick={() => onModerate('kick', player)}>
                Kick
              </Button>
              <Button variant="destructive" size="sm" onClick={() => onModerate('ban', player)}>
                Ban
              </Button>
            </div>
          </section>
        )}

        <section className="space-y-2">
          <h3 className="font-medium">Bans on this server</h3>
          {bans.isPending && <p className="text-muted-foreground">Loading…</p>}
          {bans.data && playerBans.length === 0 && <p className="text-muted-foreground">None for this name, hash or address</p>}
          <ul className="space-y-2">
            {playerBans.map((ban) => (
              <li key={ban.identifier} className={cn('rounded-md border p-2', 'text-sm')}>
                <div>
                  {ban.bannedAt ? formatTime(ban.bannedAt) : '?'} · {banLength(ban)}
                  {ban.bannedBy && ` · by ${ban.bannedBy}`}
                </div>
                <div className="text-muted-foreground">
                  {ban.name ?? ban.identifier}: {ban.reason}
                </div>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </>
  )
}

function Copyable({ value }: { value: string }) {
  return (
    <span className="flex min-w-0 items-center gap-1">
      <span className="truncate font-mono text-xs" title={value}>
        {value}
      </span>
      <Button
        variant="ghost"
        size="icon-xs"
        aria-label="Copy"
        title="Copy"
        onClick={() => void navigator.clipboard.writeText(value).then(() => toast.success('Copied'))}
      >
        <Copy />
      </Button>
    </span>
  )
}
