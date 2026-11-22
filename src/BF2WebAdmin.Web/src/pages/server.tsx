import { useQuery } from '@tanstack/react-query'
import { Map as MapIcon, MessageSquare, Users } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api, type ServerSnapshot } from '@/api/client'
import { useConfirm } from '@/components/confirm-dialog'
import { ServerMap } from '@/components/server-map'
import { ActivityLog } from '@/components/server/activity-log'
import { BanList } from '@/components/server/ban-list'
import { ChatInput, ChatLog } from '@/components/server/chat-panel'
import { CommandPalette } from '@/components/server/command-palette'
import { ServerConnectCard } from '@/components/server/connect-guide'
import { HealthPanel } from '@/components/server/health-panel'
import { useServerPage } from '@/components/server/server-context'
import { TeamTable } from '@/components/server/team-table'
import { Badge } from '@/components/ui/badge'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useIsMobile } from '@/hooks/use-mobile'
import { usePlayerSheet } from '@/hooks/use-player-sheet'
import { useServerActions } from '@/hooks/use-server-actions'
import { healthQuery } from '@/hooks/use-server-live'
import { teamTextColor } from '@/lib/teams'
import { cn } from '@/lib/utils'

/**
 * The Live tab: the teams, the chat and the minimap. On a large screen they share the window's height (ServerLayout
 * gives the page the rest of it): the teams on top, a long scoreboard scrolling in its card, and under them the chat,
 * its box pinned at the bottom, beside the map. On a phone the page fills the screen too, with one view at a time and
 * the chat box always at the bottom. The round actions are in the header, the commands in a palette (Ctrl+K, or
 * Commands by the chat box). An offline server has none of them.
 */
export function ServerLivePage() {
  const { serverId, snapshot, positions } = useServerPage()
  const actions = useServerActions(serverId, snapshot.data)
  const sheet = usePlayerSheet(actions)
  const mobile = useIsMobile()
  const [commandsOpen, setCommandsOpen] = useState(false)
  const connected = snapshot.data?.server.socketState === 'Connected'
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId } } }, { enabled: connected })

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

  const { server, teams, players, chatLog } = snapshot.data
  if (server.socketState !== 'Connected') return <ServerConnectCard server={server} />

  const teamTables = (className?: string) =>
    teams.map((team) => (
      <TeamTable
        key={team.id}
        team={team}
        players={players.filter((p) => p.team === team.id)}
        positions={positions}
        authLevels={sheet.authLevels}
        serverGroup={sheet.serverGroup}
        connections={connections}
        actions={actions}
        onModerate={sheet.moderate}
        onDetails={sheet.open}
        className={className}
      />
    ))
  const chatLogFor = (className: string) => <ChatLog chatLog={chatLog} players={players} teams={teams} onPlayer={sheet.open} className={className} />
  const chatInput = (className?: string) => (
    <ChatInput players={players} commands={commands.data ?? []} onSend={actions.sendChat} onCommands={() => setCommandsOpen(true)} className={className} />
  )

  return (
    <>
      {mobile ? (
        <PhoneLive snapshot={snapshot.data} players={teamTables()} chatLog={chatLogFor('min-h-0 flex-1')} chatInput={chatInput('border-t-0')} map={(className) => <ServerMap map={server.map} players={players} positions={positions} className={className} />} />
      ) : (
        // Below lg the parts stack and the page scrolls; from lg the rows share the height: the teams' row as tall as
        // its cards up to 45 %, the chat and the map the rest
        <div className="flex flex-col gap-6 lg:grid lg:min-h-0 lg:flex-1 lg:grid-cols-2 lg:grid-rows-[fit-content(45%)_minmax(0,1fr)] lg:gap-4">
          {teamTables('lg:min-h-0 lg:overflow-y-auto')}
          <Card className="gap-0 py-0 lg:min-h-0">
            <h2 className="flex items-center gap-2 border-b px-4 py-2.5 text-sm font-medium">
              <MessageSquare className="size-4 text-muted-foreground" />
              Chat
            </h2>
            {chatLogFor('h-[min(520px,60svh)] lg:h-auto lg:min-h-0 lg:flex-1')}
            {chatInput()}
          </Card>
          <ServerMap map={server.map} players={players} positions={positions} className="h-[600px] lg:h-auto lg:min-h-0" />
        </div>
      )}

      {sheet.element}
      <CommandPalette open={commandsOpen} onOpenChange={setCommandsOpen} serverId={serverId} players={players} actions={actions} />
    </>
  )
}

/**
 * A phone shows one view at a time in the screen's height, picked with a segmented control: the chat (the default:
 * a one-line score per team on top, which opens the players), the players, or the map. The chat box stays at the
 * bottom, above the tab bar, in every view, so a message is one tap away and what is typed survives a switch. Chat that
 * comes while another view is shown is counted on the Chat segment (the log is capped, so from the last line seen, not
 * by length).
 */
function PhoneLive({
  snapshot,
  players,
  chatLog,
  chatInput,
  map,
}: {
  snapshot: ServerSnapshot
  players: React.ReactNode
  chatLog: React.ReactNode
  chatInput: React.ReactNode
  map: (className: string) => React.ReactNode
}) {
  const [view, setView] = useState('chat')
  const { chatLog: lines } = snapshot
  const lastId = lines.at(-1)?.message.id ?? ''
  const [seenId, setSeenId] = useState(lastId)
  if (view === 'chat' && seenId !== lastId) setSeenId(lastId)
  const unread = view === 'chat' ? 0 : lines.length - 1 - lines.findIndex((e) => e.message.id === seenId)

  return (
    <Tabs value={view} onValueChange={setView} className="min-h-0 flex-1 gap-3">
      <TabsList className="w-full shrink-0 group-data-horizontal/tabs:h-9">
        <TabsTrigger value="chat">
          <MessageSquare />
          Chat
          {unread > 0 && (
            <Badge className="h-4.5 min-w-4.5 px-1 tabular-nums" aria-label={`${unread} new`}>
              {unread > 99 ? '99+' : unread}
            </Badge>
          )}
        </TabsTrigger>
        <TabsTrigger value="players">
          <Users />
          Players
          <span className="text-muted-foreground tabular-nums">{snapshot.players.length}</span>
        </TabsTrigger>
        <TabsTrigger value="map">
          <MapIcon />
          Map
        </TabsTrigger>
      </TabsList>
      {/* The views and the chat box are one column: the box closes the chat's card, and sits under the others */}
      <div className="flex min-h-0 flex-1 flex-col">
        <TabsContent value="chat" className="flex min-h-0 flex-1 flex-col gap-3">
          <TeamScores snapshot={snapshot} onOpen={() => setView('players')} />
          <Card className="min-h-0 flex-1 gap-0 rounded-b-none border-b-0 py-0">
            <h2 className="sr-only">Chat</h2>
            {chatLog}
          </Card>
        </TabsContent>
        <TabsContent value="players" className="min-h-0 flex-1 space-y-4 overflow-y-auto pb-3">
          {players}
        </TabsContent>
        <TabsContent value="map" className="flex min-h-0 flex-1 flex-col pb-3">
          {map('min-h-0 flex-1')}
        </TabsContent>
        <div className={cn('shrink-0 rounded-xl bg-card ring-1 ring-foreground/10', view === 'chat' && 'rounded-t-none')}>{chatInput}</div>
      </div>
    </Tabs>
  )
}

/**
 * The scoreboard in one line per team, for the phone's chat view: the team, its players by score with their scores,
 * and the team's total. A tap opens the full players view.
 */
function TeamScores({ snapshot, onOpen }: { snapshot: ServerSnapshot; onOpen: () => void }) {
  return (
    <button
      type="button"
      onClick={onOpen}
      aria-label="Scores: open the players"
      className="grid shrink-0 divide-y rounded-xl border bg-card text-left text-sm transition-colors hover:bg-muted/50 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      {snapshot.teams.map((team) => {
        const members = snapshot.players.filter((p) => p.team === team.id).sort((a, b) => b.score.total - a.score.total)
        const total = members.reduce((sum, p) => sum + p.score.total, 0)
        return (
          <span key={team.id} className="flex min-w-0 items-center gap-3 px-3 py-2">
            <span className={cn('w-14 shrink-0 truncate font-semibold', teamTextColor(team.id))}>{team.name}</span>
            <span className="min-w-0 flex-1 truncate text-muted-foreground">
              {members.length === 0
                ? 'No players'
                : members.map((p, i) => (
                    <span key={p.index}>
                      {i > 0 && ', '}
                      <span className="text-foreground">{p.name}</span> {p.score.total}
                    </span>
                  ))}
            </span>
            <span className="shrink-0 font-semibold tabular-nums">{total}</span>
          </span>
        )
      })}
    </button>
  )
}

/** The Health tab: the server process and the players' connections, at full width. A player's name opens their sheet. */
export function ServerHealthPage() {
  const { serverId, snapshot } = useServerPage()
  const sheet = usePlayerSheet(useServerActions(serverId, snapshot.data))
  return (
    <>
      <HealthPanel serverId={serverId} connected={snapshot.data?.server.socketState === 'Connected'} players={snapshot.data?.players ?? []} onPlayer={sheet.open} />
      {sheet.element}
    </>
  )
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

/** The Activity tab: what web users sent to the server and the game's events, in one timeline. */
export function ServerActivityPage() {
  const { serverId, snapshot } = useServerPage()
  const sheet = usePlayerSheet(useServerActions(serverId, snapshot.data))

  return (
    <Card className="max-w-4xl gap-0 pb-0">
      <CardHeader className="pb-3">
        <CardTitle>Activity</CardTitle>
        <CardDescription>What web users sent to this server, with its answers, and what happened in the game. Newest first.</CardDescription>
      </CardHeader>
      <ActivityLog serverId={serverId} events={snapshot.data?.eventLog ?? []} players={snapshot.data?.players ?? []} onPlayer={sheet.open} />
      {sheet.element}
    </Card>
  )
}
