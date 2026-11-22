import type { Player } from '@/api/client'
import { cn } from '@/lib/utils'

/** A player's name that opens their sheet. */
export function PlayerLink({ player, onOpen, className, children }: { player: Player; onOpen: (player: Player) => void; className?: string; children?: React.ReactNode }) {
  return (
    <button
      type="button"
      className={cn('rounded-sm text-left underline-offset-4 hover:underline focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none', className)}
      title={`Details of ${player.name}`}
      onClick={() => onOpen(player)}
    >
      {children ?? player.name}
    </button>
  )
}

const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/**
 * A line of text (a game event, a command) with the names of the players on the server as links to their sheets. A
 * name is only taken whole, not inside a longer word; a player who has left is plain text, since the log does not say
 * which player of that name it was.
 */
export function PlayerText({ text, players, onOpen, linkClassName }: { text: string; players: Player[]; onOpen: (player: Player) => void; linkClassName?: string }) {
  const named = players.filter((p) => p.name.trim())
  if (named.length === 0) return text
  const byName = new Map(named.map((p) => [p.name, p]))
  // Longest first, so a name that contains another one wins where both match
  const names = [...byName.keys()].sort((a, b) => b.length - a.length).map(escape)
  const pattern = new RegExp(`(?<![\\p{L}\\p{N}])(${names.join('|')})(?![\\p{L}\\p{N}])`, 'u')
  const parts = text.split(pattern)
  // split with one group alternates text and the matched names
  return parts.map((part, i) => {
    const player = i % 2 === 1 ? byName.get(part) : undefined
    return player ? (
      <PlayerLink key={i} player={player} onOpen={onOpen} className={linkClassName} />
    ) : (
      part
    )
  })
}
