import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useLocation } from '@tanstack/react-router'
import { Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type PlayerAuth } from '@/api/client'
import { LevelSelect } from '@/components/auth-level-select'
import { useConfirm } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { ModulesForm } from '@/components/group/modules-form'
import { ServerStatusDot } from '@/components/server-status'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { useServers } from '@/hooks/use-servers'
import { authLevelName, defaultAuthLevel } from '@/lib/auth-levels'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'
import { serverGroupRoute } from '@/router'

const defaultGroup = 'default'

export function ServerGroupPage() {
  const { serverGroup } = serverGroupRoute.useParams()
  const groupModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })
  const defaultModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: defaultGroup } } })
  const allModules = api.useQuery('get', '/api/modules')
  const servers = useServers()
  const groupServers = (servers.data ?? []).filter((s) => s.serverGroup === serverGroup)
  // A setup step links to #admin-levels: on a phone that opens its tab
  const hash = useLocation({ select: (location) => location.hash })
  const [tab, setTab] = useState(hash === 'admin-levels' ? 'admin-levels' : 'modules')

  const loaded = groupModules.data && defaultModules.data && allModules.data

  return (
    <div className="space-y-8">
      <title>{`BF2 Web Admin - Server group ${serverGroup}`}</title>
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">Server group {serverGroup}</h1>
        <p className="text-muted-foreground">
          {groupServers.length === 0
            ? 'Modules and admin levels for the servers of this group. No server is in it yet.'
            : `Modules and admin levels shared by ${groupServers.length === 1 ? 'one server' : `${groupServers.length} servers`}:`}
        </p>
        {groupServers.length > 0 && (
          <ul className="flex flex-wrap gap-2">
            {groupServers.map((s) => (
              <li key={s.id} className="min-w-0">
                <Link
                  to="/server/$serverId"
                  params={{ serverId: encodeServerId(s.id) }}
                  className="flex max-w-80 items-center gap-2 rounded-full border px-3 py-1 text-sm transition-colors hover:bg-muted"
                  title={s.name}
                >
                  <ServerStatusDot socketState={s.socketState} />
                  <span className="truncate">{s.name}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* On a phone the two sections are tabs, so the admin levels are not a long scroll past every module away. Both
          stay mounted (the other one hidden), so unsaved module changes survive a look at the admin levels */}
      <Tabs value={tab} onValueChange={setTab} className="gap-6 md:hidden">
        <TabsList className="w-full">
          <TabsTrigger value="modules">Modules</TabsTrigger>
          <TabsTrigger value="admin-levels">Admin levels</TabsTrigger>
        </TabsList>
      </Tabs>
      <div className="grid gap-8 xl:grid-cols-[minmax(0,32rem)_minmax(0,1fr)]">
        <section id="modules" className={cn('scroll-mt-16 space-y-4', tab !== 'modules' && 'max-md:hidden')}>
          <h2 className="text-xl font-medium max-md:sr-only">Modules</h2>
          {!loaded && <Skeleton className="h-96" />}
          {loaded && (
            <ModulesForm
              key={serverGroup}
              serverGroup={serverGroup}
              active={groupModules.data}
              defaults={defaultModules.data}
              all={allModules.data}
              servers={servers.data ?? []}
            />
          )}
        </section>

        <section id="admin-levels" className={cn('scroll-mt-16 space-y-4', tab !== 'admin-levels' && 'max-md:hidden')}>
          <h2 className="text-xl font-medium max-md:sr-only">Admin levels</h2>
          <PlayerAuthList serverGroup={serverGroup} />
        </section>
      </div>
    </div>
  )
}

/** The players with a level in this group. A level back to All removes the player from the list. */
function PlayerAuthList({ serverGroup }: { serverGroup: string }) {
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
