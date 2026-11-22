import { Link } from '@tanstack/react-router'
import { ArrowRight, ChevronRight, CircleCheck, CircleHelp, Loader2, PlugZap, Settings, Unplug } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api, errorMessage, type Server, type ServerSnapshot } from '@/api/client'
import { CopyButton } from '@/components/copy-button'
import { RelativeTime } from '@/components/relative-time'
import { ConnectionTestResults } from '@/components/server/connection-test'
import { SetupStepsSection } from '@/components/setup-checklist'
import { StepBar } from '@/components/step-bar'
import { setupProgress, useSetupSteps } from '@/hooks/use-setup-steps'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { healthQuery } from '@/hooks/use-server-live'
import { useServers } from '@/hooks/use-servers'
import { adminChannelQuery, defaultAdminPort } from '@/lib/admin-channel'
import { formatDayTime, formatTime } from '@/lib/format'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'

// The host the owner typed, while WebAdmin's configuration names none
const hostKey = 'connect.webAdminHost'

function readHost() {
  try {
    return localStorage.getItem(hostKey) || location.hostname
  } catch {
    return location.hostname
  }
}

type Line = { name: string; value: string; placeholder?: boolean; secret?: boolean }

/**
 * The game server's serverext.cfg lines, complete: WebAdmin's host, admin channel port and certificate pin (from
 * GET /api/admin-channel), the server id and the admin key. When WebAdmin's configuration names no public host, the
 * host is this page's, in a field the owner can change (remembered in this browser), with a note on where to set it.
 * The key is masked on the screen and copied in full.
 */
export function ConnectLines({ serverId, adminKey }: { serverId: string; adminKey: string | null | undefined }) {
  const channel = useQuery(adminChannelQuery)
  const [host, setHost] = useState(readHost)
  const changeHost = (value: string) => {
    setHost(value)
    try {
      localStorage.setItem(hostKey, value)
    } catch {
      // Remembered for this visit only
    }
  }
  if (channel.isPending) return <Skeleton className="h-48" />
  const publicHost = channel.data?.publicHost
  const pin = channel.data?.certificatePin
  const shownHost = publicHost ?? host.trim()
  const key = adminKey?.trim()
  const lines: Line[] = [
    shownHost ? { name: 'adminConnect', value: `${shownHost}:${channel.data?.port ?? defaultAdminPort}` } : { name: 'adminConnect', value: "<WebAdmin's host>", placeholder: true },
    { name: 'adminServerId', value: serverId },
    key ? { name: 'adminKey', value: key, secret: true } : { name: 'adminKey', value: '<no key is stored: set one in Settings>', placeholder: true },
    pin ? { name: 'adminCertSha256', value: pin } : { name: 'adminCertSha256', value: "<WebAdmin's certificate pin>", placeholder: true },
  ]
  const text = lines.map((l) => `${l.name} ${l.value}`).join('\n')

  return (
    <div className="space-y-4 text-sm">
      {!publicHost && (
        <Field>
          <FieldLabel htmlFor="webadmin-host">WebAdmin's host</FieldLabel>
          <Input id="webadmin-host" value={host} spellCheck={false} className="max-w-80 font-mono text-xs" onChange={(e) => changeHost(e.target.value)} />
          <FieldDescription>
            As the game server reaches this WebAdmin. Guessed from this page's address, as WebAdmin's configuration names none: its{' '}
            <code>ServerSettings:AdminChannel:PublicHost</code> sets it for everyone. What you type here is remembered in this browser.
          </FieldDescription>
        </Field>
      )}
      <div className="space-y-2">
        <p>
          Put these lines in the game server's <code>serverext.cfg</code>, beside the OpenBF2 <code>bf2</code> binary:
        </p>
        <div className="relative">
          <pre tabIndex={0} aria-label="serverext.cfg lines" className="overflow-x-auto rounded-lg bg-muted p-3 pr-10 font-mono text-xs leading-relaxed">
            {lines.map((l) => (
              <span key={l.name} className="block">
                {l.name}{' '}
                {l.secret ? (
                  <span title="Hidden here; Copy puts the whole key in">{`${l.value.slice(0, 6)}${'•'.repeat(12)}`}</span>
                ) : (
                  <span className={cn(l.placeholder && 'text-amber-600 dark:text-amber-400')}>{l.value}</span>
                )}
              </span>
            ))}
          </pre>
          <CopyButton value={text} label="Copy the lines" className="absolute top-2 right-2" />
        </div>
        {key && <p className="text-xs text-muted-foreground">The key is hidden here; Copy puts it in full.</p>}
        {channel.isError && (
          <p className="text-xs text-muted-foreground">
            WebAdmin's admin channel settings could not be loaded ({errorMessage(channel.error)}). The port is {defaultAdminPort} unless its{' '}
            <code>ServerSettings:AdminChannel:Port</code> says otherwise, and <code>BF2WebAdmin.Server --admin-pin</code> prints the pin.
          </p>
        )}
      </div>
    </div>
  )
}

/** The waiting state: the server list follows the hub's server updates, so it turns into the connected state by itself. */
function Waiting({ address, again }: { address: string; again?: boolean }) {
  return (
    <div role="status" className="flex items-start gap-3 rounded-lg border border-dashed p-3 text-sm">
      <Loader2 className="mt-0.5 size-4 shrink-0 animate-spin text-muted-foreground" />
      <div>
        <p className="font-medium">
          Waiting for <span className="font-mono">{address}</span> to connect{again && ' again'}…
        </p>
        <p className="text-muted-foreground">
          {again
            ? 'It connects by itself once it runs and reaches WebAdmin. This page changes by itself when it does.'
            : 'Start or restart the game server with these lines; it connects within seconds. This page changes by itself when it does.'}
        </p>
      </div>
    </div>
  )
}

/**
 * The connected state: where to go now, and where the setup stands. A server added to a group that has its modules and
 * admins already has those steps done, so the done ones are named with the reason; "every step is done" only shows
 * when the optional ones are done too, and an open optional step stays listed as optional. In the add flow (inFlow)
 * the flow's next steps follow instead, so only what is done shows.
 */
function Connected({ server, inFlow }: { server: Server; inFlow?: boolean }) {
  const steps = useSetupSteps(server)
  const next = steps && setupProgress(steps).next
  const optional = steps?.filter((s) => s.optional && !s.done) ?? []
  const fromGroup = steps?.filter((s) => s.done && (s.id === 'modules' || s.id === 'admins')) ?? []

  return (
    <div role="status" className="space-y-3 rounded-lg border border-emerald-500/40 bg-emerald-500/10 p-4 text-sm">
      <p className="flex items-center gap-2 text-base font-medium">
        <CircleCheck className="size-5 shrink-0 text-emerald-500" />
        <span className="min-w-0 break-words">{server.name} is connected</span>
      </p>
      {steps && (
        <ul className="flex flex-wrap gap-x-3 gap-y-1 text-muted-foreground" aria-label="Setup steps done">
          {steps
            .filter((s) => s.done)
            .map((s) => (
              <li key={s.id} className="inline-flex items-center gap-1">
                <CircleCheck className="size-4 text-emerald-500" aria-hidden />
                {s.label}
              </li>
            ))}
        </ul>
      )}
      {inFlow ? null : next ? (
        <p className="text-muted-foreground">
          <span className="font-medium text-foreground">Next: {next.label}.</span> {next.hint} {next.link}
        </p>
      ) : (
        steps && (
          <p className="text-muted-foreground">
            {optional.length > 0 ? 'The required setup steps are done.' : 'Every setup step is done.'}
            {fromGroup.length > 0 && ` Group ${server.serverGroup} already had its ${fromGroup.map((s) => (s.id === 'modules' ? 'modules' : 'admins')).join(' and ')}.`}
          </p>
        )
      )}
      {!inFlow && optional.map((step) => (
        <p key={step.id} className="text-muted-foreground">
          <span className="font-medium text-foreground">{step.label}</span> <span className="rounded-full border px-1.5 text-[0.7rem]">Optional</span> {step.hint}{' '}
          {step.link}
        </p>
      ))}
      {!inFlow && (
        <Button nativeButton={false} render={<Link to="/server/$serverId" params={{ serverId: encodeServerId(server.id) }} />}>
          Go to server
          <ArrowRight data-icon="inline-end" />
        </Button>
      )}
    </div>
  )
}

/** The config lines behind a disclosure, once they have done their work. */
export function ConfigLinesDisclosure({ serverId, adminKey }: { serverId: string; adminKey: string | null | undefined }) {
  return (
    <Collapsible className="rounded-lg border">
      <CollapsibleTrigger className="flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-left text-sm font-medium outline-none hover:bg-muted/50 focus-visible:ring-[3px] focus-visible:ring-ring/50">
        <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform in-data-[panel-open]:rotate-90" />
        Show the config lines
      </CollapsibleTrigger>
      <CollapsibleContent className="px-3 pb-3">
        <ConnectLines serverId={serverId} adminKey={adminKey} />
      </CollapsibleContent>
    </Collapsible>
  )
}

/**
 * Step 2 of adding a server: the lines for the game server, then a live wait for it to connect, then the flow's next
 * step: the modules (onContinue), at once when it connects or skipped to while it waits; or, when the server's group
 * already has its modules and admins, the end (onFinish), with the modules there to look over. The new server's
 * settings come from the form that saved them, so the generated key is in hand without another request.
 */
export function NewServerConnect({
  serverId,
  address,
  adminKey,
  onContinue,
  onFinish,
}: {
  serverId: string
  address: string
  adminKey: string | null
  onContinue: () => void
  onFinish: () => void
}) {
  const server = useServers().data?.find((s) => s.id === serverId)
  const connected = server?.socketState === 'Connected'

  return (
    <div className="max-w-3xl space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Connect the game server</CardTitle>
          <CardDescription>The game server dials WebAdmin with these lines; nothing connects to the game server.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {connected && server ? <Connected server={server} inFlow /> : <Waiting address={address} />}
          {connected ? <ConfigLinesDisclosure serverId={serverId} adminKey={adminKey} /> : <ConnectLines serverId={serverId} adminKey={adminKey} />}
        </CardContent>
      </Card>
      {connected && server ? (
        <ConnectedStepBar server={server} onContinue={onContinue} onFinish={onFinish} />
      ) : (
        <StepBar
          label="Connect step"
          status={
            <>
              The modules and admins can be set before it connects. The server's Live tab shows these lines until it does.{' '}
              <Link to="/server/$serverId" params={{ serverId: encodeServerId(serverId) }} className="font-medium text-foreground underline-offset-4 hover:underline">
                Open the server
              </Link>
            </>
          }
        >
          <Button variant="outline" onClick={onContinue}>
            Skip for now
          </Button>
        </StepBar>
      )}
    </div>
  )
}

/**
 * The connected server's way on. A server added to a group that has its modules and admins has nothing left to do, so
 * the bar says so and offers Finish, with Review modules for an owner who wants to look; otherwise on to the modules.
 * Nothing until the group's steps have loaded, so the buttons do not change under the pointer.
 */
function ConnectedStepBar({ server, onContinue, onFinish }: { server: Server; onContinue: () => void; onFinish: () => void }) {
  const steps = useSetupSteps(server)
  if (!steps) return null
  const ready = steps.filter((s) => s.id === 'modules' || s.id === 'admins').every((s) => s.done)

  return ready ? (
    <StepBar label="Connect step" status={`Group ${server.serverGroup} already has its modules and admins, so the server is ready.`}>
      <Button variant="outline" onClick={onContinue}>
        Review modules
      </Button>
      <Button onClick={onFinish}>
        Finish
        <ArrowRight data-icon="inline-end" />
      </Button>
    </StepBar>
  ) : (
    <StepBar label="Connect step">
      <Button onClick={onContinue}>
        Continue: modules
        <ArrowRight data-icon="inline-end" />
      </Button>
    </StepBar>
  )
}

/**
 * An offline server's Live tab: no players, chat or maps to show and no commands to take, so the page is about getting
 * it connected. It tells two cases apart by whether the admin channel was connected since WebAdmin started (the
 * server's disconnectedAt, or what WebAdmin kept of it: its last health window, map, events, players or chat):
 *
 * - Not connected since WebAdmin started: the lines for the game server, the live wait, a connection test with the
 *   stored settings, and the rest of its setup.
 * - Connection lost: when (the connection's end, or its last health window), what to check, the test, and the lines
 *   behind a disclosure, since they worked before.
 *
 * WebAdmin keeps none of it over a restart, so a server that was connected before then reads as not connected since.
 */
export function ServerConnectCard({ server, snapshot }: { server: Server; snapshot: ServerSnapshot }) {
  const serverId = server.id
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId } } })
  const test = api.useMutation('post', '/api/servers/test')
  const lastSeen = useQuery({ ...healthQuery(serverId), staleTime: Infinity }).data?.latest?.time
  const startedAt = useQuery(adminChannelQuery).data?.startedAt
  const lostAt = server.disconnectedAt ?? lastSeen
  const lost = !!lostAt || !!server.map || snapshot.eventLog.length > 0 || snapshot.players.length > 0 || snapshot.chatLog.length > 0
  const address = `${server.ipAddress}:${server.gamePort}`

  const testButtons = (
    <div className="space-y-3">
      {(test.isPending || test.data) && <ConnectionTestResults pending={test.isPending} results={test.data} discordConfigured={!!settings.data?.discordBotToken} />}
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" disabled={!settings.data || test.isPending} onClick={() => settings.data && test.mutate({ body: settings.data })}>
          Test connection
        </Button>
        <Button variant="outline" nativeButton={false} render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(serverId) }} />}>
          <Settings data-icon="inline-start" />
          Settings
        </Button>
      </div>
      {settings.isError && <p className="text-sm text-destructive">{errorMessage(settings.error)}</p>}
    </div>
  )

  if (lost)
    return (
      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Unplug className="size-5 shrink-0 text-destructive" />
            Connection lost
          </CardTitle>
          <CardDescription>
            {lostAt ? (
              <>
                {server.disconnectedAt ? 'The connection closed' : 'Last seen'} at {formatTime(lostAt)} (<RelativeTime value={lostAt} />)
                {server.map && <>, on {server.map}</>}.
              </>
            ) : (
              <>It was connected since WebAdmin started{server.map && <>, last on {server.map}</>}; when it was last seen is not known.</>
            )}{' '}
            Until it is back there are no players, chat or maps here, and commands cannot reach it.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <Waiting address={address} again />
          <div className="space-y-2 text-sm">
            <h3 className="font-medium">What to check</h3>
            <ul className="space-y-2 text-muted-foreground">
              <CheckItem title="Is the game server running?">
                Its process or container may have stopped, crashed or be restarting; a restart takes about a minute.
              </CheckItem>
              <CheckItem title="Can it reach WebAdmin?">
                The host and port of its <code>adminConnect</code> line must be reachable from the game server: a firewall, a changed address or WebAdmin
                restarting on another port stops it.
              </CheckItem>
              <CheckItem title="Did the key or certificate change?">
                A new admin key in Settings, or a new WebAdmin certificate, needs the same change in its <code>serverext.cfg</code>; the lines below have
                them.
              </CheckItem>
            </ul>
          </div>
          {testButtons}
          {settings.isPending ? <Skeleton className="h-11" /> : <ConfigLinesDisclosure serverId={serverId} adminKey={settings.data?.adminKey} />}
        </CardContent>
      </Card>
    )

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <PlugZap className="size-5 shrink-0 text-destructive" />
          Connect the game server
        </CardTitle>
        <CardDescription>
          It has not connected since WebAdmin started
          {startedAt && (
            <>
              {' '}
              on {formatDayTime(startedAt)} (<RelativeTime value={startedAt} />)
            </>
          )}
          , so there are no players, chat or maps here and commands cannot reach the server. The game server connects to WebAdmin by itself once it
          has these lines.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <Waiting address={address} />
        {settings.isPending ? <Skeleton className="h-48" /> : <ConnectLines serverId={serverId} adminKey={settings.data?.adminKey} />}
        {testButtons}
        <SetupStepsSection server={server} className="border-t pt-6" />
      </CardContent>
    </Card>
  )
}

function CheckItem({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <li className="flex gap-2">
      <CircleHelp aria-hidden className="mt-0.5 size-4 shrink-0" />
      <span>
        <span className="font-medium text-foreground">{title}</span> {children}
      </span>
    </li>
  )
}
