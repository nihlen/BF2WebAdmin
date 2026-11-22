import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { authLevels, defaultAuthLevel } from '@/lib/auth-levels'

/** An admin level, with what it allows under each name. */
export function LevelSelect({
  id,
  label,
  value,
  disabled,
  onChange,
  withoutDefault,
}: {
  id?: string
  label: string
  value: number
  disabled?: boolean
  onChange: (level: number) => void
  /** Leaves out All, the level a player has without an entry */
  withoutDefault?: boolean
}) {
  const levels = authLevels.filter((l) => !withoutDefault || l.value !== defaultAuthLevel)
  const items = levels.map((l) => ({ value: l.value, label: l.name }))
  return (
    <Select items={items} value={value} disabled={disabled} onValueChange={(next) => next !== null && onChange(next)}>
      <SelectTrigger id={id} className="w-full" aria-label={label}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
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
