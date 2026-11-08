import { Trash2 } from 'lucide-react'
import { api, errorMessage, type Ban } from '@/api/client'
import type { ConfirmRequest } from '@/components/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ServerActions } from '@/hooks/use-server-actions'
import { formatTime } from '@/lib/format'

const banTime = (ban: Ban) => {
  const time = ban.datetime ? new Date(ban.datetime).getTime() : NaN
  return Number.isNaN(time) ? 0 : time
}

/** The server's ban list (iga banlist), newest first. Unban asks first. */
export function BanList({ serverId, actions, confirm }: { serverId: string; actions: ServerActions; confirm: (request: ConfirmRequest) => void }) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })

  if (bans.isPending) return <Skeleton className="m-4 h-40" />
  if (bans.isError) return <p className="p-4 text-destructive">{errorMessage(bans.error)}</p>

  const sorted = [...bans.data].sort((a, b) => banTime(b) - banTime(a))

  return (
    <div className="h-[550px] overflow-y-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Date</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Period</TableHead>
            <TableHead className="hidden md:table-cell">Method</TableHead>
            <TableHead className="hidden md:table-cell">Identifier</TableHead>
            <TableHead className="hidden sm:table-cell">By</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead className="w-px" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {sorted.length === 0 && (
            <TableRow>
              <TableCell colSpan={8} className="text-center text-muted-foreground">
                No bans
              </TableCell>
            </TableRow>
          )}
          {sorted.map((ban, index) => {
            const identifier = ban.method === 'Key' ? ban.cdkeyhash : ban.address
            return (
              <TableRow key={`${identifier}-${index}`}>
                <TableCell className="whitespace-nowrap" title={ban.datetime ?? undefined}>
                  {ban.datetime ? formatTime(ban.datetime) : ''}
                </TableCell>
                <TableCell>{ban.nick}</TableCell>
                <TableCell>{ban.period}</TableCell>
                <TableCell className="hidden md:table-cell">{ban.method}</TableCell>
                <TableCell className="hidden max-w-40 truncate font-mono text-xs md:table-cell" title={identifier ?? undefined}>
                  {identifier}
                </TableCell>
                <TableCell className="hidden sm:table-cell">{ban.by}</TableCell>
                <TableCell>{ban.reason}</TableCell>
                <TableCell>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    title="Unban"
                    aria-label={`Unban ${ban.nick ?? identifier}`}
                    disabled={!identifier}
                    onClick={() =>
                      identifier &&
                      confirm({
                        title: `Unban ${ban.nick ?? identifier}?`,
                        description: `The ${ban.method === 'Key' ? 'CD-key hash' : 'address'} ${identifier} can join again.`,
                        confirmLabel: 'Unban',
                        destructive: true,
                        onConfirm: () => void actions.unban(identifier, ban.nick),
                      })
                    }
                  >
                    <Trash2 />
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
