const timeFormat = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' })

/** A time, with the date when it is not today. An unparsable value shows as it is. */
export function formatTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const today = date.toDateString() === new Date().toDateString()
  return (today ? timeFormat : dateTimeFormat).format(date)
}

const relativeFormat = new Intl.RelativeTimeFormat('en', { numeric: 'auto' })
const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600_000],
  ['month', 30 * 24 * 3600_000],
  ['week', 7 * 24 * 3600_000],
  ['day', 24 * 3600_000],
  ['hour', 3600_000],
  ['minute', 60_000],
]

/** A time relative to now in words ("in 3 days", "2 hours ago"), in its largest whole unit. An unparsable value shows as it is. */
export function formatRelative(value: string) {
  const ms = Date.parse(value) - Date.now()
  if (Number.isNaN(ms)) return value
  // Intl says "this minute", which reads oddly in a log
  if (Math.abs(ms) < 60_000) return 'just now'
  const [unit, size] = units.find(([, size]) => Math.abs(ms) >= size) ?? ['minute', 60_000]
  return relativeFormat.format(Math.round(ms / size), unit)
}

/** A time in full, for a title beside a relative one. */
export const formatFull = (value: string) => {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : dateTimeFormat.format(date)
}

const dayFormat = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' })
const yearFormat = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' })

/** A short date and time ("19 Nov 20:31"), with the year when it is not this year, to stand beside a relative time. */
export function formatDayTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const thisYear = date.getFullYear() === new Date().getFullYear()
  return `${(thisYear ? dayFormat : yearFormat).format(date)} ${timeFormat.format(date)}`
}
