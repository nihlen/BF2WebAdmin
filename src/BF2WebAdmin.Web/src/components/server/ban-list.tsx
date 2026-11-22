import { api, errorMessage } from '@/api/client'
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
 * sorts them). Unban asks first. The table is as tall as its rows and scrolls sideways when it is wider than its place.
 */
export function BanList({ serverId, actions, confirm }: { serverId: string; actions: ServerActions; confirm: (request: ConfirmRequest) => void }) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })

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
      <Table className="[&_tr>*:first-child]:pl-4 [&_tr>*:last-child]:pr-4">
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
                <TableCell className="text-right">
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
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
