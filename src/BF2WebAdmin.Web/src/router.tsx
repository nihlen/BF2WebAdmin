import type { QueryClient } from '@tanstack/react-query'
import { createRootRouteWithContext, createRoute, createRouter, Outlet, redirect } from '@tanstack/react-router'
import { api, isUnauthorized } from '@/api/client'
import { AppLayout } from '@/components/app-layout'
import { DashboardPage } from '@/pages/dashboard'
import { LoginPage } from '@/pages/login'
import { ServerPage } from '@/pages/server'
import { ServerAddPage, ServerEditPage } from '@/pages/server-settings'
import { ServerGroupPage } from '@/pages/server-group'

export const meQuery = api.queryOptions('get', '/api/auth/me')

const rootRoute = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: Outlet,
})

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
      await context.queryClient.ensureQueryData(meQuery)
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
    // React Query caches the data; the router only needs to run beforeLoad
    defaultPreloadStaleTime: 0,
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>
  }
}
