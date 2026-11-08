import { useEffect, useRef } from 'react'
import type { Player } from '@/api/client'
import type { PlayerPosition } from '@/hooks/use-server-live'
import { teamCanvasColor } from '@/lib/teams'

const minimapUrl = (map: string) => `https://static.nihlen.net/bf2/minimaps/${map.toLowerCase()}.png`
const zoomStep = 0.2
// Positions arrive every 250 ms by default; interpolate over the measured interval
const defaultUpdateIntervalMs = 250

type View = { scale: number; offsetX: number; offsetY: number }

/**
 * The minimap with the live player positions. Drawn on every animation frame from the positions ref, so position
 * updates never re-render React. Drag to pan, scroll to zoom.
 */
export function ServerMap({
  map,
  players,
  positions,
}: {
  map: string | null
  players: Player[]
  positions: React.RefObject<Map<number, PlayerPosition>>
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const view = useRef<View>({ scale: 0.6, offsetX: 0, offsetY: 0 })
  const playersRef = useRef(players)
  const imageRef = useRef<HTMLImageElement | null>(null)

  useEffect(() => {
    playersRef.current = players
  }, [players])

  useEffect(() => {
    imageRef.current = null
    if (!map) return
    const image = new Image()
    image.onload = () => (imageRef.current = image)
    image.src = minimapUrl(map)
  }, [map])

  // Canvas size follows the container
  useEffect(() => {
    const container = containerRef.current
    const canvas = canvasRef.current
    if (!container || !canvas) return
    const observer = new ResizeObserver(() => {
      canvas.width = container.clientWidth
      canvas.height = container.clientHeight
    })
    observer.observe(container)
    return () => observer.disconnect()
  }, [])

  // Zoom with the wheel (a non-passive listener, to keep the page from scrolling)
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const onWheel = (e: WheelEvent) => {
      e.preventDefault()
      const factor = e.deltaY > 0 ? 1 - zoomStep : 1 + zoomStep
      const v = view.current
      view.current = { scale: v.scale * factor, offsetX: v.offsetX * factor, offsetY: v.offsetY * factor }
    }
    canvas.addEventListener('wheel', onWheel, { passive: false })
    return () => canvas.removeEventListener('wheel', onWheel)
  }, [])

  useEffect(() => {
    let frame = 0
    const draw = () => {
      frame = requestAnimationFrame(draw)
      const canvas = canvasRef.current
      const context = canvas?.getContext('2d')
      if (!canvas || !context) return
      drawMap(context, canvas.width, canvas.height, view.current, imageRef.current, playersRef.current, positions.current)
    }
    frame = requestAnimationFrame(draw)
    return () => cancelAnimationFrame(frame)
  }, [positions])

  const drag = useRef<{ x: number; y: number } | null>(null)

  return (
    <div ref={containerRef} className="h-[600px] overflow-hidden rounded-xl border bg-card">
      <canvas
        ref={canvasRef}
        className="cursor-grab active:cursor-grabbing"
        onPointerDown={(e) => {
          e.currentTarget.setPointerCapture(e.pointerId)
          drag.current = { x: e.clientX - view.current.offsetX, y: e.clientY - view.current.offsetY }
        }}
        onPointerMove={(e) => {
          if (!drag.current) return
          view.current = { ...view.current, offsetX: e.clientX - drag.current.x, offsetY: e.clientY - drag.current.y }
        }}
        onPointerUp={() => (drag.current = null)}
      />
    </div>
  )
}

function drawMap(
  context: CanvasRenderingContext2D,
  width: number,
  height: number,
  view: View,
  image: HTMLImageElement | null,
  players: Player[],
  positions: Map<number, PlayerPosition>,
) {
  context.clearRect(0, 0, width, height)
  const centerX = width / 2 + view.offsetX
  const centerY = height / 2 + view.offsetY

  if (image) {
    // Some minimap images are saved at half size
    const imageScale = image.naturalWidth < 1000 ? 2 : 1
    const w = image.naturalWidth * imageScale * view.scale
    const h = image.naturalHeight * imageScale * view.scale
    context.drawImage(image, centerX - w / 2, centerY - h / 2, w, h)
  }

  context.font = '12px Verdana'
  context.textAlign = 'center'
  context.lineWidth = 3
  context.strokeStyle = '#000000'

  const now = performance.now()
  for (const player of players) {
    const position = positions.get(player.index)
    if (!player.isAlive || !position) continue

    const t = Math.min((now - position.receivedAt) / defaultUpdateIntervalMs, 1)
    const x = lerp(position.previous.x, position.current.x, t) * view.scale + centerX
    // Game Y grows north, canvas Y grows down
    const y = -lerp(position.previous.y, position.current.y, t) * view.scale + centerY

    context.fillStyle = teamCanvasColor(player.team)
    context.strokeText(player.name, x, y - 10)
    context.fillText(player.name, x, y - 10)
    context.beginPath()
    context.arc(x, y, 3, 0, 2 * Math.PI)
    context.stroke()
    context.fill()
  }
}

const lerp = (a: number, b: number, t: number) => a + (b - a) * t
