import { CircleAlert, TriangleAlert } from 'lucide-react'
import type { Server } from '@/api/client'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { overallHealth } from '@/lib/health'
import { cn } from '@/lib/utils'

/** The health now of a connected server, from the server list (a ServerUpdateEvent comes when its status changes). */
const latestHealth = (server: Pick<Server, 'socketState' | 'health'>) =>
  server.socketState === 'Connected' ? overallHealth(server.health ?? undefined) : null

/**
 * The server's health as a small warning sign, where a word does not fit (the sidebar), with the reason in a tooltip
 * and for screen readers. It shows only when something is off: a row of green marks would say nothing that the status
 * dot does not, and a sign rather than a second dot keeps it apart from that dot.
 */
export function HealthDot({ server }: { server: Pick<Server, 'socketState' | 'health'> }) {
  const health = latestHealth(server)
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
