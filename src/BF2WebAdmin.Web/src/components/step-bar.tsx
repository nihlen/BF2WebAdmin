import { ArrowLeft } from 'lucide-react'
import { Button } from '@/components/ui/button'

/**
 * The buttons of a step of the add server flow, in the same place on every step: Back on the left, a line on where the
 * step stands, and the step's actions on the right with the main one last. It sticks to the bottom of the window, so a
 * long step (the modules) has its buttons in reach. On a phone the line takes a row of its own above the buttons.
 */
export function StepBar({ label, status, onBack, children }: { label: string; status?: React.ReactNode; onBack?: () => void; children: React.ReactNode }) {
  return (
    <div
      role="region"
      aria-label={label}
      className="sticky bottom-4 z-10 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border bg-background/95 p-3 shadow-lg backdrop-blur supports-backdrop-filter:bg-background/80"
    >
      {onBack && (
        <Button variant="ghost" onClick={onBack}>
          <ArrowLeft data-icon="inline-start" />
          Back
        </Button>
      )}
      {status && <div className="order-first w-full min-w-0 text-sm text-muted-foreground sm:order-none sm:w-auto sm:flex-1">{status}</div>}
      <div className="ml-auto flex flex-wrap justify-end gap-2">{children}</div>
    </div>
  )
}
