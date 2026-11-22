import { cn } from '@/lib/utils'

// The UI's three kinds of switch, each with one look: the server's tab bar for navigation (server-layout.tsx), a
// segmented control for switching what a panel shows (this one, and the Tabs list, which it matches), and chips for
// filters and quick picks (Chip)

/**
 * A segmented control: one of a few views or orders of a panel, such as a list's sort or a chart's time range. It
 * looks like the Tabs list (a muted track, the chosen one raised), which does the same where the views are panels.
 */
export function Segmented<T extends string | number>({
  label,
  options,
  value,
  onChange,
  className,
}: {
  label: string
  options: readonly { value: T; label: React.ReactNode }[]
  value: T
  onChange: (value: T) => void
  className?: string
}) {
  return (
    <div role="group" aria-label={label} className={cn('inline-flex h-7 items-center rounded-lg bg-muted p-[3px] text-muted-foreground', className)}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          aria-pressed={value === o.value}
          onClick={() => onChange(o.value)}
          className="inline-flex h-full items-center justify-center rounded-md border border-transparent px-2.5 text-xs font-medium whitespace-nowrap text-foreground/60 transition-all hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none aria-pressed:bg-background aria-pressed:text-foreground aria-pressed:shadow-sm dark:text-muted-foreground dark:aria-pressed:border-input dark:aria-pressed:bg-input/30 dark:aria-pressed:text-foreground"
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}

/** A chip: a filter that is on or off, or a quick pick of a value. A chosen one is filled. */
export function Chip({ pressed, className, ...props }: Omit<React.ComponentProps<'button'>, 'aria-pressed'> & { pressed: boolean }) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition-colors focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50',
        pressed ? 'border-primary bg-primary text-primary-foreground' : 'hover:bg-muted',
        className,
      )}
      {...props}
    />
  )
}
