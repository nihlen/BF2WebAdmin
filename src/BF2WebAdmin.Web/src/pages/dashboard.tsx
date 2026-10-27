import { Link } from '@tanstack/react-router'
import { Plus, Settings } from 'lucide-react'
import { errorMessage } from '@/api/client'
import { ServerStatusDot } from '@/components/server-status'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { groupServers, useServers } from '@/hooks/use-servers'
import { encodeServerId } from '@/lib/server-id'

export function DashboardPage() {
  const servers = useServers()

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-semibold">Servers</h1>
        <Button nativeButton={false} render={<Link to="/server/add" />}>
          <Plus data-icon="inline-start" />
          Add server
        </Button>
      </div>

      {servers.isPending && <Skeleton className="h-40" />}
      {servers.isError && <p className="text-destructive">{errorMessage(servers.error)}</p>}

      {groupServers(servers.data ?? []).map(([group, groupServers]) => (
        <section key={group} className="space-y-4">
          <Link to="/servergroup/$serverGroup" params={{ serverGroup: group }} className="text-xl font-medium hover:underline">
            {group}
          </Link>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {groupServers.map((server) => (
              <Card key={server.id} className="relative transition-colors hover:bg-muted/40">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <ServerStatusDot socketState={server.socketState} />
                    {/* The whole card links to the server; the settings button sits above the link */}
                    <Link
                      to="/server/$serverId"
                      params={{ serverId: encodeServerId(server.id) }}
                      className="truncate after:absolute after:inset-0"
                    >
                      {server.name}
                    </Link>
                  </CardTitle>
                  <CardDescription>
                    {server.ipAddress}:{server.gamePort}
                  </CardDescription>
                  <CardAction className="relative">
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      nativeButton={false}
                      render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(server.id) }} />}
                    >
                      <Settings />
                    </Button>
                  </CardAction>
                </CardHeader>
                <CardContent className="flex flex-wrap gap-2">
                  <Badge variant="secondary">{server.gameState}</Badge>
                  {server.map && <Badge variant="secondary">{server.map}</Badge>}
                  <Badge variant="secondary">
                    {server.players} / {server.maxPlayers} players
                  </Badge>
                </CardContent>
              </Card>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
