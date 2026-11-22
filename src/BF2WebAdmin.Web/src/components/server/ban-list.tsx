import { api, type Ban, errorMessage } from '@/api/client'
import type { ConfirmRequest } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ServerActions } from '@/hooks/use-server-actions'
import { banLength } from '@/lib/bans'
import { formatTime } from '@/lib/format'

/** A CD-key hash is 32 characters: its start and end tell bans apart, the title and the copy button have all of it. */
const shortHash = (value: string) => (value.length > 12 ? `${value.slice(0, 6)}…${value.slice(-4)}` : value)

const missing = <span className="text-muted-foreground">—</span>

/**
 * The server's ban list: the engine's bans, with who banned and why when WebAdmin did it (newest first, as the server
 * sorts them). Unban asks first. A table from the sm width up, as tall as its rows; on a phone one card per ban, so
 * nothing (Unban least of all) is hidden to the side.
 */
export function BanList({ serverId, actions, confirm }: { serverId: string; actions: ServerActions; confirm: (request: ConfirmRequest) => void }) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })

  const unbanButton = (ban: Ban) => {
    const who = ban.name ?? ban.identifier
    return (
      <Button
        variant="outline"
        size="xs"
        aria-label={`Unban ${who}`}
        onClick={() =>
          confirm({
            title: `Unban ${who}?`,
            description: `The ${ban.byCdKey ? 'CD-key hash' : 'address'} ${ban.identifier} can join again.`,
            confirmLabel: 'Unban',
            destructive: true,
            onConfirm: () => void actions.unban(ban.identifier, ban.name),
          })
        }
      >
        Unban
      </Button>
    )
  }

  if (bans.isPending) return <Skeleton className="m-4 h-40" />
  if (bans.isError) return <p className="p-4 text-destructive">{errorMessage(bans.error)}</p>

  if (bans.data.length === 0)
    return (
      <div className="space-y-1 p-8 text-center">
        <p className="font-medium">No bans</p>
        <p className="text-sm text-muted-foreground">Players banned on this server, from WebAdmin, Discord or in game, show up here.</p>
      </div>
    )

  return (
    <div className="space-y-1 pt-3">
      <p className="px-4 text-sm text-muted-foreground">
        {bans.data.length} {bans.data.length === 1 ? 'ban' : 'bans'}, newest first
      </p>
      <ul className="divide-y sm:hidden">
        {bans.data.map((ban) => (
          <li key={ban.identifier} className="space-y-1.5 px-4 py-3">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate font-medium">{ban.name ?? <span className="text-muted-foreground">Unknown</span>}</p>
                <p className="text-sm text-muted-foreground">
                  {ban.byCdKey ? 'CD key' : 'Address'} · {banLength(ban)}
                </p>
              </div>
              {unbanButton(ban)}
            </div>
            <p className="text-sm">{ban.reason ?? <span className="text-muted-foreground">No reason given</span>}</p>
            <p className="flex flex-wrap items-center gap-x-1.5 text-xs text-muted-foreground">
              {ban.bannedBy && <span>by {ban.bannedBy}</span>}
              {ban.bannedBy && ban.bannedAt && <span aria-hidden>·</span>}
              {ban.bannedAt && <span>{formatTime(ban.bannedAt)}</span>}
              <span aria-hidden>·</span>
              <span className="inline-flex items-center gap-0.5">
                <span className="font-mono" title={ban.identifier}>
                  {ban.byCdKey ? shortHash(ban.identifier) : ban.identifier}
                </span>
                <CopyButton value={ban.identifier} label={`Copy the identifier of ${ban.name ?? ban.identifier}`} />
              </span>
            </p>
          </li>
        ))}
      </ul>
      <Table className="max-sm:hidden [&_tr>*:first-child]:pl-4 [&_tr>*:last-child]:pr-4">
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Identifier</TableHead>
            <TableHead>Type · length</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead>By</TableHead>
            <TableHead>Date</TableHead>
            <TableHead>
              <span className="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {bans.data.map((ban) => {
            const who = ban.name ?? ban.identifier
            return (
              <TableRow key={ban.identifier}>
                <TableCell className="font-medium">{ban.name ?? <span className="text-muted-foreground">Unknown</span>}</TableCell>
                <TableCell>
                  <span className="flex items-center gap-1">
                    <span className="font-mono text-xs" title={ban.identifier}>
                      {ban.byCdKey ? shortHash(ban.identifier) : ban.identifier}
                    </span>
                    <CopyButton value={ban.identifier} label={`Copy the identifier of ${who}`} />
                  </span>
                </TableCell>
                <TableCell>
                  {ban.byCdKey ? 'CD key' : 'Address'} <span className="text-muted-foreground">·</span> {banLength(ban)}
                </TableCell>
                {/* A reason can be a sentence: it wraps instead of making the table very wide */}
                <TableCell className="max-w-96 min-w-48 whitespace-normal">{ban.reason ?? missing}</TableCell>
                <TableCell>{ban.bannedBy ?? missing}</TableCell>
                <TableCell title={ban.bannedAt ?? undefined}>{ban.bannedAt ? formatTime(ban.bannedAt) : missing}</TableCell>
                <TableCell className="text-right">{unbanButton(ban)}</TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
