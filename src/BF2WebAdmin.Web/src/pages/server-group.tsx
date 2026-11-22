import { Link, useLocation } from '@tanstack/react-router'
import { ArrowRight } from 'lucide-react'
import { useState } from 'react'
import { api, type Server } from '@/api/client'
import { ModulesForm } from '@/components/group/modules-form'
import { PlayerAuthList } from '@/components/group/player-auth-list'
import { useServerPage } from '@/components/server/server-context'
import { ServerStatusDot } from '@/components/server-status'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useServers } from '@/hooks/use-servers'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'
import { serverGroupRoute } from '@/router'

const defaultGroup = 'default'

export function ServerGroupPage() {
  const { serverGroup } = serverGroupRoute.useParams()
  const servers = useServers()
  const groupServers = (servers.data ?? []).filter((s) => s.serverGroup === serverGroup)

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
        <GroupServerList servers={groupServers} />
      </div>
      <GroupSettings serverGroup={serverGroup} />
    </div>
  )
}

/**
 * The Group tab of a server: its group's modules and admin levels, the same as on the group's page, so they are found
 * from the server. The page names the group's servers, since a change here changes all of them.
 */
export function ServerGroupTabPage() {
  const { serverId } = useServerPage()
  const servers = useServers()
  const serverGroup = servers.data?.find((s) => s.id === serverId)?.serverGroup
  if (!serverGroup) return <Skeleton className="h-96" />
  const groupServers = (servers.data ?? []).filter((s) => s.serverGroup === serverGroup)
  const others = groupServers.length - 1

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
        <div className="min-w-0 space-y-2">
          <h2 className="text-xl font-semibold">Group {serverGroup}</h2>
          <p className="text-sm text-muted-foreground">
            {others === 0
              ? 'This server is the only one in the group. Its modules and admin levels are set for the group, so a server you add to it later gets them too.'
              : `This server shares its modules and admin levels with ${others === 1 ? 'one other server' : `${others} other servers`}: a change here changes all of them.`}
          </p>
          {others > 0 && <GroupServerList servers={groupServers} current={serverId} />}
        </div>
        <Button variant="outline" nativeButton={false} render={<Link to="/servergroup/$serverGroup" params={{ serverGroup }} />}>
          Open the group page
          <ArrowRight data-icon="inline-end" />
        </Button>
      </div>
      <GroupSettings serverGroup={serverGroup} />
    </div>
  )
}

/** The group's servers as links, the one open now marked. */
function GroupServerList({ servers, current }: { servers: Server[]; current?: string }) {
  if (servers.length === 0) return null
  return (
    <ul className="flex flex-wrap gap-2" aria-label="Servers in this group">
      {servers.map((s) => (
        <li key={s.id} className="min-w-0">
          <Link
            to="/server/$serverId"
            params={{ serverId: encodeServerId(s.id) }}
            aria-current={s.id === current ? 'page' : undefined}
            className="flex max-w-80 items-center gap-2 rounded-full border px-3 py-1 text-sm transition-colors hover:bg-muted aria-[current=page]:border-primary/60 aria-[current=page]:bg-primary/10"
            title={s.name}
          >
            <ServerStatusDot socketState={s.socketState} />
            <span className="truncate">{s.name}</span>
            {s.id === current && <span className="shrink-0 text-xs text-muted-foreground">this server</span>}
          </Link>
        </li>
      ))}
    </ul>
  )
}

/**
 * A group's modules and admin levels: side by side on a wide screen, under each other in between, and on a phone two
 * tabs, so the admin levels are not a long scroll past every module away. Both stay mounted (the other one hidden), so
 * unsaved module changes survive a look at the admin levels. A setup step links to #admin-levels, which opens that tab.
 */
function GroupSettings({ serverGroup }: { serverGroup: string }) {
  const groupModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })
  const defaultModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: defaultGroup } } })
  const allModules = api.useQuery('get', '/api/modules')
  const servers = useServers()
  const hash = useLocation({ select: (location) => location.hash })
  const [tab, setTab] = useState(hash === 'admin-levels' ? 'admin-levels' : 'modules')
  const loaded = groupModules.data && defaultModules.data && allModules.data

  return (
    <>
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
    </>
  )
}
