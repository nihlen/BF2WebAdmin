import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { toast } from 'sonner'
import { errorMessage, isUnauthorized } from '@/api/client'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { createAppRouter } from '@/router'
import 'flag-icons/css/flag-icons.min.css'
import './index.css'

// A 401 from any request means the login cookie is gone: go to the login page
const onError = (error: unknown) => {
  if (isUnauthorized(error)) {
    queryClient.clear()
    void router.navigate({ to: '/login' })
  }
}

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError }),
  mutationCache: new MutationCache({
    onError: (error) => {
      onError(error)
      if (!isUnauthorized(error)) toast.error(errorMessage(error))
    },
  }),
  defaultOptions: {
    queries: {
      // Queries the hub keeps current (servers, server snapshots) set staleTime: Infinity themselves
      staleTime: 30_000,
      retry: (failureCount, error) => !isUnauthorized(error) && failureCount < 2,
    },
  },
})

const router = createAppRouter(queryClient)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <RouterProvider router={router} />
        <Toaster position="bottom-right" />
      </TooltipProvider>
      <ReactQueryDevtools buttonPosition="bottom-left" />
    </QueryClientProvider>
  </StrictMode>,
)
