import { useQueries, useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight } from 'lucide-react'
import { api, type PlayerAuth, type Server } from '@/api/client'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { encodeServerId } from '@/lib/server-id'

// The group a server has until it is given one; its modules are fixed (the base modules)
export const defaultGroup = 'default'

export type Step = {
  id: 'added' | 'connected' | 'modules' | 'admins' | 'discord'
  label: string
  hint: string
  done: boolean
  optional?: boolean
  /** A link to where the step is done */
  link?: React.ReactNode
}

const linkClass = 'inline-flex items-center gap-1 font-medium text-foreground underline-offset-4 hover:underline'

const modulesQuery = (serverGroup: string) => api.queryOptions('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })

/** The steps of a new server, as far as the UI can tell from its group's modules and admins and its Discord token. */
function setupSteps(server: Server, modules: string[], auth: PlayerAuth[], discordToken: string | null | undefined): Step[] {
  const serverId = encodeServerId(server.id)
  const group = server.serverGroup

  const settingsLink = (
    <Link to="/server/$serverId/edit" params={{ serverId }} className={linkClass}>
      Server settings
      <ArrowRight className="size-3.5" />
    </Link>
  )
  const groupLink = (hash: string) => (
    <Link to="/servergroup/$serverGroup" params={{ serverGroup: group }} hash={hash} className={linkClass}>
      Group {group}
      <ArrowRight className="size-3.5" />
    </Link>
  )

  return [
    { id: 'added', label: 'Add the server', hint: 'Its address, ports and admin key.', done: true },
    {
      id: 'connected',
      label: 'Connect the game server',
      hint: "Put the adminConnect lines from the server's settings in the game server's serverext.cfg, then start it.",
      done: server.socketState === 'Connected',
      link: settingsLink,
    },
    {
      id: 'modules',
      label: 'Choose its modules',
      hint: group === defaultGroup ? 'Give the server a group of its own: the default group runs the base modules only.' : `Turn on the modules that group ${group} runs.`,
      done: group !== defaultGroup && modules.length > 0,
      link: group === defaultGroup ? settingsLink : groupLink('modules'),
    },
    {
      id: 'admins',
      label: 'Add admins',
      hint: `Give players an admin level in group ${group}, so they can run the admin commands in game.`,
      done: auth.length > 0,
      link: groupLink('admin-levels'),
    },
    {
      id: 'discord',
      label: 'Set up the Discord bot',
      hint: 'A bot token and channels, for the game in Discord and admin commands from there.',
      done: !!discordToken,
      optional: true,
      link: settingsLink,
    },
  ]
}

/** The steps of one server, or null until its group's modules and admins and its settings have loaded. */
export function useSetupSteps(server: Server): Step[] | null {
  const group = server.serverGroup
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId: server.id } } })
  const modules = useQuery(modulesQuery(group))
  const auth = useQuery(playerAuthQuery(group))
  // Until everything has loaded, nothing: a card that shows and goes again would be noise. The settings only decide the
  // optional Discord step, so a server whose settings cannot be read (just added, not in the list yet) still has steps
  if (settings.isPending || !modules.data || !auth.data) return null
  return setupSteps(server, modules.data, auth.data, settings.data?.discordBotToken)
}

/** The required steps' progress: how many are done, of how many, and the first one left (none when all are done). */
export function setupProgress(steps: Step[]) {
  const required = steps.filter((s) => !s.optional)
  return { done: required.filter((s) => s.done).length, total: required.length, next: required.find((s) => !s.done) }
}

/**
 * The required steps of every listed server, for the dashboard: a request per group (its modules and admins), not per
 * server, and none for the settings, which only decide the optional Discord step. Also the groups' module and admin
 * counts, which the dashboard's group headings show.
 */
export function useSetupOverview(servers: Server[]) {
  const groups = [...new Set(servers.map((s) => s.serverGroup))]
  const modules = useQueries({ queries: groups.map((g) => modulesQuery(g)) })
  const auth = useQueries({ queries: groups.map((g) => playerAuthQuery(g)) })

  const groupInfo = new Map(groups.map((g, i) => [g, { modules: modules[i].data, admins: auth[i].data }]))
  const steps = new Map<string, Step[]>()
  for (const server of servers) {
    const info = groupInfo.get(server.serverGroup)
    if (info?.modules && info.admins) steps.set(server.id, setupSteps(server, info.modules, info.admins, null))
  }
  return { steps, groups: groupInfo }
}
