const timeFormat = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' })

/** A time, with the date when it is not today. An unparsable value shows as it is. */
export function formatTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const today = date.toDateString() === new Date().toDateString()
  return (today ? timeFormat : dateTimeFormat).format(date)
}
