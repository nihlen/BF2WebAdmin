import type { QueryClient } from '@tanstack/react-query'
import {
  createRootRouteWithContext,
  createRoute,
  createRouter,
  type ErrorComponentProps,
  lazyRouteComponent,
  Outlet,
  redirect,
  useRouter,
} from '@tanstack/react-router'
import { api, errorMessage, isUnauthorized } from '@/api/client'
import { AppLayout } from '@/components/app-layout'
import { Button } from '@/components/ui/button'
import { AboutPage } from '@/pages/about'
import { DashboardPage } from '@/pages/dashboard'
import { LoginPage } from '@/pages/login'
import { ServerActivityPage, ServerBansPage, ServerHealthPage, ServerLivePage } from '@/pages/server'
import { ServerGroupPage, ServerGroupTabPage } from '@/pages/server-group'
import { ServerLayout } from '@/pages/server-layout'
import { ServerAddPage, ServerEditPage } from '@/pages/server-settings'

export const meQuery = api.queryOptions('get', '/api/auth/me')

const rootRoute = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: Outlet,
})

/** Shown while a route's beforeLoad runs longer than a moment, such as the login check on a slow or stopped server. */
function PendingPage() {
  return <div className="flex min-h-svh items-center justify-center text-muted-foreground">Connecting to the server…</div>
}

/** A route that could not load, such as the login check failing because the server is down. */
function ErrorPage({ error, reset }: ErrorComponentProps) {
  const router = useRouter()
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-center">
      <p className="text-lg font-medium">The server could not be reached</p>
      <p className="text-sm text-muted-foreground">{errorMessage(error)}</p>
      <Button
        onClick={() => {
          reset()
          void router.invalidate()
        }}
      >
        Try again
      </Button>
    </div>
  )
}

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  component: LoginPage,
})

// Every other page needs a login
const appRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: 'app',
  component: AppLayout,
  beforeLoad: async ({ context }) => {
    try {
      // No retries: when the server is down, show the error page instead of waiting through them
      await context.queryClient.ensureQueryData({ ...meQuery, retry: false })
    } catch (error) {
      if (isUnauthorized(error)) throw redirect({ to: '/login' })
      throw error
    }
  },
})

const dashboardRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/',
  component: DashboardPage,
})

export const serverAddRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/server/add',
  component: ServerAddPage,
})

// Every page of a server shares one header and tab bar (ServerLayout), which keeps the server's live data subscribed
export const serverRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/server/$serverId',
  component: ServerLayout,
})

export const serverLiveRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/',
  component: ServerLivePage,
})

export const serverHealthRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/health',
  component: ServerHealthPage,
})

export const serverBansRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/bans',
  component: ServerBansPage,
})

export const serverActivityRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/activity',
  component: ServerActivityPage,
})

// The group's modules and admin levels, as on the group's own page
export const serverGroupTabRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/group',
  component: ServerGroupTabPage,
})

// The settings: "edit" as before, so links to it keep working
export const serverEditRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/edit',
  component: ServerEditPage,
})

// The 3D viewer brings three.js and its models: loaded when opened. It fills the window below the header on desktop
export const viewerRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/viewer',
  component: lazyRouteComponent(() => import('@/pages/viewer'), 'ViewerPage'),
  staticData: { fullHeight: true },
})

// The map builder: three.js too, loaded when opened
export const builderRoute = createRoute({
  getParentRoute: () => serverRoute,
  path: '/builder',
  component: lazyRouteComponent(() => import('@/pages/builder'), 'BuilderPage'),
  staticData: { fullHeight: true },
})

export const serverGroupRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/servergroup/$serverGroup',
  component: ServerGroupPage,
})

// WebAdmin's admin channel and version, from the account menu
export const aboutRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/about',
  component: AboutPage,
})

const routeTree = rootRoute.addChildren([
  loginRoute,
  appRoute.addChildren([
    dashboardRoute,
    serverAddRoute,
    serverRoute.addChildren([serverLiveRoute, serverHealthRoute, serverBansRoute, serverActivityRoute, serverGroupTabRoute, serverEditRoute, viewerRoute, builderRoute]),
    serverGroupRoute,
    aboutRoute,
  ]),
])

export function createAppRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree,
    context: { queryClient },
    defaultPreload: 'intent',
    defaultPendingComponent: PendingPage,
    defaultErrorComponent: ErrorPage,
    // React Query caches the data; the router only needs to run beforeLoad
    defaultPreloadStaleTime: 0,
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>
  }
  interface StaticDataRouteOption {
    /** The page fills the window below the server header on desktop (a 3D view), instead of scrolling */
    fullHeight?: boolean
  }
}
