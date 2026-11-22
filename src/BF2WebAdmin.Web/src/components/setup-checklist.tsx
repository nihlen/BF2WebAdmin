import { Link } from '@tanstack/react-router'
import { ArrowRight, ChevronDown, CircleCheck, Circle, ListChecks, Plus, X } from 'lucide-react'
import { useState } from 'react'
import type { Server } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { defaultGroup, setupProgress, type Step, useSetupSteps } from '@/hooks/use-setup-steps'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'

const dismissedKey = (serverId: string) => `setup.dismissed.${serverId}`

function readDismissed(serverId: string) {
  try {
    return localStorage.getItem(dismissedKey(serverId)) === '1'
  } catch {
    return false
  }
}

/** Whether the owner hid a server's setup steps (in this browser), and how to hide them. */
function useDismissed(serverId: string) {
  const [dismissed, setDismissed] = useState(() => readDismissed(serverId))
  const dismiss = () => {
    setDismissed(true)
    try {
      localStorage.setItem(dismissedKey(serverId), '1')
    } catch {
      // Hidden for this visit only
    }
  }
  return [dismissed, dismiss] as const
}

/**
 * "Set up <server>": the steps of a new server with their done states, each undone one linking to where it is done.
 * Shows while a required step is undone, until it is hidden (remembered per server in this browser).
 */
export function SetupChecklist({ server, className }: { server: Server; className?: string }) {
  const steps = useSetupSteps(server)
  const [dismissed, dismiss] = useDismissed(server.id)
  if (!steps || dismissed || steps.every((s) => s.done || s.optional)) return null

  const { done, total } = setupProgress(steps)

  return (
    <Card size="sm" className={className} aria-label={`Set up ${server.name}`}>
      <CardHeader>
        <CardTitle className="truncate" title={server.name}>
          Set up {server.name}
        </CardTitle>
        <CardDescription>
          {done} of {total} steps done
        </CardDescription>
        <CardAction>
          <Button variant="ghost" size="icon-sm" aria-label={`Hide the setup steps of ${server.name}`} title="Hide these steps" onClick={dismiss}>
            <X />
          </Button>
        </CardAction>
        <Progress value={done / total} />
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

function Progress({ value, className }: { value: number; className?: string }) {
  return (
    <div className={cn('col-span-full mt-1 h-1.5 overflow-hidden rounded-full bg-muted', className)} aria-hidden>
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

/**
 * The setup steps without their card, inside the connect card of an offline server: where the server is in its setup.
 * The card itself is the connect step, so that step points up to it instead of to the settings.
 */
export function SetupStepsSection({ server, className }: { server: Server; className?: string }) {
  const steps = useSetupSteps(server)
  if (!steps) return null
  const { done, total } = setupProgress(steps)

  return (
    <section className={cn('space-y-3', className)} aria-label={`Setup of ${server.name}`}>
      <div className="space-y-2">
        <div className="flex items-baseline justify-between gap-3">
          <h2 className="font-medium">Setup</h2>
          <span className="text-sm text-muted-foreground">
            {done} of {total} steps done
          </span>
        </div>
        <Progress value={done / total} />
      </div>
      <StepList steps={steps.map((s) => (s.id === 'connected' && !s.done ? { ...s, hint: 'The lines and the start above.', link: undefined } : s))} />
    </section>
  )
}

/**
 * A link to where a step is done, for the one-line summaries. An offline server's Live tab shows how to connect it, so
 * the connect step leads there; the others lead to the group page or the settings, as in the checklist.
 */
function NextStepLink({ server, step, className, children }: { server: Server; step: Step; className?: string; children: React.ReactNode }) {
  const serverId = encodeServerId(server.id)
  const serverGroup = server.serverGroup
  if (step.id === 'connected')
    return (
      <Link to="/server/$serverId" params={{ serverId }} className={className}>
        {children}
      </Link>
    )
  if (step.id === 'admins' || (step.id === 'modules' && serverGroup !== defaultGroup))
    return (
      <Link to="/servergroup/$serverGroup" params={{ serverGroup }} hash={step.id === 'admins' ? 'admin-levels' : 'modules'} className={className}>
        {children}
      </Link>
    )
  return (
    <Link to="/server/$serverId/edit" params={{ serverId }} className={className}>
      {children}
    </Link>
  )
}

const nextLinkClass = 'inline-flex min-w-0 items-center gap-1 font-medium text-foreground underline-offset-4 hover:underline'

/** "Setup 3 of 4 · Add admins →" with a small bar, on a dashboard card or row; nothing when the required steps are done. */
export function SetupLine({ server, steps, className }: { server: Server; steps: Step[] | undefined; className?: string }) {
  if (!steps) return null
  const { done, total, next } = setupProgress(steps)
  if (!next) return null
  return (
    <div className={cn('flex min-w-0 items-center gap-2 text-sm', className)}>
      <Progress value={done / total} className="mt-0 w-10 shrink-0" />
      <span className="shrink-0 text-muted-foreground tabular-nums">
        Setup {done} of {total}
      </span>
      <span aria-hidden className="text-muted-foreground/50">
        ·
      </span>
      <NextStepLink server={server} step={next} className={nextLinkClass}>
        <span className="truncate">{next.label}</span>
        <ArrowRight className="size-3.5 shrink-0" />
      </NextStepLink>
    </div>
  )
}

/**
 * One line under a server's header while a required setup step is open: the progress, the next step and the way to
 * all of them on Settings. It hides with the checklist's own hide button (the same choice, remembered per server).
 */
export function SetupStrip({ server, className }: { server: Server; className?: string }) {
  const steps = useSetupSteps(server)
  const [dismissed, dismiss] = useDismissed(server.id)
  if (!steps || dismissed) return null
  const { done, total, next } = setupProgress(steps)
  if (!next) return null

  return (
    <div role="status" className={cn('flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border bg-muted/30 py-1.5 pr-1.5 pl-3 text-sm', className)}>
      <Progress value={done / total} className="mt-0 w-12 shrink-0" />
      <span className="text-muted-foreground tabular-nums">
        Setup {done} of {total} done
      </span>
      <span className="inline-flex min-w-0 items-center gap-1.5">
        <span className="text-muted-foreground">Next:</span>
        <NextStepLink server={server} step={next} className={nextLinkClass}>
          {next.label}
          <ArrowRight className="size-3.5 shrink-0" />
        </NextStepLink>
      </span>
      <span className="ml-auto flex items-center gap-1">
        <Button variant="ghost" size="sm" nativeButton={false} render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(server.id) }} />}>
          All steps
        </Button>
        <Button variant="ghost" size="icon-sm" aria-label={`Hide the setup steps of ${server.name}`} title="Hide the setup steps" onClick={dismiss}>
          <X />
        </Button>
      </span>
    </div>
  )
}

const summaryKey = 'setup.summaryHidden'

function readSummaryHidden() {
  try {
    return localStorage.getItem(summaryKey)
  } catch {
    return null
  }
}

/**
 * The dashboard's one row about setup: "2 servers need setup steps", which opens into their checklists. Hiding it
 * holds for the servers it named; a server that needs steps later brings it back.
 */
export function SetupSummary({ servers, steps }: { servers: Server[]; steps: Map<string, Step[]> }) {
  const [open, setOpen] = useState(false)
  const [hidden, setHidden] = useState(readSummaryHidden)
  const needing = servers.filter((s) => {
    const serverSteps = steps.get(s.id)
    return serverSteps && setupProgress(serverSteps).next && !readDismissed(s.id)
  })
  const ids = needing.map((s) => s.id).join(' ')
  if (needing.length === 0 || hidden === ids) return null

  const hide = () => {
    setHidden(ids)
    try {
      localStorage.setItem(summaryKey, ids)
    } catch {
      // Hidden for this visit only
    }
  }

  return (
    <section aria-label="Setup" className="space-y-4">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border bg-muted/30 py-1.5 pr-1.5 pl-3 text-sm">
        <ListChecks className="size-4 shrink-0 text-primary" aria-hidden />
        <span className="font-medium">
          {needing.length} {needing.length === 1 ? 'server needs' : 'servers need'} setup steps
        </span>
        <span className="hidden min-w-0 truncate text-muted-foreground sm:inline">{needing.map((s) => s.name).join(', ')}</span>
        <span className="ml-auto flex items-center gap-1">
          <Button variant="ghost" size="sm" aria-expanded={open} onClick={() => setOpen(!open)}>
            {open ? 'Hide steps' : 'Show steps'}
            <ChevronDown data-icon="inline-end" className={cn(open && 'rotate-180')} />
          </Button>
          <Button variant="ghost" size="icon-sm" aria-label="Hide this reminder" title="Hide this reminder" onClick={hide}>
            <X />
          </Button>
        </span>
      </div>
      {open && (
        <div className="grid items-start gap-4 lg:grid-cols-2">
          {needing.map((server) => (
            <SetupChecklist key={server.id} server={server} />
          ))}
        </div>
      )}
    </section>
  )
}

/** The empty dashboard: the first server's steps ahead, starting with adding it. */
export function FirstServerSetup() {
  const steps: Step[] = [
    {
      id: 'added',
      label: 'Add the server',
      hint: 'Its address, game and query ports, and an admin key that WebAdmin can generate.',
      done: false,
    },
    { id: 'connected', label: 'Connect the game server', hint: "Put the lines the form shows in the game server's serverext.cfg, then start it.", done: false },
    { id: 'modules', label: 'Choose its modules', hint: 'Give the server a group, and turn on the modules the group runs.', done: false },
    { id: 'admins', label: 'Add admins', hint: 'Give players an admin level in the group.', done: false },
    { id: 'discord', label: 'Set up the Discord bot', hint: 'A bot token and channels.', done: false, optional: true },
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
