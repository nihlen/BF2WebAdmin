import { ArrowDown } from 'lucide-react'
import { useLayoutEffect, useRef, useState } from 'react'
import { formatTime } from '@/lib/format'
import { cn } from '@/lib/utils'

/**
 * A scrolling list that stays at the bottom while new items arrive, unless the user scrolled up. With itemKey, a list
 * scrolled up counts the items that arrived since and offers a jump back down, so nothing new goes unseen.
 */
export function LogList<T>({
  items,
  className,
  empty,
  itemKey,
  children,
}: {
  items: T[]
  className?: string
  /** Shown when there are no items */
  empty?: string
  /** A stable key per item, to count the new ones while scrolled up (the list is capped, so not by its length) */
  itemKey?: (item: T) => string
  children: (item: T, index: number) => React.ReactNode
}) {
  const ref = useRef<HTMLUListElement>(null)
  const atBottom = useRef(true)
  // The last item seen before the user scrolled up; null while at the bottom
  const [seenKey, setSeenKey] = useState<string | null>(null)

  useLayoutEffect(() => {
    if (atBottom.current && ref.current) ref.current.scrollTop = ref.current.scrollHeight
  }, [items])

  const seenIndex = seenKey === null || !itemKey ? -1 : items.findIndex((item) => itemKey(item) === seenKey)
  const unread = seenIndex < 0 ? 0 : items.length - 1 - seenIndex

  return (
    <ul
      ref={ref}
      // Focusable, so the keyboard can scroll it when its lines have nothing to focus
      tabIndex={0}
      className={cn('overflow-y-auto p-4 text-sm focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none focus-visible:ring-inset', className)}
      onScroll={(e) => {
        const el = e.currentTarget
        atBottom.current = el.scrollHeight - (el.scrollTop + el.clientHeight) < 30
        const last = items.at(-1)
        if (atBottom.current) setSeenKey(null)
        else if (seenKey === null && itemKey && last) setSeenKey(itemKey(last))
      }}
    >
      {items.length === 0 && empty && <li className="text-muted-foreground">{empty}</li>}
      {items.map(children)}
      {/* Sticky at the bottom of the visible part, so it needs no wrapper around the scrolling list */}
      {unread > 0 && (
        <li className="pointer-events-none sticky bottom-0 flex justify-center pt-2">
          <button
            type="button"
            className="pointer-events-auto inline-flex items-center gap-1.5 rounded-full bg-primary px-3 py-1 text-xs font-medium text-primary-foreground shadow-md focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            onClick={() => ref.current?.scrollTo({ top: ref.current.scrollHeight })}
          >
            <ArrowDown className="size-3.5" />
            {unread} new {unread === 1 ? 'message' : 'messages'}
          </button>
        </li>
      )}
    </ul>
  )
}

/** The time of a log entry; with the date when it is not today. */
export function Time({ value }: { value: string }) {
  return (
    <time dateTime={value} title={new Date(value).toLocaleString()} className="text-muted-foreground">
      {formatTime(value)}
    </time>
  )
}
