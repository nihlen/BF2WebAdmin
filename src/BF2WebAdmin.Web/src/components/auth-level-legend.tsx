import { authLevels } from '@/lib/auth-levels'
import { cn } from '@/lib/utils'

/**
 * The six admin levels and what each may do, in a compact grid: the list of players only names a level, and a newcomer
 * cannot tell an Admin from a SuperAdmin without it. Each level may also do what the levels under it may.
 */
export function AuthLevelLegend({ className }: { className?: string }) {
  return (
    <section aria-labelledby="auth-level-legend" className={cn('rounded-lg border bg-muted/30 px-3 py-2.5 text-sm', className)}>
      <h3 id="auth-level-legend" className="mb-1.5 text-xs font-medium text-muted-foreground">
        What each level may do, besides what the levels before it may
      </h3>
      <dl className="grid gap-x-6 gap-y-1 sm:grid-flow-col sm:grid-cols-2 sm:grid-rows-3">
        {authLevels.map((level) => (
          <div key={level.value} className="flex min-w-0 gap-2">
            <dt className="w-22 shrink-0 font-medium">{level.name}</dt>
            <dd className="min-w-0 text-muted-foreground">{level.description}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
