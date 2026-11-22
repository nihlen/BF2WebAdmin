import { Infinity as Forever, Plus, Search, X } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { api, type Ban, errorMessage } from '@/api/client'
import type { ConfirmRequest } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { AddBanDialog } from '@/components/server/add-ban-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ServerActions } from '@/hooks/use-server-actions'
import { banLength } from '@/lib/bans'
import { formatDayTime, formatFull, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

/** A CD-key hash is 32 characters: its start and end tell bans apart, the title and the copy button have all of it. */
const shortHash = (value: string) => (value.length > 12 ? `${value.slice(0, 6)}…${value.slice(-4)}` : value)

const missing = <span className="text-muted-foreground">—</span>

const banType = (ban: Ban) => (ban.byCdKey ? 'CD key' : 'Address')

/** Whether a ban matches the search: its name, hash or address, reason or banning admin, ignoring case. */
const matches = (ban: Ban, query: string) =>
  [ban.name, ban.identifier, ban.reason, ban.bannedBy].some((field) => field?.toLowerCase().includes(query))

/**
 * How long the ban lasts as a badge: a permanent ban stands out in red with an infinity sign, a timed one says when it
 * ends relative to now and, beside it, the date and time it ends; a round's ban is plain.
 */
function LengthBadge({ ban }: { ban: Ban }) {
  if (ban.kind === 'Permanent')
    return (
      <Badge variant="destructive">
        <Forever data-icon="inline-start" />
        Permanent
      </Badge>
    )
  return (
    <span className="inline-flex flex-wrap items-center gap-x-1.5 gap-y-0.5">
      <Badge variant={ban.kind === 'Timed' ? 'secondary' : 'outline'}>{banLength(ban)}</Badge>
      {ban.expiresAt && (
        <time dateTime={ban.expiresAt} title={formatFull(ban.expiresAt)} className="text-xs whitespace-nowrap text-muted-foreground">
          {formatDayTime(ban.expiresAt)}
        </time>
      )}
    </span>
  )
}

/** When the ban was made: relative to now, then the date and time ("2 days ago · 19 Nov 20:31"). */
function BannedAt({ value }: { value: string }) {
  return (
    <time dateTime={value} title={formatFull(value)} className="whitespace-nowrap">
      {formatRelative(value)}
      <span className="text-muted-foreground"> · {formatDayTime(value)}</span>
    </time>
  )
}

/**
 * The server's ban list: the engine's bans, with who banned and why when WebAdmin did it (newest first, as the server
 * sorts them). Unban asks first; Add ban bans a player who need not be online, while the server is connected. A table
 * from the sm width up, as tall as its rows; on a phone one card per ban, so nothing (Unban least of all) is hidden to
 * the side.
 */
export function BanList({
  serverId,
  serverGroup,
  connected,
  actions,
  confirm,
}: {
  serverId: string
  serverGroup: string
  connected: boolean
  actions: ServerActions
  confirm: (request: ConfirmRequest) => void
}) {
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })
  const [search, setSearch] = useState('')
  const [adding, setAdding] = useState(false)
  const query = useDeferredValue(search.trim().toLowerCase())

  const addButton = (
    <Button variant="outline" size="sm" disabled={!connected} title={connected ? undefined : 'The server must be connected to ban'} onClick={() => setAdding(true)}>
      <Plus data-icon="inline-start" />
      Add ban
    </Button>
  )
  const addDialog = <AddBanDialog open={adding} onOpenChange={setAdding} serverGroup={serverGroup} actions={actions} />

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
      <div className="space-y-3 p-8 text-center">
        <div className="space-y-1">
          <p className="font-medium">No bans</p>
          <p className="text-sm text-muted-foreground">Players banned on this server, from WebAdmin, Discord or in game, show up here.</p>
        </div>
        {addButton}
        {addDialog}
      </div>
    )

  const all = bans.data
  const shown = query ? all.filter((ban) => matches(ban, query)) : all
  const permanent = all.filter((ban) => ban.kind === 'Permanent').length

  return (
    <div className="space-y-3 pt-4">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4">
        <div className="relative w-full sm:w-80">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search name, hash, IP or reason"
            aria-label="Search the bans"
            className="pr-8 pl-8 [&::-webkit-search-cancel-button]:hidden"
          />
          {search && (
            <Button variant="ghost" size="icon-xs" className="absolute top-1/2 right-1 -translate-y-1/2" aria-label="Clear the search" onClick={() => setSearch('')}>
              <X />
            </Button>
          )}
        </div>
        <p className="text-sm text-muted-foreground" aria-live="polite">
          {query ? `${shown.length} of ${all.length}` : all.length} {all.length === 1 ? 'ban' : 'bans'}
          {!query && permanent > 0 && `, ${permanent} permanent`}, newest first
        </p>
        <div className="ml-auto">{addButton}</div>
      </div>
      {addDialog}
      {shown.length === 0 && (
        <p className="border-t px-4 py-8 text-center text-sm text-muted-foreground">
          No ban matches “{search.trim()}”. The search looks in names, CD-key hashes, addresses, reasons and who banned.
        </p>
      )}
      <ul className="divide-y border-t sm:hidden">
        {shown.map((ban) => (
          <li key={ban.identifier} className={cn('space-y-1.5 px-4 py-3', ban.kind === 'Permanent' && 'border-l-2 border-l-destructive/60')}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0 space-y-1">
                <p className="truncate font-medium">{ban.name ?? <span className="text-muted-foreground">Unknown</span>}</p>
                <LengthBadge ban={ban} />
              </div>
              {unbanButton(ban)}
            </div>
            <p className="text-sm">{ban.reason ?? <span className="text-muted-foreground">No reason given</span>}</p>
            <p className="flex flex-wrap items-center gap-x-1.5 text-xs text-muted-foreground">
              {ban.bannedBy && <span>by {ban.bannedBy}</span>}
              {ban.bannedBy && ban.bannedAt && <span aria-hidden>·</span>}
              {ban.bannedAt && <BannedAt value={ban.bannedAt} />}
              {(ban.bannedBy || ban.bannedAt) && <span aria-hidden>·</span>}
              <span>{banType(ban)}</span>
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
      <Table className={cn('max-sm:hidden [&_tr>*:first-child]:pl-4 [&_tr>*:last-child]:pr-4', shown.length === 0 && 'hidden')}>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Length</TableHead>
            <TableHead>CD key or address</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead>By</TableHead>
            <TableHead>When</TableHead>
            <TableHead>
              <span className="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {shown.map((ban) => {
            const who = ban.name ?? ban.identifier
            return (
              <TableRow key={ban.identifier} className={cn(ban.kind === 'Permanent' && '[&>td:first-child]:shadow-[inset_2px_0_0_var(--color-destructive)]')}>
                <TableCell className="font-medium">{ban.name ?? <span className="text-muted-foreground">Unknown</span>}</TableCell>
                <TableCell>
                  <LengthBadge ban={ban} />
                </TableCell>
                <TableCell>
                  <span className="flex items-center gap-1">
                    <span className="w-14 shrink-0 text-xs text-muted-foreground">{banType(ban)}</span>
                    <span className="font-mono text-xs" title={ban.identifier}>
                      {ban.byCdKey ? shortHash(ban.identifier) : ban.identifier}
                    </span>
                    <CopyButton value={ban.identifier} label={`Copy the identifier of ${who}`} />
                  </span>
                </TableCell>
                {/* A reason can be a sentence: it wraps instead of making the table very wide */}
                <TableCell className="max-w-96 min-w-48 whitespace-normal">{ban.reason ?? missing}</TableCell>
                <TableCell>{ban.bannedBy ?? missing}</TableCell>
                <TableCell>{ban.bannedAt ? <BannedAt value={ban.bannedAt} /> : missing}</TableCell>
                <TableCell className="text-right">{unbanButton(ban)}</TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
