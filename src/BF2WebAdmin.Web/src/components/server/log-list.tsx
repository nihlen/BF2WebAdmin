import { useLayoutEffect, useRef } from 'react'
import { formatTime } from '@/lib/format'
import { cn } from '@/lib/utils'

/** A scrolling list that stays at the bottom while new items arrive, unless the user scrolled up. */
export function LogList<T>({
  items,
  className,
  empty,
  children,
}: {
  items: T[]
  className?: string
  /** Shown when there are no items */
  empty?: string
  children: (item: T, index: number) => React.ReactNode
}) {
  const ref = useRef<HTMLUListElement>(null)
  const atBottom = useRef(true)

  useLayoutEffect(() => {
    if (atBottom.current && ref.current) ref.current.scrollTop = ref.current.scrollHeight
  }, [items])

  return (
    <ul
      ref={ref}
      className={cn('overflow-y-auto p-4 text-sm', className)}
      onScroll={(e) => {
        const el = e.currentTarget
        atBottom.current = el.scrollHeight - (el.scrollTop + el.clientHeight) < 30
      }}
    >
      {items.length === 0 && empty && <li className="text-muted-foreground">{empty}</li>}
      {items.map(children)}
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
