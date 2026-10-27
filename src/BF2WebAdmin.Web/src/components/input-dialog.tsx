import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'

export type InputRequest = {
  title: string
  label: string
  submitLabel?: string
  onSubmit: (value: string) => void
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

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault()
        request.onSubmit(value)
        onClose()
      }}
    >
      <DialogHeader>
        <DialogTitle>{request.title}</DialogTitle>
      </DialogHeader>
      <Field>
        <FieldLabel htmlFor="input-dialog-value">{request.label}</FieldLabel>
        <Input id="input-dialog-value" autoFocus value={value} onChange={(e) => setValue(e.target.value)} />
      </Field>
      <DialogFooter>
        <DialogClose render={<Button type="button" variant="outline" />}>Cancel</DialogClose>
        <Button type="submit" variant="destructive">
          {request.submitLabel ?? 'Send'}
        </Button>
      </DialogFooter>
    </form>
  )
}
