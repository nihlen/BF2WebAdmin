import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { errorMessage } from '@/api/client'
import { CopyButton } from '@/components/copy-button'
import { RelativeTime } from '@/components/relative-time'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useServers } from '@/hooks/use-servers'
import { adminChannelQuery } from '@/lib/admin-channel'
import { formatDayTime } from '@/lib/format'

/**
 * About this WebAdmin (from the account menu): the admin channel game servers connect to (host, port and certificate
 * pin, to copy into a serverext.cfg by hand), how many are connected, and WebAdmin's version and start time. Read
 * only: they are set in WebAdmin's configuration.
 */
export function AboutPage() {
  const channel = useQuery(adminChannelQuery)
  const servers = useServers().data ?? []
  const connected = servers.filter((s) => s.socketState === 'Connected').length

  return (
    <div className="max-w-3xl space-y-6">
      <title>BF2 Web Admin - About WebAdmin</title>
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">About WebAdmin</h1>
        <p className="text-muted-foreground">
          What game servers connect to, and which WebAdmin this is. These come from WebAdmin's configuration; a server's own lines are in its settings.
        </p>
      </div>

      {channel.isPending && <Skeleton className="h-64" />}
      {channel.isError && <p className="text-destructive">{errorMessage(channel.error)}</p>}
      {channel.data && (
        <>
          <Card>
            <CardHeader>
              <CardTitle>Admin channel</CardTitle>
              <CardDescription>
                Game servers dial it with the <code>adminConnect</code> and <code>adminCertSha256</code> lines of their <code>serverext.cfg</code>.{' '}
                <Link to="/" className="font-medium text-foreground underline-offset-4 hover:underline">
                  {connected} of {servers.length} {servers.length === 1 ? 'server is' : 'servers are'} connected
                </Link>
                .
              </CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="space-y-3 text-sm">
                <Row term="Host">
                  {channel.data.publicHost ? (
                    <Value value={channel.data.publicHost} label="Copy the host" />
                  ) : (
                    <span className="text-muted-foreground">
                      Not set: the setup steps use the address this page is opened at. <code>ServerSettings:AdminChannel:PublicHost</code> sets it.
                    </span>
                  )}
                </Row>
                <Row term="Port">
                  <Value value={String(channel.data.port)} label="Copy the port" />
                </Row>
                <Row term="Certificate pin">
                  {channel.data.certificatePin ? (
                    <Value value={channel.data.certificatePin} label="Copy the certificate pin" wrap />
                  ) : (
                    <span className="text-muted-foreground">Not loaded yet. WebAdmin writes it to its log at startup.</span>
                  )}
                </Row>
              </dl>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>WebAdmin</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="space-y-3 text-sm">
                <Row term="Version">
                  <Value value={channel.data.version || 'unknown'} label="Copy the version" />
                </Row>
                <Row term="Started">
                  {formatDayTime(channel.data.startedAt)} <span className="text-muted-foreground">(<RelativeTime value={channel.data.startedAt} />)</span>
                </Row>
              </dl>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}

/** A term and its value: beside each other from sm up, one under the other on a phone. */
function Row({ term, children }: { term: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1 sm:grid-cols-[9rem_minmax(0,1fr)] sm:gap-4">
      <dt className="text-muted-foreground">{term}</dt>
      <dd className="min-w-0">{children}</dd>
    </div>
  )
}

/** A value in monospace with a copy button; a long one (the pin) wraps instead of widening the page. */
function Value({ value, label, wrap }: { value: string; label: string; wrap?: boolean }) {
  return (
    <span className="flex min-w-0 items-start gap-1">
      <span className={wrap ? 'min-w-0 font-mono text-xs leading-5 break-all' : 'font-mono text-xs leading-5'}>{value}</span>
      <CopyButton value={value} label={label} className="shrink-0" />
    </span>
  )
}
