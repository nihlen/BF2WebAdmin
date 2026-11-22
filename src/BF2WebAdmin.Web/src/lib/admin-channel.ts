import { api } from '@/api/client'

/**
 * What a game server needs to connect to this WebAdmin (the adminConnect host and port, the certificate pin) and
 * WebAdmin's version and start time. It changes only when WebAdmin restarts, so it is fetched once per visit.
 */
export const adminChannelQuery = { ...api.queryOptions('get', '/api/admin-channel'), staleTime: Infinity }

/** The admin channel's port when WebAdmin's configuration does not set another (ServerSettings:AdminChannel:Port). */
export const defaultAdminPort = 4310
