import { useQuery } from '@tanstack/react-query'
import { CircleAlert, TriangleAlert } from 'lucide-react'
import type { Server } from '@/api/client'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { healthQuery } from '@/hooks/use-server-live'
import { overallHealth } from '@/lib/health'
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
 * The server's health as a small warning sign, where a word does not fit (the sidebar), with the reason in a tooltip
 * and for screen readers. It shows only when something is off: a row of green marks would say nothing that the status
 * dot does not, and a sign rather than a second dot keeps it apart from that dot.
 */
export function HealthDot({ server }: { server: Pick<Server, 'id' | 'socketState'> }) {
  const health = useLatestHealth(server)
  if (!health || (health.status !== 'Warn' && health.status !== 'Bad')) return null
  const Icon = health.status === 'Bad' ? CircleAlert : TriangleAlert
  return (
    <Tooltip>
      <TooltipTrigger render={<span className="flex shrink-0 items-center" />}>
        <Icon className={cn('size-3.5!', health.status === 'Bad' ? 'text-red-500' : 'text-amber-500')} aria-hidden />
        <span className="sr-only">{health.title}</span>
      </TooltipTrigger>
      <TooltipContent side="right" className="max-w-64 flex-col items-start gap-0.5">
        <span className="font-medium">{health.label}</span>
        <span>{health.summary}</span>
      </TooltipContent>
    </Tooltip>
  )
}
