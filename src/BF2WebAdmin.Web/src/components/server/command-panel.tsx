import { Play } from 'lucide-react'
import { useState } from 'react'
import { api, errorMessage, type CommandInfo, type Player } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { ServerActions } from '@/hooks/use-server-actions'
import { authLevelName } from '@/lib/auth-levels'
import { moduleName } from '@/lib/modules'
import { usePendingActions } from '@/lib/pending-actions'

// Commands with buttons elsewhere on the page, or that only make sense in game
const hidden = new Set(['restartserver', 'crash', 'switchid', 'killid', 'auth'])

/**
 * Every chat command of the server's loaded modules, grouped by module, with a field per parameter. Runs as the web
 * user (God level); the notification shows what the command said in game. The rows share one grid of columns (name,
 * arguments, level, run) so that they line up across the modules; a narrow panel puts the arguments on a line of
 * their own under the name.
 */
export function CommandPanel({ serverId, players, actions }: { serverId: string; players: Player[]; actions: ServerActions }) {
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId } } })
  const [filter, setFilter] = useState('')

  if (commands.isPending) return <Skeleton className="m-4 h-40" />
  if (commands.isError) return <p className="p-4 text-destructive">{errorMessage(commands.error)}</p>

  const matches = (c: CommandInfo) =>
    !c.aliases.some((a) => hidden.has(a)) && (!filter || c.aliases.some((a) => a.includes(filter.toLowerCase())) || c.modules.some((m) => m.toLowerCase().includes(filter.toLowerCase())))

  const byModule = new Map<string, CommandInfo[]>()
  for (const command of commands.data.filter(matches)) {
    const module = command.modules[0]
    byModule.set(module, [...(byModule.get(module) ?? []), command])
  }

  return (
    <div className="flex h-[550px] flex-col">
      <div className="border-b p-2">
        <Input placeholder="Search commands or modules" aria-label="Search commands" value={filter} onChange={(e) => setFilter(e.target.value)} />
      </div>
      <datalist id="command-panel-players">
        {players.map((p) => (
          <option key={p.index} value={p.name} />
        ))}
      </datalist>
      <div className="@container flex-1 space-y-4 overflow-y-auto p-4">
        {byModule.size === 0 && <p className="text-sm text-muted-foreground">No command matches</p>}
        {[...byModule.entries()]
          .sort(([a], [b]) => a.localeCompare(b))
          .map(([module, list]) => (
            <section key={module} className="space-y-2">
              <h3 className="text-sm font-medium text-muted-foreground">{moduleName(module)}</h3>
              <ul className="space-y-2">
                {list.map((command) => (
                  <CommandRow key={`${command.aliases.join('|')} ${command.parameters.join(' ')}`} serverId={serverId} command={command} actions={actions} />
                ))}
              </ul>
            </section>
          ))}
      </div>
    </div>
  )
}

function CommandRow({ serverId, command, actions }: { serverId: string; command: CommandInfo; actions: ServerActions }) {
  const [values, setValues] = useState<string[]>(() => command.parameters.map(() => ''))
  const pending = usePendingActions(serverId)
  const alias = command.aliases[0]
  const text = `.${alias}${values.map((v) => (v.trim() ? ` ${v.trim()}` : '')).join('')}`
  const complete = values.every((v) => v.trim())
  const running = pending.has(`command:${text}`)

  return (
    <li>
      <form
        className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-x-2 gap-y-1.5 @md:grid-cols-[8rem_minmax(0,1fr)_6rem_auto]"
        onSubmit={(e) => {
          e.preventDefault()
          if (complete) void actions.custom(text)
        }}
      >
        <span className="col-start-1 row-start-1 truncate font-mono text-sm" title={command.aliases.map((a) => `.${a}`).join(', ')}>
          .{alias}
        </span>
        {command.parameters.length > 0 && (
          <div className="col-span-3 row-start-2 flex flex-wrap gap-2 @md:col-span-1 @md:col-start-2 @md:row-start-1">
            {command.parameters.map((parameter, i) => (
              <Input
                key={parameter}
                className="h-7 w-auto min-w-24 flex-1"
                placeholder={parameter}
                aria-label={`${alias} ${parameter}`}
                list={/name/i.test(parameter) ? 'command-panel-players' : undefined}
                value={values[i]}
                onChange={(e) => setValues((v) => v.map((x, j) => (j === i ? e.target.value : x)))}
              />
            ))}
          </div>
        )}
        <Badge variant="outline" className="col-start-2 row-start-1 @md:col-start-3" title="Auth level a player needs for this command">
          {authLevelName(command.authLevel)}
        </Badge>
        <Button
          type="submit"
          size="icon-sm"
          variant="outline"
          className="col-start-3 row-start-1 @md:col-start-4"
          disabled={!complete || running}
          aria-label={`Run .${alias}`}
          title={`Run ${text}`}
        >
          <Play />
        </Button>
      </form>
    </li>
  )
}
