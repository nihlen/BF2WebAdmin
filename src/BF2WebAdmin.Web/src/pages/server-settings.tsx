import { useQueryClient } from '@tanstack/react-query'
import { useBlocker, useNavigate } from '@tanstack/react-router'
import { CircleCheck, CircleX, Eye, EyeOff } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type ServerSettings } from '@/api/client'
import { ConfirmButton } from '@/components/confirm-button'
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
import { Card, CardContent, CardFooter } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { serverEditRoute } from '@/router'

const newServer: ServerSettings = {
  serverId: '',
  serverGroup: 'default',
  ipAddress: '',
  gamePort: 16567,
  queryPort: 29900,
  rconPort: 4711,
  rconPassword: '',
  discordBotToken: '',
  discordAdminChannel: '',
  discordNotificationChannel: '',
  discordMatchResultChannel: '',
  adminKey: null,
  isTestServer: false,
}

const adminKeyPattern = /^\s*([0-9a-fA-F]{2}){16,64}\s*$/

export function ServerAddPage() {
  return (
    <div className="space-y-8">
      <h1 className="text-3xl font-semibold">Add server</h1>
      <ServerSettingsForm initial={newServer} />
    </div>
  )
}

export function ServerEditPage() {
  const serverId = decodeServerId(serverEditRoute.useParams().serverId)
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId } } })

  return (
    <div className="space-y-8">
      <h1 className="text-3xl font-semibold">Edit server</h1>
      {settings.isPending && <Skeleton className="h-96 max-w-2xl" />}
      {settings.isError && <p className="text-destructive">{errorMessage(settings.error)}</p>}
      {settings.data && <ServerSettingsForm key={serverId} initial={settings.data} serverId={serverId} />}
    </div>
  )
}

/** The settings of a new server (no serverId) or an existing one, whose address and game port are fixed. */
function ServerSettingsForm({ initial, serverId }: { initial: ServerSettings; serverId?: string }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [form, setForm] = useState(initial)
  const set = <K extends keyof ServerSettings>(key: K, value: ServerSettings[K]) => setForm((f) => ({ ...f, [key]: value }))
  const dirty = (Object.keys(initial) as (keyof ServerSettings)[]).some((key) => (form[key] ?? '') !== (initial[key] ?? ''))
  // Set before the navigation after a save or a removal, which must not ask
  const leaving = useRef(false)
  const blocker = useBlocker({ shouldBlockFn: () => dirty && !leaving.current, enableBeforeUnload: () => dirty && !leaving.current, withResolver: true })

  const adminKeyInvalid = !!form.adminKey?.trim() && !adminKeyPattern.test(form.adminKey)
  const toDto = (): ServerSettings => ({
    ...form,
    serverId: serverId ?? `${form.ipAddress.trim()}:${form.gamePort}`,
    ipAddress: form.ipAddress.trim(),
    adminKey: form.adminKey?.trim() || null,
  })

  const onSaved = async (savedId: string) => {
    leaving.current = true
    toast.success('Server has been saved')
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/servers'] })
    await navigate({ to: '/server/$serverId', params: { serverId: encodeServerId(savedId) } })
  }

  const create = api.useMutation('post', '/api/servers', { onSuccess: (_, { body }) => onSaved(body.serverId) })
  const update = api.useMutation('put', '/api/servers/{serverId}/settings', { onSuccess: (_, { body }) => onSaved(body.serverId) })
  const remove = api.useMutation('delete', '/api/servers/{serverId}', {
    onSuccess: async () => {
      leaving.current = true
      toast.success('Server has been removed')
      await navigate({ to: '/' })
    },
  })
  const test = api.useMutation('post', '/api/servers/test')

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    if (adminKeyInvalid) return
    const body = toDto()
    if (serverId) update.mutate({ params: { path: { serverId } }, body })
    else create.mutate({ body })
  }

  const text = (key: keyof ServerSettings, label: string, props?: React.ComponentProps<typeof Input>) => (
    <Field>
      <FieldLabel htmlFor={key}>{label}</FieldLabel>
      <Input id={key} value={String(form[key] ?? '')} onChange={(e) => set(key, e.target.value as never)} {...props} />
    </Field>
  )

  const number = (key: 'gamePort' | 'queryPort' | 'rconPort', label: string, props?: React.ComponentProps<typeof Input>) =>
    text(key, label, { type: 'number', min: 1, max: 65535, required: true, onChange: (e) => set(key, Number(e.target.value)), ...props })

  const secret = (key: 'rconPassword' | 'discordBotToken', label: string, props?: React.ComponentProps<typeof Input>) => (
    <Field>
      <FieldLabel htmlFor={key}>{label}</FieldLabel>
      <SecretInput id={key} value={form[key] ?? ''} onChange={(e) => set(key, e.target.value)} {...props} />
    </Field>
  )

  return (
    <form onSubmit={submit} className="max-w-2xl">
      <Card>
        <CardContent>
          <FieldGroup>
            {text('serverGroup', 'Server group', { required: true })}
            {text('ipAddress', 'IP address', { required: true, readOnly: !!serverId })}
            <div className="grid gap-4 sm:grid-cols-3">
              {number('gamePort', 'Game port', { readOnly: !!serverId })}
              {number('queryPort', 'Query port')}
              {number('rconPort', 'Rcon port')}
            </div>
            {secret('rconPassword', 'Rcon password', { required: true })}
            {secret('discordBotToken', 'Discord bot token')}
            {text('discordAdminChannel', 'Discord admin channel')}
            {text('discordNotificationChannel', 'Discord notification channel')}
            {text('discordMatchResultChannel', 'Discord match channel')}
            <Field data-invalid={adminKeyInvalid || undefined}>
              <FieldLabel htmlFor="adminKey">Admin channel key (adminKey, 32-128 hex digits)</FieldLabel>
              <SecretInput
                id="adminKey"
                value={form.adminKey ?? ''}
                aria-invalid={adminKeyInvalid}
                onChange={(e) => set('adminKey', e.target.value)}
              />
              {adminKeyInvalid && <FieldError>32-128 hex digits</FieldError>}
            </Field>
            <Field orientation="horizontal">
              <Checkbox id="isTestServer" checked={form.isTestServer} onCheckedChange={(checked) => set('isTestServer', checked)} />
              <FieldLabel htmlFor="isTestServer">Test server (players without a CD-key hash are keyed by name)</FieldLabel>
            </Field>
          </FieldGroup>
        </CardContent>
        <CardFooter className="flex-wrap gap-2">
          <Button type="submit" disabled={create.isPending || update.isPending || !dirty}>
            Save
          </Button>
          <Button type="button" variant="outline" disabled={test.isPending} onClick={() => test.mutate({ body: toDto() })}>
            Test connection
          </Button>
          {serverId && (
            <ConfirmButton
              className="ml-auto"
              title="Remove server"
              description="Do you want to remove this server?"
              confirmLabel="Remove"
              onConfirm={() => remove.mutate({ params: { path: { serverId } } })}
            >
              Remove server
            </ConfirmButton>
          )}
          {test.isPending && <p className="w-full text-sm text-muted-foreground">Testing the connection…</p>}
          {test.data && !test.isPending && (
            <ul className="w-full space-y-1 text-sm">
              {test.data.map((result) => (
                <li key={result.type} className="flex items-start gap-2">
                  {result.success ? <CircleCheck className="mt-0.5 size-4 shrink-0 text-emerald-500" /> : <CircleX className="mt-0.5 size-4 shrink-0 text-destructive" />}
                  <span>
                    <span className="font-medium">{result.type}</span>: {result.success ? 'OK' : 'failed'}
                    {result.message && <span className="text-muted-foreground"> ({result.message})</span>}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardFooter>
      </Card>

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

/** A password field with a button that shows the text. */
function SecretInput(props: Omit<React.ComponentProps<typeof Input>, 'type'>) {
  const [shown, setShown] = useState(false)
  return (
    <div className="relative">
      <Input {...props} type={shown ? 'text' : 'password'} autoComplete="off" className="pr-9" />
      <Button
        type="button"
        variant="ghost"
        size="icon-xs"
        className="absolute top-1/2 right-1.5 -translate-y-1/2"
        aria-label={shown ? 'Hide' : 'Show'}
        title={shown ? 'Hide' : 'Show'}
        onClick={() => setShown((s) => !s)}
      >
        {shown ? <EyeOff /> : <Eye />}
      </Button>
    </div>
  )
}
