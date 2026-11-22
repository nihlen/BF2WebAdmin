import { CircleHelp } from 'lucide-react'
import { useId, useState } from 'react'
import type { ChatLogEntry, CommandInfo, Player, Team } from '@/api/client'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'
import { LogList, Time } from './log-list'

export function ChatPanel({
  chatLog,
  players,
  teams,
  commands,
  onSend,
  onPlayer,
  listClassName = 'h-[500px]',
}: {
  chatLog: ChatLogEntry[]
  players: Player[]
  teams: Team[]
  commands: CommandInfo[]
  onSend: (message: string) => void
  /** Opens a player's details from their name in the chat */
  onPlayer?: (player: Player) => void
  /** The list's size: a fixed height on the server page, the rest of a column in the map builder */
  listClassName?: string
}) {
  return (
    <>
      <LogList className={listClassName} items={chatLog} empty="No chat yet">
        {(entry) => <ChatLine key={entry.message.id} entry={entry} players={players} teams={teams} onPlayer={onPlayer} />}
      </LogList>
      <ChatInput players={players} commands={commands} onSend={onSend} />
    </>
  )
}

function ChatLine({ entry, players, teams, onPlayer }: { entry: ChatLogEntry; players: Player[]; teams: Team[]; onPlayer?: (player: Player) => void }) {
  const { message } = entry
  // The sender while still on the server: same index and name, as an index is reused after a player leaves
  const sender = players.find((p) => p.index === message.playerId && p.name === message.playerName)
  // By team id: the sender's current team, or the team with the name the message carries
  const teamId = sender?.team ?? teams.find((t) => t.name === message.teamName)?.id
  const color =
    message.channel === 'Team'
      ? teamTextColor(teamId)
      : message.channel === 'Squad'
        ? 'text-green-400'
        : message.channel === 'ServerMessage'
          ? 'text-muted-foreground'
          : undefined

  return (
    <li className={color}>
      <Time value={entry.timestamp} />
      {message.type !== 'Server' && (
        <>
          {' '}
          {sender && onPlayer ? (
            <button
              type="button"
              className="font-medium underline-offset-2 hover:underline focus-visible:underline focus-visible:outline-none"
              title={`${sender.name}: details and actions`}
              onClick={() => onPlayer(sender)}
            >
              {message.playerName}
            </button>
          ) : (
            message.playerName
          )}
          :
        </>
      )}{' '}
      {message.text}
    </li>
  )
}

const maxSuggestions = 8

type Suggestion = { insert: string; label: string; detail?: string }

/**
 * The text before the cursor's last word, and the suggestions for that word: "." commands for the first word,
 * player names for the others.
 */
function suggest(text: string, players: Player[], commands: CommandInfo[]): Suggestion[] {
  const words = text.split(' ')
  const last = words[words.length - 1]
  const before = words.slice(0, -1).join(' ')
  const prefix = before ? `${before} ` : ''

  if (words.length === 1) {
    if (!last.startsWith('.')) return []
    const typed = last.slice(1).toLowerCase()
    const seen = new Set<string>()
    const result: Suggestion[] = []
    for (const command of commands) {
      for (const alias of command.aliases) {
        const usage = `.${alias}${command.parameters.map((p) => ` <${p}>`).join('')}`
        if (!alias.startsWith(typed) || seen.has(usage)) continue
        seen.add(usage)
        result.push({ insert: `.${alias}${command.parameters.length > 0 ? ' ' : ''}`, label: usage, detail: command.modules.join(', ') })
      }
    }
    return result.slice(0, maxSuggestions)
  }

  // Names only in commands, not while chatting
  if (!last || !(text.startsWith('.') || text.startsWith('!'))) return []
  return players
    .filter((p) => p.name.toLowerCase().includes(last.toLowerCase()))
    .slice(0, maxSuggestions)
    .map((p) => ({ insert: `${prefix}${p.name} `, label: p.name, detail: `player ${p.index}` }))
}

// What the chat box takes besides chat; the placeholder is short, so this is in the box's help
const chatHelp = 'A message is said in game. ".command" runs a chat command, "!command" an admin command, and "!" with a console line runs it on the server. ↑ and ↓ go through what you sent, Tab completes commands and player names.'

/** One line: chat, ".command" or "!command" (an admin command or a console line). Up and down go through what was sent; Tab takes the selected suggestion. */
function ChatInput({ players, commands, onSend }: { players: Player[]; commands: CommandInfo[]; onSend: (message: string) => void }) {
  const helpId = useId()
  const [text, setText] = useState('')
  const [history, setHistory] = useState<string[]>([])
  // Position in history while browsing it with the arrow keys; null when editing new text
  const [historyIndex, setHistoryIndex] = useState<number | null>(null)
  const [selected, setSelected] = useState(0)
  const [dismissed, setDismissed] = useState(false)

  const suggestions = dismissed || historyIndex !== null ? [] : suggest(text, players, commands)
  const active = Math.min(selected, suggestions.length - 1)

  const update = (value: string) => {
    setText(value)
    setHistoryIndex(null)
    setSelected(0)
    setDismissed(false)
  }

  const accept = (suggestion: Suggestion) => {
    update(suggestion.insert)
  }

  return (
    <form
      className="relative border-t p-2"
      onSubmit={(e) => {
        e.preventDefault()
        if (!text.trim()) return
        onSend(text)
        setHistory((h) => [...h.filter((x) => x !== text), text].slice(-50))
        update('')
      }}
    >
      {suggestions.length > 0 && (
        <ul role="listbox" className="absolute right-2 bottom-full left-2 z-10 mb-1 overflow-hidden rounded-lg border bg-popover text-sm shadow-md">
          {suggestions.map((s, i) => (
            <li key={s.label} role="option" aria-selected={i === active}>
              <button
                type="button"
                className={cn('flex w-full items-center justify-between gap-4 px-3 py-1.5 text-left', i === active && 'bg-accent text-accent-foreground')}
                // Before the input loses focus
                onMouseDown={(e) => {
                  e.preventDefault()
                  accept(s)
                }}
              >
                <span className="font-mono">{s.label}</span>
                {s.detail && <span className="text-xs text-muted-foreground">{s.detail}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
      <p id={helpId} className="sr-only">
        {chatHelp}
      </p>
      <Input
        aria-label="Chat message or command"
        placeholder="Message or .command"
        aria-describedby={helpId}
        className="pr-8"
        value={text}
        onChange={(e) => update(e.target.value)}
        onBlur={() => setDismissed(true)}
        onKeyDown={(e) => {
          if (suggestions.length > 0) {
            if (e.key === 'Tab') {
              e.preventDefault()
              accept(suggestions[active])
              return
            }
            if (e.key === 'ArrowDown') {
              e.preventDefault()
              setSelected((active + 1) % suggestions.length)
              return
            }
            if (e.key === 'ArrowUp') {
              e.preventDefault()
              setSelected((active - 1 + suggestions.length) % suggestions.length)
              return
            }
            if (e.key === 'Escape') {
              setDismissed(true)
              return
            }
          }

          if (e.key === 'ArrowUp' && history.length > 0) {
            e.preventDefault()
            const index = historyIndex === null ? history.length - 1 : Math.max(historyIndex - 1, 0)
            setHistoryIndex(index)
            setText(history[index])
          } else if (e.key === 'ArrowDown' && historyIndex !== null) {
            e.preventDefault()
            const index = historyIndex + 1
            if (index >= history.length) update('')
            else {
              setHistoryIndex(index)
              setText(history[index])
            }
          }
        }}
      />
      <Tooltip>
        <TooltipTrigger
          render={<button type="button" />}
          aria-label="What the chat box takes"
          className="absolute top-1/2 right-4 -translate-y-1/2 rounded-sm text-muted-foreground hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
        >
          <CircleHelp className="size-4" />
        </TooltipTrigger>
        <TooltipContent side="top" align="end" className="max-w-sm">
          {chatHelp}
        </TooltipContent>
      </Tooltip>
    </form>
  )
}
