import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CircleAlert } from 'lucide-react'
import { useRef, useState } from 'react'
import { api, errorMessage, isUnauthorized } from '@/api/client'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader } from '@/components/ui/card'
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { meQuery } from '@/router'

export function LoginPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const passwordRef = useRef<HTMLInputElement>(null)

  const login = api.useMutation('post', '/api/auth/login', {
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: meQuery.queryKey })
      await navigate({ to: '/' })
    },
    // Most failures are a mistyped password: clear it and put the cursor there for the next try
    onError: () => {
      setPassword('')
      passwordRef.current?.focus()
    },
  })
  const wrongPassword = login.isError && isUnauthorized(login.error)

  return (
    <div className="flex min-h-svh items-center justify-center p-6">
      <title>BF2 Web Admin - Sign in</title>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <h1 className="text-2xl font-semibold">Sign in to BF2 Web Admin</h1>
          <CardDescription>Manage your Battlefield 2 game servers.</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault()
              login.mutate({ body: { username, password } })
            }}
          >
            <FieldGroup>
              {login.isError && (
                <Alert variant="destructive" role="alert">
                  <CircleAlert />
                  <AlertTitle>{wrongPassword ? 'Wrong username or password' : 'Could not sign in'}</AlertTitle>
                  <AlertDescription>{wrongPassword ? 'Check them and try again.' : errorMessage(login.error)}</AlertDescription>
                </Alert>
              )}
              <Field>
                <FieldLabel htmlFor="username">Username</FieldLabel>
                <Input id="username" autoComplete="username" required value={username} onChange={(e) => setUsername(e.target.value)} />
              </Field>
              <Field data-invalid={wrongPassword || undefined}>
                <FieldLabel htmlFor="password">Password</FieldLabel>
                <Input
                  ref={passwordRef}
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  required
                  aria-invalid={wrongPassword}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </Field>
              <Button type="submit" disabled={login.isPending}>
                {login.isPending ? 'Signing in…' : 'Sign in'}
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
