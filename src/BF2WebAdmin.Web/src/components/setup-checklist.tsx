import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight, CircleCheck, Circle, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { api, type Server } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'

// The group a server has until it is given one; its modules are fixed (the base modules)
const defaultGroup = 'default'

type Step = {
  label: string
  hint: string
  done: boolean
  optional?: boolean
  /** A link to where the step is done */
  link?: React.ReactNode
}

const linkClass = 'inline-flex items-center gap-1 font-medium text-foreground underline-offset-4 hover:underline'

/** The steps of a new server, as far as the UI can tell from the data it has. */
function useSetupSteps(server: Server): Step[] | null {
  const serverId = encodeServerId(server.id)
  const group = server.serverGroup
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId: server.id } } })
  const modules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: group } } })
  const auth = useQuery(playerAuthQuery(group))
  // Until everything has loaded, nothing: a card that shows and goes again would be noise
  if (!settings.data || !modules.data || !auth.data) return null

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
    { label: 'Add the server', hint: 'Its address, ports and admin key.', done: true },
    {
      label: 'Connect the game server',
      hint: "Put the adminConnect lines from the server's settings in the game server's serverext.cfg, then start it.",
      done: server.socketState === 'Connected',
      link: settingsLink,
    },
    {
      label: 'Choose its modules',
      hint: group === defaultGroup ? 'Give the server a group of its own: the default group runs the base modules only.' : `Turn on the modules that group ${group} runs.`,
      done: group !== defaultGroup && modules.data.length > 0,
      link: group === defaultGroup ? settingsLink : groupLink('modules'),
    },
    {
      label: 'Add admins',
      hint: `Give players an admin level in group ${group}, so they can run the admin commands in game.`,
      done: auth.data.length > 0,
      link: groupLink('admin-levels'),
    },
    {
      label: 'Set up the Discord bot',
      hint: 'A bot token and channels, for the game in Discord and admin commands from there.',
      done: !!settings.data.discordBotToken,
      optional: true,
      link: settingsLink,
    },
  ]
}

const dismissedKey = (serverId: string) => `setup.dismissed.${serverId}`

function readDismissed(serverId: string) {
  try {
    return localStorage.getItem(dismissedKey(serverId)) === '1'
  } catch {
    return false
  }
}

/**
 * "Set up <server>": the steps of a new server with their done states, each undone one linking to where it is done.
 * Shows while a required step is undone, until it is hidden (remembered per server in this browser).
 */
export function SetupChecklist({ server, className }: { server: Server; className?: string }) {
  const steps = useSetupSteps(server)
  const [dismissed, setDismissed] = useState(() => readDismissed(server.id))
  if (!steps || dismissed || steps.every((s) => s.done || s.optional)) return null

  const required = steps.filter((s) => !s.optional)
  const done = required.filter((s) => s.done).length
  const dismiss = () => {
    setDismissed(true)
    try {
      localStorage.setItem(dismissedKey(server.id), '1')
    } catch {
      // Hidden for this visit only
    }
  }

  return (
    <Card size="sm" className={className} aria-label={`Set up ${server.name}`}>
      <CardHeader>
        <CardTitle className="truncate" title={server.name}>
          Set up {server.name}
        </CardTitle>
        <CardDescription>
          {done} of {required.length} steps done
        </CardDescription>
        <CardAction>
          <Button variant="ghost" size="icon-sm" aria-label={`Hide the setup steps of ${server.name}`} title="Hide these steps" onClick={dismiss}>
            <X />
          </Button>
        </CardAction>
        <Progress value={done / required.length} />
      </CardHeader>
      <CardContent className="space-y-3">
        {/* The done steps on one line, so the card is about what is left */}
        <ul className="flex flex-wrap gap-x-3 gap-y-1 text-sm text-muted-foreground" aria-label="Done">
          {steps
            .filter((s) => s.done)
            .map((s) => (
              <li key={s.label} className="inline-flex items-center gap-1">
                <CircleCheck className="size-4 text-emerald-500" aria-hidden />
                {s.label}
              </li>
            ))}
        </ul>
        <StepList steps={steps.filter((s) => !s.done)} />
      </CardContent>
    </Card>
  )
}

function Progress({ value }: { value: number }) {
  return (
    <div className="col-span-full mt-1 h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden>
      <div className="h-full rounded-full bg-emerald-500" style={{ width: `${Math.round(value * 100)}%` }} />
    </div>
  )
}

/** The steps as a list: the done ones ticked, the undone ones with their hint and link, the next one marked. */
function StepList({ steps, upcoming }: { steps: Step[]; upcoming?: boolean }) {
  const next = steps.findIndex((s) => !s.done && !s.optional)
  return (
    <ol className="space-y-3">
      {steps.map((step, i) => {
        const current = i === next || (!step.done && step.optional && next === -1)
        return (
          <li key={step.label} className="flex gap-3">
            {step.done ? (
              <CircleCheck className="mt-0.5 size-5 shrink-0 text-emerald-500" aria-label="Done" />
            ) : (
              <Circle className={cn('mt-0.5 size-5 shrink-0', current ? 'text-primary' : 'text-muted-foreground/50')} aria-label="To do" />
            )}
            <div className="min-w-0 space-y-0.5">
              <p className={cn('font-medium', step.done && 'text-muted-foreground line-through decoration-muted-foreground/40', upcoming && !current && 'text-muted-foreground')}>
                {step.label}
                {step.optional && <span className="font-normal text-muted-foreground"> (optional)</span>}
              </p>
              {!step.done && (
                <p className="text-sm text-muted-foreground">
                  {step.hint} {step.link}
                </p>
              )}
            </div>
          </li>
        )
      })}
    </ol>
  )
}

/** The empty dashboard: the first server's steps ahead, starting with adding it. */
export function FirstServerSetup() {
  const steps: Step[] = [
    {
      label: 'Add the server',
      hint: 'Its address, game and query ports, and an admin key that WebAdmin can generate.',
      done: false,
    },
    { label: 'Connect the game server', hint: "Put the lines the form shows in the game server's serverext.cfg, then start it.", done: false },
    { label: 'Choose its modules', hint: 'Give the server a group, and turn on the modules the group runs.', done: false },
    { label: 'Add admins', hint: 'Give players an admin level in the group.', done: false },
    { label: 'Set up the Discord bot', hint: 'A bot token and channels.', done: false, optional: true },
  ]

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle className="text-lg">Set up your first server</CardTitle>
        <CardDescription>
          WebAdmin runs a Battlefield 2 server through the OpenBF2 server extension, which connects to WebAdmin. These are the steps; the first one
          starts here.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <StepList steps={steps} upcoming />
        <Button nativeButton={false} render={<Link to="/server/add" />}>
          <Plus data-icon="inline-start" />
          Add your first server
        </Button>
      </CardContent>
    </Card>
  )
}
