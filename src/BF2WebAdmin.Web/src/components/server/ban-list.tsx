import { Trash2 } from 'lucide-react'
import { api, errorMessage } from '@/api/client'
import type { ConfirmRequest } from '@/components/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ServerActions } from '@/hooks/use-server-actions'
import { banLength } from '@/lib/bans'
import { formatTime } from '@/lib/format'

/**
 * The server's ban list: the engine's bans, with who banned and why when WebAdmin did it (newest first, as the server
 * sorts them). Unban asks first.
 */
export function BanList({ serverId, actions, confirm }: { serverId: string; actions: ServerActions; confirm: (request: ConfirmRequest) => void }) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })

  if (bans.isPending) return <Skeleton className="m-4 h-40" />
  if (bans.isError) return <p className="p-4 text-destructive">{errorMessage(bans.error)}</p>

  return (
    <div className="h-[550px] overflow-y-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Date</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Length</TableHead>
            <TableHead className="hidden md:table-cell">Type</TableHead>
            <TableHead className="hidden md:table-cell">Identifier</TableHead>
            <TableHead className="hidden sm:table-cell">Banned by</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead className="w-px" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {bans.data.length === 0 && (
            <TableRow>
              <TableCell colSpan={8} className="text-center text-muted-foreground">
                No bans
              </TableCell>
            </TableRow>
          )}
          {bans.data.map((ban) => (
            <TableRow key={ban.identifier}>
              <TableCell className="whitespace-nowrap" title={ban.bannedAt ?? undefined}>
                {ban.bannedAt ? formatTime(ban.bannedAt) : ''}
              </TableCell>
              <TableCell>{ban.name}</TableCell>
              <TableCell className="whitespace-nowrap">{banLength(ban)}</TableCell>
              <TableCell className="hidden md:table-cell">{ban.byCdKey ? 'CD key' : 'Address'}</TableCell>
              <TableCell className="hidden max-w-40 truncate font-mono text-xs md:table-cell" title={ban.identifier}>
                {ban.identifier}
              </TableCell>
              <TableCell className="hidden sm:table-cell">{ban.bannedBy}</TableCell>
              <TableCell>{ban.reason}</TableCell>
              <TableCell>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  title="Unban"
                  aria-label={`Unban ${ban.name ?? ban.identifier}`}
                  onClick={() =>
                    confirm({
                      title: `Unban ${ban.name ?? ban.identifier}?`,
                      description: `The ${ban.byCdKey ? 'CD-key hash' : 'address'} ${ban.identifier} can join again.`,
                      confirmLabel: 'Unban',
                      destructive: true,
                      onConfirm: () => void actions.unban(ban.identifier, ban.name),
                    })
                  }
                >
                  <Trash2 />
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
