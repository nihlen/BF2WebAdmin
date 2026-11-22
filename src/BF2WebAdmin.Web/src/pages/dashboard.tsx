import { Link } from '@tanstack/react-router'
import { Plus, Settings, Settings2 } from 'lucide-react'
import { errorMessage } from '@/api/client'
import { HealthBadge } from '@/components/server/health-dot'
import { FirstServerSetup, SetupChecklist } from '@/components/setup-checklist'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { groupServers, useServers } from '@/hooks/use-servers'
import { encodeServerId } from '@/lib/server-id'
import { serverStatus, toneDot, toneText } from '@/lib/server-state'
import { cn } from '@/lib/utils'

export function DashboardPage() {
  const servers = useServers()
  const list = servers.data ?? []
  const online = list.filter((s) => s.socketState === 'Connected')
  const players = online.reduce((sum, s) => sum + s.players, 0)

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-semibold">Servers</h1>
          {list.length > 0 && (
            <p className="text-sm text-muted-foreground">
              {online.length} of {list.length} online, {players} {players === 1 ? 'player' : 'players'}
            </p>
          )}
        </div>
        <Button nativeButton={false} render={<Link to="/server/add" />}>
          <Plus data-icon="inline-start" />
          Add server
        </Button>
      </div>

      {servers.isPending && <Skeleton className="h-40" />}
      {servers.isError && <p className="text-destructive">{errorMessage(servers.error)}</p>}
      {servers.data?.length === 0 && <FirstServerSetup />}

      {/* New servers' setup steps, until done or hidden; each card hides itself then */}
      {list.length > 0 && (
        <div className="grid items-start gap-4 empty:hidden lg:grid-cols-2">
          {list.map((server) => (
            <SetupChecklist key={server.id} server={server} />
          ))}
        </div>
      )}

      {groupServers(servers.data ?? []).map(([group, groupServers]) => (
        <section key={group} className="space-y-4">
          <div className="flex items-baseline justify-between gap-4">
            <h2 className="text-xl font-medium">Group {group}</h2>
            <Link
              to="/servergroup/$serverGroup"
              params={{ serverGroup: group }}
              className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground hover:underline"
            >
              <Settings2 className="size-4" />
              Group settings
            </Link>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {groupServers.map((server) => {
              const status = serverStatus(server)
              const connected = server.socketState === 'Connected'
              return (
                <Card key={server.id} className="relative transition-colors has-[a[data-card-link]:hover]:bg-muted/40 has-[a[data-card-link]:focus-visible]:ring-3 has-[a[data-card-link]:focus-visible]:ring-ring/50">
                  {/* The name truncates before the settings button, which keeps its place */}
                  <CardHeader className="grid-cols-[minmax(0,1fr)_auto]">
                    <CardTitle className="min-w-0">
                      {/* The whole card links to the server; the settings button sits above the link */}
                      <Link
                        to="/server/$serverId"
                        params={{ serverId: encodeServerId(server.id) }}
                        data-card-link
                        title={server.name}
                        className="block truncate outline-none after:absolute after:inset-0 after:rounded-xl"
                      >
                        {server.name}
                      </Link>
                    </CardTitle>
                    <CardDescription className="font-mono text-xs">
                      {server.ipAddress}:{server.gamePort}
                    </CardDescription>
                    <CardAction className="relative">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Settings of ${server.name}`}
                        title="Server settings"
                        nativeButton={false}
                        render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(server.id) }} />}
                      >
                        <Settings />
                      </Button>
                    </CardAction>
                  </CardHeader>
                  <CardContent className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
                    <span className={cn('inline-flex items-center gap-1.5 font-medium', toneText[status.tone])}>
                      <span className={cn('size-2 rounded-full', toneDot[status.tone])} />
                      {status.label}
                    </span>
                    {/* Above the card's link, so its title shows on hover */}
                    <HealthBadge server={server} className="relative" />
                    {connected && server.map && (
                      <>
                        <span aria-hidden className="text-muted-foreground/50">
                          ·
                        </span>
                        <span>{server.map}</span>
                      </>
                    )}
                    <span aria-hidden className="text-muted-foreground/50">
                      ·
                    </span>
                    <span className="tabular-nums">
                      {connected ? `${server.players}/${server.maxPlayers} players` : 'Admin channel not connected'}
                    </span>
                  </CardContent>
                </Card>
              )
            })}
          </div>
        </section>
      ))}
    </div>
  )
}
