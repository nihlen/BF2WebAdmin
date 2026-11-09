import type { Ban } from '@/api/client'
import { formatTime } from '@/lib/format'

/** How long a ban lasts: permanent, this round, or until a time. */
export const banLength = (ban: Ban) =>
  ban.kind === 'Permanent' ? 'Permanent' : ban.kind === 'Round' ? 'This round' : ban.expiresAt ? `Until ${formatTime(ban.expiresAt)}` : 'Timed'
