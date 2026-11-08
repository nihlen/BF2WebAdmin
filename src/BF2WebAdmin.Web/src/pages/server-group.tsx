import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type PlayerAuth } from '@/api/client'
import { useConfirm } from '@/components/confirm-dialog'
import { NativeSelect } from '@/components/native-select'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { useServers } from '@/hooks/use-servers'
import { authLevelName, authLevels, defaultAuthLevel } from '@/lib/auth-levels'
import { encodeServerId } from '@/lib/server-id'
import { serverGroupRoute } from '@/router'

const defaultGroup = 'default'

export function ServerGroupPage() {
  const { serverGroup } = serverGroupRoute.useParams()
  const groupModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })
  const defaultModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: defaultGroup } } })
  const allModules = api.useQuery('get', '/api/modules')
  const servers = useServers()
  const groupServers = (servers.data ?? []).filter((s) => s.serverGroup === serverGroup)

  const loaded = groupModules.data && defaultModules.data && allModules.data

  return (
    <div className="space-y-8">
      <title>{`BF2 Web Admin - ${serverGroup}`}</title>
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">{serverGroup}</h1>
        <p className="text-muted-foreground">
          {groupServers.length === 0
            ? 'No server is in this group.'
            : groupServers.map((s, i) => (
                <span key={s.id}>
                  {i > 0 && ', '}
                  <Link to="/server/$serverId" params={{ serverId: encodeServerId(s.id) }} className="hover:underline">
                    {s.name}
                  </Link>
                </span>
              ))}
        </p>
      </div>

      <div className="grid gap-8 xl:grid-cols-[minmax(0,28rem)_minmax(0,1fr)]">
        <section className="space-y-4">
          <h2 className="text-xl font-medium">Modules</h2>
          {!loaded && <Skeleton className="h-96" />}
          {loaded && (
            <ModulesForm
              key={serverGroup}
              serverGroup={serverGroup}
              active={groupModules.data}
              defaults={defaultModules.data}
              all={allModules.data}
              serverCount={groupServers.length}
            />
          )}
        </section>

        <section className="space-y-4">
          <h2 className="text-xl font-medium">Player auth levels</h2>
          <PlayerAuthList serverGroup={serverGroup} />
        </section>
      </div>
    </div>
  )
}

function ModulesForm({
  serverGroup,
  active,
  defaults,
  all,
  serverCount,
}: {
  serverGroup: string
  active: string[]
  defaults: string[]
  all: string[]
  serverCount: number
}) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState(() => new Set(active))
  const readOnly = serverGroup === defaultGroup
  const params = { path: { serverGroup } }
  const dirty = selected.size !== active.length || active.some((m) => !selected.has(m))

  const save = api.useMutation('put', '/api/server-groups/{serverGroup}/modules', {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['get', '/api/server-groups/{serverGroup}/modules'] }),
  })
  const reload = api.useMutation('post', '/api/server-groups/{serverGroup}/modules/reload')
  const servers = `${serverCount} ${serverCount === 1 ? 'server' : 'servers'}`

  const saveAndReload = async () => {
    const id = toast.loading('Saving the modules…')
    try {
      if (dirty) await save.mutateAsync({ params, body: [...selected] })
      toast.loading(`Reloading the modules on ${servers}…`, { id })
      await reload.mutateAsync({ params })
      toast.success(dirty ? `Saved, and reloaded on ${servers}` : `Reloaded on ${servers}`, { id })
    } catch {
      // The mutation cache shows the error
      toast.dismiss(id)
    }
  }

  const toggle = (name: string, checked: boolean) =>
    setSelected((s) => {
      const next = new Set(s)
      if (checked) next.add(name)
      else next.delete(name)
      return next
    })

  return (
    <div className="space-y-4">
      {readOnly ? (
        <p className="text-muted-foreground">The default server group cannot be changed. Give your server a group of its own to choose its modules.</p>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <Button onClick={() => void saveAndReload()} disabled={save.isPending || reload.isPending || serverCount === 0}>
            {dirty ? 'Save and reload' : 'Reload modules'}
          </Button>
          <Button
            variant="outline"
            onClick={() => save.mutate({ params, body: [...selected] }, { onSuccess: () => toast.success('The modules are saved. They load at the next reload') })}
            disabled={!dirty || save.isPending}
          >
            Save only
          </Button>
          {dirty && <Badge variant="outline">Unsaved changes</Badge>}
        </div>
      )}
      {!readOnly && <p className="text-sm text-muted-foreground">A reload restarts the modules on the {servers} in this group. Their state, such as a running match, is lost.</p>}

      <Card>
        <CardContent className="space-y-3">
          {all.map((name) => {
            const always = defaults.includes(name)
            return (
              <Field key={name} orientation="horizontal">
                <Checkbox
                  id={`module-${name}`}
                  checked={selected.has(name) || always}
                  disabled={readOnly || always}
                  onCheckedChange={(checked) => toggle(name, checked)}
                />
                <FieldLabel htmlFor={`module-${name}`}>
                  {name}
                  {always && <span className="font-normal text-muted-foreground">(always on)</span>}
                </FieldLabel>
              </Field>
            )
          })}
        </CardContent>
      </Card>
    </div>
  )
}

/** The players with a level in this group. A level back to All removes the player from the list. */
function PlayerAuthList({ serverGroup }: { serverGroup: string }) {
  const queryClient = useQueryClient()
  const auth = useQuery(playerAuthQuery(serverGroup))
  const { confirm, dialog } = useConfirm()
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
      {auth.data && (
        <Card className="py-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Player</TableHead>
                <TableHead className="hidden md:table-cell">Hash</TableHead>
                <TableHead className="w-44">Level</TableHead>
                <TableHead className="w-px" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {auth.data.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-muted-foreground">
                    No player has a level in this group
                  </TableCell>
                </TableRow>
              )}
              {auth.data.map((entry) => (
                <TableRow key={entry.playerHash}>
                  <TableCell>
                    {entry.name ?? <span className="text-muted-foreground">Unknown name</span>}
                    {entry.serverId && (
                      <Badge variant="secondary" className="ml-2">
                        online
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="hidden max-w-48 truncate font-mono text-xs md:table-cell" title={entry.playerHash}>
                    {entry.playerHash}
                  </TableCell>
                  <TableCell>
                    <NativeSelect
                      aria-label={`Auth level of ${entry.name ?? entry.playerHash}`}
                      value={String(entry.authLevel)}
                      disabled={set.isPending}
                      onChange={(e) => setLevel(entry, Number(e.target.value))}
                    >
                      {authLevels.map((l) => (
                        <option key={l.value} value={l.value} title={l.description}>
                          {l.name}
                        </option>
                      ))}
                    </NativeSelect>
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
            <NativeSelect id="auth-level" value={String(level)} onChange={(e) => setLevel(Number(e.target.value))}>
              {authLevels
                .filter((l) => l.value !== defaultAuthLevel)
                .map((l) => (
                  <option key={l.value} value={l.value}>
                    {l.name}
                  </option>
                ))}
            </NativeSelect>
          </Field>
          <Button type="submit" disabled={pending || !hash.trim()}>
            Add
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
