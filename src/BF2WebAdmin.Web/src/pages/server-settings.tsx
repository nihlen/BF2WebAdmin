import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useBlocker, useNavigate } from '@tanstack/react-router'
import { ArrowRight, Check, ChevronDown, ChevronRight, CircleCheck, Eye, EyeOff, KeyRound, Plus, Settings2, Sparkles } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type ServerSettings } from '@/api/client'
import { ConfirmButton } from '@/components/confirm-button'
import { CopyButton } from '@/components/copy-button'
import { ConfigLinesDisclosure, ConnectLines, NewServerConnect } from '@/components/server/connect-guide'
import { ConnectionTestResults } from '@/components/server/connection-test'
import { useServerPage } from '@/components/server/server-context'
import { ModulesForm } from '@/components/group/modules-form'
import { PlayerAuthList } from '@/components/group/player-auth-list'
import { SetupChecklist, SetupStepsSection } from '@/components/setup-checklist'
import { Chip, Segmented } from '@/components/toggles'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldContent, FieldDescription, FieldError, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectSeparator, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { playerAuthQuery } from '@/hooks/use-server-actions'
import { useServers } from '@/hooks/use-servers'
import { groupKind, isRecommended, type ServerKind } from '@/lib/modules'
import { encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'

// No group is filled in: the "default" group's modules cannot be changed, so an owner who kept it would be stuck
const newServer: ServerSettings = {
  serverId: '',
  serverGroup: '',
  ipAddress: '',
  gamePort: 16567,
  queryPort: 29900,
  discordBotToken: '',
  discordAdminChannel: '',
  discordNotificationChannel: '',
  discordMatchResultChannel: '',
  adminKey: null,
  isTestServer: false,
}

const adminKeyPattern = /^\s*([0-9a-fA-F]{2}){16,64}\s*$/
const ipPattern = /^\s*(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}\s*$/
const defaultGroup = 'default'
const newGroupValue = '\u0000new'

/** 32 random bytes as 64 hex digits, the longest key the admin channel takes. */
function generateKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(32))
  return [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('')
}

const addSteps = ['Server', 'Connect', 'Modules', 'Admins', 'Done'] as const
type AddStep = (typeof addSteps)[number]

/**
 * Adding a server, in the steps the setup checklist names: its details, connecting the game server, its group's modules
 * and a first admin, then where to go. The steps after the first are states of this page, not pages of their own: the
 * connect step needs the key the form just generated, which it has here without fetching it again. Connecting can be
 * skipped (the modules and admins do not need it), and so can the modules and admins. The Discord bot stays in the
 * server's settings: it is optional. When the owner leaves, the server's Live tab shows the connect card until the
 * server connects.
 */
export function ServerAddPage() {
  // The key is generated when the page opens, so the owner has nothing to make up
  const [initial] = useState<ServerSettings>(() => ({ ...newServer, adminKey: generateKey() }))
  const [added, setAdded] = useState<ServerSettings | null>(null)
  const [step, setStep] = useState<AddStep>('Server')

  const intro: Record<AddStep, string> = {
    Server: 'A game server that runs OpenBF2 with the server extension. After you add it here, the next step gives the lines for its configuration.',
    Connect: 'The server is saved. Now the game server needs these lines to connect to this WebAdmin; it does so by itself once it has them.',
    Modules: `The modules are what the server runs: the chat commands, game modes and tools. They are set for group ${added?.serverGroup}, so every server in it runs the same ones.`,
    Admins: `Admin levels decide who may run which chat commands, on every server in group ${added?.serverGroup}. Add yourself first, by your player hash.`,
    Done: 'The server is set up. You can change all of this later.',
  }

  return (
    <div className="space-y-6">
      <title>BF2 Web Admin - Add server</title>
      <div className="space-y-3">
        <h1 className="text-3xl font-semibold">Add a server</h1>
        <AddSteps current={addSteps.indexOf(step) + 1} />
        <p className="max-w-3xl text-muted-foreground">{intro[step]}</p>
      </div>
      {!added ? (
        <ServerSettingsForm
          initial={initial}
          onCreated={(settings) => {
            setAdded(settings)
            setStep('Connect')
          }}
        />
      ) : step === 'Connect' ? (
        <NewServerConnect serverId={added.serverId} address={`${added.ipAddress}:${added.gamePort}`} adminKey={added.adminKey} onContinue={() => setStep('Modules')} />
      ) : step === 'Modules' ? (
        <AddModulesStep added={added} onContinue={() => setStep('Admins')} />
      ) : step === 'Admins' ? (
        <AddAdminsStep serverGroup={added.serverGroup} onContinue={() => setStep('Done')} />
      ) : (
        <AddDoneStep added={added} />
      )}
    </div>
  )
}

function AddSteps({ current }: { current: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm" aria-label="Steps">
      {addSteps.map((label, i) => {
        const n = i + 1
        const done = n < current
        return (
          <li key={label} className="flex items-center gap-2" aria-current={n === current ? 'step' : undefined}>
            {i > 0 && <ChevronRight aria-hidden className="size-4 text-muted-foreground/60" />}
            <span
              className={cn(
                'flex size-6 items-center justify-center rounded-full border text-xs font-medium',
                n === current && 'border-primary bg-primary text-primary-foreground',
                done && 'border-emerald-500 text-emerald-500',
              )}
            >
              {done ? <Check className="size-3.5" aria-label="Done" /> : n}
            </span>
            {/* On a phone only the current step is named, so the five fit on a line or two */}
            <span className={cn(n === current ? 'font-medium' : 'text-muted-foreground max-sm:sr-only')}>{label}</span>
          </li>
        )
      })}
    </ol>
  )
}

const kindOptions = [
  { value: '2v2', label: '2v2 helicopter' },
  { value: 'public', label: 'Public' },
  { value: 'other', label: 'Something else' },
] as const

/**
 * Step 3: the group's modules. A group that already has its modules shows them as they are, since a change here changes
 * its other servers too. A new group starts from the modules usually run by its kind of server, which the owner picks
 * (guessed from the group's name or the server's map): all but the Discord bot, which needs a token from the server's
 * settings first.
 */
function AddModulesStep({ added, onContinue }: { added: ServerSettings; onContinue: () => void }) {
  const serverGroup = added.serverGroup
  const servers = useServers()
  const groupModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })
  const defaultModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: defaultGroup } } })
  const allModules = api.useQuery('get', '/api/modules')
  const groupServers = (servers.data ?? []).filter((s) => s.serverGroup === serverGroup)
  const isNewGroup = !groupServers.some((s) => s.id !== added.serverId)
  const [kind, setKind] = useState<ServerKind | 'other'>(() => (/2v2/i.test(serverGroup) ? '2v2' : (groupKind(groupServers, []) ?? 'other')))

  if (serverGroup === defaultGroup)
    return (
      <Card className="max-w-3xl">
        <CardHeader>
          <CardTitle className="text-lg">The default group</CardTitle>
          <CardDescription>The default group runs the base modules only, and they cannot be changed. Give the server a group of its own in its settings to choose them.</CardDescription>
        </CardHeader>
        <CardFooter>
          <Button onClick={onContinue}>
            Continue
            <ArrowRight data-icon="inline-end" />
          </Button>
        </CardFooter>
      </Card>
    )
  if (!groupModules.data || !defaultModules.data || !allModules.data) return <Skeleton className="h-96 max-w-3xl" />
  const recommended = kind === 'other' ? [] : allModules.data.filter((m) => m !== 'DiscordModule' && isRecommended(m, kind))

  return (
    <div className="max-w-3xl space-y-4">
      <h2 className="text-xl font-semibold">Modules of group {serverGroup}</h2>
      {isNewGroup ? (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm">
          <span className="font-medium">What kind of server is it?</span>
          <Segmented label="Kind of server" options={kindOptions} value={kind} onChange={setKind} />
          <span className="w-full text-muted-foreground">
            {kind === 'other' ? 'Tick the modules it needs.' : `The modules a ${kindOptions.find((k) => k.value === kind)?.label.toLowerCase()} server usually runs are ticked. The Discord bot comes later, with its token in the server's settings.`}
          </span>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          Group {serverGroup} already has its modules. A change here changes them on its other {groupServers.length - 1 === 1 ? 'server' : 'servers'} too.
        </p>
      )}
      <ModulesForm
        // A new kind starts the ticks over
        key={kind}
        serverGroup={serverGroup}
        active={groupModules.data}
        defaults={defaultModules.data}
        all={allModules.data}
        servers={servers.data ?? []}
        initialSelected={isNewGroup ? [...new Set([...groupModules.data, ...recommended])] : undefined}
        onContinue={onContinue}
      />
    </div>
  )
}

/** Step 4: the group's admin levels, with what each level may do, to add the first admin by hash. */
function AddAdminsStep({ serverGroup, onContinue }: { serverGroup: string; onContinue: () => void }) {
  const auth = useQuery(playerAuthQuery(serverGroup))
  const any = (auth.data?.length ?? 0) > 0
  return (
    <div className="max-w-3xl space-y-4">
      <h2 className="text-xl font-semibold">Admins of group {serverGroup}</h2>
      <PlayerAuthList serverGroup={serverGroup} />
      <div className="flex flex-wrap items-center gap-3 border-t pt-4">
        <Button variant={any ? 'default' : 'outline'} onClick={onContinue}>
          {any ? 'Continue' : 'Skip for now'}
          {any && <ArrowRight data-icon="inline-end" />}
        </Button>
        <span className="text-sm text-muted-foreground">{any ? `${auth.data?.length} ${auth.data?.length === 1 ? 'player has' : 'players have'} a level in group ${serverGroup}.` : 'A player can also be given a level later, from their menu on the server page.'}</span>
      </div>
    </div>
  )
}

/** The end of the flow: where the setup stands, the optional Discord bot, and the way to the server. */
function AddDoneStep({ added }: { added: ServerSettings }) {
  const server = useServers().data?.find((s) => s.id === added.serverId)
  const params = { serverId: encodeServerId(added.serverId) }
  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <CircleCheck className="size-5 shrink-0 text-emerald-500" />
          {server?.name ?? added.serverId} is added
        </CardTitle>
        <CardDescription>
          {server?.socketState === 'Connected' ? 'It is connected and runs the modules of its group.' : 'It connects by itself once the game server has the lines from step 2; its Live tab shows them until then.'}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        {server && <SetupStepsSection server={server} />}
        <p className="text-muted-foreground">
          <span className="font-medium text-foreground">Discord bot (optional):</span> a bot token and channels, for the game in Discord and admin commands from
          there, are in the{' '}
          <Link to="/server/$serverId/edit" params={params} className="font-medium text-foreground underline-offset-4 hover:underline">
            server's settings
          </Link>
          .
        </p>
      </CardContent>
      <CardFooter className="gap-2">
        <Button nativeButton={false} render={<Link to="/server/$serverId" params={params} />}>
          Go to the server
          <ArrowRight data-icon="inline-end" />
        </Button>
      </CardFooter>
    </Card>
  )
}

/** The Settings tab of a server. */
export function ServerEditPage() {
  const { serverId } = useServerPage()
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId } } })
  const [version, setVersion] = useState(0)

  return (
    <div className="space-y-6">
      {settings.isPending && <Skeleton className="h-96 max-w-2xl" />}
      {settings.isError && <p className="text-destructive">{errorMessage(settings.error)}</p>}
      {settings.data && <ServerSettingsForm key={`${serverId}#${version}`} initial={settings.data} serverId={serverId} onDiscard={() => setVersion((v) => v + 1)} />}
    </div>
  )
}

type FieldName = 'serverGroup' | 'ipAddress' | 'gamePort' | 'queryPort' | 'adminKey'

/**
 * The settings of a new server (no serverId) or an existing one, whose address and game port are fixed. A new one is
 * handed to onCreated once saved, for the connect step.
 */
function ServerSettingsForm({
  initial,
  serverId,
  onCreated,
  onDiscard,
}: {
  initial: ServerSettings
  serverId?: string
  onCreated?: (settings: ServerSettings) => void
  /** Starts the form over from the saved settings (a new key remounts it, so the secret fields close again too) */
  onDiscard?: () => void
}) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [form, setForm] = useState(initial)
  const set = <K extends keyof ServerSettings>(key: K, value: ServerSettings[K]) => setForm((f) => ({ ...f, [key]: value }))
  const dirty = (Object.keys(initial) as (keyof ServerSettings)[]).some((key) => (form[key] ?? '') !== (initial[key] ?? ''))
  // Errors show once a field has been left, or after a save was tried: not while the page opens
  const [touched, setTouched] = useState<ReadonlySet<FieldName>>(new Set())
  const [submitted, setSubmitted] = useState(false)
  const touch = (name: FieldName) => () => setTouched((t) => new Set(t).add(name))
  // Set before the navigation after a save or a removal, which must not ask
  const leaving = useRef(false)
  const blocker = useBlocker({ shouldBlockFn: () => dirty && !leaving.current, enableBeforeUnload: () => dirty && !leaving.current, withResolver: true })

  const errors: Partial<Record<FieldName, string>> = {}
  if (!form.serverGroup.trim()) errors.serverGroup = 'Choose a group, or name a new one'
  if (!serverId && !ipPattern.test(form.ipAddress)) errors.ipAddress = 'An IPv4 address, such as 203.0.113.10'
  const badPort = (port: number) => !Number.isInteger(port) || port < 1 || port > 65535
  if (badPort(form.gamePort)) errors.gamePort = 'A port from 1 to 65535'
  if (badPort(form.queryPort)) errors.queryPort = 'A port from 1 to 65535'
  if (form.adminKey?.trim() && !adminKeyPattern.test(form.adminKey)) errors.adminKey = '32 to 128 hex digits (0-9, a-f), an even number'
  const shown = (name: FieldName) => (submitted || touched.has(name) ? errors[name] : undefined)
  const invalid = Object.keys(errors).length > 0

  const toDto = (): ServerSettings => ({
    ...form,
    serverId: serverId ?? `${form.ipAddress.trim()}:${form.gamePort}`,
    serverGroup: form.serverGroup.trim(),
    ipAddress: form.ipAddress.trim(),
    adminKey: form.adminKey?.trim() || null,
  })

  const onSaved = async (savedId: string) => {
    leaving.current = true
    toast.success('Server has been saved')
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/servers'] })
    await navigate({ to: '/server/$serverId', params: { serverId: encodeServerId(savedId) } })
  }

  const create = api.useMutation('post', '/api/servers', {
    onSuccess: async (_, { body }) => {
      leaving.current = true
      // No toast: the connect step that follows says the server is saved, and a toast would cover its lines on a phone.
      // The list then has the new server, offline, and the hub's server updates turn it connected
      await queryClient.invalidateQueries({ queryKey: ['get', '/api/servers'] })
      onCreated?.(body)
    },
  })
  const update = api.useMutation('put', '/api/servers/{serverId}/settings', { onSuccess: (_, { body }) => onSaved(body.serverId) })
  const remove = api.useMutation('delete', '/api/servers/{serverId}', {
    onSuccess: async () => {
      leaving.current = true
      toast.success('Server has been removed')
      await navigate({ to: '/' })
    },
  })
  const test = api.useMutation('post', '/api/servers/test')
  const saving = create.isPending || update.isPending
  // The saved server as the server list has it, which the hub keeps current: its setup steps and whether it is connected
  const server = useServers().data?.find((s) => s.id === serverId)
  const connected = server?.socketState === 'Connected'

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitted(true)
    if (invalid) return
    const body = toDto()
    if (serverId) update.mutate({ params: { path: { serverId } }, body })
    else create.mutate({ body })
  }

  // Why Save cannot be pressed, said next to it
  const saveBlocked = saving ? null : serverId && !dirty ? 'No changes to save' : submitted && invalid ? 'Fix the fields marked in red' : null

  return (
    <form onSubmit={submit} noValidate className="grid items-start gap-6 lg:grid-cols-[minmax(0,42rem)_minmax(0,1fr)]">
      <div className="min-w-0 space-y-6">
        {serverId && (
          <div className="space-y-1">
            <h2 className="text-xl font-semibold">Settings</h2>
            <p className="text-sm text-muted-foreground">How WebAdmin reaches this server, its group and its Discord bot.</p>
          </div>
        )}
        {/* The full setup checklist lives here; the other pages only point to it */}
        {server && <SetupChecklist server={server} />}

        <Card>
          <CardHeader>
            <CardTitle>Game server</CardTitle>
            <CardDescription>Where the game server runs. {serverId && 'The address and game port identify the server and cannot change.'}</CardDescription>
          </CardHeader>
          <CardContent>
            <FieldGroup>
              <Field data-invalid={!!shown('serverGroup') || undefined}>
                <FieldLabel htmlFor="serverGroup">Server group</FieldLabel>
                <GroupPicker value={form.serverGroup} onChange={(group) => set('serverGroup', group)} onBlur={touch('serverGroup')} invalid={!!shown('serverGroup')} />
                <FieldDescription>
                  Servers in a group share their modules and admins.
                  {form.serverGroup.trim() === defaultGroup && ' The default group runs the base modules only and they cannot be changed: give the server a group of its own to choose its modules.'}
                  {serverId && form.serverGroup.trim() && (
                    <>
                      {' '}
                      <Link to="/servergroup/$serverGroup" params={{ serverGroup: initial.serverGroup }} className="inline-flex items-center gap-1 text-foreground underline-offset-4 hover:underline">
                        <Settings2 className="size-3.5" />
                        Group settings of {initial.serverGroup}
                      </Link>
                    </>
                  )}
                </FieldDescription>
                {shown('serverGroup') && <FieldError>{shown('serverGroup')}</FieldError>}
              </Field>
              <Field data-invalid={!!shown('ipAddress') || undefined}>
                <FieldLabel htmlFor="ipAddress">IP address</FieldLabel>
                <Input
                  id="ipAddress"
                  value={form.ipAddress}
                  readOnly={!!serverId}
                  required
                  inputMode="decimal"
                  placeholder="203.0.113.10"
                  aria-invalid={!!shown('ipAddress')}
                  onChange={(e) => set('ipAddress', e.target.value)}
                  onBlur={touch('ipAddress')}
                />
                <FieldDescription>The game server's public address, as players connect to it.</FieldDescription>
                {shown('ipAddress') && <FieldError>{shown('ipAddress')}</FieldError>}
              </Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <PortField name="gamePort" label="Game port" hint="UDP, 16567 by default" value={form.gamePort} readOnly={!!serverId} error={shown('gamePort')} onChange={(v) => set('gamePort', v)} onBlur={touch('gamePort')} />
                <PortField name="queryPort" label="Query port" hint="UDP, GameSpy, 29900 by default" value={form.queryPort} error={shown('queryPort')} onChange={(v) => set('queryPort', v)} onBlur={touch('queryPort')} />
              </div>
            </FieldGroup>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Admin channel</CardTitle>
            <CardDescription>
              The game server's server extension connects to this WebAdmin and proves who it is with this key. The game server needs the same key in
              its <code>serverext.cfg</code> (<code>adminKey</code>); without a key it cannot connect.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Field data-invalid={!!shown('adminKey') || undefined}>
              <FieldLabel htmlFor="adminKey">Admin key</FieldLabel>
              <SecretField
                id="adminKey"
                stored={!!serverId && !!initial.adminKey}
                value={form.adminKey ?? ''}
                initialValue={initial.adminKey ?? ''}
                invalid={!!shown('adminKey')}
                onChange={(value) => set('adminKey', value)}
                onBlur={touch('adminKey')}
                generate
                setLabel="Key set"
              />
              <FieldDescription>
                {serverId
                  ? '32 to 128 hex digits. Generate makes a new random key of 64 digits; the game server then needs the new key too.'
                  : 'WebAdmin made a random key of 64 hex digits; the next step puts it in the lines for the game server. Your own key needs 32 to 128 hex digits.'}
              </FieldDescription>
              {shown('adminKey') && <FieldError>{shown('adminKey')}</FieldError>}
            </Field>
          </CardContent>
        </Card>

        <OptionalSection form={form} initial={initial} set={set} />

        {/* At the bottom of the window while the form is longer than it, so Save is in reach from every field; on a
            server's Settings tab on a phone, above the bottom tab bar */}
        <div
          role="region"
          aria-label="Save or cancel"
          className={cn(
            'sticky bottom-4 z-10 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border bg-background/95 p-3 shadow-lg backdrop-blur supports-backdrop-filter:bg-background/80',
            serverId && 'max-md:bottom-[calc(4.25rem+env(safe-area-inset-bottom))]',
          )}
        >
          <Button type="submit" disabled={saving || (!!serverId && !dirty)}>
            {serverId ? 'Save changes' : 'Add server'}
          </Button>
          <Button
            type="button"
            variant="ghost"
            disabled={saving || (!!serverId && !dirty)}
            onClick={() => (serverId ? onDiscard?.() : void navigate({ to: '/' }))}
          >
            {serverId ? 'Discard' : 'Cancel'}
          </Button>
          <Button type="button" variant="outline" disabled={test.isPending} onClick={() => test.mutate({ body: toDto() })}>
            Test connection
          </Button>
          {saving && <span className="text-sm text-muted-foreground">Saving…</span>}
          {saveBlocked && <span className="text-sm text-muted-foreground">{saveBlocked}</span>}
        </div>

        {serverId && (
          <Card className="border-destructive/40 ring-destructive/30">
            <CardHeader>
              <CardTitle className="text-destructive">Danger zone</CardTitle>
              <CardDescription>
                Removing the server deletes its settings from WebAdmin. Its matches and bans stay in the database; the game server cannot connect any
                more.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <ConfirmButton
                title="Remove this server?"
                description={`${serverId} is removed from WebAdmin and disconnected. You can add it again later with its admin key.`}
                confirmLabel="Remove server"
                onConfirm={() => remove.mutate({ params: { path: { serverId } } })}
              >
                Remove server
              </ConfirmButton>
            </CardContent>
          </Card>
        )}
      </div>

      <aside className="min-w-0 space-y-6 lg:sticky lg:top-6">
        <Card>
          <CardHeader>
            <CardTitle>Connection test</CardTitle>
            <CardDescription>Checks the query port and whether the game server's admin channel is connected, with the settings as they are in the form.</CardDescription>
          </CardHeader>
          <CardContent>
            {test.isPending || test.data ? (
              <ConnectionTestResults pending={test.isPending} results={test.data} discordConfigured={!!form.discordBotToken} unsaved={!serverId} />
            ) : connected ? (
              <p className="flex items-center gap-2 text-sm">
                <CircleCheck className="size-4 shrink-0 text-emerald-500" />
                <span>
                  <span className="font-medium">Admin channel connected.</span> <span className="text-muted-foreground">Test connection checks the query port too.</span>
                </span>
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">
                {serverId ? 'Not tested yet. Press Test connection.' : 'Press Test connection to check that the query port answers. The admin channel connects after you add the server.'}
              </p>
            )}
          </CardContent>
        </Card>
        {serverId ? (
          <Card>
            <CardHeader>
              <CardTitle>Connect the game server</CardTitle>
              <CardDescription>
                {connected ? 'The game server is connected with these lines in its serverext.cfg.' : 'The game server dials WebAdmin with these lines; nothing connects to the game server.'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {/* The lines show the key as it is in the form, so a new key here is the one to copy */}
              {connected ? <ConfigLinesDisclosure serverId={serverId} adminKey={form.adminKey} /> : <ConnectLines serverId={serverId} adminKey={form.adminKey} />}
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardHeader>
              <CardTitle>Next: connect the game server</CardTitle>
              <CardDescription>
                After you add the server, WebAdmin shows the lines for the game server's <code>serverext.cfg</code> with this key, and waits for it to
                connect.
              </CardDescription>
            </CardHeader>
          </Card>
        )}
      </aside>

      <AlertDialog open={blocker.status === 'blocked'} onOpenChange={(open) => !open && blocker.reset?.()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Leave without saving?</AlertDialogTitle>
            <AlertDialogDescription>The changes to this server's settings are lost.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Stay</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => blocker.proceed?.()}>
              Leave
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  )
}

function PortField({
  name,
  label,
  hint,
  value,
  readOnly,
  error,
  onChange,
  onBlur,
}: {
  name: string
  label: string
  hint: string
  value: number
  readOnly?: boolean
  error: string | undefined
  onChange: (value: number) => void
  onBlur: () => void
}) {
  return (
    <Field data-invalid={!!error || undefined}>
      <FieldLabel htmlFor={name}>{label}</FieldLabel>
      <Input
        id={name}
        type="number"
        min={1}
        max={65535}
        required
        readOnly={readOnly}
        value={Number.isNaN(value) ? '' : String(value)}
        aria-invalid={!!error}
        onChange={(e) => onChange(e.target.value === '' ? Number.NaN : Number(e.target.value))}
        onBlur={onBlur}
      />
      <FieldDescription>{hint}</FieldDescription>
      {error && <FieldError>{error}</FieldError>}
    </Field>
  )
}

// Names to start from when there is no group yet: one click fills the field
const suggestedGroups = ['main', '2v2', 'public']

/**
 * The server group: one of the groups the servers have, or a new one by name ("New group" beside the list, and in it).
 * With no servers yet, only the name, with suggestions.
 */
function GroupPicker({ value, onChange, onBlur, invalid }: { value: string; onChange: (group: string) => void; onBlur: () => void; invalid: boolean }) {
  const servers = useServers()
  const groups = [...new Set((servers.data ?? []).map((s) => s.serverGroup))].sort()
  // Until the user picks, the list decides (it may still be loading): a name that is not one of the groups is typed
  const [picked, setTyping] = useState<boolean | null>(null)
  const typing = picked ?? (groups.length === 0 || (value !== '' && !groups.includes(value)))
  const newGroup = () => {
    setTyping(true)
    onChange('')
  }

  if (typing)
    return (
      <div className="space-y-2">
        <div className="flex gap-2">
          <Input
            id="serverGroup"
            value={value}
            placeholder={groups.length === 0 ? 'Name the first group, such as main' : 'The new group\'s name'}
            autoFocus={picked === true}
            aria-invalid={invalid}
            onChange={(e) => onChange(e.target.value)}
            onBlur={onBlur}
          />
          {groups.length > 0 && (
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setTyping(false)
                onChange('')
              }}
            >
              Pick a group
            </Button>
          )}
        </div>
        {groups.length === 0 && (
          <div className="flex flex-wrap items-center gap-1.5 text-sm text-muted-foreground">
            Suggested:
            {suggestedGroups.map((name) => (
              <Chip key={name} pressed={value === name} onClick={() => onChange(name)}>
                {name}
              </Chip>
            ))}
          </div>
        )}
      </div>
    )

  const items = [...groups.map((g) => ({ value: g, label: g })), { value: newGroupValue, label: 'New group…' }]
  return (
    <div className="flex gap-2">
      <Select
        items={items}
        value={value || null}
        onValueChange={(next) => {
          if (next === newGroupValue) newGroup()
          else onChange(next ?? '')
        }}
        onOpenChange={(open) => !open && onBlur()}
      >
        <SelectTrigger id="serverGroup" className="min-w-0 flex-1" aria-invalid={invalid}>
          <SelectValue placeholder="Choose a group" />
        </SelectTrigger>
        <SelectContent>
          {groups.map((g) => (
            <SelectItem key={g} value={g}>
              {g}
            </SelectItem>
          ))}
          <SelectSeparator />
          <SelectItem value={newGroupValue}>New group…</SelectItem>
        </SelectContent>
      </Select>
      <Button type="button" variant="outline" onClick={newGroup}>
        <Plus data-icon="inline-start" />
        New group
      </Button>
    </div>
  )
}

/**
 * A secret (the admin key, the bot token). A stored one is not shown as a field: "Key set" with Replace, so the page
 * opens without a value to judge. Replace shows the field empty; Cancel keeps the stored one.
 */
function SecretField({
  id,
  stored,
  value,
  initialValue,
  invalid,
  onChange,
  onBlur,
  generate,
  setLabel,
}: {
  id: string
  stored: boolean
  value: string
  initialValue: string
  invalid?: boolean
  onChange: (value: string) => void
  onBlur?: () => void
  generate?: boolean
  setLabel: string
}) {
  const [replacing, setReplacing] = useState(!stored)
  const [shown, setShown] = useState(false)
  const input = useRef<HTMLInputElement>(null)

  if (!replacing)
    return (
      <div className="flex flex-wrap items-center gap-2">
        <span className="inline-flex h-8 items-center gap-2 rounded-lg border border-dashed px-2.5 text-sm text-muted-foreground">
          <KeyRound className="size-4 text-emerald-500" />
          {setLabel}
        </span>
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            setReplacing(true)
            onChange('')
          }}
        >
          Replace
        </Button>
      </div>
    )

  return (
    <div className="flex flex-wrap gap-2">
      <div className="relative min-w-0 flex-[1_1_16rem]">
        <Input
          ref={input}
          id={id}
          value={value}
          type={shown ? 'text' : 'password'}
          autoComplete="off"
          spellCheck={false}
          className={cn('pr-16', shown && 'font-mono text-xs')}
          aria-invalid={invalid}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
        />
        <div className="absolute top-1/2 right-1.5 flex -translate-y-1/2 gap-0.5">
          {value && <CopyButton value={value} label="Copy" />}
          <Button
            type="button"
            variant="ghost"
            size="icon-xs"
            aria-label={shown ? 'Hide' : 'Show'}
            title={shown ? 'Hide' : 'Show'}
            onClick={() => setShown((s) => !s)}
          >
            {shown ? <EyeOff /> : <Eye />}
          </Button>
        </div>
      </div>
      {generate && (
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            onChange(generateKey())
            setShown(true)
          }}
        >
          <Sparkles data-icon="inline-start" />
          {value ? 'Regenerate' : 'Generate key'}
        </Button>
      )}
      {/* A key the game server already has: the field empties and takes it */}
      {generate && value && !stored && (
        <Button
          type="button"
          variant="ghost"
          onClick={() => {
            onChange('')
            setShown(true)
            input.current?.focus()
          }}
        >
          Use my own key
        </Button>
      )}
      {stored && (
        <Button
          type="button"
          variant="ghost"
          onClick={() => {
            setReplacing(false)
            onChange(initialValue)
          }}
        >
          Cancel
        </Button>
      )}
    </div>
  )
}

const discordChannels = [
  { key: 'discordAdminChannel', label: 'Admin channel', hint: 'Where admins read the game chat and run commands with !' },
  { key: 'discordNotificationChannel', label: 'Notification channel', hint: 'Joins, server restarts and other notices' },
  { key: 'discordMatchResultChannel', label: 'Match result channel', hint: 'The result of every finished 2v2 match' },
] as const

/**
 * What a server works without: the Discord bot and the test-server flag. Closed until opened, and open from the start
 * when something in it is set, so the required fields come first.
 */
function OptionalSection({
  form,
  initial,
  set,
}: {
  form: ServerSettings
  initial: ServerSettings
  set: <K extends keyof ServerSettings>(key: K, value: ServerSettings[K]) => void
}) {
  const discord = !!(initial.discordBotToken || initial.discordAdminChannel || initial.discordNotificationChannel || initial.discordMatchResultChannel)
  const [open, setOpen] = useState(discord || initial.isTestServer)
  const summary = [
    discord ? 'The Discord bot is set up.' : 'The server runs without them; no Discord bot is set up.',
    initial.isTestServer && 'Marked as a test server.',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <Card>
      <CardHeader>
        <button type="button" className="flex w-full items-start justify-between gap-4 text-left" aria-expanded={open} aria-controls="optional-settings" onClick={() => setOpen((o) => !o)}>
          <span className="space-y-1.5">
            <CardTitle>Discord and test server (optional)</CardTitle>
            <CardDescription>{summary}</CardDescription>
          </span>
          <ChevronDown className={cn('mt-0.5 size-4 shrink-0 text-muted-foreground transition-transform', open && 'rotate-180')} />
        </button>
      </CardHeader>
      {open && (
        <CardContent id="optional-settings" className="space-y-8">
          <FieldSet>
            <FieldLegend>Discord</FieldLegend>
            <FieldDescription>A bot posts the game to Discord channels and takes admin commands there.</FieldDescription>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="discordBotToken">Bot token</FieldLabel>
                <SecretField
                  id="discordBotToken"
                  stored={!!initial.discordBotToken}
                  value={form.discordBotToken}
                  initialValue={initial.discordBotToken}
                  onChange={(value) => set('discordBotToken', value)}
                  setLabel="Token set"
                />
                <FieldDescription>From the Discord developer portal: your application → Bot → Reset Token. The bot must be in your Discord server.</FieldDescription>
              </Field>
              <p className="text-sm text-muted-foreground">
                Channel ids: in Discord, turn on Developer Mode (User Settings → Advanced), then right-click a channel and choose Copy Channel ID. Leave
                a channel empty to skip it.
              </p>
              {discordChannels.map((c) => (
                <Field key={c.key}>
                  <FieldLabel htmlFor={c.key}>{c.label}</FieldLabel>
                  <Input id={c.key} value={form[c.key]} inputMode="numeric" placeholder="Channel id, such as 1098765432109876543" onChange={(e) => set(c.key, e.target.value)} />
                  <FieldDescription>{c.hint}</FieldDescription>
                </Field>
              ))}
            </FieldGroup>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Test server</FieldLegend>
            <Field orientation="horizontal">
              <Checkbox id="isTestServer" checked={form.isTestServer} onCheckedChange={(checked) => set('isTestServer', checked)} />
              <FieldContent>
                <FieldLabel htmlFor="isTestServer">This is a test server</FieldLabel>
                <FieldDescription>
                  For a server where players join without a CD key, such as a local one for testing and map building. They are keyed by their name, so
                  admin levels and statistics still work.
                </FieldDescription>
              </FieldContent>
            </Field>
          </FieldSet>
        </CardContent>
      )}
    </Card>
  )
}
