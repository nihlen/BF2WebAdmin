import type { QueryClient } from '@tanstack/react-query'
import { createRootRouteWithContext, createRoute, createRouter, type ErrorComponentProps, Outlet, redirect, useRouter } from '@tanstack/react-router'
import { api, errorMessage, isUnauthorized } from '@/api/client'
import { AppLayout } from '@/components/app-layout'
import { Button } from '@/components/ui/button'
import { DashboardPage } from '@/pages/dashboard'
import { LoginPage } from '@/pages/login'
import { ServerPage } from '@/pages/server'
import { ServerAddPage, ServerEditPage } from '@/pages/server-settings'
import { ServerGroupPage } from '@/pages/server-group'

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

const serverAddRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/server/add',
  component: ServerAddPage,
})

export const serverRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/server/$serverId',
  component: ServerPage,
})

export const serverEditRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/server/$serverId/edit',
  component: ServerEditPage,
})

export const serverGroupRoute = createRoute({
  getParentRoute: () => appRoute,
  path: '/servergroup/$serverGroup',
  component: ServerGroupPage,
})

const routeTree = rootRoute.addChildren([
  loginRoute,
  appRoute.addChildren([dashboardRoute, serverAddRoute, serverRoute, serverEditRoute, serverGroupRoute]),
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
}
