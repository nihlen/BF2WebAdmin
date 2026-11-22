import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type PlayerAuth } from '@/api/client'
import { AuthLevelLegend } from '@/components/auth-level-legend'
import { LevelSelect } from '@/components/auth-level-select'
import { useConfirm } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { authLevelName, defaultAuthLevel } from '@/lib/auth-levels'

/**
 * The players with a level in this group, under a legend of what each level may do. A level back to All removes the
 * player from the list. The add server flow uses it for its admins step.
 */
export function PlayerAuthList({ serverGroup }: { serverGroup: string }) {
  const queryClient = useQueryClient()
  const auth = useQuery(playerAuthQuery(serverGroup))
  const { confirm, dialog } = useConfirm()
  const [filter, setFilter] = useState('')
  const words = filter.toLowerCase().split(/\s+/).filter(Boolean)
  const shown = (auth.data ?? []).filter((entry) => words.every((w) => (entry.name ?? '').toLowerCase().includes(w) || entry.playerHash.toLowerCase().includes(w)))
  const set = api.useMutation('put', '/api/server-groups/{serverGroup}/auth', {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playerAuthQuery(serverGroup).queryKey }),
  })

  const setLevel = (entry: Pick<PlayerAuth, 'playerHash' | 'name'>, level: number) =>
    set.mutate(
      { params: { path: { serverGroup } }, body: { playerHash: entry.playerHash, authLevel: level } },
      {
        onSuccess: () =>
          toast.success(level === defaultAuthLevel ? `${entry.name ?? entry.playerHash} has no level any more` : `${entry.name ?? entry.playerHash} is now ${authLevelName(level)}`),
      },
    )

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        The levels the chat commands check, for every server in this group. Players without a level have All. You can also set a level from a player's
        menu on the server page.
      </p>
      <AuthLevelLegend />

      {auth.isPending && <Skeleton className="h-40" />}
      {auth.isError && <p className="text-destructive">{errorMessage(auth.error)}</p>}
      {auth.data && auth.data.length > 0 && (
        <div className="relative">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder="Filter by name or hash"
            aria-label="Filter the players by name or hash"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
        </div>
      )}
      {auth.data && (
        <Card className="py-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Player</TableHead>
                <TableHead className="hidden md:table-cell">Hash</TableHead>
                <TableHead className="w-36">Level</TableHead>
                <TableHead className="w-px">
                  <span className="sr-only">Remove</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {auth.data.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-muted-foreground">
                    No player has a level in this group.
                  </TableCell>
                </TableRow>
              )}
              {auth.data.length > 0 && shown.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-muted-foreground">
                    No player matches “{filter.trim()}”.
                  </TableCell>
                </TableRow>
              )}
              {shown.map((entry) => (
                <TableRow key={entry.playerHash}>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                      {entry.name ?? <span className="text-muted-foreground">Unknown name</span>}
                      {entry.serverId && <Badge variant="secondary">online</Badge>}
                    </div>
                    {/* Without the hash column on a phone, the hash goes under the name: its ends, and the copy button for all of it */}
                    <div className="md:hidden">
                      <Hash value={entry.playerHash} short />
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <Hash value={entry.playerHash} />
                  </TableCell>
                  <TableCell>
                    <LevelSelect
                      compact
                      label={`Admin level of ${entry.name ?? entry.playerHash}`}
                      value={entry.authLevel}
                      disabled={set.isPending}
                      onChange={(level) => setLevel(entry, level)}
                    />
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Remove the level of ${entry.name ?? entry.playerHash}`}
                      title="Remove (back to All)"
                      onClick={() =>
                        confirm({
                          title: `Remove the level of ${entry.name ?? entry.playerHash}?`,
                          description: `The player goes back to the default level, All, on every server in ${serverGroup}.`,
                          confirmLabel: 'Remove',
                          destructive: true,
                          onConfirm: () => setLevel(entry, defaultAuthLevel),
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
        </Card>
      )}

      <AddPlayerAuth onAdd={(playerHash, level) => setLevel({ playerHash, name: null }, level)} pending={set.isPending} />
      {dialog}
    </div>
  )
}

function AddPlayerAuth({ onAdd, pending }: { onAdd: (playerHash: string, level: number) => void; pending: boolean }) {
  const [hash, setHash] = useState('')
  const [level, setLevel] = useState(50)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Add a player by hash</CardTitle>
        <CardDescription>For a player who is not online. The hash is the player's CD-key hash, or "name:" and the name on a test server.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            if (!hash.trim()) return
            onAdd(hash.trim(), level)
            setHash('')
          }}
        >
          <Field className="min-w-56 flex-1">
            <FieldLabel htmlFor="auth-hash">Hash</FieldLabel>
            <Input id="auth-hash" value={hash} maxLength={50} onChange={(e) => setHash(e.target.value)} />
          </Field>
          <Field className="w-44">
            <FieldLabel htmlFor="auth-level">Level</FieldLabel>
            <LevelSelect id="auth-level" label="Level" value={level} onChange={setLevel} withoutDefault compact />
          </Field>
          <Button type="submit" disabled={pending || !hash.trim()}>
            Add
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

/**
 * A player's hash with a copy button. In full where there is room (a CD-key hash is 32 characters); short keeps its
 * start and end, which tell hashes apart, and the title and the copy button have all of it.
 */
function Hash({ value, short }: { value: string; short?: boolean }) {
  const text = short && value.length > 14 ? `${value.slice(0, 8)}…${value.slice(-4)}` : value
  return (
    <span className="flex items-center gap-0.5">
      <span className="font-mono text-xs whitespace-nowrap text-muted-foreground" title={value}>
        {text}
      </span>
      <CopyButton value={value} label="Copy the hash" />
    </span>
  )
}
