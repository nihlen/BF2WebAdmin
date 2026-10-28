import { useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Ban as BanIcon, Bolt, Box, Map as MapIcon, MessageSquare, MoreVertical, Pause, RefreshCw, RotateCcw, Send, Settings, Trash2 } from 'lucide-react'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { api, errorMessage, type ChatLogEntry, type Player, type RconResponse, type ServerSnapshot, type Team } from '@/api/client'
import { InputDialog, type InputRequest } from '@/components/input-dialog'
import { ServerMap } from '@/components/server-map'
import { ServerStatusDot } from '@/components/server-status'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { type PlayerPosition, useServerLive } from '@/hooks/use-server-live'
import { decodeServerId, encodeServerId } from '@/lib/server-id'
import { cn } from '@/lib/utils'
import { serverRoute } from '@/router'

// Rcon commands whose response is worth a notification
const notifyCommands = ['!w', '!k', '!b', 'iga warn', 'iga kick', 'iga ban', '!unban', '!clearbans']
// A ban is applied a few seconds after the command: the player sees a message first
const banDelayMs = 5000

export function ServerPage() {
  const serverId = decodeServerId(serverRoute.useParams().serverId)
  const { snapshot, positions } = useServerLive(serverId)
  const commands = useServerCommands(serverId)
  const [inputRequest, setInputRequest] = useState<InputRequest | null>(null)

  if (snapshot.isError) return <p className="text-destructive">{errorMessage(snapshot.error)}</p>
  if (!snapshot.data) return <Skeleton className="h-[600px]" />

  const { server, teams, players, maps, chatLog, eventLog } = snapshot.data

  return (
    <div className="space-y-6">
      <title>{`BF2 Web Admin - ${server.name}`}</title>

      {server.socketState === 'Disconnected' && (
        <Alert variant="destructive">
          <AlertTitle>Server is offline</AlertTitle>
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
            3D viewer
          </Button>
          <Button
            variant="outline"
            size="icon-lg"
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
        <Badge variant="secondary">{server.gameState}</Badge>
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

      <div className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={() => commands.custom('.crash')}>
          <RefreshCw data-icon="inline-start" className="text-destructive" />
          Restart server
        </Button>
        <Button variant="outline" onClick={() => commands.rcon('exec admin.restartMap')}>
          <RotateCcw data-icon="inline-start" className="text-emerald-500" />
          Restart map
        </Button>
        <Button variant="outline" onClick={() => commands.rcon('exec gameLogic.togglePause')}>
          <Pause data-icon="inline-start" className="text-amber-500" />
          Toggle pause
        </Button>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {teams.map((team) => (
          <TeamTable
            key={team.id}
            team={team}
            players={players.filter((p) => p.team === team.id)}
            positions={positions}
            onCustom={commands.custom}
            onRcon={commands.rcon}
            onInput={setInputRequest}
          />
        ))}
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <ServerTabs serverId={serverId} snapshot={snapshot.data} chatLog={chatLog} eventLog={eventLog} maps={maps} commands={commands} />
        <ServerMap map={server.map} players={players} positions={positions} />
      </div>

      <InputDialog request={inputRequest} onClose={() => setInputRequest(null)} />
    </div>
  )
}

type Commands = ReturnType<typeof useServerCommands>

/** The commands of the server page. Rcon responses go to the Requests tab, and some to a notification. */
function useServerCommands(serverId: string) {
  const queryClient = useQueryClient()
  const [responses, setResponses] = useState<RconResponse[]>([])
  const params = { path: { serverId } }
  const bansKey = api.queryOptions('get', '/api/servers/{serverId}/bans', { params }).queryKey

  const addResponse = (response: RconResponse) => {
    setResponses((r) => [...r, response])
    if (notifyCommands.some((c) => response.request.toLowerCase().startsWith(c))) toast(response.response)
    if (response.request.startsWith('iga ban') || response.request.startsWith('!b')) {
      setTimeout(() => void queryClient.invalidateQueries({ queryKey: bansKey }), banDelayMs)
    }
  }

  const rcon = api.useMutation('post', '/api/servers/{serverId}/rcon', { onSuccess: addResponse })
  const chat = api.useMutation('post', '/api/servers/{serverId}/chat', {
    onSuccess: (data, { body }) => {
      if (data.response !== null) addResponse({ request: body.message, response: data.response })
    },
  })
  const custom = api.useMutation('post', '/api/servers/{serverId}/commands')

  return {
    responses,
    bansKey,
    // Failures are shown by the mutation cache's error notification
    rcon: (command: string) => rcon.mutateAsync({ params, body: { command } }).catch(() => undefined),
    chat: (message: string) => chat.mutate({ params, body: { message } }),
    custom: (command: string) => custom.mutate({ params, body: { command } }),
  }
}

function TeamTable({
  team,
  players,
  positions,
  onCustom,
  onRcon,
  onInput,
}: {
  team: Team
  players: Player[]
  positions: React.RefObject<Map<number, PlayerPosition>>
  onCustom: Commands['custom']
  onRcon: Commands['rcon']
  onInput: (request: InputRequest) => void
}) {
  // Pings arrive with the positions, which do not re-render: read them once a second
  const [, setTick] = useState(0)
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 1000)
    return () => clearInterval(timer)
  }, [])

  const askReason = (title: string, command: (reason: string) => string) =>
    onInput({ title, label: 'Reason', onSubmit: (reason) => void onRcon(command(reason)) })

  return (
    <Card className="py-0">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-10">{team.name}</TableHead>
            <TableHead>Name</TableHead>
            <TableHead className="text-center">S</TableHead>
            <TableHead className="text-center">K</TableHead>
            <TableHead className="text-center">D</TableHead>
            <TableHead className="text-center">P</TableHead>
            <TableHead className="w-px" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {[...players]
            .sort((a, b) => b.score.total - a.score.total)
            .map((player) => (
              <TableRow key={player.index}>
                <TableCell>{player.index}</TableCell>
                <TableCell>
                  {player.country && <span className={`fi fi-${player.country.toLowerCase()} mr-2`} title={player.country} />}
                  <span className={cn(!player.isAlive && 'text-muted-foreground/50')}>{player.name}</span>
                </TableCell>
                <TableCell className="text-center">{player.score.total}</TableCell>
                <TableCell className="text-center">{player.score.kills}</TableCell>
                <TableCell className="text-center">{player.score.deaths}</TableCell>
                <TableCell className="text-center">{positions.current.get(player.index)?.ping ?? player.score.ping}</TableCell>
                <TableCell>
                  <DropdownMenu>
                    <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" />}>
                      <MoreVertical />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => onCustom(`.switchid ${player.index}`)}>Switch team</DropdownMenuItem>
                      <DropdownMenuItem onClick={() => onCustom(`.killid ${player.index}`)}>Kill player</DropdownMenuItem>
                      <DropdownMenuItem onClick={() => askReason(`Warn ${player.name}`, (r) => `iga warn ${player.index} ${r}`)}>
                        Warn player
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => askReason(`Kick ${player.name}`, (r) => `iga kick ${player.index} ${r}`)}>
                        Kick player
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        variant="destructive"
                        onClick={() => askReason(`Ban ${player.name}`, (r) => `iga ban ${player.index} Perm ${r}`)}
                      >
                        Ban player
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            ))}
        </TableBody>
      </Table>
    </Card>
  )
}

function ServerTabs({
  serverId,
  snapshot,
  chatLog,
  eventLog,
  maps,
  commands,
}: {
  serverId: string
  snapshot: ServerSnapshot
  chatLog: ServerSnapshot['chatLog']
  eventLog: ServerSnapshot['eventLog']
  maps: ServerSnapshot['maps']
  commands: Commands
}) {
  const [tab, setTab] = useState('chat')

  return (
    <Card className="gap-0 py-0">
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList variant="line" className="w-full justify-start border-b px-2">
          <TabsTrigger value="chat">
            <MessageSquare />
            Chat
          </TabsTrigger>
          <TabsTrigger value="maps">
            <MapIcon />
            Maps
          </TabsTrigger>
          <TabsTrigger value="bans">
            <BanIcon />
            Bans
          </TabsTrigger>
          <TabsTrigger value="events">
            <Bolt />
            Events
          </TabsTrigger>
          <TabsTrigger value="requests">
            <Send />
            Requests
          </TabsTrigger>
        </TabsList>

        <TabsContent value="chat">
          <LogList className="h-[500px]" items={chatLog}>
            {(entry) => <ChatLine key={entry.message.id} entry={entry} />}
          </LogList>
          <ChatInput onSend={commands.chat} />
        </TabsContent>

        <TabsContent value="maps">
          <ul className="h-[550px] overflow-y-auto p-2">
            {maps.map((map) => (
              <li key={map.index}>
                <button
                  type="button"
                  className={cn(
                    'flex w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm hover:bg-muted',
                    map.name === snapshot.server.map && 'bg-muted font-medium',
                  )}
                  onClick={() => void commands.rcon(`map ${map.name} gpm_cq ${map.size}`)}
                >
                  <MapIcon className="size-4 text-muted-foreground" />
                  <span className="w-6 text-muted-foreground">{map.index}</span>
                  {map.name}
                  <span className="text-muted-foreground">{map.size}</span>
                </button>
              </li>
            ))}
          </ul>
        </TabsContent>

        <TabsContent value="bans">
          <BanList serverId={serverId} commands={commands} />
        </TabsContent>

        <TabsContent value="events">
          <LogList className="h-[550px]" items={eventLog}>
            {(entry) => (
              <li key={entry.id}>
                <Time value={entry.timestamp} /> {entry.message}
              </li>
            )}
          </LogList>
        </TabsContent>

        <TabsContent value="requests">
          <LogList className="h-[550px]" items={commands.responses}>
            {(response, index) => (
              <li key={index} className="mb-4 space-y-1">
                <div className="text-muted-foreground">Request:</div>
                <pre className="whitespace-pre-wrap">{response.request}</pre>
                <div className="text-muted-foreground">Response:</div>
                <pre className="whitespace-pre-wrap">{response.response}</pre>
              </li>
            )}
          </LogList>
        </TabsContent>
      </Tabs>
    </Card>
  )
}

/** A scrolling list that stays at the bottom while new items arrive, unless the user scrolled up. */
function LogList<T>({ items, className, children }: { items: T[]; className?: string; children: (item: T, index: number) => React.ReactNode }) {
  const ref = useRef<HTMLUListElement>(null)
  const atBottom = useRef(true)

  useLayoutEffect(() => {
    if (atBottom.current && ref.current) ref.current.scrollTop = ref.current.scrollHeight
  }, [items])

  return (
    <ul
      ref={ref}
      className={cn('overflow-y-auto p-4 text-sm', className)}
      onScroll={(e) => {
        const el = e.currentTarget
        atBottom.current = el.scrollHeight - (el.scrollTop + el.clientHeight) < 30
      }}
    >
      {items.map(children)}
    </ul>
  )
}

function ChatLine({ entry }: { entry: ChatLogEntry }) {
  const { message } = entry
  const color =
    message.channel === 'Team'
      ? message.teamName === 'US'
        ? 'text-blue-400'
        : 'text-red-400'
      : message.channel === 'Squad'
        ? 'text-green-400'
        : message.channel === 'ServerMessage'
          ? 'text-muted-foreground'
          : undefined

  return (
    <li className={color}>
      <Time value={entry.timestamp} />
      {message.type !== 'Server' && ` ${message.playerName}:`} {message.text}
    </li>
  )
}

function ChatInput({ onSend }: { onSend: (message: string) => void }) {
  const [text, setText] = useState('')

  return (
    <form
      className="border-t p-2"
      onSubmit={(e) => {
        e.preventDefault()
        if (!text.trim()) return
        onSend(text)
        setText('')
      }}
    >
      <Input placeholder='Enter message (".command" for a custom command, "!command" for rcon)' value={text} onChange={(e) => setText(e.target.value)} />
    </form>
  )
}

function BanList({ serverId, commands }: { serverId: string; commands: Commands }) {
  const queryClient = useQueryClient()
  const bans = api.useQuery('get', '/api/servers/{serverId}/bans', { params: { path: { serverId } } })

  const unban = async (identifier: string | null) => {
    if (!identifier) return
    await commands.rcon(`!unban ${identifier}`)
    await queryClient.invalidateQueries({ queryKey: commands.bansKey })
  }

  if (bans.isPending) return <Skeleton className="m-4 h-40" />
  if (bans.isError) return <p className="p-4 text-destructive">{errorMessage(bans.error)}</p>

  return (
    <div className="h-[550px] overflow-y-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Date</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Period</TableHead>
            <TableHead>Method</TableHead>
            <TableHead>Identifier</TableHead>
            <TableHead>By</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead className="w-px" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {bans.data.map((ban, index) => {
            const identifier = ban.method === 'Key' ? ban.cdkeyhash : ban.address
            return (
              <TableRow key={index}>
                <TableCell>{ban.datetime}</TableCell>
                <TableCell>{ban.nick}</TableCell>
                <TableCell>{ban.period}</TableCell>
                <TableCell>{ban.method}</TableCell>
                <TableCell>{identifier}</TableCell>
                <TableCell>{ban.by}</TableCell>
                <TableCell>{ban.reason}</TableCell>
                <TableCell>
                  <Button variant="ghost" size="icon-sm" title="Unban" onClick={() => void unban(identifier)}>
                    <Trash2 />
                  </Button>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}

const timeFormat = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' })

/** The time of a log entry; with the date when it is not today. */
function Time({ value }: { value: string }) {
  const date = new Date(value)
  const today = date.toDateString() === new Date().toDateString()
  return <span className="text-muted-foreground">{(today ? timeFormat : dateTimeFormat).format(date)}</span>
}
