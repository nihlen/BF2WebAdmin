import { useQuery } from '@tanstack/react-query'
import type { Server } from '@/api/client'
import { healthQuery } from '@/hooks/use-server-live'
import { overallHealth, statusColors } from '@/lib/health'
import { cn } from '@/lib/utils'

/**
 * The health now of a connected server, for the sidebar. One request per server and half minute: the
 * last minute only, as the server page's hub updates reach only the page of its server.
 */
function useLatestHealth(server: Pick<Server, 'id' | 'socketState'>) {
  const connected = server.socketState === 'Connected'
  const health = useQuery({ ...healthQuery(server.id, 1), enabled: connected, refetchInterval: 30_000 })
  return connected ? overallHealth(health.data) : null
}

/**
 * The server's health as a dot only, where a word does not fit (the sidebar). It shows only when something is off: a
 * row of green dots would say nothing that the status dot does not.
 */
export function HealthDot({ server }: { server: Pick<Server, 'id' | 'socketState'> }) {
  const health = useLatestHealth(server)
  if (!health || health.status === 'Good') return null
  return (
    <span className="flex shrink-0 items-center" title={health.title}>
      <span className={cn('size-2 rounded-full ring-2 ring-sidebar', statusColors[health.status])} aria-hidden />
      <span className="sr-only">{health.title}</span>
    </span>
  )
}
