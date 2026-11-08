import { useSyncExternalStore } from 'react'
import { toast } from 'sonner'
import { errorMessage, type Player, type Server } from '@/api/client'

// Server actions that take effect some time after the request, such as a map change: one notification follows the
// action from "sent" to the moment the game shows the change (or a timeout). The hub's events feed the checks:
// ServerUpdateEvent for every client (app layout), and on a server page also the round starts and the players.

/** What the game reported. Each update carries one of the fields. */
export type GameUpdate = {
  /** The server as the server list shows it (map, game state, socket state) */
  server?: Server
  /** A round started (MapChangeEvent): after a map change or a round restart */
  roundStart?: { map: string }
  /** The players of the server page's snapshot */
  players?: Player[]
}

/**
 * Checks the game's updates for the action's effect: true when it is done, a text to show progress, or false to keep
 * waiting. Called for every update of the action's server.
 */
export type GameCheck = (update: GameUpdate) => boolean | string

type Waiter = {
  serverId: string
  key: string
  toastId: string | number
  check: GameCheck
  success: string | ((update: GameUpdate) => string)
  timer: ReturnType<typeof setTimeout>
}

const waiters = new Set<Waiter>()
// Keys of the actions in progress per server (request running or waiting for the game), for the buttons' state
let pending = new Map<string, ReadonlySet<string>>()
const listeners = new Set<() => void>()

function setPending(serverId: string, key: string, on: boolean) {
  const keys = new Set(pending.get(serverId))
  if (on) keys.add(key)
  else keys.delete(key)
  pending = new Map(pending).set(serverId, keys)
  listeners.forEach((listener) => listener())
}

const emptyKeys: ReadonlySet<string> = new Set()

/** The keys of a server's actions in progress, such as "map:dalian_plant" or "restart-map". */
export function usePendingActions(serverId: string): ReadonlySet<string> {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => pending.get(serverId) ?? emptyKeys,
  )
}

/** Hands an update from the game to the waiting actions of that server. */
export function reportGameUpdate(serverId: string, update: GameUpdate) {
  for (const waiter of [...waiters]) {
    if (waiter.serverId !== serverId) continue
    const result = waiter.check(update)
    if (result === true) {
      finish(waiter)
      toast.success(typeof waiter.success === 'string' ? waiter.success : waiter.success(update), { id: waiter.toastId })
    } else if (typeof result === 'string') {
      toast.loading(result, { id: waiter.toastId })
    }
  }
}

function finish(waiter: Waiter) {
  clearTimeout(waiter.timer)
  waiters.delete(waiter)
  setPending(waiter.serverId, waiter.key, false)
}

export type TrackedAction<T> = {
  serverId: string
  /** One action per key at a time, such as "map:dalian_plant" */
  key: string
  /** Shown while the request runs */
  sending: string
  run: () => Promise<T>
  /** The notification when the request succeeded; without until, this ends the action */
  sent?: (result: T) => string
  /** Wait for the game to show the effect */
  until?: {
    check: GameCheck
    /** The notification when check returns true; a function gets the update that did it */
    success: string | ((update: GameUpdate) => string)
    timeoutMs: number
    timeout: string
  }
}

/**
 * Runs an action with one notification from start to end. A failed request shows its error in the same notification.
 * Pass meta: { silent: true } to the mutation, so the mutation cache does not show the error a second time.
 */
export async function trackAction<T>(action: TrackedAction<T>) {
  const { serverId, key } = action
  if (pending.get(serverId)?.has(key)) return

  const toastId = toast.loading(action.sending)
  setPending(serverId, key, true)

  let result: T
  try {
    result = await action.run()
  } catch (error) {
    setPending(serverId, key, false)
    toast.error(errorMessage(error), { id: toastId })
    return
  }

  const sent = action.sent?.(result)
  if (!action.until) {
    setPending(serverId, key, false)
    toast.success(sent ?? 'Done', { id: toastId })
    return
  }

  if (sent) toast.loading(sent, { id: toastId })
  const { check, success, timeoutMs, timeout } = action.until
  const waiter: Waiter = {
    serverId,
    key,
    toastId,
    check,
    success,
    timer: setTimeout(() => {
      finish(waiter)
      toast.warning(timeout, { id: toastId, duration: 15_000 })
    }, timeoutMs),
  }
  waiters.add(waiter)
}
