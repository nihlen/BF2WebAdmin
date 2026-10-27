import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Server } from '@/api/client'
import { useHubEvent } from '@/api/hub'

// One query key for the hook and the cache updates
const serversQuery = api.queryOptions('get', '/api/servers')

/** All servers, kept current by the hub's server list events. */
export function useServers() {
  return useQuery({ ...serversQuery, select: sortServers, staleTime: Infinity })
}

/** Applies the hub's server list events to the servers query. Mount once, in the app layout. */
export function useServerListUpdates() {
  const queryClient = useQueryClient()

  useHubEvent('ServerUpdateEvent', (event) => {
    queryClient.setQueryData<Server[]>(serversQuery.queryKey, (servers) => {
      if (!servers) return servers
      // The event carries a timeStamp as well, which the list does not need
      const { timeStamp: _, ...server } = event
      return servers.some((s) => s.id === server.id) ? servers.map((s) => (s.id === server.id ? server : s)) : [...servers, server]
    })
  })

  useHubEvent('ServerRemoveEvent', (event) => {
    queryClient.setQueryData<Server[]>(serversQuery.queryKey, (servers) => servers?.filter((s) => s.id !== event.serverId))
  })
}

function sortServers(servers: Server[]) {
  return [...servers].sort((a, b) => a.serverGroup.localeCompare(b.serverGroup) || a.name.localeCompare(b.name))
}

/** Servers grouped by server group, groups in name order. */
export function groupServers(servers: Server[]) {
  const groups = new Map<string, Server[]>()
  for (const server of servers) {
    groups.set(server.serverGroup, [...(groups.get(server.serverGroup) ?? []), server])
  }
  return [...groups.entries()]
}
