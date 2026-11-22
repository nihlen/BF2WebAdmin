import { useQuery } from '@tanstack/react-query'
import type { Server } from '@/api/client'
import { healthQuery } from '@/hooks/use-server-live'
import { chipLabels, healthTitle, statusColors } from '@/lib/health'
import { cn } from '@/lib/utils'

/**
 * The latest health window of a connected server, for the pages that list servers. One request per server and half
 * minute: the last minute only, as the server page's hub updates reach only the page of its server.
 */
function useLatestHealth(server: Pick<Server, 'id' | 'socketState'>) {
  const connected = server.socketState === 'Connected'
  const health = useQuery({ ...healthQuery(server.id, 1), enabled: connected, refetchInterval: 30_000 })
  if (!connected || !health.data?.latest) return null
  const { latest, connections } = health.data
  const troubled = (connections?.players ?? []).filter((p) => p.status !== 'Good')
  const players = troubled.length === 0 ? '' : ` ${troubled.length} ${troubled.length === 1 ? 'player has' : 'players have'} a poor connection.`
  return { status: latest.status, title: `Health: ${chipLabels[latest.status]}. ${healthTitle(latest)}.${players}` }
}

/**
 * The server's health as a dot and a word ("Healthy", "Warning", "Problems"), as in the server's header. Nothing for
 * an offline server, or one whose module sends no health windows.
 */
export function HealthBadge({ server, className }: { server: Pick<Server, 'id' | 'socketState'>; className?: string }) {
  const health = useLatestHealth(server)
  if (!health) return null
  return (
    <span className={cn('inline-flex h-5 shrink-0 items-center gap-1.5 rounded-full border px-2 text-xs font-medium whitespace-nowrap', className)} title={health.title}>
      <span className={cn('size-2 shrink-0 rounded-full', statusColors[health.status])} aria-hidden />
      {chipLabels[health.status]}
    </span>
  )
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
