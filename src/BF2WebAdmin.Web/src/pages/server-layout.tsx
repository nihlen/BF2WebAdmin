import { HubConnectionState } from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { Link, Outlet, useMatches, useMatchRoute } from '@tanstack/react-router'
import { ChevronDown, Ellipsis, Loader2, Pause, Play, Power, RotateCcw } from 'lucide-react'
import { errorMessage, type Server, type ServerSnapshot } from '@/api/client'
import { useHubState } from '@/api/hub'
import { useConfirm } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { HealthChip } from '@/components/server/health-panel'
import { ServerPageContext, useServerPage } from '@/components/server/server-context'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { type ServerActions, useServerActions } from '@/hooks/use-server-actions'
import { healthQuery, useServerLive } from '@/hooks/use-server-live'
import { useServers } from '@/hooks/use-servers'
import { usePendingActions } from '@/lib/pending-actions'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { serverStatus, toneDot, toneText } from '@/lib/server-state'
import { serverTabs } from '@/lib/server-tabs'
import { cn } from '@/lib/utils'
import { serverRoute } from '@/router'

/**
 * Every page of a server: one header (name, status, the round controls) and a tab bar of the server's pages. The
 * server's live data is subscribed here, once, for as long as any of its pages is open.
 *
 * The 3D pages (route staticData.fullHeight) get a compact header on desktop: the name and status on one line, the
 * round controls in a menu, and on a wide screen the tabs beside them, so the 3D view keeps the window's height.
 */
export function ServerLayout() {
  const serverId = decodeServerId(serverRoute.useParams().serverId)
  const live = useServerLive(serverId)
  const servers = useServers()
  const compact = useMatches({ select: (matches) => matches.some((m) => m.staticData.fullHeight) })
  // The server list knows the server before its snapshot has loaded, so the header shows at once
  const server = live.snapshot.data?.server ?? servers.data?.find((s) => s.id === serverId)
  const snapshot = live.snapshot.data

  return (
    <ServerPageContext value={{ ...live, serverId }}>
      {/* A page with a 3D view fills the window below the header on desktop: the app bar is 3rem, the padding 3rem */}
      <div className={cn('flex flex-col gap-4', compact && 'lg:h-[calc(100svh-6rem)] lg:min-h-[640px] lg:gap-3')}>
        {server && <title>{`BF2 Web Admin - ${server.name}`}</title>}
        <div
          className={cn(
            'flex flex-wrap items-start justify-between gap-x-6 gap-y-4',
            compact && 'lg:items-center lg:gap-y-2 xl:flex-nowrap xl:items-end xl:gap-x-4 xl:border-b',
          )}
        >
          {server ? <ServerHeader server={server} compact={compact} /> : <Skeleton className={cn('h-24 flex-1', compact && 'lg:h-10')} />}
          {server?.socketState === 'Connected' && snapshot && <RoundControls serverId={serverId} snapshot={snapshot} compact={compact} />}
          <ServerTabBar serverId={serverId} compact={compact} />
        </div>
        <HubAlert />
        {live.snapshot.isError ? (
          <p className="text-destructive">{errorMessage(live.snapshot.error)}</p>
        ) : (
          <div className={cn('flex min-w-0 flex-col', compact && 'lg:min-h-0 lg:flex-1')}>
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

/**
 * The name (two lines at most), one coloured status and the facts after it. Compact (the 3D pages on desktop): the
 * name on one line and the status only; the breadcrumb in the app bar still names the group.
 */
function ServerHeader({ server, compact }: { server: Server; compact: boolean }) {
  const { serverId } = useServerPage()
  const status = serverStatus(server)
  const connected = server.socketState === 'Connected'
  const address = `${server.ipAddress}:${server.gamePort}`
  // The chip has nothing to say without a health window; then no empty link either
  const hasHealth = !!useQuery({ ...healthQuery(serverId), staleTime: Infinity }).data?.latest
  // The facts that the compact header leaves out
  const fact = cn('contents', compact && 'lg:hidden')

  return (
    <header className={cn('min-w-0 flex-[1_1_20rem] space-y-1.5', compact && 'lg:flex lg:flex-[0_1_auto] lg:items-center lg:gap-3 lg:space-y-0 xl:pb-2.5')}>
      <h1
        className={cn('line-clamp-2 text-2xl font-semibold break-words sm:text-3xl', compact && 'lg:line-clamp-1 lg:max-w-80 lg:text-lg lg:break-all')}
        title={server.name}
      >
        {server.name}
      </h1>
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
        <span className={cn('inline-flex items-center gap-1.5 font-medium whitespace-nowrap', toneText[status.tone])} title={connected ? 'The admin channel is connected' : 'The admin channel is not connected'}>
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
          <span className={fact}>
            <Dot />
            <span>{server.map}</span>
          </span>
        )}
        {connected && (
          <span className={fact}>
            <Dot />
            <span className="tabular-nums">
              {server.players}/{server.maxPlayers} players
            </span>
          </span>
        )}
        <span className={fact}>
          <Dot />
          <span className="inline-flex items-center gap-0.5">
            <span className="font-mono text-xs">{address}</span>
            <CopyButton value={address} label="Copy the address" />
          </span>
        </span>
        <span className={fact}>
          <Dot />
          <Link to="/servergroup/$serverGroup" params={{ serverGroup: server.serverGroup }} className="hover:text-foreground hover:underline">
            Group {server.serverGroup}
          </Link>
        </span>
      </div>
    </header>
  )
}

const Dot = () => (
  <span aria-hidden className="text-muted-foreground/50">
    ·
  </span>
)

/**
 * Restart round, pause and restart server. Each shows its progress until the game confirms it. As buttons, or in the
 * compact header on desktop as one labelled menu.
 */
function RoundControls({ serverId, snapshot, compact }: { serverId: string; snapshot: ServerSnapshot; compact: boolean }) {
  const actions: ServerActions = useServerActions(serverId, snapshot)
  const { confirm, dialog } = useConfirm()
  const pending = usePendingActions(serverId)
  const players = snapshot.players.length
  const paused = snapshot.server.gameState === 'Paused'
  const playersText = players === 0 ? 'No players are online.' : `${players} ${players === 1 ? 'player is' : 'players are'} online.`

  const busy = (key: string) => pending.has(key)
  const icon = (key: string, Icon: React.ComponentType<{ className?: string }>, className: string) =>
    busy(key) ? <Loader2 data-icon="inline-start" className="animate-spin" /> : <Icon data-icon="inline-start" className={className} />

  const restartRound = () =>
    confirm({
      title: 'Restart the round?',
      description: `The scores are reset and everyone respawns. ${playersText}`,
      confirmLabel: 'Restart round',
      onConfirm: () => void actions.restartMap(),
    })
  const restartServer = () =>
    confirm({
      title: 'Restart the game server?',
      description: (
        <>
          The server quits cleanly (console <code>quit</code>) and its container starts it again, which takes about a minute. {playersText} Everyone
          is disconnected. The container needs a restart policy (<code>always</code> or <code>unless-stopped</code>).
        </>
      ),
      confirmLabel: 'Restart server',
      destructive: true,
      onConfirm: () => void actions.restartServer(),
    })

  return (
    <>
      <div className={cn('flex flex-wrap gap-2', compact && 'lg:hidden')}>
        <Button variant="outline" disabled={busy('restart-map')} onClick={restartRound}>
          {icon('restart-map', RotateCcw, 'text-emerald-500')}
          Restart round
        </Button>
        <Button variant="outline" disabled={busy('pause')} onClick={() => void actions.togglePause()}>
          {paused ? icon('pause', Play, 'text-emerald-500') : icon('pause', Pause, 'text-amber-500')}
          {paused ? 'Resume' : 'Pause'}
        </Button>
        <Button variant="outline" disabled={busy('restart-server')} onClick={restartServer}>
          {icon('restart-server', Power, 'text-destructive')}
          Restart server
        </Button>
      </div>
      {compact && (
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="outline" className="hidden lg:inline-flex xl:order-last xl:mb-1.5" />}>
            {[...pending].some((key) => ['restart-map', 'pause', 'restart-server'].includes(key)) && <Loader2 data-icon="inline-start" className="animate-spin" />}
            Round and server
            <ChevronDown data-icon="inline-end" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-auto min-w-48">
            <DropdownMenuItem disabled={busy('restart-map')} onClick={restartRound}>
              <RotateCcw className="text-emerald-500" />
              Restart round
            </DropdownMenuItem>
            <DropdownMenuItem disabled={busy('pause')} onClick={() => void actions.togglePause()}>
              {paused ? <Play className="text-emerald-500" /> : <Pause className="text-amber-500" />}
              {paused ? 'Resume' : 'Pause'}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" disabled={busy('restart-server')} onClick={restartServer}>
              <Power />
              Restart server
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
      {dialog}
    </>
  )
}

// The tabs that stay in the row when it is narrow; the others go in the More menu
const alwaysShown = 3

const tabClass =
  'relative flex shrink-0 items-center gap-1.5 px-2 py-2.5 sm:px-2.5 text-sm font-medium whitespace-nowrap text-muted-foreground transition-colors after:absolute after:inset-x-1 after:bottom-0 after:h-0.5 after:rounded-full hover:text-foreground focus-visible:rounded-md focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none data-[status=active]:text-foreground data-[status=active]:after:bg-primary [&_svg]:size-4'

/**
 * The server's pages as a row of links. Where the row is too narrow for all seven (a phone, or a desktop window with the
 * sidebar), the first three stay and the rest are in a More menu, which shows the current page's name when it is one of
 * them: every page is one or two taps away, with nothing to discover by scrolling. Beside the compact header on a wide
 * screen the tabs leave out their icons, so all seven fit there.
 */
function ServerTabBar({ serverId, compact }: { serverId: string; compact: boolean }) {
  const matchRoute = useMatchRoute()
  const params = { serverId: encodeServerId(serverId) }
  const more = serverTabs.slice(alwaysShown)
  const current = more.find((tab) => matchRoute({ to: tab.to, params }))

  return (
    <nav aria-label="Server pages" className={cn('@container -mx-4 basis-full border-b sm:mx-0', compact && 'xl:mx-0 xl:min-w-0 xl:flex-1 xl:basis-auto xl:border-b-0')}>
      <div className="flex gap-1 px-4 sm:px-0">
        {serverTabs.map((tab, i) => (
          <Link
            key={tab.to}
            to={tab.to}
            params={params}
            activeOptions={{ exact: 'exact' in tab }}
            className={cn(tabClass, i >= alwaysShown && 'hidden @2xl:flex', i >= alwaysShown && compact && 'xl:@xl:flex', compact && 'xl:[&>svg]:hidden')}
          >
            <tab.icon />
            {tab.label}
          </Link>
        ))}
        <DropdownMenu>
          <DropdownMenuTrigger
            className={cn(tabClass, 'cursor-pointer @2xl:hidden', compact && 'xl:@xl:hidden')}
            data-status={current ? 'active' : undefined}
            aria-label={current ? `${current.label}, more pages` : 'More pages'}
          >
            {current ? <current.icon /> : <Ellipsis />}
            {current?.label ?? 'More'}
            <ChevronDown className="size-3.5!" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-auto min-w-44">
            {more.map((tab) => (
              <DropdownMenuItem key={tab.to} render={<Link to={tab.to} params={params} />} className={cn(tab === current && 'font-medium')}>
                <tab.icon />
                {tab.label}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </nav>
  )
}
