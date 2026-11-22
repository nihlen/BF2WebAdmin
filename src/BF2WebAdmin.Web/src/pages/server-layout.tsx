import { HubConnectionState } from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { Link, Outlet, useMatches, useMatchRoute } from '@tanstack/react-router'
import { ChevronDown, ChevronRight, ChevronUp, Copy, Ellipsis, Loader2, Map as MapIcon, Pause, Play, Power, RotateCcw, Settings, Settings2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { errorMessage, type Server, type ServerSnapshot } from '@/api/client'
import { useHubState } from '@/api/hub'
import { useConfirm } from '@/components/confirm-dialog'
import { CopyButton } from '@/components/copy-button'
import { Separated } from '@/components/separated'
import { ChangeMapDialog } from '@/components/server/change-map-dialog'
import { HealthChip } from '@/components/server/health-panel'
import { ServerPageContext, useServerPage } from '@/components/server/server-context'
import { SetupStrip } from '@/components/setup-checklist'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useIsMobile, useIsWide } from '@/hooks/use-mobile'
import { useServerActions } from '@/hooks/use-server-actions'
import { healthQuery, useServerLive } from '@/hooks/use-server-live'
import { useServers } from '@/hooks/use-servers'
import { usePendingActions } from '@/lib/pending-actions'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { connectionText, serverStatus, toneDot, toneText } from '@/lib/server-state'
import { serverTabs } from '@/lib/server-tabs'
import { cn } from '@/lib/utils'
import { serverRoute } from '@/router'

/**
 * Every page of a server: one header (name, status and health on a line, the facts under it on desktop, and the same
 * action group at its end on every tab), the server's pages as tabs (at the top on desktop, a bar at the bottom on a
 * phone), and a one-line setup reminder while a required step is open. The server's live data is subscribed here,
 * once, for as long as any of its pages is open.
 *
 * Some pages fill the window instead of scrolling, so what matters stays in view: the 3D pages (route
 * staticData.fullHeight) on desktop, and a connected server's Live tab on desktop and phone, whose chat box would
 * otherwise be below the fold.
 */
export function ServerLayout() {
  const serverId = decodeServerId(serverRoute.useParams().serverId)
  const live = useServerLive(serverId)
  const servers = useServers()
  const matchRoute = useMatchRoute()
  const fullHeight = useMatches({ select: (matches) => matches.some((m) => m.staticData.fullHeight) })
  // The server list knows the server before its snapshot has loaded, so the header shows at once
  const server = live.snapshot.data?.server ?? servers.data?.find((s) => s.id === serverId)
  const snapshot = live.snapshot.data
  const connected = server?.socketState === 'Connected'
  const params = { serverId: encodeServerId(serverId) }
  const onLive = !!matchRoute({ to: '/server/$serverId', params })
  // An offline server's Live tab is the connect card, a page that scrolls
  const liveFill = onLive && connected && !!snapshot
  // Settings has the full checklist, and an offline server's Live tab the connect card with the steps in it
  const setupElsewhere = !!matchRoute({ to: '/server/$serverId/edit', params }) || (!connected && onLive)

  return (
    <ServerPageContext value={{ ...live, serverId }}>
      {/* A page that fills the window: the app bar is 3rem and the page's padding 2rem on a phone, 3rem from sm up. On a
          phone the page ends above the bottom tab bar (the padding at the end) */}
      <div
        className={cn(
          'flex flex-col gap-4 pb-[calc(3.5rem+env(safe-area-inset-bottom))] md:pb-0',
          (fullHeight || liveFill) && 'lg:h-[calc(100svh-6rem)] lg:min-h-[640px]',
          liveFill && 'max-md:min-h-[560px] max-sm:h-[calc(100svh-5rem)] sm:max-md:h-[calc(100svh-6rem)]',
        )}
      >
        {server && <title>{`BF2 Web Admin - ${server.name}`}</title>}
        <div className="flex flex-wrap items-start gap-x-4 gap-y-3 max-md:flex-nowrap max-md:items-center">
          {server ? <ServerHeader server={server} /> : <Skeleton className="h-12 flex-1" />}
          {server && <ServerActions server={server} snapshot={connected ? snapshot : undefined} onLive={onLive} />}
        </div>
        <ServerTabBar serverId={serverId} />
        {server && !setupElsewhere && <SetupStrip server={server} />}
        <HubAlert />
        {live.snapshot.isError ? (
          <p className="text-destructive">{errorMessage(live.snapshot.error)}</p>
        ) : (
          <div className={cn('flex min-w-0 flex-col', (fullHeight || liveFill) && 'lg:min-h-0 lg:flex-1', liveFill && 'max-md:min-h-0 max-md:flex-1')}>
            <Outlet />
          </div>
        )}
        <PhoneTabBar serverId={serverId} />
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
 * The name, the status and the health on one line (on desktop the status wraps under a long name; on a phone the name
 * truncates, as the app bar above has it whole). On desktop a quiet second line has the map, players, address and
 * group; on a phone the address and group are in the actions menu. The health chip leads to the Health tab.
 */
function ServerHeader({ server }: { server: Server }) {
  const { serverId } = useServerPage()
  const status = serverStatus(server)
  const connected = server.socketState === 'Connected'
  const address = `${server.ipAddress}:${server.gamePort}`
  // The chip has nothing to say without a health window; then no empty link either
  const hasHealth = !!useQuery({ ...healthQuery(serverId), staleTime: Infinity }).data?.latest

  return (
    <header className="min-w-0 flex-[1_1_18rem] space-y-1">
      <div className="flex min-w-0 items-center gap-x-3 gap-y-1 max-sm:gap-x-2 md:flex-wrap">
        <h1 className="max-w-full min-w-0 truncate text-lg font-semibold sm:text-2xl" title={server.name}>
          {server.name}
        </h1>
        <span className="flex shrink-0 items-center gap-2 text-sm">
          <span className={cn('inline-flex items-center gap-1.5 font-medium whitespace-nowrap', toneText[status.tone])} title={connectionText(server)}>
            <span className={cn('size-2 rounded-full', toneDot[status.tone])} />
            {status.label}
          </span>
          {/* Problems show on every tab; the chip leads to the details */}
          {connected && hasHealth && (
            <Link
              to="/server/$serverId/health"
              params={{ serverId: encodeServerId(serverId) }}
              title="Open the Health tab"
              className="group/health inline-flex items-center rounded-full pr-1 transition-colors hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              <HealthChip serverId={serverId} connected={connected} compact />
              <ChevronRight aria-hidden className="size-3.5 text-muted-foreground group-hover/health:text-foreground" />
            </Link>
          )}
        </span>
      </div>
      <p className="hidden flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground md:flex">
        <Separated>
          {connected && server.map && <span>{server.map}</span>}
          {connected && (
            <span className="tabular-nums">
              {server.players}/{server.maxPlayers} players
            </span>
          )}
          <span className="inline-flex items-center gap-0.5">
            <span className="font-mono text-xs">{address}</span>
            <CopyButton value={address} label="Copy the address" />
          </span>
          <Link to="/servergroup/$serverGroup" params={{ serverGroup: server.serverGroup }} className="hover:text-foreground hover:underline">
            Group {server.serverGroup}
          </Link>
        </Separated>
      </p>
    </header>
  )
}

/**
 * The round and server actions, in the same place on every tab. From md up: a split button, Restart round… with Pause
 * or Resume and Change map… in its menu, and a "⋯" button with the server actions: Settings, Copy address, the group's
 * page and, apart at the end, Restart server, which disconnects everyone. On desktop Live, where there is room, Pause
 * or Resume is a button of its own before them (the header keeps its height). On a phone all of them are one "⋯" menu
 * beside the name, so the header is one line. The round actions and Restart server need the connection (the snapshot
 * is given then); the rest are there while offline too. Every action shows its progress until the game confirms it.
 */
function ServerActions({ server, snapshot, onLive }: { server: Server; snapshot: ServerSnapshot | undefined; onLive: boolean }) {
  const { serverId } = useServerPage()
  const actions = useServerActions(serverId, snapshot)
  const { confirm, dialog } = useConfirm()
  const pending = usePendingActions(serverId)
  const mobile = useIsMobile()
  const wide = useIsWide()
  const [mapsOpen, setMapsOpen] = useState(false)
  const params = { serverId: encodeServerId(serverId) }
  const address = `${server.ipAddress}:${server.gamePort}`
  const players = snapshot?.players.length ?? 0
  const paused = snapshot?.server.gameState === 'Paused'
  const pauseApart = onLive && wide
  const playersText = players === 0 ? 'No players are online.' : `${players} ${players === 1 ? 'player is' : 'players are'} online.`

  // A key ending in a colon stands for all of its kind, such as map: for a change to any map
  const busy = (...keys: string[]) => [...pending].some((key) => keys.some((k) => (k.endsWith(':') ? key.startsWith(k) : key === k)))

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
  const copyAddress = () =>
    navigator.clipboard.writeText(address).then(
      () => toast.success(`Copied ${address}`),
      () => toast.error('Could not copy the address'),
    )

  const pauseItem = (
    <DropdownMenuItem disabled={busy('pause')} onClick={() => void actions.togglePause()}>
      {paused ? <Play /> : <Pause />}
      {paused ? 'Resume' : 'Pause'}
    </DropdownMenuItem>
  )
  const changeMapItem = (
    <DropdownMenuItem onClick={() => setMapsOpen(true)}>
      <MapIcon />
      Change map…
    </DropdownMenuItem>
  )
  const serverItems = (
    <>
      <DropdownMenuItem render={<Link to="/server/$serverId/edit" params={params} />}>
        <Settings />
        Settings
      </DropdownMenuItem>
      <DropdownMenuItem onClick={() => void copyAddress()}>
        <Copy />
        Copy {address}
      </DropdownMenuItem>
      <DropdownMenuItem render={<Link to="/servergroup/$serverGroup" params={{ serverGroup: server.serverGroup }} />}>
        <Settings2 />
        Group {server.serverGroup} page
      </DropdownMenuItem>
      {snapshot && (
        <>
          <DropdownMenuSeparator />
          <DropdownMenuItem variant="destructive" disabled={busy('restart-server')} onClick={restartServer}>
            <Power />
            Restart server…
          </DropdownMenuItem>
        </>
      )}
    </>
  )
  const dialogs = (
    <>
      {snapshot && <ChangeMapDialog open={mapsOpen} onOpenChange={setMapsOpen} serverId={serverId} snapshot={snapshot} actions={actions} />}
      {dialog}
    </>
  )

  if (mobile)
    return (
      <div className="shrink-0">
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="outline" size="icon" aria-label={snapshot ? 'Round and server actions' : 'Server actions'} />}>
            {busy('restart-map', 'pause', 'next-map', 'map:', 'restart-server') ? <Loader2 className="animate-spin" /> : <Ellipsis />}
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-auto min-w-52">
            {snapshot && (
              <>
                <DropdownMenuGroup>
                  <DropdownMenuLabel>Round</DropdownMenuLabel>
                  <DropdownMenuItem disabled={busy('restart-map')} onClick={restartRound}>
                    <RotateCcw />
                    Restart round…
                  </DropdownMenuItem>
                  {pauseItem}
                  {changeMapItem}
                </DropdownMenuGroup>
                <DropdownMenuSeparator />
              </>
            )}
            <DropdownMenuGroup>
              <DropdownMenuLabel>Server</DropdownMenuLabel>
              {serverItems}
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
        {dialogs}
      </div>
    )

  return (
    <div className="flex shrink-0 items-center gap-1.5">
      {snapshot && pauseApart && (
        <Button variant="outline" disabled={busy('pause')} onClick={() => void actions.togglePause()}>
          {busy('pause') ? <Loader2 data-icon="inline-start" className="animate-spin" /> : paused ? <Play data-icon="inline-start" /> : <Pause data-icon="inline-start" />}
          {paused ? 'Resume' : 'Pause'}
        </Button>
      )}
      {snapshot && (
        <div className="flex" role="group" aria-label="Round actions">
          <Button variant="outline" className="rounded-r-none" disabled={busy('restart-map')} onClick={restartRound}>
            {busy('restart-map') ? <Loader2 data-icon="inline-start" className="animate-spin" /> : <RotateCcw data-icon="inline-start" />}
            Restart round…
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <Button
                  variant="outline"
                  size="icon"
                  className="-ml-px rounded-l-none"
                  aria-label={pauseApart ? 'More round actions: change map' : paused ? 'More round actions: resume, change map' : 'More round actions: pause, change map'}
                />
              }
            >
              {busy(...(pauseApart ? [] : ['pause']), 'next-map', 'map:') ? <Loader2 className="animate-spin" /> : <ChevronDown />}
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-auto min-w-44">
              {!pauseApart && pauseItem}
              {changeMapItem}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}
      {/* Apart from the round actions, so the action that disconnects everyone is never a stray click */}
      <DropdownMenu>
        <Tooltip>
          <TooltipTrigger render={<DropdownMenuTrigger render={<Button variant="outline" size="icon" aria-label="Server actions" />} />}>
            {busy('restart-server') ? <Loader2 className="animate-spin" /> : <Ellipsis />}
          </TooltipTrigger>
          <TooltipContent side="bottom">Server actions</TooltipContent>
        </Tooltip>
        <DropdownMenuContent align="end" className="w-auto min-w-52">
          {serverItems}
        </DropdownMenuContent>
      </DropdownMenu>
      {dialogs}
    </div>
  )
}

// The tabs that stay in the row when it is narrow; the others go in the More menu
const alwaysShown = 3

const tabClass =
  'relative flex shrink-0 items-center gap-1.5 px-2 py-2.5 sm:px-2.5 text-sm font-medium whitespace-nowrap text-muted-foreground transition-colors after:absolute after:inset-x-1 after:bottom-0 after:h-0.5 after:rounded-full hover:text-foreground focus-visible:rounded-md focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none data-[status=active]:text-foreground data-[status=active]:after:bg-primary [&_svg]:size-4'

/**
 * The server's pages as a row of links, from md up (a phone has the bottom bar), the same on every tab. Where the row
 * is too narrow for all of them (a desktop window with the sidebar), the first three stay and the rest are in a More
 * menu, which shows the current page's name when it is one of them.
 */
function ServerTabBar({ serverId }: { serverId: string }) {
  const matchRoute = useMatchRoute()
  const params = { serverId: encodeServerId(serverId) }
  const more = serverTabs.slice(alwaysShown)
  const current = more.find((tab) => matchRoute({ to: tab.to, params }))

  return (
    <nav aria-label="Server pages" className="@container hidden border-b md:block">
      <div className="flex gap-1">
        {serverTabs.map((tab, i) => (
          <Link key={tab.to} to={tab.to} params={params} activeOptions={{ exact: 'exact' in tab }} className={cn(tabClass, i >= alwaysShown && 'hidden @3xl:flex')}>
            <tab.icon />
            {tab.label}
          </Link>
        ))}
        <DropdownMenu>
          <DropdownMenuTrigger
            className={cn(tabClass, 'cursor-pointer @3xl:hidden')}
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

const phoneTabClass =
  'relative flex min-w-0 flex-col items-center justify-center gap-0.5 text-[11px] font-medium text-muted-foreground transition-colors before:absolute before:inset-x-5 before:top-0 before:h-0.5 before:rounded-full focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none focus-visible:ring-inset data-[status=active]:text-foreground data-[status=active]:before:bg-primary [&_svg]:size-5'

/**
 * Below md the server's pages are a bar fixed to the bottom of the screen, where a thumb reaches: Live, Health, 3D,
 * Builder, and More with Bans, Activity, Group and Settings. While one of those is open, the More slot is named after it with a
 * small chevron and lit, so the bar says where you are and the slot still reads as a menu. It keeps clear of the
 * phone's home indicator (the safe area).
 */
function PhoneTabBar({ serverId }: { serverId: string }) {
  const matchRoute = useMatchRoute()
  const params = { serverId: encodeServerId(serverId) }
  const more = serverTabs.filter((tab) => !tab.phone)
  const current = more.find((tab) => matchRoute({ to: tab.to, params }))

  return (
    <nav aria-label="Server pages" className="fixed inset-x-0 bottom-0 z-40 border-t bg-background/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden">
      <div className="grid h-14 grid-cols-5">
        {serverTabs
          .filter((tab) => tab.phone)
          .map((tab) => (
            <Link key={tab.to} to={tab.to} params={params} activeOptions={{ exact: 'exact' in tab }} className={phoneTabClass}>
              <tab.icon aria-hidden />
              {tab.short}
            </Link>
          ))}
        <DropdownMenu>
          <DropdownMenuTrigger
            className={cn(phoneTabClass, 'cursor-pointer')}
            data-status={current ? 'active' : undefined}
            aria-label={current ? `${current.label}, more pages` : 'More pages'}
          >
            {current ? <current.icon aria-hidden /> : <Ellipsis aria-hidden />}
            <span className="flex max-w-full items-center gap-0.5">
              <span className="truncate">{current?.short ?? 'More'}</span>
              {current && <ChevronUp aria-hidden className="size-3! shrink-0" />}
            </span>
          </DropdownMenuTrigger>
          <DropdownMenuContent side="top" align="end" sideOffset={8} className="w-auto min-w-44">
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
