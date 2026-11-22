import { ArrowLeft, CornerDownLeft, Play, Search } from 'lucide-react'
import { useEffect, useId, useState } from 'react'
import { api, errorMessage, type CommandInfo, type Player } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevelName } from '@/lib/auth-levels'
import { moduleName } from '@/lib/modules'
import { cn } from '@/lib/utils'

// Commands with buttons elsewhere on the page, or that only make sense in game
const hidden = new Set(['restartserver', 'crash', 'switchid', 'killid', 'auth'])

const commandKey = (c: CommandInfo) => `${c.aliases.join('|')} ${c.parameters.join(' ')}`

/** The commands that match the search, by module (by the module's name in words), in the order they are listed. */
function groupCommands(commands: CommandInfo[], query: string) {
  const q = query.trim().toLowerCase().replace(/^\./, '')
  const matches = (c: CommandInfo) =>
    !c.aliases.some((a) => hidden.has(a)) &&
    (!q || c.aliases.some((a) => a.includes(q)) || c.modules.some((m) => m.toLowerCase().includes(q) || moduleName(m).toLowerCase().includes(q)))

  const byModule = new Map<string, CommandInfo[]>()
  for (const command of commands.filter(matches)) {
    const module = moduleName(command.modules[0])
    byModule.set(module, [...(byModule.get(module) ?? []), command])
  }
  return [...byModule.entries()].sort(([a], [b]) => a.localeCompare(b))
}

/**
 * Every chat command of the server's loaded modules, found by typing (Ctrl+K on the Live tab, or Commands by the chat
 * box). A command without arguments runs on Enter; one with arguments asks for them first. Runs as the web user (God
 * level), and the notification shows what the command said in game.
 */
export function CommandPalette({
  open,
  onOpenChange,
  serverId,
  players,
  actions,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  serverId: string
  players: Player[]
  actions: ServerActions
}) {
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId } } })
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const [chosen, setChosen] = useState<CommandInfo | null>(null)
  const listId = useId()

  const groups = groupCommands(commands.data ?? [], query)
  const flat = groups.flatMap(([, list]) => list)
  const current = Math.min(active, flat.length - 1)
  const optionId = (i: number) => `${listId}-${i}`

  // The active row stays in view while the arrow keys move it
  useEffect(() => {
    document.getElementById(`${listId}-${current}`)?.scrollIntoView({ block: 'nearest' })
  }, [listId, current])

  const close = (next: boolean) => {
    onOpenChange(next)
    if (!next) {
      setQuery('')
      setActive(0)
      setChosen(null)
    }
  }
  const choose = (command: CommandInfo) => {
    if (command.parameters.length > 0) return setChosen(command)
    void actions.custom(`.${command.aliases[0]}`)
    close(false)
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="top-[10svh] translate-y-0 gap-0 p-0 sm:max-w-xl" showCloseButton={false}>
        <DialogTitle className="sr-only">Commands</DialogTitle>
        <DialogDescription className="sr-only">Find a chat command of this server's modules and run it as the web user.</DialogDescription>
        {chosen ? (
          <CommandArguments
            command={chosen}
            players={players}
            onBack={() => setChosen(null)}
            onRun={(text) => {
              void actions.custom(text)
              close(false)
            }}
          />
        ) : (
          <>
            <div className="relative border-b">
              <Search className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-muted-foreground" />
              <input
                autoFocus
                role="combobox"
                aria-expanded
                aria-controls={listId}
                aria-activedescendant={flat.length > 0 ? optionId(current) : undefined}
                aria-label="Search commands or modules"
                placeholder="Search commands or modules"
                className="h-12 w-full bg-transparent pr-4 pl-10 text-base outline-none placeholder:text-muted-foreground sm:text-sm"
                value={query}
                onChange={(e) => {
                  setQuery(e.target.value)
                  setActive(0)
                }}
                onKeyDown={(e) => {
                  if (e.key === 'ArrowDown' && flat.length > 0) {
                    e.preventDefault()
                    setActive((current + 1) % flat.length)
                  } else if (e.key === 'ArrowUp' && flat.length > 0) {
                    e.preventDefault()
                    setActive((current - 1 + flat.length) % flat.length)
                  } else if (e.key === 'Enter' && flat[current]) {
                    e.preventDefault()
                    choose(flat[current])
                  }
                }}
              />
            </div>
            {/* The arrow keys move through the list from the search field; a tab stop of its own lets the keyboard scroll it */}
            <div tabIndex={0} className="max-h-[min(28rem,60svh)] overflow-y-auto p-2 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none">
              {commands.isPending && <Skeleton className="m-2 h-32" />}
              {commands.isError && <p className="p-3 text-sm text-destructive">{errorMessage(commands.error)}</p>}
              {commands.data && flat.length === 0 && <p className="p-3 text-sm text-muted-foreground">No command matches</p>}
              <div role="listbox" id={listId} aria-label="Commands">
                {groups.map(([module, list]) => (
                  <div key={module} role="group" aria-label={module} className="pb-1">
                    <div className="px-2 pt-2 pb-1 text-xs font-medium text-muted-foreground">{module}</div>
                    {list.map((command) => {
                      const i = flat.indexOf(command)
                      return (
                        <div
                          key={commandKey(command)}
                          id={optionId(i)}
                          role="option"
                          aria-selected={i === current}
                          className={cn('flex cursor-pointer items-center gap-3 rounded-md px-2 py-1.5 text-sm', i === current && 'bg-accent text-accent-foreground')}
                          title={command.aliases.map((a) => `.${a}`).join(', ')}
                          onMouseMove={() => i !== current && setActive(i)}
                          onClick={() => choose(command)}
                        >
                          <span className="min-w-0 flex-1 truncate font-mono">
                            .{command.aliases[0]}
                            {command.parameters.map((p) => (
                              <span key={p} className="text-muted-foreground">
                                {' '}
                                &lt;{p}&gt;
                              </span>
                            ))}
                          </span>
                          <Badge variant="outline" title="Auth level a player needs for this command">
                            {authLevelName(command.authLevel)}
                          </Badge>
                        </div>
                      )
                    })}
                  </div>
                ))}
              </div>
            </div>
            <p className="flex items-center gap-3 border-t px-4 py-2 text-xs text-muted-foreground">
              <span>↑↓ to move</span>
              <span className="inline-flex items-center gap-1">
                <CornerDownLeft className="size-3" /> to run or fill in
              </span>
              <span>Esc to close</span>
            </p>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}

/** The arguments of a chosen command, one field each; player names are suggested where an argument is a name. */
function CommandArguments({ command, players, onBack, onRun }: { command: CommandInfo; players: Player[]; onBack: () => void; onRun: (text: string) => void }) {
  const playersId = useId()
  const [values, setValues] = useState<string[]>(() => command.parameters.map(() => ''))
  const alias = command.aliases[0]
  const text = `.${alias}${values.map((v) => (v.trim() ? ` ${v.trim()}` : '')).join('')}`
  const complete = values.every((v) => v.trim())

  return (
    <form
      className="space-y-4 p-4"
      onSubmit={(e) => {
        e.preventDefault()
        if (complete) onRun(text)
      }}
    >
      <div className="flex items-center gap-2">
        <Button type="button" variant="ghost" size="icon-sm" aria-label="Back to the commands" onClick={onBack}>
          <ArrowLeft />
        </Button>
        <span className="min-w-0 flex-1 truncate font-mono font-medium">.{alias}</span>
        <Badge variant="outline">{moduleName(command.modules[0])}</Badge>
        <Badge variant="outline" title="Auth level a player needs for this command">
          {authLevelName(command.authLevel)}
        </Badge>
      </div>
      <datalist id={playersId}>
        {players.map((p) => (
          <option key={p.index} value={p.name} />
        ))}
      </datalist>
      <div className="grid gap-3">
        {command.parameters.map((parameter, i) => (
          <label key={parameter} className="grid gap-1.5 text-sm">
            <span className="font-medium">{parameter}</span>
            <Input
              autoFocus={i === 0}
              list={/name/i.test(parameter) ? playersId : undefined}
              value={values[i]}
              onChange={(e) => setValues((v) => v.map((x, j) => (j === i ? e.target.value : x)))}
            />
          </label>
        ))}
      </div>
      <div className="flex items-center justify-between gap-3">
        <code className="min-w-0 truncate text-xs text-muted-foreground">{text}</code>
        <Button type="submit" disabled={!complete}>
          <Play data-icon="inline-start" />
          Run
        </Button>
      </div>
    </form>
  )
}
