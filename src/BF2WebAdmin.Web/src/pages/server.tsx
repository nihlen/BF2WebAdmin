import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Map as MapIcon, MessageSquare, PlugZap, Settings, SquareTerminal } from 'lucide-react'
import { useMemo, useState } from 'react'
import { api, errorMessage, type Player, type ServerSnapshot } from '@/api/client'
import { useConfirm } from '@/components/confirm-dialog'
import { InputDialog, type InputRequest } from '@/components/input-dialog'
import { ServerMap } from '@/components/server-map'
import { ActivityLog } from '@/components/server/activity-log'
import { BanList } from '@/components/server/ban-list'
import { ChatPanel } from '@/components/server/chat-panel'
import { CommandPanel } from '@/components/server/command-panel'
import { ConnectionTestResults } from '@/components/server/connection-test'
import { HealthPanel } from '@/components/server/health-panel'
import { LogList, Time } from '@/components/server/log-list'
import { MapList } from '@/components/server/map-list'
import { PlayerSheet } from '@/components/server/player-sheet'
import { useServerPage } from '@/components/server/server-context'
import { TeamTable } from '@/components/server/team-table'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { playerAuthQuery, useServerActions, type ServerActions } from '@/hooks/use-server-actions'
import { encodeServerId } from '@/lib/server-id'

/** The Live tab: the teams, the chat, maps and commands, and the minimap. An offline server has none of them. */
export function ServerLivePage() {
  const { serverId, snapshot, positions } = useServerPage()
  const actions = useServerActions(serverId, snapshot.data)
  const { confirm, dialog: confirmDialog } = useConfirm()
  const [inputRequest, setInputRequest] = useState<InputRequest | null>(null)
  const [detailsPlayer, setDetailsPlayer] = useState<Player | null>(null)

  const serverGroup = snapshot.data?.server.serverGroup ?? ''
  const auth = useQuery({ ...playerAuthQuery(serverGroup), enabled: !!serverGroup })
  const authLevels = useMemo(() => new Map((auth.data ?? []).map((a) => [a.playerHash, a.authLevel])), [auth.data])

  if (!snapshot.data) return <Skeleton className="h-[600px]" />

  const { server, teams, players } = snapshot.data
  if (server.socketState !== 'Connected') return <OfflineCard serverId={serverId} />

  return (
    <div className="space-y-6">
      <div className="grid gap-6 lg:grid-cols-2">
        {teams.map((team) => (
          <TeamTable
            key={team.id}
            team={team}
            players={players.filter((p) => p.team === team.id)}
            positions={positions}
            authLevels={authLevels}
            serverGroup={serverGroup}
            actions={actions}
            onInput={setInputRequest}
            onDetails={setDetailsPlayer}
          />
        ))}
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <LiveTabs serverId={serverId} snapshot={snapshot.data} actions={actions} confirm={confirm} />
        <ServerMap map={server.map} players={players} positions={positions} />
      </div>

      <PlayerSheet
        serverId={serverId}
        player={detailsPlayer}
        current={detailsPlayer ? players.find((p) => p.index === detailsPlayer.index && p.name === detailsPlayer.name) : undefined}
        teams={teams}
        ping={detailsPlayer ? positions.current.get(detailsPlayer.index)?.ping : undefined}
        authLevels={authLevels}
        serverGroup={serverGroup}
        actions={actions}
        onInput={setInputRequest}
        onClose={() => setDetailsPlayer(null)}
      />
      <InputDialog request={inputRequest} onClose={() => setInputRequest(null)} />
      {confirmDialog}
    </div>
  )
}

/**
 * An offline server has no players, chat or maps to show and takes no commands: one card says so and offers the
 * settings and a connection test with the stored settings.
 */
function OfflineCard({ serverId }: { serverId: string }) {
  const settings = api.useQuery('get', '/api/servers/{serverId}/settings', { params: { path: { serverId } } })
  const test = api.useMutation('post', '/api/servers/test')

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <PlugZap className="size-5 text-destructive" />
          The admin channel is not connected
        </CardTitle>
        <CardDescription>
          WebAdmin is waiting for this game server to connect. Until it does, there are no players, chat or maps here, and commands cannot reach
          it. Check that the game server is running, that its server extension has this server's admin key and connects to this WebAdmin's admin
          channel port.
        </CardDescription>
      </CardHeader>
      {(test.isPending || test.data) && (
        <CardContent>
          <ConnectionTestResults pending={test.isPending} results={test.data} discordConfigured={!!settings.data?.discordBotToken} />
        </CardContent>
      )}
      <CardFooter className="flex-wrap gap-2">
        <Button disabled={!settings.data || test.isPending} onClick={() => settings.data && test.mutate({ body: settings.data })}>
          Test connection
        </Button>
        <Button variant="outline" nativeButton={false} render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(serverId) }} />}>
          <Settings data-icon="inline-start" />
          Settings
        </Button>
        {settings.isError && <p className="w-full text-sm text-destructive">{errorMessage(settings.error)}</p>}
      </CardFooter>
    </Card>
  )
}

function LiveTabs({
  serverId,
  snapshot,
  actions,
  confirm,
}: {
  serverId: string
  snapshot: ServerSnapshot
  actions: ServerActions
  confirm: ReturnType<typeof useConfirm>['confirm']
}) {
  const [tab, setTab] = useState('chat')
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId } } })

  return (
    <Card className="gap-0 py-0">
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList variant="line" className="w-full justify-start overflow-x-auto border-b px-2">
          <TabsTrigger value="chat">
            <MessageSquare />
            Chat
          </TabsTrigger>
          <TabsTrigger value="maps">
            <MapIcon />
            Maps
          </TabsTrigger>
          <TabsTrigger value="commands">
            <SquareTerminal />
            Commands
          </TabsTrigger>
        </TabsList>

        <TabsContent value="chat">
          <ChatPanel
            chatLog={snapshot.chatLog}
            players={snapshot.players}
            teams={snapshot.teams}
            commands={commands.data ?? []}
            onSend={actions.sendChat}
          />
        </TabsContent>

        <TabsContent value="maps">
          <MapList serverId={serverId} snapshot={snapshot} actions={actions} confirm={confirm} />
        </TabsContent>

        <TabsContent value="commands">
          <CommandPanel serverId={serverId} players={snapshot.players} actions={actions} />
        </TabsContent>
      </Tabs>
    </Card>
  )
}

/** The Health tab: the server process and the players' connections, at full width. */
export function ServerHealthPage() {
  const { serverId, snapshot } = useServerPage()
  return <HealthPanel serverId={serverId} connected={snapshot.data?.server.socketState === 'Connected'} />
}

/** The Bans tab: the engine's ban list, with an unban per row. */
export function ServerBansPage() {
  const { serverId, snapshot } = useServerPage()
  const actions = useServerActions(serverId, snapshot.data)
  const { confirm, dialog } = useConfirm()

  return (
    <Card className="gap-0 py-0">
      <BanList serverId={serverId} actions={actions} confirm={confirm} />
      {dialog}
    </Card>
  )
}

/** The Activity tab: what web users sent to the server, and the game's events. */
export function ServerActivityPage() {
  const { serverId, snapshot } = useServerPage()

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card className="gap-0 pb-0">
        <CardHeader className="border-b">
          <CardTitle>Sent from the web</CardTitle>
          <CardDescription>Chat, commands and actions of every web user, with the server's answers.</CardDescription>
        </CardHeader>
        <ActivityLog serverId={serverId} />
      </Card>
      <Card className="gap-0 pb-0">
        <CardHeader className="border-b">
          <CardTitle>Game events</CardTitle>
          <CardDescription>Joins, deaths, kills, map changes and other game events, newest at the bottom.</CardDescription>
        </CardHeader>
        <LogList className="h-[min(600px,70svh)]" items={snapshot.data?.eventLog ?? []} empty="No events yet">
          {(entry) => (
            <li key={entry.id}>
              <Time value={entry.timestamp} /> {entry.message}
            </li>
          )}
        </LogList>
      </Card>
    </div>
  )
}
