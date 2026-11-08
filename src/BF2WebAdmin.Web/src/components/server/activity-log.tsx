import { useQuery } from '@tanstack/react-query'
import type { CommandLogEntry } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { commandLogQuery } from '@/hooks/use-server-live'
import { cn } from '@/lib/utils'
import { LogList, Time } from './log-list'

const kindLabels: Record<CommandLogEntry['kind'], string> = {
  Chat: 'chat',
  Command: 'command',
  Rcon: 'rcon',
  Action: 'action',
}

/**
 * What web users sent to this server and the answers, from every browser. WebAdmin keeps the last entries in memory,
 * so the list starts empty after WebAdmin restarts.
 */
export function ActivityLog({ serverId }: { serverId: string }) {
  // Fetched and kept current by useServerLive
  const log = useQuery({ ...commandLogQuery(serverId), staleTime: Infinity })

  return (
    <LogList className="h-[550px] space-y-3" items={log.data ?? []} empty="Nothing has been sent from the web UI since WebAdmin started">
      {(entry) => (
        <li key={entry.id} className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <Time value={entry.time} />
            <span className="font-medium">{entry.user}</span>
            <Badge variant={entry.success ? 'secondary' : 'destructive'}>{entry.success ? kindLabels[entry.kind] : `${kindLabels[entry.kind]} failed`}</Badge>
          </div>
          <pre className="whitespace-pre-wrap">{entry.command}</pre>
          {entry.response && (
            <pre className={cn('border-l-2 pl-2 whitespace-pre-wrap text-muted-foreground', !entry.success && 'border-destructive text-destructive')}>
              {entry.response}
            </pre>
          )}
        </li>
      )}
    </LogList>
  )
}
