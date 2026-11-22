import { CircleCheck, CircleMinus, CircleX, Loader2 } from 'lucide-react'
import type { TestServerResult } from '@/api/client'

/**
 * The results of a connection test (POST /api/servers/test), one line per check. Two checks that fail are not failures
 * here: Discord without a bot token is optional, so it shows as not set up; and a server that is not saved yet cannot
 * have connected, because the game server connects only once WebAdmin knows its key.
 */
export function ConnectionTestResults({
  pending,
  results,
  discordConfigured = true,
  unsaved = false,
}: {
  pending: boolean
  results: TestServerResult[] | undefined
  discordConfigured?: boolean
  unsaved?: boolean
}) {
  if (pending)
    return (
      <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Testing the connection…
      </p>
    )
  if (!results) return null

  return (
    <div role="status">
      <ul className="space-y-2 text-sm">
      {results.map((result) => {
        const notSetUp = !discordConfigured && /discord/i.test(result.type)
        const notYet = unsaved && !result.success && /admin/i.test(result.type)
        if (notYet)
          return (
            <li key={result.type} className="flex items-start gap-2">
              <CircleMinus className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
              <span>
                <span className="font-medium">{result.type}</span>: connects after you add the server
              </span>
            </li>
          )
        return (
          <li key={result.type} className="flex items-start gap-2">
            {notSetUp ? (
              <CircleMinus className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
            ) : result.success ? (
              <CircleCheck className="mt-0.5 size-4 shrink-0 text-emerald-500" />
            ) : (
              <CircleX className="mt-0.5 size-4 shrink-0 text-destructive" />
            )}
            <span>
              <span className="font-medium">{result.type}</span>
              {': '}
              {notSetUp ? 'not set up (optional)' : result.success ? 'OK' : 'failed'}
              {result.message && !notSetUp && <span className="block text-muted-foreground">{result.message}</span>}
            </span>
          </li>
        )
      })}
      </ul>
    </div>
  )
}
