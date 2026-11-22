import { useState } from 'react'
import type { Player } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Chip } from '@/components/toggles'
import { banPeriods, type ServerActions } from '@/hooks/use-server-actions'
import { banLengthLabel, banLengthText, presetReasons } from '@/lib/bans'
import { cn } from '@/lib/utils'

export type ModerationKind = 'warn' | 'kick' | 'ban'

export type ModerationRequest = { kind: ModerationKind; player: Player }



const kinds: { kind: ModerationKind; label: string }[] = [
  { kind: 'warn', label: 'Warn' },
  { kind: 'kick', label: 'Kick' },
  { kind: 'ban', label: 'Ban' },
]

/**
 * Warn, kick or ban a player: one dialog with the action as a choice at the top, a reason with the usual ones as
 * chips, and for a ban its length as chips. Open while request is set.
 */
export function ModerationDialog({ request, actions, onClose }: { request: ModerationRequest | null; actions: ServerActions; onClose: () => void }) {
  return (
    <Dialog open={!!request} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {/* Keyed so the reason starts empty for every player and action */}
        {request && <ModerationForm key={`${request.kind}:${request.player.index}`} request={request} actions={actions} onClose={onClose} />}
      </DialogContent>
    </Dialog>
  )
}

function ModerationForm({ request, actions, onClose }: { request: ModerationRequest; actions: ServerActions; onClose: () => void }) {
  const { player } = request
  const [kind, setKind] = useState(request.kind)
  const [reason, setReason] = useState('')
  const [length, setLength] = useState('Perm')
  const lengthLabel = banLengthLabel(length)

  const descriptions: Record<ModerationKind, string> = {
    warn: `${player.name} sees the reason in game and stays on the server.`,
    kick: `${player.name} sees the reason and is removed from the server. They can join again.`,
    ban: `${player.name} sees the reason and is removed a few seconds later, and cannot join again until the ban ends.`,
  }
  const submitLabels: Record<ModerationKind, string> = {
    warn: `Warn ${player.name}`,
    kick: `Kick ${player.name}`,
    ban: length === 'Perm' ? `Ban ${player.name} permanently` : `Ban ${player.name} for ${lengthLabel.toLowerCase()}`,
  }

  return (
    <form
      className="space-y-5"
      onSubmit={(e) => {
        e.preventDefault()
        const text = reason.trim()
        if (kind === 'warn') void actions.warn(player, text)
        else if (kind === 'kick') void actions.kick(player, text)
        else void actions.ban(player, text, length)
        onClose()
      }}
    >
      <DialogHeader>
        <DialogTitle>Moderate {player.name}</DialogTitle>
        <DialogDescription>{descriptions[kind]}</DialogDescription>
      </DialogHeader>

      <div role="radiogroup" aria-label="Action" className="grid grid-cols-3 gap-1 rounded-lg bg-muted p-1">
        {kinds.map((k) => (
          <button
            key={k.kind}
            type="button"
            role="radio"
            aria-checked={kind === k.kind}
            onClick={() => setKind(k.kind)}
            className={cn(
              'rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
              kind === k.kind && 'bg-background text-foreground shadow-sm',
            )}
          >
            {k.label}
          </button>
        ))}
      </div>

      <Field>
        <FieldLabel htmlFor="moderation-reason">Reason</FieldLabel>
        <Input id="moderation-reason" autoFocus value={reason} placeholder="What the player sees" onChange={(e) => setReason(e.target.value)} />
        <Chips label="Usual reasons" options={presetReasons.map((r) => ({ value: r, label: r }))} value={reason} onChange={setReason} />
      </Field>

      {kind === 'ban' && (
        <FieldSet>
          <FieldLegend variant="label">Length</FieldLegend>
          <Chips label="Length" options={banPeriods} value={length} onChange={setLength} />
          <FieldDescription>{banLengthText(length)}</FieldDescription>
        </FieldSet>
      )}

      <DialogFooter>
        <DialogClose render={<Button type="button" variant="outline" />}>Cancel</DialogClose>
        <Button type="submit" variant={kind === 'warn' ? 'default' : 'destructive'}>
          {submitLabels[kind]}
        </Button>
      </DialogFooter>
    </form>
  )
}

/** A row of choices as pill buttons; the selected one is filled. */
export function Chips({ label, options, value, onChange }: { label: string; options: { value: string; label: string }[]; value: string; onChange: (value: string) => void }) {
  return (
    <div role="group" aria-label={label} className="flex flex-wrap gap-1.5">
      {options.map((o) => (
        <Chip key={o.value} pressed={value === o.value} onClick={() => onChange(o.value)}>
          {o.label}
        </Chip>
      ))}
    </div>
  )
}
