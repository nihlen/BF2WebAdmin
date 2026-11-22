import { Link } from '@tanstack/react-router'
import { ArrowRight, Box, Plus, Radio, Settings, Settings2 } from 'lucide-react'
import { errorMessage, type Server } from '@/api/client'
import { Separated } from '@/components/separated'
import { FirstServerSetup, SetupLine, SetupSummary } from '@/components/setup-checklist'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { groupServers, useServers } from '@/hooks/use-servers'
import { type Step, useSetupOverview } from '@/hooks/use-setup-steps'
import { overallHealth, statusColors } from '@/lib/health'
import { encodeServerId } from '@/lib/server-id'
import { formatTime } from '@/lib/format'
import { connectionText, serverStatus, toneDot, toneText } from '@/lib/server-state'
import { cn } from '@/lib/utils'

type Health = ReturnType<typeof overallHealth>

/**
 * The health now of every connected server, by id, from the server list: the server sends a ServerUpdateEvent to every
 * client when a server's health status changes, so the list stays current without asking each server.
 */
const serversHealth = (servers: Server[]) =>
  new Map(servers.map((s) => [s.id, s.socketState === 'Connected' ? overallHealth(s.health ?? undefined) : null]))

// What needs a look comes first in a group: offline servers, then degraded, then warning; the rest keep their order
const attention = (server: Server, health: Health) =>
  server.socketState !== 'Connected' ? 0 : health?.status === 'Bad' ? 1 : health?.status === 'Warn' ? 2 : 3

/**
 * The servers as an operations view: per group, the servers that need a look first, each with its status, map,
 * players, health and the reason when it warns, and quick links. A table on desktop, cards on a phone. Setup that is
 * left shows as one line per server and one reminder row above the list.
 */
export function DashboardPage() {
  const servers = useServers()
  const list = servers.data ?? []
  const online = list.filter((s) => s.socketState === 'Connected')
  const players = online.reduce((sum, s) => sum + s.players, 0)
  const setup = useSetupOverview(list)
  const health = serversHealth(list)

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
      {list.length > 0 && <SetupSummary servers={list} steps={setup.steps} />}

      {groupServers(list).map(([group, groupServers]) => {
        const sorted = groupServers
          .map((server, i) => ({ server, i, rank: attention(server, health.get(server.id) ?? null) }))
          .sort((a, b) => a.rank - b.rank || a.i - b.i)
          .map((x) => x.server)
        const info = setup.groups.get(group)
        return (
          <section key={group} className="space-y-3" aria-labelledby={`group-${group}`}>
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h2 id={`group-${group}`} className="text-xl font-medium">
                Group {group}
              </h2>
              <p className="flex items-center gap-2 text-sm text-muted-foreground">
                <Separated>
                  {`${groupServers.length} ${groupServers.length === 1 ? 'server' : 'servers'}`}
                  {info?.modules && `${info.modules.length} ${info.modules.length === 1 ? 'module' : 'modules'}`}
                  {info?.admins && `${info.admins.length} ${info.admins.length === 1 ? 'admin' : 'admins'}`}
                </Separated>
              </p>
              <Link
                to="/servergroup/$serverGroup"
                params={{ serverGroup: group }}
                className="ml-auto inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground hover:underline"
              >
                <Settings2 className="size-4" />
                Group settings
              </Link>
            </div>
            <ServerTable servers={sorted} health={health} steps={setup.steps} />
            <div className="grid gap-3 md:hidden">
              {sorted.map((server) => (
                <ServerCard key={server.id} server={server} health={health.get(server.id) ?? null} steps={setup.steps.get(server.id)} />
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}

/** The status in a word; the row's title has the admin channel's connection (since when, or when it was lost). */
function Status({ server }: { server: Server }) {
  const status = serverStatus(server)
  return (
    <span className={cn('inline-flex items-center gap-1.5 font-medium whitespace-nowrap', toneText[status.tone])}>
      <span className={cn('size-2 shrink-0 rounded-full', toneDot[status.tone])} />
      {status.label}
    </span>
  )
}

/** The health as a dot and a word, with the reasons in its title (above the row's link, so the title shows on hover). */
function HealthPill({ health }: { health: NonNullable<Health> }) {
  return (
    <span className="relative inline-flex h-5 shrink-0 items-center gap-1.5 rounded-full border px-2 text-xs font-medium whitespace-nowrap" title={health.title}>
      <span className={cn('size-2 shrink-0 rounded-full', statusColors[health.status])} aria-hidden />
      {health.label}
    </span>
  )
}

/** Why an offline server has no map or players: its connection was lost (when), or it has not connected since WebAdmin started. */
function OfflineSince({ server }: { server: Server }) {
  return server.disconnectedAt ? (
    <span>Connection lost at {formatTime(server.disconnectedAt)}</span>
  ) : (
    <span>Not connected since WebAdmin started</span>
  )
}

/** An offline server's next step: its Live tab shows how to connect it. */
function HowToConnect({ server }: { server: Server }) {
  return (
    <Link
      to="/server/$serverId"
      params={{ serverId: encodeServerId(server.id) }}
      className="relative inline-flex items-center gap-1 font-medium text-foreground underline-offset-4 hover:underline"
    >
      How to connect
      <ArrowRight className="size-3.5" />
    </Link>
  )
}

const quickLinks = [
  { to: '/server/$serverId', label: 'Live', icon: Radio },
  { to: '/server/$serverId/viewer', label: '3D viewer', icon: Box },
  { to: '/server/$serverId/edit', label: 'Settings', icon: Settings },
] as const

/**
 * A group's servers as table rows, from md up. The whole row links to the server; the quick links at its end show on
 * hover or focus where there is a mouse.
 */
function ServerTable({ servers, health, steps }: { servers: Server[]; health: Map<string, Health>; steps: Map<string, Step[]> }) {
  return (
    <div className="hidden overflow-hidden rounded-xl border md:block">
      <Table className="table-fixed">
        <TableHeader>
          <TableRow className="bg-muted/30 hover:bg-muted/30">
            <TableHead className="w-28 pl-4">Status</TableHead>
            <TableHead>Server</TableHead>
            <TableHead className="w-36">Map</TableHead>
            <TableHead className="w-24 text-right">Players</TableHead>
            <TableHead className="w-[30%]">Health</TableHead>
            <TableHead className="w-28">
              <span className="sr-only">Open</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {servers.map((server) => {
            const connected = server.socketState === 'Connected'
            const serverHealth = health.get(server.id)
            const params = { serverId: encodeServerId(server.id) }
            return (
              <TableRow key={server.id} className="group relative has-[a[data-row-link]:focus-visible]:bg-muted/50">
                <TableCell className="pl-4">
                  <Status server={server} />
                </TableCell>
                <TableCell className="py-2.5">
                  <Link
                    to="/server/$serverId"
                    params={params}
                    data-row-link
                    title={`${server.name}\n${connectionText(server)}`}
                    className="block truncate font-medium outline-none after:absolute after:inset-0"
                  >
                    {server.name}
                  </Link>
                  <span className="block font-mono text-xs text-muted-foreground">
                    {server.ipAddress}:{server.gamePort}
                  </span>
                  {/* An offline server's next step is to connect it, which How to connect leads to */}
                  {connected && <SetupLine server={server} steps={steps.get(server.id)} className="relative mt-1 w-fit max-w-full" />}
                </TableCell>
                {connected ? (
                  <>
                    <TableCell className="truncate text-muted-foreground">{server.map || '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {server.players}/{server.maxPlayers}
                    </TableCell>
                    <TableCell>
                      {serverHealth ? (
                        <span className="flex min-w-0 items-center gap-2">
                          <HealthPill health={serverHealth} />
                          {serverHealth.status !== 'Good' && (
                            <span className="relative truncate text-xs text-muted-foreground" title={serverHealth.reason}>
                              {serverHealth.summary}
                            </span>
                          )}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                  </>
                ) : (
                  <TableCell colSpan={3} className="text-muted-foreground">
                    <span className="flex flex-wrap items-center gap-x-2">
                      <Separated>
                        <OfflineSince server={server} />
                        <HowToConnect server={server} />
                      </Separated>
                    </span>
                  </TableCell>
                )}
                <TableCell className="pr-3">
                  <span className="relative flex justify-end gap-0.5 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100 [@media(hover:hover)]:opacity-0">
                    {quickLinks.map((link) => (
                      <Button
                        key={link.to}
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`${link.label} of ${server.name}`}
                        title={link.label}
                        nativeButton={false}
                        render={<Link to={link.to} params={params} />}
                      >
                        <link.icon />
                      </Button>
                    ))}
                  </span>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}

/** A server as a card, below md: the whole card links to the server, the settings button sits above the link. */
function ServerCard({ server, health, steps }: { server: Server; health: Health; steps: Step[] | undefined }) {
  const connected = server.socketState === 'Connected'
  return (
    <Card
      size="sm"
      className="relative transition-colors has-[a[data-card-link]:hover]:bg-muted/40 has-[a[data-card-link]:focus-visible]:ring-3 has-[a[data-card-link]:focus-visible]:ring-ring/50"
    >
      {/* The name truncates before the settings button, which keeps its place */}
      <CardHeader className="grid-cols-[minmax(0,1fr)_auto]">
        <CardTitle className="min-w-0">
          <Link
            to="/server/$serverId"
            params={{ serverId: encodeServerId(server.id) }}
            data-card-link
            title={`${server.name}\n${connectionText(server)}`}
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
      <CardContent className="space-y-2 text-sm text-muted-foreground">
        {/* Two short lines rather than one that wraps, so no separator ends a line */}
        <p className="flex items-center gap-2">
          <Status server={server} />
          {health && <HealthPill health={health} />}
          {!connected && <HowToConnect server={server} />}
        </p>
        {!connected && (
          <p className="text-xs">
            <OfflineSince server={server} />
          </p>
        )}
        {connected && (
          <p className="flex min-w-0 items-center gap-2">
            <Separated>
              {server.map && <span className="truncate">{server.map}</span>}
              <span className="shrink-0 tabular-nums">
                {server.players}/{server.maxPlayers} players
              </span>
            </Separated>
          </p>
        )}
        {health && health.status !== 'Good' && (
          <p className="relative text-xs" title={health.reason}>
            {health.summary}
          </p>
        )}
        {connected && <SetupLine server={server} steps={steps} className="relative" />}
      </CardContent>
    </Card>
  )
}
