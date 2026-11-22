import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

export type InputRequest = {
  title: string
  description?: string
  label: string
  submitLabel?: string
  /** Red submit button, for kicks and bans; the default is the normal button */
  destructive?: boolean
  /** A second field with fixed choices, such as the length of a ban */
  choice?: { label: string; options: { value: string; label: string }[]; initial: string }
  onSubmit: (value: string, choice: string) => void
}

/** Asks for one line of text, such as the reason for a kick. Open while request is set. */
export function InputDialog({ request, onClose }: { request: InputRequest | null; onClose: () => void }) {
  return (
    <Dialog open={!!request} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {/* Keyed so the text starts empty for every request */}
        {request && <InputForm key={request.title} request={request} onClose={onClose} />}
      </DialogContent>
    </Dialog>
  )
}

function InputForm({ request, onClose }: { request: InputRequest; onClose: () => void }) {
  const [value, setValue] = useState('')
  const [choice, setChoice] = useState(request.choice?.initial ?? '')

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault()
        request.onSubmit(value, choice)
        onClose()
      }}
    >
      <DialogHeader>
        <DialogTitle>{request.title}</DialogTitle>
        {request.description && <DialogDescription>{request.description}</DialogDescription>}
      </DialogHeader>
      <Field>
        <FieldLabel htmlFor="input-dialog-value">{request.label}</FieldLabel>
        <Input id="input-dialog-value" autoFocus value={value} onChange={(e) => setValue(e.target.value)} />
      </Field>
      {request.choice && (
        <Field>
          <FieldLabel htmlFor="input-dialog-choice">{request.choice.label}</FieldLabel>
          <Select items={request.choice.options} value={choice} onValueChange={(next) => setChoice(next ?? '')}>
            <SelectTrigger id="input-dialog-choice" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {request.choice.options.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      )}
      <DialogFooter>
        <DialogClose render={<Button type="button" variant="outline" />}>Cancel</DialogClose>
        <Button type="submit" variant={request.destructive ? 'destructive' : 'default'}>
          {request.submitLabel ?? 'Send'}
        </Button>
      </DialogFooter>
    </form>
  )
}
