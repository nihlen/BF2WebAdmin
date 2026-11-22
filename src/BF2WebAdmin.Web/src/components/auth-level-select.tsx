import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { authLevels, defaultAuthLevel } from '@/lib/auth-levels'
import { cn } from '@/lib/utils'

/**
 * An admin level, with what it allows under each name. The trigger shows the description under the name too, unless
 * <compact> (a table cell), where only the name fits; it never gets narrower than its longest name, "SuperAdmin".
 */
export function LevelSelect({
  id,
  label,
  value,
  disabled,
  onChange,
  withoutDefault,
  compact,
  className,
}: {
  id?: string
  label: string
  value: number
  disabled?: boolean
  onChange: (level: number) => void
  /** Leaves out All, the level a player has without an entry */
  withoutDefault?: boolean
  /** Only the name in the trigger, for a narrow place */
  compact?: boolean
  className?: string
}) {
  const levels = authLevels.filter((l) => !withoutDefault || l.value !== defaultAuthLevel)
  const items = levels.map((l) => ({ value: l.value, label: l.name }))
  const current = authLevels.find((l) => l.value === value)

  return (
    <Select items={items} value={value} disabled={disabled} onValueChange={(next) => next !== null && onChange(next)}>
      <SelectTrigger
        id={id}
        className={cn('w-full min-w-36', !compact && 'py-1.5 text-left whitespace-normal data-[size=default]:h-auto', className)}
        aria-label={label}
        title={current ? `${current.name}: ${current.description}` : undefined}
      >
        <SelectValue className={cn(!compact && 'flex-col items-start! gap-0! whitespace-normal')}>
          {() =>
            current && !compact ? (
              <>
                <span>{current.name}</span>
                <span className="text-xs text-muted-foreground">{current.description}</span>
              </>
            ) : (
              (current?.name ?? String(value))
            )
          }
        </SelectValue>
      </SelectTrigger>
      <SelectContent className="min-w-64">
        {levels.map((l) => (
          <SelectItem key={l.value} value={l.value} className="py-1.5">
            <span className="flex flex-col">
              <span>{l.name}</span>
              <span className="text-xs whitespace-normal text-muted-foreground">{l.description}</span>
            </span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
