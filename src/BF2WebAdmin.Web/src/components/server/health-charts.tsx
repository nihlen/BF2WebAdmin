import { useEffect, useId, useRef, useState } from 'react'
import type { ServerHealthStatus } from '@/api/client'
import { nearestIndex } from '@/lib/nearest-index'
import { cn } from '@/lib/utils'

// The plot's margins inside each chart, the same for all so that their times line up. The top and right ones leave
// room for half of the top value label and of a time label at the right edge, which are centred on their place.
const left = 44
const right = 18
const top = 8
const plotHeight = 96
const axisHeight = 18

/** A window or connections arrive every 10 s; a longer gap (an offline server) breaks the lines. */
const gapMs = 25_000

/** Each status's fill in the status strip (the panel's badges use the bg- classes of the same colours). */
const statusFills: Record<ServerHealthStatus, string> = {
  Good: 'fill-emerald-500',
  Warn: 'fill-amber-500',
  Bad: 'fill-red-500',
  Loading: 'fill-sky-500',
  Stepped: 'fill-muted-foreground/40',
}

/** A line of a chart: one value per time of the chart (null where it has none), its colour as stroke and bg classes. */
export type Series = {
  name: string
  stroke: string
  dot: string
  values: (number | null)[]
}

/** A 2v2 round, shaded behind the lines. Rounds of one match have the same shade, the next match the other. */
export type Band = { key: string; from: number; to: number; label: string; alternate: boolean }

/** The part of the history the charts show, in epoch milliseconds. */
export type TimeRange = { from: number; to: number }

/** The width of an element, kept current. */
function useWidth() {
  const ref = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)
  useEffect(() => {
    const element = ref.current
    if (!element) return
    const observer = new ResizeObserver(() => setWidth(element.clientWidth))
    observer.observe(element)
    setWidth(element.clientWidth)
    return () => observer.disconnect()
  }, [])
  return [ref, width] as const
}


const clockShort = (time: number) => new Date(time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })

/** The times to label on the time axis: every 10 minutes for the last hour, every hour for longer. */
function timeTicks({ from, to }: TimeRange) {
  const step = to - from <= 90 * 60_000 ? 10 * 60_000 : 60 * 60_000
  const ticks: number[] = []
  for (let t = Math.ceil(from / step) * step; t <= to; t += step) ticks.push(t)
  return ticks
}

/** Three round values for the value axis: 0, the middle and the top. */
function valueTicks(max: number) {
  return [0, max / 2, max]
}

type ChartProps = {
  title: string
  /** How a value is written in the legend and the axis */
  format: (value: number) => string
  times: number[]
  series: Series[]
  range: TimeRange
  hover: number | null
  onHover: (time: number | null) => void
  /** The value axis goes at least this high, so that a calm chart is not all noise */
  minMax: number
  /** The value axis goes at most this high: values above it are drawn at the top with a mark, the legend has them */
  cap?: number
  /** Dashed lines at limits: the late tick, the tick's time */
  limits?: { value: number; label: string }[]
  bands?: Band[]
  /** Shown instead of the plot when there are no times */
  empty?: string
}

/**
 * A time chart of a few lines: the health windows' figures over the part of the history the panel shows. The legend
 * has the values under the pointer, or the latest; the pointer's time is shared by every chart of the panel.
 */
export function TimeChart({ title, format, times, series, range, hover, onHover, minMax, cap, limits = [], bands = [], empty }: ChartProps) {
  const [ref, width] = useWidth()
  // useId's characters are not all valid in url(#...)
  const clipId = `chart${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`
  const plotWidth = Math.max(width - left - right, 1)
  const x = (t: number) => left + ((t - range.from) / Math.max(range.to - range.from, 1)) * plotWidth

  let dataMax = 0
  for (const s of series) for (const v of s.values) if (v !== null && v > dataMax) dataMax = v
  const max = niceMax(Math.min(Math.max(dataMax, minMax, ...limits.map((l) => l.value * 1.1)), cap ?? Infinity))
  const y = (v: number) => plotHeight - (Math.min(v, max) / max) * plotHeight

  const index = hover !== null ? nearestIndex(times, hover) : times.length - 1
  const shown = index >= 0 && (hover === null || Math.abs(times[index] - hover) <= gapMs) ? index : -1

  const onPointer = (event: React.PointerEvent<SVGSVGElement>) => {
    const box = event.currentTarget.getBoundingClientRect()
    const px = event.clientX - box.left
    if (px < left || px > left + plotWidth) return onHover(null)
    onHover(range.from + ((px - left) / plotWidth) * (range.to - range.from))
  }

  return (
    <div className="min-w-0 space-y-1">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-sm">
        <span className="font-medium">{title}</span>
        {series.map((s) => {
          const value = shown >= 0 ? s.values[shown] : null
          return (
            <span key={s.name} className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className={cn('inline-block h-0.5 w-3 shrink-0 rounded', s.dot)} />
              {s.name}
              <span className="font-mono text-foreground tabular-nums">{value === null ? '–' : format(value)}</span>
            </span>
          )
        })}
      </div>
      <div ref={ref} className="w-full">
        {times.length === 0 && empty ? (
          <div className="flex items-center justify-center rounded-md border border-dashed text-xs text-muted-foreground" style={{ height: top + plotHeight + axisHeight }}>
            {empty}
          </div>
        ) : (
          width > 0 && (
            <svg
              width={width}
              height={top + plotHeight + axisHeight}
              className="block touch-none select-none"
              onPointerMove={onPointer}
              onPointerDown={onPointer}
              onPointerLeave={() => onHover(null)}
              role="img"
              aria-label={`${title} over time`}
            >
              <g transform={`translate(0,${top})`}>
                {bands.map((b) => {
                  const x1 = Math.max(x(b.from), left)
                  const x2 = Math.min(x(b.to), left + plotWidth)
                  if (x2 <= x1) return null
                  return (
                    <g key={b.key}>
                      <rect x={x1} y={0} width={Math.max(x2 - x1, 1)} height={plotHeight} className={b.alternate ? 'fill-violet-500/10' : 'fill-sky-500/10'} />
                      {x2 - x1 > 18 && (
                        <text x={x1 + 2} y={9} className="fill-muted-foreground text-[9px]">
                          {b.label}
                        </text>
                      )}
                    </g>
                  )
                })}

                {valueTicks(max).map((v) => (
                  <g key={v}>
                    <line x1={left} x2={left + plotWidth} y1={y(v)} y2={y(v)} className="stroke-border" strokeWidth={1} />
                    <text x={left - 4} y={y(v)} dy="0.32em" textAnchor="end" className="fill-muted-foreground text-[10px] tabular-nums">
                      {format(v)}
                    </text>
                  </g>
                ))}

                {limits
                  .filter((l) => l.value <= max)
                  .map((l) => (
                    <g key={l.label}>
                      <line x1={left} x2={left + plotWidth} y1={y(l.value)} y2={y(l.value)} className="stroke-red-500/60" strokeDasharray="4 3" strokeWidth={1} />
                      <text x={left + plotWidth - 2} y={y(l.value) - 3} textAnchor="end" className="fill-red-500/80 text-[9px]">
                        {l.label}
                      </text>
                    </g>
                  ))}

                {timeTicks(range).map((t) => (
                  <text key={t} x={x(t)} y={plotHeight + 13} textAnchor="middle" className="fill-muted-foreground text-[10px] tabular-nums">
                    {clockShort(t)}
                  </text>
                ))}

                <clipPath id={clipId}>
                  <rect x={left} y={0} width={plotWidth} height={plotHeight} />
                </clipPath>
                {series.map((s) => (
                  <g key={s.name} clipPath={`url(#${clipId})`}>
                    <path d={linePath(times, s.values, x, y)} fill="none" className={s.stroke} strokeWidth={1.5} strokeLinejoin="round" />
                    {/* Values above the cap: a mark at the top, the legend has the value */}
                    {s.values.map((v, i) =>
                      v !== null && v > max ? (
                        <path key={i} d={`M${x(times[i]) - 3},6 L${x(times[i]) + 3},6 L${x(times[i])},1 Z`} fill="none" className={s.stroke} strokeWidth={1.5} />
                      ) : null,
                    )}
                  </g>
                ))}

                {shown >= 0 && hover !== null && (
                  <line x1={x(times[shown])} x2={x(times[shown])} y1={0} y2={plotHeight} className="stroke-foreground/50" strokeWidth={1} />
                )}
              </g>
            </svg>
          )
        )}
      </div>
    </div>
  )
}

/** The windows' statuses as a strip of cells on the charts' time axis, with the pointer's line. */
export function StatusStrip({
  times,
  statuses,
  range,
  hover,
  onHover,
}: {
  times: number[]
  statuses: ServerHealthStatus[]
  range: TimeRange
  hover: number | null
  onHover: (time: number | null) => void
}) {
  const [ref, width] = useWidth()
  const plotWidth = Math.max(width - left - right, 1)
  const x = (t: number) => left + ((t - range.from) / Math.max(range.to - range.from, 1)) * plotWidth
  const height = 14

  const onPointer = (event: React.PointerEvent<SVGSVGElement>) => {
    const box = event.currentTarget.getBoundingClientRect()
    const px = event.clientX - box.left
    if (px < left || px > left + plotWidth) return onHover(null)
    onHover(range.from + ((px - left) / plotWidth) * (range.to - range.from))
  }

  const index = hover !== null ? nearestIndex(times, hover) : -1
  return (
    <div ref={ref} className="w-full">
      {width > 0 && (
        <svg width={width} height={height} className="block touch-none select-none" onPointerMove={onPointer} onPointerDown={onPointer} onPointerLeave={() => onHover(null)}>
          <text x={left - 4} y={height / 2} dy="0.32em" textAnchor="end" className="fill-muted-foreground text-[10px]">
            status
          </text>
          {times.map((t, i) => {
            // A window covers the 10 s before it was received
            const x1 = Math.max(x(t - 10_000), left)
            const x2 = x(t)
            if (x2 <= left) return null
            return <rect key={t} x={x1} y={0} width={Math.max(x2 - x1 - 0.5, 1)} height={height} rx={1} className={statusFills[statuses[i]]} />
          })}
          {index >= 0 && Math.abs(times[index] - hover!) <= gapMs && (
            <rect x={x(times[index] - 10_000)} y={0} width={Math.max(x(times[index]) - x(times[index] - 10_000), 2)} height={height} className="fill-none stroke-foreground" strokeWidth={1.5} />
          )}
        </svg>
      )}
    </div>
  )
}

/** The SVG path of a line through the values, broken where a value is missing or the times have a gap. */
function linePath(times: number[], values: (number | null)[], x: (t: number) => number, y: (v: number) => number) {
  let d = ''
  let open = false
  for (let i = 0; i < times.length; i++) {
    const v = values[i]
    if (v === null || (open && times[i] - times[i - 1] > gapMs)) {
      open = false
      if (v === null) continue
    }
    d += `${open ? 'L' : 'M'}${x(times[i]).toFixed(1)},${y(v).toFixed(1)}`
    open = true
  }
  return d
}

/** A round top for the value axis: 1, 2 or 5 times a power of ten, at or above <value>. */
function niceMax(value: number) {
  if (!(value > 0)) return 1
  const power = 10 ** Math.floor(Math.log10(value))
  for (const step of [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10]) if (step * power >= value) return step * power
  return 10 * power
}
