import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import type { HubEventName, HubEvents } from './events'

// The one connection to the server hub. It only carries live updates: list changes for every client and the game
// events of the servers this client subscribed to. Everything else is a web API request.

const connection = new HubConnectionBuilder()
  .withUrl('/hubs/server')
  // Keep retrying: 0, 2, 5, 10 seconds, then every 15 seconds
  .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (retry) => [0, 2000, 5000, 10000][retry.previousRetryCount] ?? 15000 })
  .configureLogging(LogLevel.Warning)
  .build()

// Hub state for useHubState, and the callbacks that run after a reconnect
const stateListeners = new Set<() => void>()
const reconnectListeners = new Set<() => void>()
const notifyState = () => stateListeners.forEach((listener) => listener())

// Subscribed server ids, counted, so two components can subscribe to the same server
const subscriptions = new Map<string, number>()

connection.onreconnecting(notifyState)
connection.onclose(notifyState)
// A new connection has no groups: subscribe again, then let the pages refetch what they missed
async function resubscribe() {
  await Promise.all([...subscriptions.keys()].map((serverId) => connection.invoke('Subscribe', serverId)))
  reconnectListeners.forEach((listener) => listener())
}

connection.onreconnected(async () => {
  notifyState()
  await resubscribe()
})

let starting: Promise<void> | null = null
let startFailed = false

/** Starts the connection once; later calls wait for the same start. Rejects when the start fails (such as a 401). */
export function startHub(): Promise<void> {
  if (connection.state === HubConnectionState.Connected) return Promise.resolve()
  starting ??= connection
    .start()
    .then(async () => {
      // A start after a failed one is a reconnect for the pages that subscribed in between
      if (startFailed) await resubscribe()
      startFailed = false
    })
    .catch((error: unknown) => {
      startFailed = true
      throw error
    })
    .finally(() => {
      starting = null
      notifyState()
    })
  notifyState()
  return starting
}

export function stopHub() {
  subscriptions.clear()
  return connection.stop()
}

export function onHubReconnected(listener: () => void) {
  reconnectListeners.add(listener)
  return () => {
    reconnectListeners.delete(listener)
  }
}

export function useHubState() {
  return useSyncExternalStore(
    (listener) => {
      stateListeners.add(listener)
      return () => stateListeners.delete(listener)
    },
    () => connection.state,
  )
}

/** Calls the handler for every event with this name, for as long as the component is mounted. */
export function useHubEvent<T extends HubEventName>(name: T, handler: (event: HubEvents[T]) => void) {
  const handlerRef = useRef(handler)
  useEffect(() => {
    handlerRef.current = handler
  })

  useEffect(() => {
    const listener = (event: HubEvents[T]) => handlerRef.current(event)
    connection.on(name, listener)
    return () => connection.off(name, listener)
  }, [name])
}

/**
 * Subscribes to the game events of a server while the component is mounted. Returns true once the subscription is in
 * place: fetch the server's snapshot after that, so no event falls between the snapshot and the subscription.
 */
export function useServerSubscription(serverId: string) {
  const [subscribedId, setSubscribedId] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    subscriptions.set(serverId, (subscriptions.get(serverId) ?? 0) + 1)

    startHub()
      .then(() => connection.invoke('Subscribe', serverId))
      .then(() => {
        if (!cancelled) setSubscribedId(serverId)
      })
      .catch(() => {
        // Not connected: the layout shows the hub state and restarts it; a reconnect subscribes again
      })

    // After a failed start, the next start (or a reconnect) subscribes to everything in subscriptions
    const removeListener = onHubReconnected(() => {
      if (!cancelled) setSubscribedId(serverId)
    })

    return () => {
      cancelled = true
      removeListener()
      const count = (subscriptions.get(serverId) ?? 1) - 1
      if (count > 0) {
        subscriptions.set(serverId, count)
        return
      }
      subscriptions.delete(serverId)
      if (connection.state === HubConnectionState.Connected) connection.invoke('Unsubscribe', serverId).catch(() => {})
    }
  }, [serverId])

  return subscribedId === serverId
}
