import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type ServerSettings } from '@/api/client'
import { ConfirmButton } from '@/components/confirm-button'
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

  const adminKeyInvalid = !!form.adminKey?.trim() && !adminKeyPattern.test(form.adminKey)
  const toDto = (): ServerSettings => ({
    ...form,
    serverId: serverId ?? `${form.ipAddress.trim()}:${form.gamePort}`,
    ipAddress: form.ipAddress.trim(),
    adminKey: form.adminKey?.trim() || null,
  })

  const onSaved = async (savedId: string) => {
    toast.success('Server has been saved')
    await queryClient.invalidateQueries({ queryKey: ['get', '/api/servers'] })
    await navigate({ to: '/server/$serverId', params: { serverId: encodeServerId(savedId) } })
  }

  const create = api.useMutation('post', '/api/servers', { onSuccess: (_, { body }) => onSaved(body.serverId) })
  const update = api.useMutation('put', '/api/servers/{serverId}/settings', { onSuccess: (_, { body }) => onSaved(body.serverId) })
  const remove = api.useMutation('delete', '/api/servers/{serverId}', {
    onSuccess: async () => {
      toast.success('Server has been removed')
      await navigate({ to: '/' })
    },
  })
  const test = api.useMutation('post', '/api/servers/test', {
    onSuccess: (results) => {
      for (const result of results) {
        if (result.success) toast.success(`${result.type} response OK`)
        else toast.error(`${result.type} response error`)
      }
    },
  })

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
            {text('rconPassword', 'Rcon password', { required: true })}
            {text('discordBotToken', 'Discord bot token')}
            {text('discordAdminChannel', 'Discord admin channel')}
            {text('discordNotificationChannel', 'Discord notification channel')}
            {text('discordMatchResultChannel', 'Discord match channel')}
            <Field data-invalid={adminKeyInvalid || undefined}>
              <FieldLabel htmlFor="adminKey">Admin channel key (adminKey, 32-128 hex digits)</FieldLabel>
              <Input
                id="adminKey"
                type="password"
                autoComplete="off"
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
        <CardFooter className="gap-2">
          <Button type="submit" disabled={create.isPending || update.isPending}>
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
        </CardFooter>
      </Card>
    </form>
  )
}
