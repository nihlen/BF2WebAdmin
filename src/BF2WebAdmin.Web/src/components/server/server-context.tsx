import { createContext, use } from 'react'
import type { useServerLive } from '@/hooks/use-server-live'

/**
 * The server whose pages are open: its id and its live state. The server layout runs useServerLive once for every tab;
 * a second instance on a tab would apply each hub event to the cache twice (a chat line twice), so the tabs read it
 * from here.
 */
export type ServerPageState = ReturnType<typeof useServerLive> & { serverId: string }

export const ServerPageContext = createContext<ServerPageState | null>(null)

export function useServerPage() {
  const state = use(ServerPageContext)
  if (!state) throw new Error('useServerPage needs the server layout')
  return state
}
