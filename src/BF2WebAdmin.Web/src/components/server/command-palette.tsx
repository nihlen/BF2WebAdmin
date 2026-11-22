import { ArrowLeft, CornerDownLeft, Play, Search } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { api, errorMessage, type CommandInfo, type Player } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useIsMobile } from '@/hooks/use-mobile'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevelName } from '@/lib/auth-levels'
import { moduleName } from '@/lib/modules'
import { cn } from '@/lib/utils'

// Commands with buttons elsewhere on the page, or that only make sense in game
const hidden = new Set(['restartserver', 'crash', 'switchid', 'killid', 'auth'])

const commandKey = (c: CommandInfo) => `${c.aliases.join('|')} ${c.parameters.join(' ')}`

// An argument that names a player gets the online players to pick from
const isPlayerArgument = (parameter: string) => /name/i.test(parameter)

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
 * box). From md up the selected command's arguments are fields beside the list, with its Run button: Enter in the
 * search runs a command without arguments and goes to the first field of one with them, and Enter in a field runs it.
 * On a phone a command opens its fields on a screen of their own. Runs as the web user (God level), and the
 * notification shows what the command said in game.
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
  const isMobile = useIsMobile()
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  // The phone's screen of one command's fields
  const [chosen, setChosen] = useState<CommandInfo | null>(null)
  const detail = useRef<HTMLFormElement>(null)
  const listId = useId()

  const groups = groupCommands(commands.data ?? [], query)
  const flat = groups.flatMap(([, list]) => list)
  const current = Math.min(active, flat.length - 1)
  const selected = flat[current]
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
  const run = (text: string) => {
    void actions.custom(text)
    close(false)
  }
  const choose = (command: CommandInfo) => {
    if (command.parameters.length === 0) return run(`.${command.aliases[0]}`)
    if (isMobile) return setChosen(command)
    detail.current?.querySelector('input')?.focus()
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="top-[10svh] translate-y-0 gap-0 p-0 sm:max-w-xl md:max-w-3xl" showCloseButton={false}>
        <DialogTitle className="sr-only">Commands</DialogTitle>
        <DialogDescription className="sr-only">Find a chat command of this server's modules, fill in its arguments and run it as the web user.</DialogDescription>
        {chosen ? (
          <CommandForm key={commandKey(chosen)} command={chosen} players={players} onBack={() => setChosen(null)} onRun={run} autoFocus />
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
                  } else if (e.key === 'Enter' && selected) {
                    e.preventDefault()
                    choose(selected)
                  }
                }}
              />
            </div>
            <div className="md:grid md:grid-cols-[minmax(0,1fr)_20rem]">
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
                            // From md up a click only selects: the fields and Run are beside the list
                            onClick={() => (isMobile ? choose(command) : setActive(i))}
                            onDoubleClick={() => !isMobile && choose(command)}
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
              {selected && (
                <div className="hidden border-l bg-muted/20 md:block">
                  <CommandForm key={commandKey(selected)} ref={detail} command={selected} players={players} onRun={run} />
                </div>
              )}
            </div>
            <p className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t px-4 py-2 text-xs text-muted-foreground">
              <span>↑↓ to move</span>
              <span className="inline-flex items-center gap-1">
                <CornerDownLeft className="size-3" /> to run, or to fill in the arguments
              </span>
              <span>Esc to close</span>
            </p>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}

/**
 * One command's arguments, a field each, and its Run button; Enter in a field runs it once every field is filled. An
 * argument that names a player offers the online players as one-tap choices (and as suggestions while typing).
 */
function CommandForm({
  ref,
  command,
  players,
  onRun,
  onBack,
  autoFocus,
}: {
  ref?: React.Ref<HTMLFormElement>
  command: CommandInfo
  players: Player[]
  onRun: (text: string) => void
  /** The phone's way back to the list */
  onBack?: () => void
  autoFocus?: boolean
}) {
  const playersId = useId()
  const [values, setValues] = useState<string[]>(() => command.parameters.map(() => ''))
  const alias = command.aliases[0]
  const text = `.${alias}${values.map((v) => (v.trim() ? ` ${v.trim()}` : '')).join('')}`
  const complete = values.every((v) => v.trim())
  const setValue = (i: number, value: string) => setValues((v) => v.map((x, j) => (j === i ? value : x)))

  return (
    <form
      ref={ref}
      aria-label={`Run .${alias}`}
      className="flex h-full flex-col gap-4 p-4"
      onSubmit={(e) => {
        e.preventDefault()
        if (complete) onRun(text)
      }}
    >
      <div className="space-y-1.5">
        <div className="flex items-center gap-2">
          {onBack && (
            <Button type="button" variant="ghost" size="icon-sm" aria-label="Back to the commands" onClick={onBack}>
              <ArrowLeft />
            </Button>
          )}
          <span className="min-w-0 flex-1 truncate font-mono font-medium">.{alias}</span>
        </div>
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
          <span>{moduleName(command.modules[0])} module</span>
          <Badge variant="outline" title="Auth level a player needs for this command">
            {authLevelName(command.authLevel)} level
          </Badge>
        </div>
        {command.aliases.length > 1 && (
          <p className="text-xs text-muted-foreground">
            Also{' '}
            {command.aliases
              .slice(1)
              .map((a) => `.${a}`)
              .join(', ')}
          </p>
        )}
      </div>
      <datalist id={playersId}>
        {players.map((p) => (
          <option key={p.index} value={p.name} />
        ))}
      </datalist>
      {command.parameters.length === 0 ? (
        <p className="text-sm text-muted-foreground">No arguments: it runs as it is.</p>
      ) : (
        <div className="grid gap-3">
          {command.parameters.map((parameter, i) => (
            <div key={parameter} className="grid gap-1.5 text-sm">
              <label htmlFor={`${playersId}-${i}`} className="font-medium">
                {parameter}
              </label>
              <Input
                id={`${playersId}-${i}`}
                autoFocus={autoFocus && i === 0}
                list={isPlayerArgument(parameter) ? playersId : undefined}
                placeholder={isPlayerArgument(parameter) ? 'A player, or part of a name' : undefined}
                value={values[i]}
                onChange={(e) => setValue(i, e.target.value)}
              />
              {isPlayerArgument(parameter) && players.length > 0 && (
                <div className="flex flex-wrap gap-1" role="group" aria-label={`Online players for ${parameter}`}>
                  {players.slice(0, 12).map((p) => (
                    <Button
                      key={p.index}
                      type="button"
                      variant={values[i] === p.name ? 'secondary' : 'outline'}
                      size="xs"
                      aria-pressed={values[i] === p.name}
                      onClick={() => setValue(i, p.name)}
                    >
                      {p.name}
                    </Button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
      <div className="mt-auto flex items-center justify-between gap-3">
        <code className="min-w-0 truncate text-xs text-muted-foreground" title={text}>
          {text}
        </code>
        <Button type="submit" disabled={!complete}>
          <Play data-icon="inline-start" />
          Run
        </Button>
      </div>
    </form>
  )
}
