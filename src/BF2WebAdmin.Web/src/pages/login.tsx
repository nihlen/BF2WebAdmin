import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { api, errorMessage, isUnauthorized } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { meQuery } from '@/router'

export function LoginPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')

  const login = api.useMutation('post', '/api/auth/login', {
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: meQuery.queryKey })
      await navigate({ to: '/' })
    },
  })

  return (
    <div className="flex min-h-svh items-center justify-center p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-primary">BF2 Web Admin</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault()
              login.mutate({ body: { username, password } })
            }}
          >
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="username">Username</FieldLabel>
                <Input id="username" autoComplete="username" required value={username} onChange={(e) => setUsername(e.target.value)} />
              </Field>
              <Field data-invalid={login.isError || undefined}>
                <FieldLabel htmlFor="password">Password</FieldLabel>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
                {login.isError && <FieldError>{isUnauthorized(login.error) ? 'Wrong username or password' : errorMessage(login.error)}</FieldError>}
              </Field>
              <Button type="submit" disabled={login.isPending}>
                Login
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
