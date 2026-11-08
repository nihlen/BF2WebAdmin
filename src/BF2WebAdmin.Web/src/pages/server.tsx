import { HubConnectionState } from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Ban as BanIcon, Bolt, Box, History, Loader2, Map as MapIcon, MessageSquare, Pause, Play, Power, RotateCcw, Settings, SquareTerminal } from 'lucide-react'
import { useMemo, useState } from 'react'
import { api, errorMessage, type Player, type ServerSnapshot } from '@/api/client'
import { useHubState } from '@/api/hub'
import { useConfirm } from '@/components/confirm-dialog'
import { InputDialog, type InputRequest } from '@/components/input-dialog'
import { ServerMap } from '@/components/server-map'
import { ServerStatusDot } from '@/components/server-status'
import { ActivityLog } from '@/components/server/activity-log'
import { BanList } from '@/components/server/ban-list'
import { ChatPanel } from '@/components/server/chat-panel'
import { CommandPanel } from '@/components/server/command-panel'
import { LogList, Time } from '@/components/server/log-list'
import { MapList } from '@/components/server/map-list'
import { PlayerSheet } from '@/components/server/player-sheet'
import { TeamTable } from '@/components/server/team-table'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { playerAuthQuery, useServerActions, type ServerActions } from '@/hooks/use-server-actions'
import { useServerLive } from '@/hooks/use-server-live'
import { usePendingActions } from '@/lib/pending-actions'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { serverRoute } from '@/router'

export function ServerPage() {
  const serverId = decodeServerId(serverRoute.useParams().serverId)
  const { snapshot, positions } = useServerLive(serverId)
  const actions = useServerActions(serverId, snapshot.data)
  const { confirm, dialog: confirmDialog } = useConfirm()
  const [inputRequest, setInputRequest] = useState<InputRequest | null>(null)
  const [detailsPlayer, setDetailsPlayer] = useState<Player | null>(null)
  const hubState = useHubState()

  const serverGroup = snapshot.data?.server.serverGroup ?? ''
  const auth = useQuery({ ...playerAuthQuery(serverGroup), enabled: !!serverGroup })
  const authLevels = useMemo(() => new Map((auth.data ?? []).map((a) => [a.playerHash, a.authLevel])), [auth.data])

  if (snapshot.isError) return <p className="text-destructive">{errorMessage(snapshot.error)}</p>
  if (!snapshot.data) return <Skeleton className="h-[600px]" />

  const { server, teams, players, eventLog } = snapshot.data
  const connected = server.socketState === 'Connected'

  return (
    <div className="space-y-6">
      <title>{`BF2 Web Admin - ${server.name}`}</title>

      {!connected && (
        <Alert variant="destructive">
          <AlertTitle>The server is offline</AlertTitle>
          <AlertDescription>Its admin channel is not connected. Commands cannot reach it until it connects again.</AlertDescription>
        </Alert>
      )}
      {hubState !== HubConnectionState.Connected && (
        <Alert>
          <Loader2 className="animate-spin" />
          <AlertTitle>Live updates are offline, reconnecting…</AlertTitle>
          <AlertDescription>The players, chat and map below may be out of date until the connection is back.</AlertDescription>
        </Alert>
      )}

      <div className="flex items-center justify-between gap-4">
        <h1 className="truncate text-3xl font-semibold">{server.name}</h1>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="lg"
            nativeButton={false}
            render={<Link to="/server/$serverId/viewer" params={{ serverId: encodeServerId(serverId) }} />}
          >
            <Box data-icon="inline-start" />
            <span className="hidden sm:inline">3D viewer</span>
          </Button>
          <Button
            variant="outline"
            size="icon-lg"
            aria-label="Server settings"
            title="Server settings"
            nativeButton={false}
            render={<Link to="/server/$serverId/edit" params={{ serverId: encodeServerId(serverId) }} />}
          >
            <Settings />
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">
          <ServerStatusDot socketState={server.socketState} />
          {server.socketState}
        </Badge>
        <Badge variant={server.gameState === 'Paused' ? 'destructive' : 'secondary'}>{server.gameState}</Badge>
        <Badge variant="secondary">
          {server.ipAddress}:{server.gamePort} ({server.queryPort})
        </Badge>
        {server.map && <Badge variant="secondary">{server.map}</Badge>}
        <Badge variant="secondary">
          {server.players} / {server.maxPlayers} players
        </Badge>
        <Badge variant="secondary" render={<Link to="/servergroup/$serverGroup" params={{ serverGroup: server.serverGroup }} />}>
          {server.serverGroup}
        </Badge>
      </div>

      <ServerButtons serverId={serverId} snapshot={snapshot.data} actions={actions} confirm={confirm} disabled={!connected} />

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
        <ServerTabs serverId={serverId} snapshot={snapshot.data} eventLog={eventLog} actions={actions} confirm={confirm} />
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

/** Restart map, pause and restart server. Each shows its progress until the game confirms it. */
function ServerButtons({
  serverId,
  snapshot,
  actions,
  confirm,
  disabled,
}: {
  serverId: string
  snapshot: ServerSnapshot
  actions: ServerActions
  confirm: ReturnType<typeof useConfirm>['confirm']
  disabled: boolean
}) {
  const pending = usePendingActions(serverId)
  const players = snapshot.players.length
  const paused = snapshot.server.gameState === 'Paused'
  const playersText = players === 0 ? 'No players are online.' : `${players} ${players === 1 ? 'player is' : 'players are'} online.`

  const busy = (key: string) => pending.has(key)
  const icon = (key: string, Icon: React.ComponentType<{ className?: string }>, className: string) =>
    busy(key) ? <Loader2 data-icon="inline-start" className="animate-spin" /> : <Icon data-icon="inline-start" className={className} />

  return (
    <div className="flex flex-wrap gap-2">
      <Button
        variant="outline"
        disabled={disabled || busy('restart-map')}
        onClick={() =>
          confirm({
            title: 'Restart the round?',
            description: `The scores are reset and everyone respawns. ${playersText}`,
            confirmLabel: 'Restart round',
            onConfirm: () => void actions.restartMap(),
          })
        }
      >
        {icon('restart-map', RotateCcw, 'text-emerald-500')}
        Restart map
      </Button>
      <Button variant="outline" disabled={disabled || busy('pause')} onClick={() => void actions.togglePause()}>
        {paused ? icon('pause', Play, 'text-emerald-500') : icon('pause', Pause, 'text-amber-500')}
        {paused ? 'Resume' : 'Pause'}
      </Button>
      <Button
        variant="outline"
        disabled={disabled || busy('restart-server')}
        onClick={() =>
          confirm({
            title: 'Restart the game server?',
            description: (
              <>
                The server quits cleanly (console <code>quit</code>) and its container starts it again, which takes about a minute. {playersText}{' '}
                Everyone is disconnected. The container needs a restart policy (<code>always</code> or <code>unless-stopped</code>).
              </>
            ),
            confirmLabel: 'Restart server',
            destructive: true,
            onConfirm: () => void actions.restartServer(),
          })
        }
      >
        {icon('restart-server', Power, 'text-destructive')}
        Restart server
      </Button>
    </div>
  )
}

function ServerTabs({
  serverId,
  snapshot,
  eventLog,
  actions,
  confirm,
}: {
  serverId: string
  snapshot: ServerSnapshot
  eventLog: ServerSnapshot['eventLog']
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
          <TabsTrigger value="bans">
            <BanIcon />
            Bans
          </TabsTrigger>
          <TabsTrigger value="activity">
            <History />
            Activity
          </TabsTrigger>
          <TabsTrigger value="events">
            <Bolt />
            Events
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

        <TabsContent value="bans">
          <BanList serverId={serverId} actions={actions} confirm={confirm} />
        </TabsContent>

        <TabsContent value="activity">
          <ActivityLog serverId={serverId} />
        </TabsContent>

        <TabsContent value="events">
          <LogList className="h-[550px]" items={eventLog} empty="No events yet">
            {(entry) => (
              <li key={entry.id}>
                <Time value={entry.timestamp} /> {entry.message}
              </li>
            )}
          </LogList>
        </TabsContent>
      </Tabs>
    </Card>
  )
}
