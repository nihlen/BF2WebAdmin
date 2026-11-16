/** The index of the time nearest to <time> in <times> (ascending), or -1 when there are none. */
export function nearestIndex(times: number[], time: number) {
  if (times.length === 0) return -1
  let lo = 0
  let hi = times.length - 1
  while (lo < hi) {
    const mid = (lo + hi) >> 1
    if (times[mid] < time) lo = mid + 1
    else hi = mid
  }
  return lo > 0 && time - times[lo - 1] < times[lo] - time ? lo - 1 : lo
}
