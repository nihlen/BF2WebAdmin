import type { Ban } from '@/api/client'
import { formatRelative } from '@/lib/format'

/** How long a ban lasts: permanent, this round, or when it ends, relative to now ("Expires in 3 days"). */
export const banLength = (ban: Ban) =>
  ban.kind === 'Permanent'
    ? 'Permanent'
    : ban.kind === 'Round'
      ? 'This round'
      : ban.expiresAt
        ? `${Date.parse(ban.expiresAt) > Date.now() ? 'Expires' : 'Expired'} ${formatRelative(ban.expiresAt)}`
        : 'Timed'
