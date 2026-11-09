import createFetchClient, { type Middleware } from 'openapi-fetch'
import createClient from 'openapi-react-query'
import type { components, paths } from './schema'

// Types of the web API (generated from the server's OpenAPI document by "npm run api")
type Schemas = components['schemas']
export type Server = Schemas['ServerDto']
export type ServerSnapshot = Schemas['ServerSnapshotDto']
export type ServerSettings = Schemas['ServerDataDto']
export type Player = Schemas['PlayerDto']
export type Team = Schemas['TeamDto']
export type GameMap = Schemas['MapDto']
export type ChatLogEntry = Schemas['ChatLogDto']
export type EventLogEntry = Schemas['EventLogDto']
export type Message = Schemas['MessageDto']
export type Ban = Schemas['BanDto']
export type RconResponse = Schemas['RconResponse']
// Vector3 is nullable as a schema, because PlayerDto.Position is
export type Vector3 = NonNullable<Schemas['Vector3']>
export type GameState = Schemas['GameState']
export type SocketState = Schemas['SocketState']
export type MissileSummary = Schemas['MissileSummaryDto']
export type MissileReplay = Schemas['MissileReplayDto']
export type MissileSample = Schemas['MissileSampleDto']
export type MissileCollision = Schemas['MissileCollisionDto']
export type CommandInfo = Schemas['CommandInfoDto']
export type CommandLogEntry = Schemas['CommandLogEntryDto']
export type PlayerAuth = Schemas['PlayerAuthDto']
export type TestServerResult = Schemas['TestServerResult']

/** A failed API request. React Query receives it as the error of the query or mutation. */
export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    super(ApiError.describe(status, body))
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }

  private static describe(status: number, body: unknown) {
    if (typeof body === 'string' && body) return body
    if (body && typeof body === 'object' && 'title' in body) return String(body.title)
    return `Request failed (${status})`
  }
}

// Every non-2xx response becomes an ApiError, so the queries and mutations fail instead of resolving to undefined
const throwOnError: Middleware = {
  async onResponse({ response }) {
    if (response.ok) return undefined
    const text = await response.clone().text()
    let body: unknown = text
    try {
      body = text ? JSON.parse(text) : undefined
    } catch {
      // Plain text
    }
    throw new ApiError(response.status, body)
  },
}

const requestTimeoutMs = 15_000

// A request that gets no answer fails instead of waiting for good: Aspire's proxy accepts connections even while the
// server behind it is stopped
const timeout: Middleware = {
  onRequest({ request }) {
    return new Request(request, { signal: AbortSignal.any([request.signal, AbortSignal.timeout(requestTimeoutMs)]) })
  },
}

export const fetchClient = createFetchClient<paths>({ baseUrl: '/', credentials: 'same-origin' })
fetchClient.use(timeout, throwOnError)

/** Typed React Query hooks for the web API: api.useQuery('get', '/api/servers'), api.useMutation('post', ...). */
export const api = createClient(fetchClient)

export const isUnauthorized = (error: unknown) => error instanceof ApiError && error.status === 401

/**
 * The message of a failed query or mutation. The API documents no error bodies, so openapi-react-query types the
 * errors as never; they are the ApiErrors from throwOnError (or fetch's own errors).
 */
export const errorMessage = (error: unknown) => {
  if (error instanceof DOMException && error.name === 'TimeoutError') return 'The server did not answer'
  return error instanceof Error ? error.message : 'Request failed'
}
