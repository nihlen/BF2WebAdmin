import { Copy } from 'lucide-react'
import { toast } from 'sonner'
import { api, type Player, type Team } from '@/api/client'
import type { InputRequest } from '@/components/input-dialog'
import { NativeSelect } from '@/components/native-select'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevels, defaultAuthLevel } from '@/lib/auth-levels'
import { banLength } from '@/lib/bans'
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'
import { formatTime } from '@/lib/format'
import { playerInputs } from './player-inputs'
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
  onInput,
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
  onInput: (request: InputRequest) => void
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
            onInput={onInput}
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
  onInput,
}: {
  serverId: string
  player: Player
  left: boolean
  teams: Team[]
  ping: number | undefined
  level: number | undefined
  serverGroup: string
  actions: ServerActions
  onInput: (request: InputRequest) => void
}) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })
  const playerBans = (bans.data ?? []).filter(
    (b) => (!!player.hash && b.identifier === player.hash) || (!!player.ipAddress && b.identifier === player.ipAddress) || b.name === player.name,
  )
  const inputs = playerInputs(actions, onInput)
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
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
          <dt className="text-muted-foreground">Team</dt>
          <dd className={teamTextColor(player.team)}>{team?.name ?? player.team}</dd>
          <dt className="text-muted-foreground">Score</dt>
          <dd>
            {player.score.total} ({player.score.kills} kills, {player.score.deaths} deaths)
          </dd>
          <dt className="text-muted-foreground">Ping</dt>
          <dd>{ping ?? player.score.ping} ms</dd>
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
          <NativeSelect
            aria-label="Auth level"
            disabled={!player.hash}
            value={String(level ?? defaultAuthLevel)}
            onChange={(e) => void actions.setAuthLevel(serverGroup, player.hash, Number(e.target.value), player.name)}
          >
            {authLevels.map((l) => (
              <option key={l.value} value={l.value}>
                {l.name} ({l.value}): {l.description}
              </option>
            ))}
          </NativeSelect>
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
              <Button variant="outline" size="sm" onClick={() => inputs.warn(player)}>
                Warn
              </Button>
              <Button variant="outline" size="sm" onClick={() => inputs.kick(player)}>
                Kick
              </Button>
              <Button variant="destructive" size="sm" onClick={() => inputs.ban(player)}>
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
    <span className="flex items-center gap-1">
      <span className="truncate font-mono text-xs">{value}</span>
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
