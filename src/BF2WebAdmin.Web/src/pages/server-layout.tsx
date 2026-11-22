import { HubConnectionState } from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { Link, Outlet, useMatches } from '@tanstack/react-router'
import { Ban as BanIcon, Box, Hammer, HeartPulse, History, Loader2, Pause, Play, Power, Radio, RotateCcw, Settings } from 'lucide-react'
import { useLayoutEffect, useRef } from 'react'
import { errorMessage, type Server, type ServerSnapshot } from '@/api/client'
import { useHubState } from '@/api/hub'
import { useConfirm } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { HealthChip } from '@/components/server/health-panel'
import { ServerPageContext, useServerPage } from '@/components/server/server-context'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { type ServerActions, useServerActions } from '@/hooks/use-server-actions'
import { healthQuery, useServerLive } from '@/hooks/use-server-live'
import { useServers } from '@/hooks/use-servers'
import { usePendingActions } from '@/lib/pending-actions'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { serverStatus, toneDot, toneText } from '@/lib/server-state'
import { cn } from '@/lib/utils'
import { serverRoute } from '@/router'

/**
 * Every page of a server: one header (name, status, the round controls) and a tab bar of the server's pages. The
 * server's live data is subscribed here, once, for as long as any of its pages is open.
 */
export function ServerLayout() {
  const serverId = decodeServerId(serverRoute.useParams().serverId)
  const live = useServerLive(serverId)
  const servers = useServers()
  const fullHeight = useMatches({ select: (matches) => matches.some((m) => m.staticData.fullHeight) })
  // The server list knows the server before its snapshot has loaded, so the header shows at once
  const server = live.snapshot.data?.server ?? servers.data?.find((s) => s.id === serverId)

  return (
    <ServerPageContext value={{ ...live, serverId }}>
      {/* A page with a 3D view fills the window below the header on desktop: the app bar is 3rem, the padding 3rem */}
      <div className={cn('flex flex-col gap-4', fullHeight && 'lg:h-[calc(100svh-6rem)] lg:min-h-[640px]')}>
        {server && <title>{`BF2 Web Admin - ${server.name}`}</title>}
        {server ? <ServerHeader server={server} snapshot={live.snapshot.data} /> : <Skeleton className="h-24" />}
        <ServerTabBar serverId={serverId} />
        <HubAlert />
        {live.snapshot.isError ? (
          <p className="text-destructive">{errorMessage(live.snapshot.error)}</p>
        ) : (
          <div className={cn('flex min-w-0 flex-col', fullHeight && 'lg:min-h-0 lg:flex-1')}>
            <Outlet />
          </div>
        )}
      </div>
    </ServerPageContext>
  )
}

function HubAlert() {
  const hubState = useHubState()
  if (hubState === HubConnectionState.Connected) return null
  return (
    <Alert>
      <Loader2 className="animate-spin" />
      <AlertTitle>Live updates are offline, reconnecting…</AlertTitle>
      <AlertDescription>The players, chat and map may be out of date until the connection is back.</AlertDescription>
    </Alert>
  )
}

/** The name (two lines at most), one coloured status and the facts after it, and the round controls while online. */
function ServerHeader({ server, snapshot }: { server: Server; snapshot: ServerSnapshot | undefined }) {
  const { serverId } = useServerPage()
  const status = serverStatus(server)
  const connected = server.socketState === 'Connected'
  const address = `${server.ipAddress}:${server.gamePort}`
  // The chip has nothing to say without a health window; then no empty link either
  const hasHealth = !!useQuery({ ...healthQuery(serverId), staleTime: Infinity }).data?.latest

  return (
    <header className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
      <div className="min-w-0 flex-[1_1_20rem] space-y-1.5">
        <h1 className="line-clamp-2 text-2xl font-semibold break-words sm:text-3xl" title={server.name}>
          {server.name}
        </h1>
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
          <span className={cn('inline-flex items-center gap-1.5 font-medium', toneText[status.tone])} title={connected ? 'The admin channel is connected' : 'The admin channel is not connected'}>
            <span className={cn('size-2 rounded-full', toneDot[status.tone])} />
            {status.label}
          </span>
          {/* Problems show on every tab; the chip leads to the details */}
          {connected && hasHealth && (
          <Link to="/server/$serverId/health" params={{ serverId: encodeServerId(serverId) }} className="rounded-full focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none">
            <HealthChip serverId={serverId} connected={connected} />
          </Link>
          )}
          {connected && server.map && (
            <>
              <Dot />
              <span>{server.map}</span>
            </>
          )}
          {connected && (
            <>
              <Dot />
              <span className="tabular-nums">
                {server.players}/{server.maxPlayers} players
              </span>
            </>
          )}
          <Dot />
          <span className="inline-flex items-center gap-0.5">
            <span className="font-mono text-xs">{address}</span>
            <CopyButton value={address} label="Copy the address" />
          </span>
          <Dot />
          <Link to="/servergroup/$serverGroup" params={{ serverGroup: server.serverGroup }} className="hover:text-foreground hover:underline">
            Group {server.serverGroup}
          </Link>
        </div>
      </div>
      {connected && snapshot && <RoundControls serverId={serverId} snapshot={snapshot} />}
    </header>
  )
}

const Dot = () => (
  <span aria-hidden className="text-muted-foreground/50">
    ·
  </span>
)

/** Restart round, pause and restart server. Each shows its progress until the game confirms it. */
function RoundControls({ serverId, snapshot }: { serverId: string; snapshot: ServerSnapshot }) {
  const actions: ServerActions = useServerActions(serverId, snapshot)
  const { confirm, dialog } = useConfirm()
  const pending = usePendingActions(serverId)
  const players = snapshot.players.length
  const paused = snapshot.server.gameState === 'Paused'
  const playersText = players === 0 ? 'No players are online.' : `${players} ${players === 1 ? 'player is' : 'players are'} online.`

  const busy = (key: string) => pending.has(key)
  const icon = (key: string, Icon: React.ComponentType<{ className?: string }>, className: string) =>
    busy(key) ? <Loader2 data-icon="inline-start" className="animate-spin" /> : <Icon data-icon="inline-start" className={className} />

  return (
    <div className="flex flex-wrap gap-2">
      <Button
        variant="outline"
        disabled={busy('restart-map')}
        onClick={() =>
          confirm({
            title: 'Restart the round?',
            description: `The scores are reset and everyone respawns. ${playersText}`,
            confirmLabel: 'Restart round',
            onConfirm: () => void actions.restartMap(),
          })
        }
      >
        {icon('restart-map', RotateCcw, 'text-emerald-500')}
        Restart round
      </Button>
      <Button variant="outline" disabled={busy('pause')} onClick={() => void actions.togglePause()}>
        {paused ? icon('pause', Play, 'text-emerald-500') : icon('pause', Pause, 'text-amber-500')}
        {paused ? 'Resume' : 'Pause'}
      </Button>
      <Button
        variant="outline"
        aria-label="Restart server"
        title="Restart server"
        disabled={busy('restart-server')}
        onClick={() =>
          confirm({
            title: 'Restart the game server?',
            description: (
              <>
                The server quits cleanly (console <code>quit</code>) and its container starts it again, which takes about a minute. {playersText}{' '}
                Everyone is disconnected. The container needs a restart policy (<code>always</code> or <code>unless-stopped</code>).
              </>
            ),
            confirmLabel: 'Restart server',
            destructive: true,
            onConfirm: () => void actions.restartServer(),
          })
        }
      >
        {icon('restart-server', Power, 'text-destructive')}
        {/* Only the icon on a phone, so the three controls fit on one line */}
        <span className="hidden sm:inline">Restart server</span>
      </Button>
      {dialog}
    </div>
  )
}

const tabs = [
  { to: '/server/$serverId', label: 'Live', icon: Radio, exact: true },
  { to: '/server/$serverId/health', label: 'Health', icon: HeartPulse },
  { to: '/server/$serverId/bans', label: 'Bans', icon: BanIcon },
  { to: '/server/$serverId/activity', label: 'Activity', icon: History },
  { to: '/server/$serverId/viewer', label: '3D viewer', icon: Box },
  { to: '/server/$serverId/builder', label: 'Map builder', icon: Hammer },
  { to: '/server/$serverId/edit', label: 'Settings', icon: Settings },
] as const

/**
 * The server's pages as a row of links. On a narrow screen the row scrolls sideways, its edges fade where more tabs
 * are, and the current tab is scrolled into view.
 */
function ServerTabBar({ serverId }: { serverId: string }) {
  const ref = useRef<HTMLDivElement>(null)
  const path = useMatches({ select: (matches) => matches.at(-1)?.pathname })

  useLayoutEffect(() => {
    const list = ref.current
    const active = list?.querySelector<HTMLElement>('[data-status="active"]')
    if (!list || !active) return
    const left = active.offsetLeft - list.offsetLeft
    if (left < list.scrollLeft || left + active.offsetWidth > list.scrollLeft + list.clientWidth) list.scrollLeft = left - 24
  }, [path])

  return (
    <nav aria-label="Server pages" className="-mx-4 border-b sm:mx-0">
      <div
        ref={ref}
        className="flex gap-1 overflow-x-auto px-4 [mask-image:linear-gradient(to_right,transparent,black_1rem,black_calc(100%-2rem),transparent)] [scrollbar-width:none] sm:px-0 sm:[mask-image:none]"
      >
        {tabs.map((tab) => (
          <Link
            key={tab.to}
            to={tab.to}
            params={{ serverId: encodeServerId(serverId) }}
            activeOptions={{ exact: 'exact' in tab }}
            className="relative flex shrink-0 items-center gap-1.5 px-2.5 py-2.5 text-sm font-medium whitespace-nowrap text-muted-foreground transition-colors after:absolute after:inset-x-1 after:bottom-0 after:h-0.5 after:rounded-full hover:text-foreground focus-visible:rounded-md focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none data-[status=active]:text-foreground data-[status=active]:after:bg-primary [&_svg]:size-4"
          >
            <tab.icon />
            {tab.label}
          </Link>
        ))}
      </div>
    </nav>
  )
}
