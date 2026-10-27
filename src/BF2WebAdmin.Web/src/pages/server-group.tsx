import { useState } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldLabel } from '@/components/ui/field'
import { Skeleton } from '@/components/ui/skeleton'
import { serverGroupRoute } from '@/router'

const defaultGroup = 'default'

export function ServerGroupPage() {
  const { serverGroup } = serverGroupRoute.useParams()
  const groupModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup } } })
  const defaultModules = api.useQuery('get', '/api/server-groups/{serverGroup}/modules', { params: { path: { serverGroup: defaultGroup } } })
  const allModules = api.useQuery('get', '/api/modules')

  const loaded = groupModules.data && defaultModules.data && allModules.data

  return (
    <div className="space-y-8">
      <h1 className="text-3xl font-semibold">{serverGroup}</h1>
      {serverGroup === defaultGroup && (
        <p className="text-muted-foreground">The default server group cannot be modified. Set a new group for your server to select modules.</p>
      )}
      {!loaded && <Skeleton className="h-96 max-w-md" />}
      {loaded && (
        <ModulesForm
          key={serverGroup}
          serverGroup={serverGroup}
          active={groupModules.data}
          defaults={defaultModules.data}
          all={allModules.data}
        />
      )}
    </div>
  )
}

function ModulesForm({ serverGroup, active, defaults, all }: { serverGroup: string; active: string[]; defaults: string[]; all: string[] }) {
  const [selected, setSelected] = useState(() => new Set(active))
  const readOnly = serverGroup === defaultGroup

  const save = api.useMutation('put', '/api/server-groups/{serverGroup}/modules', {
    onSuccess: () => toast.success('Modules have been saved'),
  })
  const reload = api.useMutation('post', '/api/server-groups/{serverGroup}/modules/reload', {
    onSuccess: () => toast.success('Modules have been reloaded'),
  })
  const params = { path: { serverGroup } }

  const toggle = (name: string, checked: boolean) =>
    setSelected((s) => {
      const next = new Set(s)
      if (checked) next.add(name)
      else next.delete(name)
      return next
    })

  return (
    <div className="space-y-6">
      {!readOnly && (
        <div className="flex gap-2">
          <Button onClick={() => save.mutate({ params, body: [...selected] })} disabled={save.isPending}>
            Save
          </Button>
          <Button variant="outline" onClick={() => reload.mutate({ params })} disabled={reload.isPending}>
            Reload modules for all servers
          </Button>
        </div>
      )}

      <h2 className="text-xl font-medium">Modules</h2>
      <Card className="max-w-md">
        <CardContent className="space-y-3">
          {all.map((name) => (
            <Field key={name} orientation="horizontal">
              <Checkbox
                id={`module-${name}`}
                checked={selected.has(name)}
                disabled={readOnly || defaults.includes(name)}
                onCheckedChange={(checked) => toggle(name, checked)}
              />
              <FieldLabel htmlFor={`module-${name}`}>{name}</FieldLabel>
            </Field>
          ))}
        </CardContent>
      </Card>
    </div>
  )
}
