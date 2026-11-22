import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CircleHelp, Crosshair, Ellipsis, Loader2, MessageSquare, Move3d, PanelBottomOpen, RotateCw, Trash2, Undo2, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react'
import { toast } from 'sonner'
import { api, ApiError, errorMessage, fetchClient } from '@/api/client'
import { useHubEvent } from '@/api/hub'
import { useConfirm } from '@/components/confirm-dialog'
import { ChatPanel } from '@/components/server/chat-panel'
import { useServerPage } from '@/components/server/server-context'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useServerActions } from '@/hooks/use-server-actions'
import { normalizeDegrees, type Vec } from '@/builder/coords'
import { BUILDER_CAMERA_KEYS, FLY_WHILE_LOOKING } from '@/builder/keys'
import { addRecent, categoryKey, categoryLabel, isGroundOrWater, readRecent } from '@/builder/palette'
import { BuilderScene, type GizmoTool, type PlaceSpec, type SceneItem, teamColor } from '@/builder/scene'
import { type CatalogEntry, loadCatalog, templateThumbnail } from '@/builder/templates'
import { cn } from '@/lib/utils'
import type { components } from '@/api/schema'

type BuilderState = components['schemas']['BuilderStateDto']
type BuiltItem = components['schemas']['BuiltItemDto']
type Layout = components['schemas']['LayoutDto']

const builderQuery = (serverId: string) => api.queryOptions('get', '/api/servers/{serverId}/builder', { params: { path: { serverId } } })
const layoutsQuery = (serverId: string) => api.queryOptions('get', '/api/servers/{serverId}/builder/layouts', { params: { path: { serverId } } })

/**
 * A change not applied yet, on this browser only: a new item (no builtId), or a built one moved or removed. Apply sends
 * them all at once; the server removes, moves (remove and spawn again) and adds, then resyncs the players' clients.
 */
type Draft = {
  key: string
  builtId: number | null
  kind: PlaceSpec['kind']
  template: string
  team: number
  position: Vec
  rotation: Vec
  remove: boolean
}

const builtKey = (id: number) => `b${id}`

function sameVec(a: Vec, b: Vec) {
  return a.length === b.length && a.every((v, i) => Math.abs(v - b[i]) < 0.001)
}

/**
 * The map builder: place, move and remove static objects, spawn points and helipads on the running map, in 3D, with
 * the players where they are. Changes are drafts until Apply. Layouts save what is built for the map.
 */
export function BuilderPage() {
  const { serverId, snapshot, positions } = useServerPage()
  const queryClient = useQueryClient()
  const state = useQuery({ ...builderQuery(serverId), staleTime: Infinity })
  const [scene, setScene] = useState<BuilderScene | null>(null)
  const [catalog, setCatalog] = useState<CatalogEntry[] | null>(null)
  const [drafts, setDrafts] = useState<Map<string, Draft>>(new Map())
  const [selected, setSelected] = useState<string | null>(null)
  const [tool, setTool] = useState<GizmoTool>('translate')
  const [localAxes, setLocalAxes] = useState(false)
  const [snap, setSnap] = useState(false)
  const [applying, setApplying] = useState(false)
  const [tab, setTab] = useState('objects')
  const [chatOpen, setChatOpen] = useState(() => readFlag(chatOpenKey))
  const [recent, setRecent] = useState(readRecent)
  const [introDismissed, setIntroDismissed] = useState(() => readFlag(introKey))
  // Below lg the panels are a bottom sheet over a full-height view; the sheet closes when something is placed or picked
  const wide = useWide()
  const [panelsOpen, setPanelsOpen] = useState(false)
  const { confirm, dialog: confirmDialog } = useConfirm()
  const chatRef = useRef<HTMLDivElement>(null)
  const nextDraft = useRef(1)
  const actions = useServerActions(serverId, snapshot.data)
  const commands = api.useQuery('get', '/api/servers/{serverId}/commands', { params: { path: { serverId } } })
  const chatLog = useMemo(() => snapshot.data?.chatLog ?? [], [snapshot.data])

  // Messages that came while the chat was hidden, counted on its button (the log is capped, so from the last one seen)
  const [seenChat, setSeenChat] = useState<string | null>(null)
  const seenIndex = seenChat === null ? -1 : chatLog.findIndex((e) => e.message.id === seenChat)
  const unread = chatOpen || seenChat === null ? 0 : chatLog.length - 1 - seenIndex
  // With the chat hidden from the start, what was there when the page opened counts as seen
  useEffect(() => {
    if (seenChat === null && snapshot.data) setSeenChat(snapshot.data.chatLog.at(-1)?.message.id ?? '')
  }, [seenChat, snapshot.data])
  const toggleChat = () => {
    const open = !chatOpen
    setChatOpen(open)
    setSeenChat(chatLog.at(-1)?.message.id ?? '')
    writeFlag(chatOpenKey, open)
    // Below lg the chat opens under the 3D view, which may be off screen
    if (open) requestAnimationFrame(() => chatRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }))
  }

  const builder = state.data
  const map = builder?.map ?? snapshot.data?.server.map ?? ''
  const teams = snapshot.data?.teams ?? []
  const teamName = (id: number) => teams.find((t) => t.id === id)?.name || `Team ${id}`

  useHubEvent('BuilderEvent', (e) => queryClient.setQueryData(builderQuery(serverId).queryKey, e))

  // A new map: the drafts were for the old one
  const lastMap = useRef(map)
  useEffect(() => {
    if (lastMap.current && map && lastMap.current !== map) {
      setDrafts(new Map())
      setSelected(null)
    }
    lastMap.current = map
  }, [map])

  useEffect(() => {
    void loadCatalog().then(setCatalog)
  }, [])

  // Only the templates this map's level uses can be spawned (others can crash the game): the server asks the game
  // server about each of the catalog's once per map
  const connected = !!builder?.connected
  const onLevel = useQuery({
    queryKey: ['builder-templates', serverId, map],
    enabled: !!catalog && catalog.length > 0 && !!map && connected,
    staleTime: Infinity,
    queryFn: async () => {
      const { data } = await fetchClient.POST('/api/servers/{serverId}/builder/templates', {
        params: { path: { serverId } },
        body: catalog!.map((c) => c.name),
      })
      return new Set(data ?? [])
    },
  })
  const levelCatalog = useMemo(() => (catalog && onLevel.data ? catalog.filter((c) => onLevel.data.has(c.name)) : null), [catalog, onLevel.data])

  // ---------------------------------------------------------------------------------------------------------------
  // The scene

  const containerRef = useCallback((container: HTMLDivElement | null) => {
    if (!container) return
    const created = new BuilderScene(container)
    setScene(created)
    return () => {
      created.dispose()
      setScene(null)
    }
  }, [])

  useEffect(() => {
    if (scene && map) void scene.loadMap(map)
  }, [scene, map])

  useEffect(() => {
    if (scene && catalog) scene.setCatalog(catalog)
  }, [scene, catalog])

  // The players, read every frame from the live positions (the position DTO is x, y north, z height)
  const playersRef = useRef(snapshot.data?.players ?? [])
  useEffect(() => {
    playersRef.current = snapshot.data?.players ?? []
  }, [snapshot.data])
  useEffect(() => {
    scene?.setPlayerSource(() =>
      playersRef.current.map((p) => {
        const live = positions.current.get(p.index)?.current ?? p.position
        return { index: p.index, name: p.name, team: p.team, alive: p.isAlive, position: live ? [live.x, live.z, live.y] : null }
      }),
    )
  }, [scene, positions])

  const items: BuiltItem[] = useMemo(() => builder?.items ?? [], [builder])
  const sceneItems = useMemo(() => {
    const result: SceneItem[] = []
    for (const item of items) {
      const draft = drafts.get(builtKey(item.id))
      result.push({
        key: builtKey(item.id),
        kind: item.kind,
        template: item.template,
        team: item.team,
        position: draft?.position ?? item.position,
        rotation: draft?.rotation ?? item.rotation,
        draft: !!draft && !draft.remove,
        removed: !!draft?.remove,
        missing: !item.spawned,
        spawnReady: item.spawnReady,
      })
    }
    for (const d of drafts.values()) {
      if (d.builtId !== null) continue
      result.push({ key: d.key, kind: d.kind, template: d.template, team: d.team, position: d.position, rotation: d.rotation, draft: true, removed: false, missing: false, spawnReady: d.kind === 'Object' })
    }
    return result
  }, [items, drafts])

  useEffect(() => scene?.setItems(sceneItems), [scene, sceneItems])
  useEffect(() => scene?.select(selected), [scene, selected])
  useEffect(() => scene?.setTool(tool), [scene, tool])
  useEffect(() => scene?.setLocalSpace(localAxes), [scene, localAxes])
  useEffect(() => scene?.setSnap(snap), [scene, snap])

  // Drafts drop items the server no longer has (another admin removed them)
  useEffect(() => {
    const ids = new Set(items.map((i) => i.id))
    setDrafts((current) => {
      const stale = [...current.values()].filter((d) => d.builtId !== null && !ids.has(d.builtId))
      if (stale.length === 0) return current
      const next = new Map(current)
      for (const d of stale) next.delete(d.key)
      return next
    })
  }, [items])

  // ---------------------------------------------------------------------------------------------------------------
  // Drafts

  const sceneItemOf = (key: string | null) => (key ? sceneItems.find((i) => i.key === key) : undefined)
  const builtOf = (key: string) => items.find((i) => builtKey(i.id) === key)

  const changeItem = useCallback(
    (key: string, change: Partial<Pick<Draft, 'position' | 'rotation' | 'remove'>>) => {
      setDrafts((current) => {
        const next = new Map(current)
        let draft = next.get(key)
        if (!draft) {
          const item = items.find((i) => builtKey(i.id) === key)
          if (!item) return current
          draft = { key, builtId: item.id, kind: item.kind, template: item.template, team: item.team, position: item.position, rotation: item.rotation, remove: false }
        }
        draft = { ...draft, ...change }
        // A built item moved back where it is, and not removed, has no change left
        const item = draft.builtId !== null ? items.find((i) => i.id === draft!.builtId) : undefined
        if (item && !draft.remove && sameVec(draft.position, item.position) && sameVec(draft.rotation, item.rotation)) next.delete(key)
        else next.set(key, draft)
        return next
      })
    },
    [items],
  )

  const removeItem = (key: string) => {
    const draft = drafts.get(key)
    if (draft && draft.builtId === null) {
      // A new item that was never applied just goes
      setDrafts((current) => {
        const next = new Map(current)
        next.delete(key)
        return next
      })
      setSelected(null)
      return
    }
    changeItem(key, { remove: true })
    setSelected(null)
  }

  const revertItem = (key: string) =>
    setDrafts((current) => {
      const next = new Map(current)
      next.delete(key)
      return next
    })

  const addDraft = useCallback((spec: PlaceSpec, position: Vec, rotation: Vec) => {
    const key = `n${nextDraft.current++}`
    const draft: Draft = { key, builtId: null, kind: spec.kind, template: spec.template, team: spec.team, position, rotation, remove: false }
    setDrafts((current) => new Map(current).set(key, draft))
    setSelected(key)
    if (spec.kind === 'Object') setRecent((r) => addRecent(r, spec.template))
    return key
  }, [])

  /** A copy beside the item, one width of it to the east, selected to be moved */
  const duplicate = (key: string) => {
    const item = sceneItemOf(key)
    if (!item) return
    const entry = item.kind === 'Object' ? catalog?.find((c) => c.name === item.template.toLowerCase()) : undefined
    const width = entry ? entry.max[0] - entry.min[0] + 2 : item.kind === 'Helipad' ? 14 : 3
    addDraft(item, [item.position[0] + width, item.position[1], item.position[2]], item.rotation)
  }

  /** Stands the item on the ground or the object under it (End) */
  const toGround = (key: string) => {
    const item = sceneItemOf(key)
    const ground = scene?.groundBelow(key)
    if (!item || !ground) return
    const position = [item.position[0], ground[1], item.position[2]]
    if (key.startsWith('n')) setDrafts((c) => { const d = c.get(key); return d ? new Map(c).set(key, { ...d, position }) : c })
    else changeItem(key, { position })
  }

  // The scene's callbacks
  useEffect(() => {
    scene?.setHandlers({
      select: setSelected,
      transform: (key, position, rotation) => {
        if (key.startsWith('n'))
          setDrafts((current) => {
            const d = current.get(key)
            return d ? new Map(current).set(key, { ...d, position, rotation }) : current
          })
        else changeItem(key, { position, rotation })
      },
    })
  }, [scene, changeItem])

  /**
   * Pressing an item in a list (the palette, the spawns): dragged onto the 3D view it goes on the surface under the
   * pointer; a click puts it on the surface in the middle of the view. Either way it is selected, to move or turn.
   */
  const beginPlace = (spec: PlaceSpec, e: React.PointerEvent) => {
    if (e.button !== 0 || !scene) return
    e.preventDefault()
    const start = { x: e.clientX, y: e.clientY }
    let moved = false
    scene.beginDragPlace(spec)
    document.body.style.cursor = 'grabbing'
    const move = (ev: PointerEvent) => {
      if (!moved && Math.hypot(ev.clientX - start.x, ev.clientY - start.y) > 5) moved = true
      if (moved) scene.dragPlaceAt(ev.clientX, ev.clientY)
    }
    const end = (ev: PointerEvent | null) => {
      window.removeEventListener('pointermove', move)
      window.removeEventListener('pointerup', up)
      window.removeEventListener('pointercancel', cancel)
      window.removeEventListener('keydown', escape, true)
      document.body.style.cursor = ''
      if (!ev) {
        scene.cancelDragPlace()
        return
      }
      const position = moved ? scene.endDragPlace(ev.clientX, ev.clientY) : scene.placeAhead(spec)
      scene.cancelDragPlace()
      if (!position) return
      addDraft(spec, position, [0, 0, 0])
      setPanelsOpen(false)
      scene.focusView()
    }
    const up = (ev: PointerEvent) => end(ev)
    const cancel = () => end(null)
    const escape = (ev: KeyboardEvent) => {
      if (ev.code !== 'Escape') return
      ev.stopPropagation()
      end(null)
    }
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', up)
    window.addEventListener('pointercancel', cancel)
    window.addEventListener('keydown', escape, true)
  }

  // Unapplied drafts are lost with the page (Ctrl+W, which closes the tab, is next to the keys that fly)
  useEffect(() => {
    if (drafts.size === 0) return
    const warn = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [drafts.size])

  // ---------------------------------------------------------------------------------------------------------------
  // Keys, as in HeliGame's build mode: holding the right mouse button flies with WASD and Q/E; without it W moves and
  // E turns. Space and Ctrl go up and down, the arrow keys fly, Shift is faster, Alt slower. Ctrl snaps a gizmo drag

  const selectedRef = useRef(selected)
  const removeItemRef = useRef(removeItem)
  const groundRef = useRef<(key: string) => void>(() => {})
  useEffect(() => {
    selectedRef.current = selected
    removeItemRef.current = removeItem
    groundRef.current = toGround
  })
  useEffect(() => {
    if (!scene) return
    const typing = (e: KeyboardEvent) => e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement || e.target instanceof HTMLSelectElement
    const down = (e: KeyboardEvent) => {
      if (typing(e) || e.metaKey) return
      if (BUILDER_CAMERA_KEYS.has(e.code)) {
        scene.setKey(e.code, true)
        // W and E pick a tool unless flying
        if (!FLY_WHILE_LOOKING.has(e.code) || scene.looking || e.ctrlKey) {
          e.preventDefault()
          return
        }
      }
      // Ctrl held flies down: other browser shortcuts stay (Ctrl+W, which closes the tab, cannot be stopped)
      if (e.ctrlKey || e.repeat) return
      const key = selectedRef.current
      switch (e.code) {
        case 'Escape':
          setSelected(null)
          break
        case 'Delete':
        case 'Backspace':
          if (key) removeItemRef.current(key)
          break
        case 'KeyW':
          setTool('translate')
          break
        case 'KeyE':
          setTool('rotate')
          break
        case 'End':
          if (key) groundRef.current(key)
          break
        case 'KeyF':
          if (key) scene.focus(key)
          break
        default:
          return
      }
      e.preventDefault()
    }
    const up = (e: KeyboardEvent) => scene.setKey(e.code, false)
    const blur = () => scene.clearKeys()
    window.addEventListener('keydown', down)
    window.addEventListener('keyup', up)
    window.addEventListener('blur', blur)
    return () => {
      window.removeEventListener('keydown', down)
      window.removeEventListener('keyup', up)
      window.removeEventListener('blur', blur)
    }
  }, [scene])

  // ---------------------------------------------------------------------------------------------------------------
  // Apply

  const setState = (s: BuilderState) => queryClient.setQueryData(builderQuery(serverId).queryKey, s)
  const changes = [...drafts.values()]
  const removals = changes.filter((d) => d.builtId !== null && d.remove)
  const needsResync = changes.some((d) => d.builtId !== null && d.kind !== 'SpawnPoint')

  const apply = async () => {
    if (!builder || changes.length === 0) return
    setApplying(true)
    try {
      const { data } = await fetchClient.POST('/api/servers/{serverId}/builder/apply', {
        params: { path: { serverId } },
        body: {
          revision: builder.revision,
          add: changes.filter((d) => d.builtId === null).map((d) => ({ kind: d.kind, template: d.template, team: d.team, position: d.position, rotation: d.rotation })),
          update: changes.filter((d) => d.builtId !== null && !d.remove).map((d) => ({ id: d.builtId!, position: d.position, rotation: d.rotation })),
          remove: removals.map((d) => d.builtId!),
        },
      })
      if (!data) return
      setState(data.state)
      setDrafts(new Map())
      if (selected?.startsWith('n')) setSelected(null)
      for (const error of data.errors) toast.error(error)
      toast.success(`Applied ${changes.length} change${changes.length === 1 ? '' : 's'}${data.errors.length ? `, ${data.errors.length} failed` : ''}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        toast.error('Someone else changed the builder. It is reloaded; your drafts are kept, apply again.')
        void queryClient.invalidateQueries({ queryKey: builderQuery(serverId).queryKey })
      } else toast.error(errorMessage(error))
    } finally {
      setApplying(false)
    }
  }

  const confirmClear = () =>
    confirm({
      title: `Clear all ${items.length} built item${items.length === 1 ? '' : 's'}?`,
      description: builder?.canResync
        ? `Every built object, spawn point and helipad on ${map} (${items.length}) is removed from the game. A saved layout can bring them back.`
        : `This server can only clear by restarting the round: the ${items.length} built items are removed and the scores are put back afterwards.`,
      confirmLabel: `Clear ${items.length}`,
      destructive: true,
      onConfirm: () => void clearAll(),
    })

  const clearAll = async () => {
    try {
      const { data } = await fetchClient.POST('/api/servers/{serverId}/builder/clear', { params: { path: { serverId } } })
      setDrafts(new Map())
      setSelected(null)
      toast.success(data ? 'Cleared with a round restart' : 'Cleared')
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  // ---------------------------------------------------------------------------------------------------------------

  const selectedItem = sceneItemOf(selected)
  const selectedDraft = selected ? drafts.get(selected) : undefined
  const serverName = snapshot.data?.server.name ?? serverId
  // Closing the steps hides them for good in this browser; the "?" beside Chat brings them back
  const toggleIntro = () => {
    setIntroDismissed(!introDismissed)
    writeFlag(introKey, !introDismissed)
  }

  const panels = (
    <Tabs value={tab} onValueChange={(v) => setTab(String(v))} className="flex min-h-0 flex-1 flex-col">
      <TabsList variant="line" className="w-full justify-start">
        <TabsTrigger value="objects">Objects</TabsTrigger>
        <TabsTrigger value="spawns">Spawns</TabsTrigger>
        <TabsTrigger value="built">Built</TabsTrigger>
        <TabsTrigger value="layouts">Layouts</TabsTrigger>
      </TabsList>
      <TabsContent value="objects" className="flex min-h-0 flex-1 flex-col">
        <Palette
          catalog={catalog}
          levelCatalog={levelCatalog}
          recent={recent}
          checking={onLevel.isFetching}
          error={onLevel.isError ? errorMessage(onLevel.error) : !connected && !onLevel.data ? 'The game server is not connected: which objects this map has is not known.' : null}
          onRetry={() => void onLevel.refetch()}
          onPress={(template, e) => beginPlace({ kind: 'Object', template, team: 0 }, e)}
          onPlaceAhead={(template) => {
            if (!scene) return
            addDraft({ kind: 'Object', template, team: 0 }, scene.placeAhead({ kind: 'Object', template, team: 0 }), [0, 0, 0])
            setPanelsOpen(false)
          }}
        />
      </TabsContent>
      <TabsContent value="spawns" className="min-h-0 flex-1 overflow-y-auto">
        <SpawnsTab teamName={teamName} onPress={beginPlace} sceneItems={sceneItems} onSelect={(key) => { setSelected(key); setPanelsOpen(false) }} />
      </TabsContent>
      <TabsContent value="built" className="min-h-0 flex-1 overflow-y-auto">
        <BuiltList items={sceneItems} selected={selected} onSelect={(key) => { setSelected(key); scene?.focus(key); setPanelsOpen(false) }} />
      </TabsContent>
      <TabsContent value="layouts" className="min-h-0 flex-1 overflow-y-auto">
        <LayoutsTab serverId={serverId} state={builder} hasDrafts={drafts.size > 0} onLoaded={() => { setDrafts(new Map()); setSelected(null); setPanelsOpen(false) }} />
      </TabsContent>
    </Tabs>
  )

  return (
    <div className="flex flex-col gap-3 lg:min-h-0 lg:flex-1">
      <title>{`BF2 Web Admin - ${serverName} - Map builder`}</title>

      <div className="flex flex-wrap items-center gap-2">
        {builder && !builder.connected && <Badge variant="destructive">Not connected</Badge>}
        {builder?.layoutName && <Badge variant="outline">Layout: {builder.layoutName}</Badge>}
        <Badge variant="secondary">{items.length} built</Badge>
        {builder && !builder.canResync && (
          <Badge variant="outline" title="The server's module has no ResyncClient: objects and helipads cannot be moved or removed without a round restart (Clear restarts the round)">
            Add only
          </Badge>
        )}
        <ResyncStatus state={builder} />
        <Button
          className="ml-auto"
          variant={introDismissed ? 'ghost' : 'secondary'}
          size="icon-sm"
          aria-label="How building works"
          aria-expanded={!introDismissed}
          title="How building works"
          onClick={toggleIntro}
        >
          <CircleHelp />
        </Button>
        <Button
          variant={chatOpen ? 'default' : 'outline'}
          size="sm"
          aria-expanded={chatOpen}
          title="Show the server's chat beside the 3D view (under it on a narrow screen), to read and write while building"
          onClick={toggleChat}
        >
          <MessageSquare />
          Chat
          {unread > 0 && <Badge variant="secondary">{unread}</Badge>}
        </Button>
      </div>

      {/* Desktop: the 3D view, the chat and the panels side by side. Below lg one column: the view, the chat, then the
          selection and Apply, with the panels in a bottom sheet */}
      <div className="flex flex-col gap-3 lg:min-h-0 lg:flex-1 lg:flex-row">
        <div className="relative h-[min(68svh,40rem)] min-h-[320px] min-w-0 overflow-hidden rounded-lg border bg-black lg:h-auto lg:min-h-0 lg:flex-1">
          <div ref={containerRef} className="absolute inset-0" />
          {!scene && <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">Loading the 3D view…</div>}
          {!introDismissed && <BuilderIntro onDismiss={toggleIntro} />}
          <div className="pointer-events-none absolute top-2 left-2 hidden max-w-[calc(100%-14rem)] rounded bg-black/60 px-2 py-1 text-xs text-white/80 lg:block">
            Hold right: look, WASD fly, Q/E down/up · Space/Ctrl up/down · arrows fly · Shift faster · Drag from the list onto the map, or click it to put
            it mid-view · Click: select · W move, E rotate (Ctrl snaps) · End to ground · F focus · Del remove
          </div>
          {selectedItem && (
            <div className="absolute top-2 right-2 flex gap-1">
              <Button size="icon-sm" variant={tool === 'translate' ? 'default' : 'secondary'} title="Move (W)" onClick={() => setTool('translate')}>
                <Move3d />
              </Button>
              <Button size="icon-sm" variant={tool === 'rotate' ? 'default' : 'secondary'} title="Rotate (E)" onClick={() => setTool('rotate')}>
                <RotateCw />
              </Button>
              <Button size="sm" variant={localAxes ? 'default' : 'secondary'} title="The gizmo's axes: the object's own, or the world's" onClick={() => setLocalAxes(!localAxes)}>
                {localAxes ? 'Local' : 'World'}
              </Button>
              <Button size="sm" variant={snap ? 'default' : 'secondary'} title="Snap moves to 1 m and turns to 15°" onClick={() => setSnap(!snap)}>
                Snap
              </Button>
            </div>
          )}
        </div>

        {chatOpen && (
          // Below md the server's tab bar is fixed to the screen's bottom: scrolled into view, the chat's message box ends
          // above it (the bar's 3.5rem, the safe area and a gap)
          <Card
            ref={chatRef}
            className="h-96 shrink-0 scroll-mt-4 scroll-mb-[calc(4.25rem+env(safe-area-inset-bottom))] gap-2 overflow-hidden p-3 md:scroll-mb-4 lg:h-auto lg:w-80"
          >
            <div className="text-sm font-medium">Chat</div>
            <ChatPanel
              chatLog={chatLog}
              players={snapshot.data?.players ?? []}
              teams={teams}
              commands={commands.data ?? []}
              onSend={actions.sendChat}
              listClassName="min-h-0 flex-1"
            />
          </Card>
        )}

        <aside className="flex min-w-0 flex-col gap-3 lg:w-80 lg:shrink-0 lg:overflow-hidden">
          {wide ? (
            <Card className="gap-2 p-3 lg:min-h-0 lg:flex-1">{panels}</Card>
          ) : (
            <>
              <Button variant="outline" className="w-full justify-start" onClick={() => setPanelsOpen(true)}>
                <PanelBottomOpen data-icon="inline-start" />
                Objects, spawns and layouts
                <span className="ml-auto text-xs font-normal text-muted-foreground tabular-nums">{items.length} built</span>
              </Button>
              <Sheet open={panelsOpen} onOpenChange={setPanelsOpen}>
                <SheetContent side="bottom" className="gap-0 pb-[env(safe-area-inset-bottom)] data-[side=bottom]:h-[85svh]">
                  <SheetHeader className="pb-1">
                    <SheetTitle>Build on {map || 'the map'}</SheetTitle>
                    <SheetDescription>What you place is a draft until you apply it.</SheetDescription>
                  </SheetHeader>
                  <div className="flex min-h-0 flex-1 flex-col px-4 pb-4">{panels}</div>
                </SheetContent>
              </Sheet>
            </>
          )}

          {selectedItem && (
            <SelectionPanel
              item={selectedItem}
              isNew={selectedDraft?.builtId === null}
              changed={!!selectedDraft}
              onChange={(position, rotation) => {
                if (selected!.startsWith('n')) setDrafts((c) => { const d = c.get(selected!); return d ? new Map(c).set(selected!, { ...d, position, rotation }) : c })
                else changeItem(selected!, { position, rotation })
              }}
              onGround={() => toGround(selected!)}
              onFocus={() => scene?.focus(selected!)}
              onDuplicate={() => duplicate(selected!)}
              onRemove={() => removeItem(selected!)}
              onRevert={builtOf(selected!) ? () => revertItem(selected!) : undefined}
            />
          )}

          <Card className="gap-2 p-3">
            <div className="flex items-center justify-between text-sm">
              <span className="font-medium">{changes.length === 0 ? 'No changes' : `${changes.length} change${changes.length === 1 ? '' : 's'}`}</span>
              {changes.length > 0 && (
                <span className="text-xs text-muted-foreground">
                  {changes.filter((d) => d.builtId === null).length} new · {changes.filter((d) => d.builtId !== null && !d.remove).length} moved · {removals.length} removed
                </span>
              )}
            </div>
            {needsResync && builder && !builder.canResync && (
              <p className="text-xs text-destructive">This server cannot move or remove objects without a round restart. Discard those changes, or use Clear.</p>
            )}
            {needsResync && builder?.canResync && (
              <p className="text-xs text-muted-foreground">Moving or removing makes every player's game reload the built objects: a short hitch, held back while a player is dead or has a TV missile in the air.</p>
            )}
            <div className="flex gap-2">
              <Button className="flex-1" disabled={changes.length === 0 || applying || !builder?.connected || (needsResync && !builder?.canResync)} onClick={() => void apply()}>
                {applying && <Loader2 className="animate-spin" />}
                {changes.length === 0 ? 'Apply' : `Apply ${changes.length} change${changes.length === 1 ? '' : 's'}`}
              </Button>
              <Button variant="outline" disabled={changes.length === 0 || applying} onClick={() => { setDrafts(new Map()); setSelected(null) }}>
                <Undo2 />
                Discard
              </Button>
              {/* Clearing everything is rare and cannot be undone: out of the way, behind a confirmation that names the count */}
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button variant="outline" size="icon" aria-label="More builder actions" title="More builder actions" />}>
                  <Ellipsis />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-auto min-w-48">
                  <DropdownMenuItem variant="destructive" disabled={items.length === 0} onClick={confirmClear}>
                    <Trash2 />
                    Clear all built ({items.length})…
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </Card>
        </aside>
      </div>
      {confirmDialog}
    </div>
  )
}

const chatOpenKey = 'builder.chatOpen'
const introKey = 'builder.introDismissed'

/** A yes/no remembered in this browser, such as whether the chat was open the last time */
function readFlag(key: string) {
  try {
    return localStorage.getItem(key) === '1'
  } catch {
    return false
  }
}

function writeFlag(key: string, value: boolean) {
  try {
    localStorage.setItem(key, value ? '1' : '0')
  } catch {
    // Not remembered, that is all
  }
}

const wideQuery = '(min-width: 64rem)'
const subscribeWide = (onChange: () => void) => {
  const query = window.matchMedia(wideQuery)
  query.addEventListener('change', onChange)
  return () => query.removeEventListener('change', onChange)
}

/** Whether the page has the desktop layout (lg): the panels beside the view, not in a sheet */
function useWide() {
  return useSyncExternalStore(subscribeWide, () => window.matchMedia(wideQuery).matches)
}

/**
 * The first visit's three steps: what a draft is, what Apply does and what a layout is for. A card in the 3D view's
 * bottom corner, over the map rather than above it, so the view keeps its height. Closing it hides it for good in this
 * browser; the "?" in the toolbar shows it again. Kept above the screenshot tool's WebGL cover (data-ui-capture-keep).
 */
function BuilderIntro({ onDismiss }: { onDismiss: () => void }) {
  return (
    <section
      data-ui-capture-keep
      aria-label="How building works"
      className="absolute inset-x-2 bottom-2 rounded-lg border bg-background/95 p-3 pr-10 text-sm shadow-lg backdrop-blur sm:right-auto sm:max-w-sm"
    >
      <p className="mb-1.5 font-medium">How building works</p>
      <ol className="space-y-1 text-muted-foreground">
        <li>
          <span className="font-medium text-foreground">1. Place.</span> Drag an object onto the map, or click it to put it mid-view. It is a draft
          that only you see.
        </li>
        <li>
          <span className="font-medium text-foreground">2. Apply.</span> Apply builds your drafts in the game, for every player on the server.
        </li>
        <li>
          <span className="font-medium text-foreground">3. Save.</span> Save what is built as a layout under Layouts, to load it again on this map.
        </li>
      </ol>
      <Button variant="ghost" size="icon-sm" className="absolute top-2 right-2" aria-label="Hide how building works" title="Got it, hide this (? shows it again)" onClick={onDismiss}>
        <X />
      </Button>
    </section>
  )
}

// -------------------------------------------------------------------------------------------------------------------

/** What a player's reload waits for (PendingResyncDto.blocker); an unknown reason as the server wrote it */
const resyncWait = (blocker: string | null) =>
  blocker === null ? 'next in line' : blocker === 'dead' ? 'waits until respawned' : blocker === 'missile' ? 'waits for a TV missile to land' : blocker

/**
 * Moving or removing a built object makes every player's game drop and load the built objects again. The chip says
 * that it is going on, and its tooltip why and for whom.
 */
function ResyncStatus({ state }: { state: BuilderState | undefined }) {
  if (!state || state.pendingResync.length === 0) return null
  const count = state.pendingResync.length
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Badge variant="outline" tabIndex={0} className="cursor-help focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none" />
        }
      >
        <Loader2 className="animate-spin" />
        Reloading objects for {count} player{count === 1 ? '' : 's'}
      </TooltipTrigger>
      <TooltipContent side="bottom" align="start" className="block max-w-sm space-y-1.5">
        <p>
          Built objects were moved or removed, so each player's game reloads them: a short hitch. It is held back while a player is dead or has a TV
          missile in the air.
        </p>
        <ul className="list-disc pl-4">
          {state.pendingResync.map((p) => (
            <li key={p.index}>
              {p.name || `Player ${p.index}`}: {resyncWait(p.blocker)}
            </li>
          ))}
        </ul>
      </TooltipContent>
    </Tooltip>
  )
}

const allCategories = 'all'
const categoryItemLabel = (key: string) => (key === allCategories ? 'All categories' : categoryLabel(key))

/** The object templates this map's level uses, to drag onto the map or click to put in the middle of the view */
function Palette({
  catalog,
  levelCatalog,
  recent,
  checking,
  error,
  onRetry,
  onPress,
  onPlaceAhead,
}: {
  catalog: CatalogEntry[] | null
  /** The catalog's templates the level uses, null until the server has said */
  levelCatalog: CatalogEntry[] | null
  /** The templates placed last in this browser, newest first */
  recent: string[]
  checking: boolean
  error: string | null
  onRetry: () => void
  onPress: (template: string, e: React.PointerEvent) => void
  onPlaceAhead: (template: string) => void
}) {
  const [filter, setFilter] = useState('')
  const [bySize, setBySize] = useState(true)
  const [category, setCategory] = useState('')
  const [showGround, setShowGround] = useState(false)
  // The categories with their counts, the biggest first, after All
  const categoryItems = useMemo(() => {
    const counts = new Map<string, number>()
    for (const c of levelCatalog ?? []) counts.set(categoryKey(c.category), (counts.get(categoryKey(c.category)) ?? 0) + 1)
    const sorted = [...counts.entries()].sort(([a, x], [b, y]) => y - x || a.localeCompare(b))
    return [
      { value: allCategories, label: `All categories (${levelCatalog?.length ?? 0})`, count: levelCatalog?.length ?? 0 },
      ...sorted.map(([key, count]) => ({ value: key, label: `${categoryLabel(key)} (${count})`, count })),
    ]
  }, [levelCatalog])
  const { shown, hidden } = useMemo(() => {
    const words = filter.toLowerCase().split(/\s+/).filter(Boolean)
    const matching = (levelCatalog ?? []).filter(
      (c) => (!category || categoryKey(c.category) === category) && words.every((w) => c.name.includes(w) || c.category.toLowerCase().includes(w)),
    )
    // A chosen category shows its objects, roads and water too
    const visible = showGround || category ? matching : matching.filter((c) => !isGroundOrWater(c))
    return { shown: visible.sort((a, b) => (bySize ? b.size - a.size : a.name.localeCompare(b.name))), hidden: matching.length - visible.length }
  }, [levelCatalog, filter, bySize, category, showGround])
  const groundCount = useMemo(() => (levelCatalog ?? []).filter(isGroundOrWater).length, [levelCatalog])
  const recentEntries = useMemo(
    () => recent.map((name) => levelCatalog?.find((c) => c.name === name)).filter((c): c is CatalogEntry => !!c),
    [recent, levelCatalog],
  )

  if (catalog === null) return <p className="text-xs text-muted-foreground">Loading the objects…</p>
  if (catalog.length === 0)
    return <p className="text-xs text-muted-foreground">No object catalog: build it with tools/builder-assets. Objects can still be placed by template name in game with .o.</p>
  if (!levelCatalog)
    return error ? (
      <div className="space-y-2 text-xs">
        <p className="text-destructive">{error}</p>
        <Button size="xs" variant="outline" onClick={onRetry}>
          Try again
        </Button>
      </div>
    ) : (
      <p className="flex items-center gap-2 text-xs text-muted-foreground">
        {checking && <Loader2 className="size-3 animate-spin" />}
        Asking the game server which objects this map has…
      </p>
    )

  const row = (c: CatalogEntry) => <PaletteRow key={c.name} entry={c} onPress={(e) => onPress(c.name, e)} onPlaceAhead={() => onPlaceAhead(c.name)} />
  const browsing = !filter.trim() && !category

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2">
      <Input placeholder="Search objects" aria-label="Search objects" value={filter} onChange={(e) => setFilter(e.target.value)} />
      <p className="text-xs text-muted-foreground" title="Templates the level does not use can crash the game">
        The {levelCatalog.length} objects this map's level uses. Drag one onto the map, or click it to put it mid-view.
      </p>
      <Select items={categoryItems} value={category || allCategories} onValueChange={(value) => setCategory(value === null || value === allCategories ? '' : value)}>
        <SelectTrigger className="w-full" aria-label="Category">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {categoryItems.map((item) => (
            <SelectItem key={item.value} value={item.value}>
              <span className="flex-1">{categoryItemLabel(item.value)}</span>
              <span className="text-muted-foreground tabular-nums">{item.count}</span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-xs">
        <div role="group" aria-labelledby="palette-sort" className="flex items-center gap-1">
          <span id="palette-sort" className="mr-0.5 text-muted-foreground">
            Sort:
          </span>
          <Button size="xs" variant={bySize ? 'default' : 'outline'} aria-pressed={bySize} onClick={() => setBySize(true)}>
            Size
          </Button>
          <Button size="xs" variant={!bySize ? 'default' : 'outline'} aria-pressed={!bySize} onClick={() => setBySize(false)}>
            Name
          </Button>
        </div>
        {groundCount > 0 && (
          <div className="flex items-center gap-2" title="Roads, airstrips and water planes: flat pieces that lie on the terrain">
            <Checkbox id="palette-ground" checked={showGround} onCheckedChange={setShowGround} />
            <Label htmlFor="palette-ground" className="text-xs font-normal text-muted-foreground">
              Show roads and water ({groundCount})
            </Label>
          </div>
        )}
      </div>
      <div className="min-h-0 flex-1 space-y-0.5 overflow-y-auto">
        {browsing && recentEntries.length > 0 && (
          <>
            <h3 className="px-1 pt-1 text-xs font-medium text-muted-foreground">Recently used</h3>
            {recentEntries.map(row)}
            <h3 className="px-1 pt-2 text-xs font-medium text-muted-foreground">All objects</h3>
          </>
        )}
        {shown.map(row)}
        {shown.length === 0 && hidden === 0 && <p className="px-1 py-2 text-xs text-muted-foreground">No object matches</p>}
        {hidden > 0 && (
          <p className="px-1 py-2 text-xs text-muted-foreground">
            {hidden} {hidden === 1 ? 'road or water object matches' : 'road and water objects match'} as well.{' '}
            <button type="button" className="text-foreground underline-offset-4 hover:underline" onClick={() => setShowGround(true)}>
              Show them
            </button>
          </p>
        )}
      </div>
    </div>
  )
}

function PaletteRow({ entry, onPress, onPlaceAhead }: { entry: CatalogEntry; onPress: (e: React.PointerEvent) => void; onPlaceAhead: () => void }) {
  const ref = useRef<HTMLButtonElement>(null)
  const [thumb, setThumb] = useState<string | null>(null)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    // Rendered when the row scrolls into view
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return
      observer.disconnect()
      void templateThumbnail(entry.name).then(setThumb)
    })
    observer.observe(el)
    return () => observer.disconnect()
  }, [entry.name])
  const dims = [entry.max[0] - entry.min[0], entry.max[2] - entry.min[2], entry.max[1] - entry.min[1]].map((v) => Math.round(v))
  return (
    <button
      ref={ref}
      type="button"
      title={`${entry.name}\n${entry.category}\n${dims.join(' × ')} m\nDrag onto the map, or click to put it in the middle of the 3D view`}
      className="flex w-full cursor-grab touch-none items-center gap-2 rounded px-1 py-0.5 text-left text-sm select-none hover:bg-muted"
      onPointerDown={onPress}
      // Enter or Space on the focused row (a mouse click is handled by the press)
      onClick={(e) => e.detail === 0 && onPlaceAhead()}
    >
      <div className="size-12 shrink-0 rounded bg-muted/60">{thumb && <img src={thumb} alt="" className="size-12" />}</div>
      <div className="min-w-0">
        <div className="truncate">{entry.name}</div>
        <div className="truncate text-xs text-muted-foreground">
          {dims.join('×')} m · {entry.category}
        </div>
      </div>
    </button>
  )
}

function SpawnsTab({
  teamName,
  onPress,
  sceneItems,
  onSelect,
}: {
  teamName: (id: number) => string
  onPress: (spec: PlaceSpec, e: React.PointerEvent) => void
  sceneItems: SceneItem[]
  onSelect: (key: string) => void
}) {
  const [helis, setHelis] = useState<Record<number, string>>({ 1: 'ahe_z10', 2: 'ahe_ah1z' })
  return (
    <div className="space-y-3 text-sm">
      <p className="text-xs text-muted-foreground">
        A helipad is the concrete pad with repair and rearm (for the team of the nearest control point), a helicopter that respawns on it, and a spawn point
        that puts the player straight into that helicopter.
      </p>
      <p className="text-xs text-muted-foreground">
        Spawn points are optional extra places to spawn on foot. Each shows on the spawn screen as a place of its own, and a player who picks it spawns there.
      </p>
      <p className="text-xs text-muted-foreground">
        Players can pick a new spawn point or helipad at once. With an older server module they can only from the next round restart (a new spawn group gets its
        team when a round starts; <code>!admin.restartMap</code> in the chat does it); those are marked "next round".
      </p>
      <p className="text-xs text-muted-foreground">Drag a button onto the map, or click it to put one in the middle of the 3D view.</p>
      {[1, 2].map((team) => {
        const points = sceneItems.filter((i) => i.kind === 'SpawnPoint' && i.team === team && !i.removed)
        const pads = sceneItems.filter((i) => i.kind === 'Helipad' && i.team === team && !i.removed)
        return (
          <div key={team} className="space-y-2 rounded border p-2">
            <div className="flex items-center gap-2 font-medium">
              <span className="size-2 rounded-full" style={{ background: teamColor(team) }} />
              {teamName(team)} (team {team})
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" className="flex-1 cursor-grab touch-none" onPointerDown={(e) => onPress({ kind: 'SpawnPoint', template: 'spawnpoint', team }, e)}>
                Spawn point
              </Button>
              <Button size="sm" variant="outline" className="flex-1 cursor-grab touch-none" onPointerDown={(e) => onPress({ kind: 'Helipad', template: helis[team], team }, e)}>
                Helipad
              </Button>
            </div>
            <label className="flex items-center gap-2 text-xs text-muted-foreground">
              Helicopter
              <Input className="h-7" value={helis[team]} onChange={(e) => setHelis({ ...helis, [team]: e.target.value.trim() })} />
            </label>
            <div className="flex flex-wrap gap-1">
              {[...points, ...pads].map((i) => (
                <Button key={i.key} size="xs" variant={i.draft ? 'secondary' : 'ghost'} onClick={() => onSelect(i.key)}>
                  {i.kind === 'SpawnPoint' ? 'Spawn' : 'Helipad'} {Math.round(i.position[0])},{Math.round(i.position[2])}
                </Button>
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}

function BuiltList({ items, selected, onSelect }: { items: SceneItem[]; selected: string | null; onSelect: (key: string) => void }) {
  if (items.length === 0) return <p className="text-xs text-muted-foreground">Nothing built yet. Drag an object, a spawn point or a helipad onto the map.</p>
  return (
    <div className="space-y-0.5">
      {items.map((i) => (
        <button
          key={i.key}
          type="button"
          className={cn('flex w-full items-center gap-2 rounded px-2 py-1 text-left text-sm hover:bg-muted', selected === i.key && 'bg-muted')}
          onClick={() => onSelect(i.key)}
        >
          {i.kind !== 'Object' && <span className="size-2 shrink-0 rounded-full" style={{ background: teamColor(i.team) }} />}
          {i.kind !== 'Object' && !i.spawnReady && !i.removed && (
            <span className="shrink-0 text-xs text-amber-500" title="Players can pick it on the spawn screen from the next round restart">
              next round
            </span>
          )}
          <span className={cn('truncate', i.removed && 'line-through opacity-60')}>{i.kind === 'Object' ? i.template : i.kind === 'SpawnPoint' ? 'Spawn point' : `Helipad (${i.template})`}</span>
          <span className="ml-auto shrink-0 text-xs text-muted-foreground">
            {i.removed ? 'removed' : i.draft ? (i.key.startsWith('n') ? 'new' : 'moved') : i.missing ? 'not in game' : ''}
          </span>
        </button>
      ))}
    </div>
  )
}

function SelectionPanel({
  item,
  isNew,
  changed,
  onChange,
  onGround,
  onFocus,
  onDuplicate,
  onRemove,
  onRevert,
}: {
  item: SceneItem
  isNew: boolean
  changed: boolean
  onChange: (position: Vec, rotation: Vec) => void
  onGround: () => void
  onFocus: () => void
  onDuplicate: () => void
  onRemove: () => void
  onRevert?: () => void
}) {
  const yawOnly = item.kind !== 'Object'
  const field = (label: string, value: number, set: (v: number) => void) => (
    <label className="flex items-center gap-1 text-xs text-muted-foreground">
      {label}
      <NumberInput value={value} onChange={set} />
    </label>
  )
  const setPos = (i: number) => (v: number) => onChange(item.position.map((p, j) => (j === i ? v : p)), item.rotation)
  const setRot = (i: number) => (v: number) => onChange(item.position, item.rotation.map((r, j) => (j === i ? normalizeDegrees(v) : r)))
  return (
    <Card className="gap-2 p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="min-w-0 truncate text-sm font-medium">
          {item.kind === 'Object' ? item.template : item.kind === 'SpawnPoint' ? `Spawn point, team ${item.team}` : `Helipad, team ${item.team}`}
        </div>
        <span className="shrink-0 text-xs text-muted-foreground">{item.removed ? 'removed' : isNew ? 'new' : changed ? 'moved' : item.missing ? 'not in game' : 'built'}</span>
      </div>
      {item.kind !== 'Object' && !item.spawnReady && !item.removed && (
        <p className="text-xs text-amber-500">
          Players can pick it on the spawn screen from the next round restart (a new spawn group gets its team when a round starts; <code>!admin.restartMap</code>{' '}
          in the chat does it).
        </p>
      )}
      {!item.removed && (
        <>
          <div className="grid grid-cols-3 gap-1">
            {field('x', item.position[0], setPos(0))}
            {field('h', item.position[1], setPos(1))}
            {field('z', item.position[2], setPos(2))}
          </div>
          <div className="grid grid-cols-3 gap-1">
            {field('yaw', item.rotation[0], setRot(0))}
            {!yawOnly && field('pitch', item.rotation[1], setRot(1))}
            {!yawOnly && field('roll', item.rotation[2], setRot(2))}
          </div>
        </>
      )}
      <div className="flex flex-wrap gap-1">
        {!item.removed && (
          <>
            <Button size="xs" variant="outline" title="Stand it on the ground or object under it (End)" onClick={onGround}>
              To ground
            </Button>
            <Button size="xs" variant="outline" title="Fly to it (F)" onClick={onFocus}>
              <Crosshair />
              Focus
            </Button>
            <Button size="xs" variant="outline" onClick={onDuplicate}>
              Duplicate
            </Button>
            <Button size="xs" variant="destructive" title="Remove (Del)" onClick={onRemove}>
              Remove
            </Button>
          </>
        )}
        {onRevert && changed && (
          <Button size="xs" variant="outline" onClick={onRevert}>
            <Undo2 />
            Undo change
          </Button>
        )}
      </div>
    </Card>
  )
}

/** A number field that only reports a whole, valid number when it loses the focus or Enter is pressed */
function NumberInput({ value, onChange }: { value: number; onChange: (v: number) => void }) {
  const [text, setText] = useState(String(value))
  useEffect(() => setText(String(value)), [value])
  const commit = () => {
    const v = Number(text)
    if (Number.isFinite(v) && v !== value) onChange(v)
    else setText(String(value))
  }
  return (
    <Input
      className="h-7 px-1 text-xs"
      value={text}
      onChange={(e) => setText(e.target.value)}
      onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === 'Enter') commit()
      }}
    />
  )
}

function LayoutsTab({ serverId, state, hasDrafts, onLoaded }: { serverId: string; state: BuilderState | undefined; hasDrafts: boolean; onLoaded: () => void }) {
  const queryClient = useQueryClient()
  const layouts = useQuery(layoutsQuery(serverId))
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const refresh = () => void queryClient.invalidateQueries({ queryKey: layoutsQuery(serverId).queryKey })

  useEffect(() => {
    if (state?.layoutName) setName((current) => current || state.layoutName!)
  }, [state?.layoutName])

  const save = async (overwrite: boolean) => {
    setBusy(true)
    try {
      await fetchClient.POST('/api/servers/{serverId}/builder/layouts', { params: { path: { serverId } }, body: { name: name.trim(), overwrite } })
      toast.success(`Saved '${name.trim()}' (${state?.items.length ?? 0} items)`)
      refresh()
    } catch (error) {
      if (error instanceof ApiError && error.status === 409 && !overwrite) {
        if (window.confirm(`${error.message}\n\nSave over it?`)) await save(true)
      } else toast.error(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  const load = async (layout: Layout) => {
    setBusy(true)
    try {
      const { data } = await fetchClient.POST('/api/servers/{serverId}/builder/layouts/{layoutId}/load', { params: { path: { serverId, layoutId: layout.id } } })
      if (data) {
        queryClient.setQueryData(builderQuery(serverId).queryKey, data.state)
        for (const error of data.errors) toast.error(error)
        toast.success(`Loaded '${layout.name}' (${data.added.length} items)`)
      }
      onLoaded()
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  const remove = async (layout: Layout) => {
    try {
      await fetchClient.DELETE('/api/servers/{serverId}/builder/layouts/{layoutId}', { params: { path: { serverId, layoutId: layout.id } } })
      refresh()
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <div className="space-y-3 text-sm">
      <div className="space-y-1">
        <div className="text-xs text-muted-foreground">Save what is built (applied changes only) as a layout of {state?.map ?? 'this map'}</div>
        <div className="flex gap-2">
          <Input placeholder="Layout name" maxLength={50} value={name} onChange={(e) => setName(e.target.value)} />
          <Button disabled={busy || !name.trim()} onClick={() => void save(false)}>
            Save
          </Button>
        </div>
        {hasDrafts && <p className="text-xs text-muted-foreground">Unapplied changes are not saved.</p>}
      </div>
      <div className="space-y-1">
        {layouts.isError && <p className="text-xs text-destructive">{errorMessage(layouts.error)}</p>}
        {layouts.data?.length === 0 && <p className="text-xs text-muted-foreground">No layouts for this map yet.</p>}
        {layouts.data?.map((l) => (
          <div key={l.id} className={cn('flex items-center gap-2 rounded border px-2 py-1', state?.layoutId === l.id && 'border-primary')}>
            <div className="min-w-0 flex-1">
              <div className="truncate font-medium">{l.name}</div>
              <div className="truncate text-xs text-muted-foreground">
                {l.createdBy} · {new Date(l.editedDate).toLocaleDateString()}
              </div>
            </div>
            <Button
              size="xs"
              variant="outline"
              disabled={busy || !state?.connected}
              onClick={() => {
                if ((state?.items.length || hasDrafts) && !window.confirm(`Replace what is built${hasDrafts ? ' and your unapplied changes' : ''} with '${l.name}'?`)) return
                void load(l)
              }}
            >
              Load
            </Button>
            <Button
              size="icon-xs"
              variant="ghost"
              title="Delete the layout"
              onClick={() => {
                if (window.confirm(`Delete the layout '${l.name}'?`)) void remove(l)
              }}
            >
              <Trash2 />
            </Button>
          </div>
        ))}
      </div>
    </div>
  )
}
