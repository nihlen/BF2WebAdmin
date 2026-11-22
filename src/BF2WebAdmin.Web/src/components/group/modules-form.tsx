import { useQueries, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight, RefreshCw, Search, TriangleAlert, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { api, type Server } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { commandsByModule, groupKind, groupModules, isRecommended, kindText, moduleDescription, moduleName } from '@/lib/modules'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'

const defaultGroup = 'default'

// The commands a module row shows before "+N more": two short lines
const shownCommands = 10

/**
 * The servers whose command lists say which commands each module adds: one connected server per group (the servers of
 * a group run the same modules), this group's first. The other groups' lists name the commands of modules that are off
 * here, so the page can say what turning one off takes away.
 */
function commandSources(servers: Server[], serverGroup: string) {
  const connected = servers.filter((s) => s.socketState === 'Connected').sort((a, b) => Number(b.serverGroup === serverGroup) - Number(a.serverGroup === serverGroup))
  return connected.filter((s, i) => connected.findIndex((other) => other.serverGroup === s.serverGroup) === i)
}

/**
 * The group's modules as checkboxes by kind, each with what it does, the commands it adds (from the servers' command
 * lists) or, when it is off, the commands that are unavailable, and a quiet mark on the ones a server of the group's
 * kind usually runs (2v2 or public, when the page can tell from its servers and modules). A filter finds a module by
 * its name, description or a command. Saving keeps the change for the next reload; Save and reload restarts the modules
 * on the group's servers.
 *
 * In the add server flow (onContinue) it starts from the given selection, and its bar is always there with the step's
 * own buttons: Save and continue (or Continue without changes) and Skip for now.
 */
export function ModulesForm({
  serverGroup,
  active,
  defaults,
  all,
  servers,
  initialSelected,
  onContinue,
}: {
  serverGroup: string
  active: string[]
  defaults: string[]
  all: string[]
  /** Every server, for the command lists; the group's own are those with this serverGroup */
  servers: Server[]
  /** The ticked modules to start from, when not the saved ones (the recommended ones for a new group) */
  initialSelected?: string[]
  /** The next step of the add server flow, after saving or skipping */
  onContinue?: () => void
}) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState(() => new Set(initialSelected ?? active))
  const [filter, setFilter] = useState('')
  const readOnly = serverGroup === defaultGroup
  const params = { path: { serverGroup } }
  const groupServers = servers.filter((s) => s.serverGroup === serverGroup)
  const serverCount = groupServers.length
  // The modules turned on or off since the last save
  const changes = all.filter((m) => selected.has(m) !== active.includes(m)).length
  const dirty = changes > 0
  const isOn = (name: string) => selected.has(name) || defaults.includes(name)
  // From the saved modules, so a mark does not come and go while the boxes are ticked
  const serverKind = groupKind(groupServers, active)

  // The row just ticked scrolls clear of the unsaved-changes bar that the tick may have brought up (the rows' scroll
  // margin is the bar's height), so the change stays in view
  const toggled = useRef<string | null>(null)
  useEffect(() => {
    if (!toggled.current) return
    document.getElementById(`module-row-${toggled.current}`)?.scrollIntoView({ block: 'nearest' })
    toggled.current = null
  }, [selected])

  const sources = commandSources(servers, serverGroup)
  const commandLists = useQueries({
    queries: sources.map((s) => api.queryOptions('get', '/api/servers/{serverId}/commands', { params: { path: { serverId: s.id } } })),
  })
  const commands = commandsByModule(
    commandLists.flatMap((q) => (q.data ? [q.data] : [])),
    all,
  )
  const commandsLoading = commandLists.some((q) => q.isPending)

  // The bot runs only where the server's settings have a token: asked for while Discord is on
  const discordOn = isOn('DiscordModule')
  const settings = useQueries({
    queries: groupServers.map((s) => ({ ...api.queryOptions('get', '/api/servers/{serverId}/settings', { params: { path: { serverId: s.id } } }), enabled: discordOn })),
  })
  const withoutToken = discordOn ? groupServers.filter((_, i) => settings[i].data && !settings[i].data.discordBotToken.trim()) : []

  const save = api.useMutation('put', '/api/server-groups/{serverGroup}/modules', {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['get', '/api/server-groups/{serverGroup}/modules'] }),
  })
  const reload = api.useMutation('post', '/api/server-groups/{serverGroup}/modules/reload')
  const busy = save.isPending || reload.isPending
  const serversText = `${serverCount} ${serverCount === 1 ? 'server' : 'servers'}`

  const saveAndReload = async () => {
    const id = toast.loading('Saving the modules…')
    try {
      if (dirty) await save.mutateAsync({ params, body: [...selected] })
      toast.loading(`Reloading the modules on ${serversText}…`, { id })
      await reload.mutateAsync({ params })
      toast.success(dirty ? `Saved, and reloaded on ${serversText}` : `Reloaded on ${serversText}`, { id })
    } catch {
      // The mutation cache shows the error
      toast.dismiss(id)
    }
  }

  // In the add flow: saved, then reloaded where a server of the group is connected (an offline one loads them when it
  // connects), then the next step
  const saveAndContinue = async () => {
    try {
      if (dirty) {
        await save.mutateAsync({ params, body: [...selected] })
        if (groupServers.some((s) => s.socketState === 'Connected')) await reload.mutateAsync({ params })
        toast.success('The modules are saved')
      }
      onContinue?.()
    } catch {
      // The mutation cache shows the error
    }
  }

  const toggle = (name: string, checked: boolean) => {
    toggled.current = name
    setSelected((s) => {
      const next = new Set(s)
      if (checked) next.add(name)
      else next.delete(name)
      return next
    })
  }

  // A command is found with or without its dot
  const query = filter.trim().toLowerCase().replace(/^\./, '')
  const matchesCommand = (alias: string) => !!query && alias.toLowerCase().includes(query)
  const matches = (name: string) =>
    !query ||
    [moduleName(name), name, moduleDescription(name) ?? '', isRecommended(name, serverKind) ? 'recommended' : ''].some((text) => text.toLowerCase().includes(query)) ||
    (commands.get(name) ?? []).some(matchesCommand)
  const kinds = groupModules(all)
    .map((kind) => ({ ...kind, modules: kind.modules.filter(matches) }))
    .filter((kind) => kind.modules.length > 0)

  return (
    <div className="space-y-4">
      {onContinue ? null : readOnly ? (
        <p className="text-muted-foreground">
          The default group runs the base modules and cannot be changed. To choose modules, give the server a group of its own in its settings.
        </p>
      ) : (
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <Button variant="outline" onClick={() => void saveAndReload()} disabled={dirty || busy || serverCount === 0}>
            <RefreshCw data-icon="inline-start" />
            Reload modules
          </Button>
          <p className="min-w-0 flex-[1_1_16rem] text-sm text-muted-foreground">
            A reload restarts the modules on {serverCount === 1 ? 'the server' : `the ${serversText}`} in this group. Their state, such as a running match, is lost.
          </p>
        </div>
      )}

      <div className="space-y-1.5">
        <div className="relative">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            type="search"
            className="pr-8 pl-8 [&::-webkit-search-cancel-button]:hidden"
            placeholder="Filter modules or commands"
            aria-label="Filter the modules by name, description or command"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
          {filter && (
            <Button variant="ghost" size="icon-xs" className="absolute top-1/2 right-1 -translate-y-1/2" aria-label="Clear the filter" onClick={() => setFilter('')}>
              <X />
            </Button>
          )}
        </div>
        {sources.length === 0 && (
          <p className="text-sm text-muted-foreground">The commands each module adds show here while one of your servers is connected: they come from its command list.</p>
        )}
      </div>

      <Card className="py-0">
        <CardContent className="divide-y px-0">
          {kinds.length === 0 && <p className="px-4 py-6 text-center text-sm text-muted-foreground">No module or command matches “{filter.trim()}”.</p>}
          {kinds.map((kind) => (
            <section key={kind.name} className="space-y-4 px-4 py-4" aria-labelledby={`modules-${kind.name}`}>
              <div>
                <h3 id={`modules-${kind.name}`} className="text-sm font-medium">
                  {kind.name}
                </h3>
                <p className="text-xs text-muted-foreground">{kind.description}</p>
              </div>
              {kind.modules.map((name) => {
                const always = defaults.includes(name)
                const description = moduleDescription(name)
                const recommended = !always && isRecommended(name, serverKind)
                const changed = selected.has(name) !== active.includes(name) && !always
                return (
                  <Field key={name} id={`module-row-${name}`} orientation="horizontal" className="scroll-mb-36 sm:scroll-mb-24">
                    <Checkbox id={`module-${name}`} checked={isOn(name)} disabled={readOnly || always} onCheckedChange={(checked) => toggle(name, checked)} />
                    <FieldContent className="min-w-0 gap-1.5">
                      <FieldLabel htmlFor={`module-${name}`} title={name} className="flex-wrap gap-y-1">
                        {moduleName(name)}
                        {always && <span className="font-normal text-muted-foreground">(always on)</span>}
                        {recommended && serverKind && (
                          <span className="text-xs font-normal text-muted-foreground" title={`Usually on for ${kindText(serverKind)}`}>
                            Recommended
                          </span>
                        )}
                        {changed && (
                          <Badge variant="outline" className="font-normal">
                            {selected.has(name) ? 'turned on' : 'turned off'}
                          </Badge>
                        )}
                      </FieldLabel>
                      {description && <FieldDescription className="nth-last-2:mt-0">{description}</FieldDescription>}
                      <ModuleCommands aliases={commands.get(name) ?? []} on={isOn(name)} loading={commandsLoading} expanded={!!query} highlight={matchesCommand} />
                      {name === 'DiscordModule' && withoutToken.map((server) => <MissingToken key={server.id} server={server} />)}
                    </FieldContent>
                  </Field>
                )
              })}
            </section>
          ))}
        </CardContent>
      </Card>

      {onContinue && (
        <div
          role="region"
          aria-label="Modules step"
          className="sticky bottom-4 z-10 flex flex-wrap items-center gap-2 rounded-xl border bg-background/95 p-3 shadow-lg backdrop-blur supports-backdrop-filter:bg-background/80"
        >
          <span className="w-full text-sm font-medium sm:mr-auto sm:w-auto">
            {dirty ? `${selected.size} ${selected.size === 1 ? 'module' : 'modules'} ticked, not saved yet` : 'No changes'}
          </span>
          <div className="ml-auto flex gap-2">
            <Button variant="ghost" size="sm" disabled={busy} onClick={onContinue}>
              Skip for now
            </Button>
            <Button size="sm" disabled={busy} onClick={() => void saveAndContinue()}>
              {dirty ? 'Save and continue' : 'Continue'}
            </Button>
          </div>
        </div>
      )}

      {/* While there are changes, the bar stays at the bottom of the window, so Save is in reach from anywhere in the list */}
      {dirty && !onContinue && (
        <div
          role="region"
          aria-label="Unsaved module changes"
          className="sticky bottom-4 z-10 flex flex-wrap items-center gap-2 rounded-xl border bg-background/95 p-3 shadow-lg backdrop-blur supports-backdrop-filter:bg-background/80"
        >
          {/* On a phone the count takes a line and the buttons the next, so all three fit */}
          <span className="w-full text-sm font-medium sm:mr-auto sm:w-auto">
            {changes} unsaved {changes === 1 ? 'change' : 'changes'}
          </span>
          <div className="ml-auto flex gap-2">
            <Button variant="ghost" size="sm" disabled={busy} onClick={() => setSelected(new Set(active))}>
              Discard
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={busy}
              onClick={() => save.mutate({ params, body: [...selected] }, { onSuccess: () => toast.success('Saved', { description: 'The modules load at the next reload.' }) })}
            >
              Save
            </Button>
            <Button size="sm" disabled={busy || serverCount === 0} onClick={() => void saveAndReload()} title={serverCount === 0 ? 'No server is in this group yet' : undefined}>
              Save and reload
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * The commands a module adds, as chips, the first few until "+N more"; while a filter is typed, all of them with the
 * matches marked. A module that is off says which commands are unavailable instead. A module no connected server runs
 * has no list to show.
 */
function ModuleCommands({
  aliases,
  on,
  loading,
  expanded,
  highlight,
}: {
  aliases: string[]
  on: boolean
  loading: boolean
  expanded: boolean
  highlight: (alias: string) => boolean
}) {
  const [open, setOpen] = useState(false)
  if (aliases.length === 0) return loading ? <span className="h-5 w-40 animate-pulse rounded bg-muted" aria-hidden /> : null

  if (!on) {
    const named = aliases.slice(0, 4).map((a) => `.${a}`)
    return (
      <p className="text-sm text-muted-foreground">
        <span className="font-medium text-foreground">Off:</span> {named.join(', ')}
        {aliases.length > named.length ? ` and ${aliases.length - named.length} more are` : aliases.length === 1 ? ' is' : ' are'} unavailable.
      </p>
    )
  }

  const shown = expanded || open ? aliases : aliases.slice(0, shownCommands)
  const hidden = aliases.length - shown.length
  return (
    <ul className="flex flex-wrap gap-1" aria-label="Commands it adds">
      {shown.map((alias) => (
        <li
          key={alias}
          className={cn(
            'rounded-md border px-1.5 font-mono text-xs leading-5 text-muted-foreground',
            highlight(alias) && 'border-primary/60 bg-primary/10 text-foreground',
          )}
        >
          .{alias}
        </li>
      ))}
      {(hidden > 0 || (open && !expanded && aliases.length > shownCommands)) && (
        <li>
          <button
            type="button"
            className="rounded-md px-1.5 text-xs leading-5 font-medium text-foreground underline-offset-4 hover:underline focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            aria-expanded={open}
            onClick={() => setOpen(!open)}
          >
            {open ? 'Fewer' : `+${hidden} more`}
          </button>
        </li>
      )}
    </ul>
  )
}

/** A server of the group without a bot token: the Discord module is on, but its bot cannot start there. */
function MissingToken({ server }: { server: Server }) {
  return (
    <p className="flex items-start gap-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-2.5 py-2 text-sm">
      <TriangleAlert className="mt-0.5 size-4 shrink-0 text-amber-500" aria-hidden />
      <span className="min-w-0">
        <span className="font-medium">{server.name}</span> has no bot token, so the bot does not run there.{' '}
        <Link
          to="/server/$serverId/edit"
          params={{ serverId: encodeServerId(server.id) }}
          className="inline-flex items-center gap-1 font-medium whitespace-nowrap underline-offset-4 hover:underline"
        >
          Its settings
          <ArrowRight className="size-3.5" />
        </Link>
      </span>
    </p>
  )
}
