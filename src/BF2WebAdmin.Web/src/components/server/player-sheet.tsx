import { Copy } from 'lucide-react'
import { toast } from 'sonner'
import { api, type Player, type PlayerRef, type Team } from '@/api/client'
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

const isPlayer = (player: PlayerRef): player is Player => 'score' in player

/**
 * A player's details and actions. Follows the player through the live snapshot (by hash, else by index); when the
 * player leaves, the last known details stay with a note. A player a chat line or an event names who is not on the
 * server any more has their name, hash, auth level and bans.
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
  /** The player as it was when the sheet opened, or as a chat line or an event names them */
  player: PlayerRef | null
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
            details={current ?? (isPlayer(player) ? player : undefined)}
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
  details,
  left,
  teams,
  ping,
  level,
  serverGroup,
  actions,
  onModerate,
}: {
  serverId: string
  player: PlayerRef
  /** What the snapshot had of the player, unless they left before this page opened */
  details: Player | undefined
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
    (b) => (!!player.hash && b.identifier === player.hash) || (!!details?.ipAddress && b.identifier === details.ipAddress) || b.name === player.name,
  )
  const team = details && teams.find((t) => t.id === details.team)

  return (
    <>
      <SheetHeader>
        <SheetTitle className="flex items-center gap-2">
          {details?.country && <span className={`fi fi-${details.country.toLowerCase()}`} title={details.country} />}
          {player.name}
        </SheetTitle>
        <SheetDescription>
          {left ? (details ? 'Has left the server' : 'Not on the server now') : `Player ${player.index}${details?.isAlive === false ? ', dead' : ''}`}
        </SheetDescription>
      </SheetHeader>

      <div className="space-y-6 px-4 pb-6">
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-2">
          {details && (
            <>
              <dt className="text-muted-foreground">Team</dt>
              <dd className={teamTextColor(details.team)}>{team?.name ?? details.team}</dd>
              <dt className="text-muted-foreground">Score</dt>
              <dd>
                {details.score.total} ({details.score.kills} kills, {details.score.deaths} deaths)
              </dd>
              <dt className="text-muted-foreground">Ping</dt>
              <dd className={pingClass(ping ?? details.score.ping)}>{ping ?? details.score.ping} ms</dd>
              <dt className="text-muted-foreground">Country</dt>
              <dd>{details.country || '-'}</dd>
              <dt className="text-muted-foreground">IP address</dt>
              <dd>
                <Copyable value={details.ipAddress} />
              </dd>
            </>
          )}
          <dt className="text-muted-foreground">Hash</dt>
          <dd>{player.hash ? <Copyable value={player.hash} /> : <span className="text-muted-foreground">none sent</span>}</dd>
        </dl>

        <section className="space-y-2">
          <h3 className="font-medium">Auth level in {serverGroup}</h3>
          <LevelSelect
            label="Auth level"
            disabled={!player.hash}
            value={level ?? defaultAuthLevel}
            onChange={(next) => player.hash && void actions.setAuthLevel(serverGroup, player.hash, next, player.name)}
          />
        </section>

        {!left && details && (
          <section className="space-y-2">
            <h3 className="font-medium">Actions</h3>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => void actions.switchTeam(details)}>
                Switch team
              </Button>
              <Button variant="outline" size="sm" onClick={() => void actions.killPlayer(details)}>
                Kill
              </Button>
              <Button variant="outline" size="sm" onClick={() => onModerate('warn', details)}>
                Warn
              </Button>
              <Button variant="outline" size="sm" onClick={() => onModerate('kick', details)}>
                Kick
              </Button>
              <Button variant="destructive" size="sm" onClick={() => onModerate('ban', details)}>
                Ban
              </Button>
            </div>
          </section>
        )}

        <section className="space-y-2">
          <h3 className="font-medium">Bans on this server</h3>
          {bans.isPending && <p className="text-muted-foreground">Loading…</p>}
          {bans.data && playerBans.length === 0 && <p className="text-muted-foreground">None for this name, hash or address.</p>}
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
