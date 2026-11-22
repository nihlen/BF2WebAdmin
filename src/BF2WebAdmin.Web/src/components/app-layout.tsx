import { HubConnectionState } from '@microsoft/signalr'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, useMatches, useNavigate } from '@tanstack/react-router'
import { ChevronDown, ChevronRight, CircleUser, LayoutDashboard, LogOut, Plus, Settings2 } from 'lucide-react'
import { Fragment, useEffect } from 'react'
import { api } from '@/api/client'
import { startHub, stopHub, useHubState } from '@/api/hub'
import { HealthDot } from '@/components/server/health-dot'
import { ServerStatusDot } from '@/components/server-status'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuGroup, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupAction,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
  useSidebar,
} from '@/components/ui/sidebar'
import { groupServers, useServerListUpdates, useServers } from '@/hooks/use-servers'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { serverTabs } from '@/lib/server-tabs'
import { cn } from '@/lib/utils'
import { meQuery, serverAddRoute, serverGroupRoute, serverRoute } from '@/router'

const hubRetryMs = 5000

// The router's Link marks the current page with data-status="active"
const activeLink = 'data-[status=active]:bg-sidebar-accent data-[status=active]:font-medium data-[status=active]:text-sidebar-accent-foreground'

export function AppLayout() {
  useHub()
  useServerListUpdates()

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <header className="flex h-12 items-center gap-2 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          <PageTrail />
          <HubStatus />
          <AccountMenu />
        </header>
        {/* SidebarInset is the page's main landmark already */}
        <div className="mx-auto w-full max-w-screen-2xl p-4 sm:p-6">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

/** Starts the hub connection, and retries until it is up. The hub reconnects by itself once it has been connected. */
function useHub() {
  const navigate = useNavigate()

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined
    let stopped = false

    const start = () =>
      startHub().catch((error: unknown) => {
        if (stopped) return
        if ((error as { statusCode?: number }).statusCode === 401) void navigate({ to: '/login' })
        else timer = setTimeout(start, hubRetryMs)
      })

    void start()
    return () => {
      stopped = true
      clearTimeout(timer)
    }
  }, [navigate])
}

function HubStatus() {
  const state = useHubState()
  if (state === HubConnectionState.Connected) return null

  // Without the hub, the pages show old data: say so where it is seen
  return (
    <span role="status" className="flex items-center gap-1.5 rounded-full bg-amber-500/15 px-2.5 py-0.5 text-xs font-medium text-amber-600 dark:text-amber-400">
      <span className="size-1.5 animate-pulse rounded-full bg-current" />
      {state === HubConnectionState.Disconnected ? 'Live updates offline, retrying' : 'Connecting live updates…'}
    </span>
  )
}

/** The signed-in user's name, with Sign out. */
function AccountMenu() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const me = useQuery(meQuery)
  const logout = api.useMutation('post', '/api/auth/logout', {
    onSuccess: async () => {
      await stopHub()
      queryClient.clear()
      await navigate({ to: '/login' })
    },
  })
  const name = me.data?.name ?? 'Account'

  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" className="shrink-0" aria-label={`Account: ${name}`} />}>
        <CircleUser data-icon="inline-start" />
        <span className="hidden max-w-32 truncate sm:inline">{name}</span>
        <ChevronDown data-icon="inline-end" className="text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-auto min-w-48">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            Signed in as <span className="text-foreground">{name}</span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled={logout.isPending} onClick={() => logout.mutate({})}>
          <LogOut />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

type Crumb = { label: string; link?: (props: { className: string; title: string; children: React.ReactNode }) => React.ReactNode }

/**
 * Where the page is: Servers › Group › server › tab on desktop, and on a phone, where the sidebar is hidden, the page's
 * own name (the server's name on a server page).
 */
function PageTrail() {
  const servers = useServers()
  const matches = useMatches()
  const ids = new Set<string>(matches.map((m) => m.routeId))
  const params = matches.at(-1)?.params as { serverId?: string; serverGroup?: string } | undefined

  const crumbs: Crumb[] = [{ label: 'Servers', link: (props) => <Link to="/" {...props} /> }]
  let title = 'Servers'
  if (ids.has(serverAddRoute.id)) crumbs.push({ label: (title = 'Add server') })
  if (ids.has(serverGroupRoute.id) && params?.serverGroup) crumbs.push({ label: (title = `Group ${params.serverGroup}`) })
  if (ids.has(serverRoute.id) && params?.serverId) {
    const { serverId } = params
    const server = servers.data?.find((s) => s.id === decodeServerId(serverId))
    const group = server?.serverGroup
    title = server?.name ?? decodeServerId(serverId)
    if (group) crumbs.push({ label: `Group ${group}`, link: (props) => <Link to="/servergroup/$serverGroup" params={{ serverGroup: group }} {...props} /> })
    crumbs.push({ label: title, link: (props) => <Link to="/server/$serverId" params={{ serverId }} {...props} /> })
    // The Live tab's path ends in a slash
    const path = matches.at(-1)?.fullPath.replace(/\/$/, '')
    const tab = serverTabs.find((t) => t.to === path)
    if (tab) crumbs.push({ label: tab.label })
  }

  return (
    <>
      <p className="min-w-0 flex-1 truncate text-sm font-medium md:hidden">{title}</p>
      <nav aria-label="Breadcrumb" className="hidden min-w-0 flex-1 md:block">
        <ol className="flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
          {crumbs.map((crumb, i) => {
            const last = i === crumbs.length - 1
            return (
              <Fragment key={crumb.label}>
                {i > 0 && <ChevronRight aria-hidden className="size-3.5 shrink-0 text-muted-foreground/60" />}
                <li className={cn('min-w-0 truncate', last ? 'shrink-0 font-medium text-foreground' : 'max-w-72')} aria-current={last ? 'page' : undefined}>
                  {crumb.link && !last ? crumb.link({ className: 'hover:text-foreground hover:underline', title: crumb.label, children: crumb.label }) : crumb.label}
                </li>
              </Fragment>
            )
          })}
        </ol>
      </nav>
    </>
  )
}

function AppSidebar() {
  const servers = useServers()
  // The phone's sidebar is a sheet opened by touch, where nothing hovers: its group buttons always show
  const { isMobile } = useSidebar()

  return (
    <Sidebar>
      <SidebarHeader className="px-4 py-3 font-semibold text-primary">BF2 Web Admin</SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton className={activeLink} render={<Link to="/" activeOptions={{ exact: true }} />}>
                <LayoutDashboard />
                Dashboard
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroup>

        {groupServers(servers.data ?? []).map(([group, groupServers]) => (
          <SidebarGroup key={group} className="group/server-group">
            <SidebarGroupLabel>Group {group}</SidebarGroupLabel>
            {/* The group's modules and admin levels. Shown on hover and focus where there is a mouse, always on touch */}
            <SidebarGroupAction
              render={<Link to="/servergroup/$serverGroup" params={{ serverGroup: group }} />}
              aria-label={`Settings of group ${group}`}
              title={`Settings of group ${group}`}
              className={cn(
                'text-sidebar-foreground/70 focus-visible:opacity-100 data-[status=active]:bg-sidebar-accent data-[status=active]:text-sidebar-accent-foreground data-[status=active]:opacity-100',
                !isMobile && '[@media(hover:hover)]:opacity-0 [@media(hover:hover)]:group-hover/server-group:opacity-100',
              )}
            >
              <Settings2 />
            </SidebarGroupAction>
            <SidebarGroupContent>
              <SidebarMenu>
                {groupServers.map((server) => (
                  <SidebarMenuItem key={server.id}>
                    <SidebarMenuButton
                      className={activeLink}
                      render={<Link to="/server/$serverId" params={{ serverId: encodeServerId(server.id) }} />}
                      tooltip={server.name}
                    >
                      <ServerStatusDot socketState={server.socketState} />
                      <span className="min-w-0 flex-1 truncate">{server.name}</span>
                      <HealthDot server={server} />
                      {/* In the button's flex row, not an absolute SidebarMenuBadge, so a long name truncates before the count */}
                      <span className="shrink-0 text-xs font-medium tabular-nums group-data-[collapsible=icon]:hidden">
                        {server.players}/{server.maxPlayers}
                      </span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}

        <SidebarGroup>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton className={activeLink} render={<Link to="/server/add" />}>
                <Plus className="text-primary" />
                Add server
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  )
}
