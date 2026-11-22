import { formatFull, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

/** A time relative to now ("5 minutes ago"), with the time in full on hover and for screen readers' time element. */
export function RelativeTime({ value, className }: { value: string; className?: string }) {
  return (
    <time dateTime={value} title={formatFull(value)} className={cn(className)}>
      {formatRelative(value)}
    </time>
  )
}
