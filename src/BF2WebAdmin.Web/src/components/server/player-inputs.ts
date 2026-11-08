import type { Player } from '@/api/client'
import type { InputRequest } from '@/components/input-dialog'
import { banPeriods, type ServerActions } from '@/hooks/use-server-actions'

/** Asks for the reason of a warn, kick or ban, and the length of a ban. */
export function playerInputs(actions: ServerActions, onInput: (request: InputRequest) => void) {
  return {
    warn: (player: Player) =>
      onInput({ title: `Warn ${player.name}`, label: 'Reason', submitLabel: 'Warn', onSubmit: (reason) => void actions.warn(player, reason) }),
    kick: (player: Player) =>
      onInput({
        title: `Kick ${player.name}`,
        label: 'Reason',
        submitLabel: 'Kick',
        destructive: true,
        onSubmit: (reason) => void actions.kick(player, reason),
      }),
    ban: (player: Player) =>
      onInput({
        title: `Ban ${player.name}`,
        description: 'The player sees the reason, and is removed a few seconds later.',
        label: 'Reason',
        submitLabel: 'Ban',
        destructive: true,
        choice: { label: 'Length', options: banPeriods, initial: 'Perm' },
        onSubmit: (reason, period) => void actions.ban(player, reason, period),
      }),
  }
}
