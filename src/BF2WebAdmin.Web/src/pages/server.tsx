import { useQuery } from '@tanstack/react-query'
import { Map as MapIcon, MessageSquare, Users } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api, type Player, type ServerSnapshot } from '@/api/client'
import { useConfirm } from '@/components/confirm-dialog'
import { ServerMap } from '@/components/server-map'
import { ActivityLog } from '@/components/server/activity-log'
import { BanList } from '@/components/server/ban-list'
import { ChatPanel } from '@/components/server/chat-panel'
import { CommandPalette } from '@/components/server/command-palette'
import { ServerConnectCard } from '@/components/server/connect-guide'
import { HealthPanel } from '@/components/server/health-panel'
import { LogList, Time } from '@/components/server/log-list'
import { ModerationDialog, type ModerationKind, type ModerationRequest } from '@/components/server/moderation-dialog'
import { PlayerSheet } from '@/components/server/player-sheet'
import { useServerPage } from '@/components/server/server-context'
import { TeamTable } from '@/components/server/team-table'
import { Badge } from '@/components/ui/badge'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useIsMobile } from '@/hooks/use-mobile'
import { playerAuthQuery, useServerActions, type ServerActions } from '@/hooks/use-server-actions'
import { healthQuery } from '@/hooks/use-server-live'

/**
 * The Live tab: the teams, the chat and the minimap, all in view on desktop; on a phone one at a time, picked with a
 * segmented control. Maps are in the header's Round menu, and the commands in a palette (Ctrl+K, or Commands by the chat
 * box). An offline server has none of them.
 */
export function ServerLivePage() {
  const { serverId, snapshot, positions } = useServerPage()
  const actions = useServerActions(serverId, snapshot.data)
  const mobile = useIsMobile()
  const [commandsOpen, setCommandsOpen] = useState(false)
  const connected = snapshot.data?.server.socketState === 'Connected'
  const [moderation, setModeration] = useState<ModerationRequest | null>(null)
  const [detailsPlayer, setDetailsPlayer] = useState<Player | null>(null)
  const moderate = (kind: ModerationKind, player: Player) => setModeration({ kind, player })

  const serverGroup = snapshot.data?.server.serverGroup ?? ''
  const auth = useQuery({ ...playerAuthQuery(serverGroup), enabled: !!serverGroup })
  const authLevels = useMemo(() => new Map((auth.data ?? []).map((a) => [a.playerHash, a.authLevel])), [auth.data])
  // The latest health window's connections, which useServerLive keeps current
  const health = useQuery({ ...healthQuery(serverId), staleTime: Infinity })
  const connections = useMemo(() => new Map((health.data?.connections?.players ?? []).map((c) => [c.playerId, c])), [health.data])

  // Ctrl+K (Cmd+K on a Mac) opens the commands from anywhere on the tab, as in most command palettes
  useEffect(() => {
    if (!connected) return
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setCommandsOpen(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [connected])

  if (!snapshot.data) return <Skeleton className="h-[600px]" />

  const { server, teams, players } = snapshot.data
  if (server.socketState !== 'Connected') return <ServerConnectCard server={server} />

  const teamTables = teams.map((team) => (
    <TeamTable
      key={team.id}
      team={team}
      players={players.filter((p) => p.team === team.id)}
      positions={positions}
      authLevels={authLevels}
      serverGroup={serverGroup}
      connections={connections}
      actions={actions}
      onModerate={moderate}
      onDetails={setDetailsPlayer}
    />
  ))
  const chat = (listClassName: string) => (
    <ChatCard snapshot={snapshot.data} actions={actions} listClassName={listClassName} onPlayer={setDetailsPlayer} onCommands={() => setCommandsOpen(true)} />
  )
  const map = <ServerMap map={server.map} players={players} positions={positions} />

  return (
    <div className="space-y-6">
      {mobile ? (
        <PhoneLive snapshot={snapshot.data} players={teamTables} chat={chat('h-[max(18rem,calc(100svh-22rem))]')} map={map} />
      ) : (
        <>
          <div className="grid gap-6 lg:grid-cols-2">{teamTables}</div>
          <div className="grid gap-6 lg:grid-cols-2">
            {chat('h-[min(520px,60svh)]')}
            {map}
          </div>
        </>
      )}

      <PlayerSheet
        serverId={serverId}
        player={detailsPlayer}
        current={detailsPlayer ? players.find((p) => p.index === detailsPlayer.index && p.name === detailsPlayer.name) : undefined}
        teams={teams}
        ping={detailsPlayer ? positions.current.get(detailsPlayer.index)?.ping : undefined}
        authLevels={authLevels}
        serverGroup={serverGroup}
        actions={actions}
        onModerate={moderate}
        onClose={() => setDetailsPlayer(null)}
      />
      <ModerationDialog request={moderation} actions={actions} onClose={() => setModeration(null)} />
      <CommandPalette open={commandsOpen} onOpenChange={setCommandsOpen} serverId={serverId} players={players} actions={actions} />
    </div>
  )
}

/** The chat with its box, which also runs commands; the Commands button beside the box opens the palette. */
function ChatCard({
  snapshot,
  actions,
  listClassName,
  onPlayer,
  onCommands,
}: {
  snapshot: ServerSnapshot
  actions: ServerActions
  listClassName: string
  onPlayer: (player: Player) => void
  onCommands: () => void
}) {
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId: snapshot.server.id } } })
  return (
    <Card className="gap-0 py-0">
      <h2 className="flex items-center gap-2 border-b px-4 py-2.5 text-sm font-medium">
        <MessageSquare className="size-4 text-muted-foreground" />
        Chat
      </h2>
      <ChatPanel
        chatLog={snapshot.chatLog}
        players={snapshot.players}
        teams={snapshot.teams}
        commands={commands.data ?? []}
        onSend={actions.sendChat}
        onPlayer={onPlayer}
        onCommands={onCommands}
        listClassName={listClassName}
      />
    </Card>
  )
}

/**
 * A phone shows the players, the chat or the map, one at a time, so each gets the whole screen. Chat that comes while
 * another one is shown is counted on the Chat segment (the log is capped, so from the last line seen, not by length).
 */
function PhoneLive({ snapshot, players, chat, map }: { snapshot: ServerSnapshot; players: React.ReactNode; chat: React.ReactNode; map: React.ReactNode }) {
  const [view, setView] = useState('players')
  const { chatLog } = snapshot
  const lastId = chatLog.at(-1)?.message.id ?? ''
  const [seenId, setSeenId] = useState(lastId)
  if (view === 'chat' && seenId !== lastId) setSeenId(lastId)
  const unread = view === 'chat' ? 0 : chatLog.length - 1 - chatLog.findIndex((e) => e.message.id === seenId)

  return (
    <Tabs value={view} onValueChange={setView} className="gap-4">
      <TabsList className="w-full group-data-horizontal/tabs:h-9">
        <TabsTrigger value="players">
          <Users />
          Players
          <span className="text-muted-foreground tabular-nums">{snapshot.players.length}</span>
        </TabsTrigger>
        <TabsTrigger value="chat">
          <MessageSquare />
          Chat
          {unread > 0 && (
            <Badge className="h-4.5 min-w-4.5 px-1 tabular-nums" aria-label={`${unread} new`}>
              {unread > 99 ? '99+' : unread}
            </Badge>
          )}
        </TabsTrigger>
        <TabsTrigger value="map">
          <MapIcon />
          Map
        </TabsTrigger>
      </TabsList>
      <TabsContent value="players" className="space-y-6">
        {players}
      </TabsContent>
      <TabsContent value="chat">{chat}</TabsContent>
      <TabsContent value="map">{map}</TabsContent>
    </Tabs>
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
