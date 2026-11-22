import type { Ban } from '@/api/client'
import { banPeriods } from '@/hooks/use-server-actions'
import { formatRelative } from '@/lib/format'

// The reasons admins give most, as chips by the reason field of the moderation and add ban dialogs; one click fills
// the field, which can still be edited
export const presetReasons = ['Team killing', 'Spawn killing', 'Cheating', 'Abusive chat', 'Ignoring warnings']

/** A ban length's chip label ("1 day"), or the value itself. */
export const banLengthLabel = (length: string) => banPeriods.find((p) => p.value === length)?.label ?? length

/** What a ban length means, under its chips. */
export const banLengthText = (length: string) =>
  length === 'Round' ? 'Until the next round starts.' : length === 'Perm' ? 'Until an admin unbans the player on the Bans tab.' : `For ${banLengthLabel(length).toLowerCase()}.`

/** How long a ban lasts: permanent, this round, or when it ends, relative to now ("Expires in 3 days"). */
export const banLength = (ban: Ban) =>
  ban.kind === 'Permanent'
    ? 'Permanent'
    : ban.kind === 'Round'
      ? 'This round'
      : ban.expiresAt
        ? `${Date.parse(ban.expiresAt) > Date.now() ? 'Expires' : 'Expired'} ${formatRelative(ban.expiresAt)}`
        : 'Timed'
