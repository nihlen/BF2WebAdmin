import { HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, useNavigate } from '@tanstack/react-router'
import { LayoutDashboard, LogOut, Plus } from 'lucide-react'
import { useEffect } from 'react'
import { api } from '@/api/client'
import { startHub, stopHub, useHubState } from '@/api/hub'
import { ServerStatusDot } from '@/components/server-status'
import { Button } from '@/components/ui/button'
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { groupServers, useServerListUpdates, useServers } from '@/hooks/use-servers'
import { encodeServerId } from '@/lib/server-id'

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
          <div className="flex-1" />
          <HubStatus />
          <LogoutButton />
        </header>
        <main className="mx-auto w-full max-w-screen-2xl p-6">
          <Outlet />
        </main>
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

function LogoutButton() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const logout = api.useMutation('post', '/api/auth/logout', {
    onSuccess: async () => {
      await stopHub()
      queryClient.clear()
      await navigate({ to: '/login' })
    },
  })

  return (
    <Button variant="ghost" size="sm" onClick={() => logout.mutate({})} disabled={logout.isPending}>
      <LogOut data-icon="inline-start" />
      Logout
    </Button>
  )
}

function AppSidebar() {
  const servers = useServers()

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
          <SidebarGroup key={group}>
            <SidebarGroupLabel render={<Link to="/servergroup/$serverGroup" params={{ serverGroup: group }} />}>
              {group}
            </SidebarGroupLabel>
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
                      <span className="truncate">{server.name}</span>
                    </SidebarMenuButton>
                    <SidebarMenuBadge>
                      {server.players}/{server.maxPlayers}
                    </SidebarMenuBadge>
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
