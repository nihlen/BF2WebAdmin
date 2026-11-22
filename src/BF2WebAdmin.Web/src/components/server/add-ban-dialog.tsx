import { useState } from 'react'
import { type PickedPlayer, PlayerPicker } from '@/components/player-picker'
import { Chips } from '@/components/server/moderation-dialog'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { banPeriods, type ServerActions } from '@/hooks/use-server-actions'
import { banLengthLabel, banLengthText, presetReasons } from '@/lib/bans'

// What POST /api/servers/{id}/bans takes: a CD-key hash (32 hex digits) or an IP address
const hashPattern = /^[0-9a-f]{32}$/i
const ipv4Pattern = /^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}$/
const isIdentifier = (value: string) => hashPattern.test(value) || ipv4Pattern.test(value) || (value.includes(':') && /^[0-9a-f:]+$/i.test(value))

/**
 * A ban of a player who need not be online (the Bans tab's Add ban): found by name among the group's players online
 * and seen before, or by a CD-key hash or an IP address typed as it is, with a reason and a length as the moderation
 * dialog has them. A player online with it is banned and kicked at once.
 */
export function AddBanDialog({
  open,
  onOpenChange,
  serverGroup,
  actions,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  serverGroup: string
  actions: ServerActions
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>{open && <AddBanForm serverGroup={serverGroup} actions={actions} onClose={() => onOpenChange(false)} />}</DialogContent>
    </Dialog>
  )
}

function AddBanForm({ serverGroup, actions, onClose }: { serverGroup: string; actions: ServerActions; onClose: () => void }) {
  const [text, setText] = useState('')
  const [picked, setPicked] = useState<PickedPlayer | null>(null)
  const [name, setName] = useState('')
  const [reason, setReason] = useState('')
  const [length, setLength] = useState('Perm')
  const [tried, setTried] = useState(false)

  const identifier = picked?.hash ?? text.trim()
  const valid = !!picked || isIdentifier(identifier)
  // Named in the button once there is someone to ban
  const who = picked?.name ?? (valid ? name.trim() || identifier : '')
  const submitLabel = !who ? 'Ban' : length === 'Perm' ? `Ban ${who} permanently` : `Ban ${who} for ${banLengthLabel(length).toLowerCase()}`

  return (
    <form
      className="space-y-5"
      onSubmit={(e) => {
        e.preventDefault()
        setTried(true)
        if (!valid) return
        void actions.addBan(identifier, picked?.name ?? (name.trim() || null), reason.trim(), length)
        onClose()
      }}
    >
      <DialogHeader>
        <DialogTitle>Add a ban</DialogTitle>
        <DialogDescription>
          For a player who need not be online: by their CD-key hash or their address. If they are on the server, they are kicked at once.
        </DialogDescription>
      </DialogHeader>

      <Field data-invalid={tried && !valid ? true : undefined}>
        <FieldLabel htmlFor="ban-player">Player, CD-key hash or address</FieldLabel>
        <PlayerPicker
          id="ban-player"
          serverGroup={serverGroup}
          text={text}
          onTextChange={setText}
          picked={picked}
          onPick={setPicked}
          placeholder="Name, hash or IP address"
          invalid={tried && !valid}
        />
        {tried && !valid ? (
          <FieldError>Pick a player from the list, or type a CD-key hash (32 hex digits) or an IP address.</FieldError>
        ) : (
          !picked && !text.trim() && <FieldDescription>A name finds the players online on this group's servers and those seen on them before.</FieldDescription>
        )}
      </Field>

      {!picked && (
        <Field>
          <FieldLabel htmlFor="ban-name">Name (optional)</FieldLabel>
          <Input id="ban-name" value={name} placeholder="Shown in the ban list" onChange={(e) => setName(e.target.value)} />
        </Field>
      )}

      <Field>
        <FieldLabel htmlFor="ban-reason">Reason</FieldLabel>
        <Input id="ban-reason" value={reason} placeholder="What the player sees" onChange={(e) => setReason(e.target.value)} />
        <Chips label="Usual reasons" options={presetReasons.map((r) => ({ value: r, label: r }))} value={reason} onChange={setReason} />
      </Field>

      <FieldSet>
        <FieldLegend variant="label">Length</FieldLegend>
        <Chips label="Length" options={banPeriods} value={length} onChange={setLength} />
        <FieldDescription>{banLengthText(length)}</FieldDescription>
      </FieldSet>

      <DialogFooter>
        <DialogClose render={<Button type="button" variant="outline" />}>Cancel</DialogClose>
        <Button type="submit" variant="destructive" className="max-w-full min-w-0">
          <span className="truncate">{submitLabel}</span>
        </Button>
      </DialogFooter>
    </form>
  )
}
