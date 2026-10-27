import type { SocketState } from '@/api/client'
import { cn } from '@/lib/utils'

/** Green when the server's admin channel is connected, red otherwise. */
export function ServerStatusDot({ socketState, className }: { socketState: SocketState; className?: string }) {
  const connected = socketState === 'Connected'
  return (
    <span
      title={connected ? 'Connected' : 'Disconnected'}
      className={cn('inline-block size-2 shrink-0 rounded-full', connected ? 'bg-emerald-500' : 'bg-red-500', className)}
    />
  )
}
