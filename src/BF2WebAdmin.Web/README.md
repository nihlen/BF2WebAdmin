# BF2WebAdmin.Web

The web UI of BF2WebAdmin: a client-side React app (Vite, TypeScript, TanStack Query and Router, shadcn/ui on Base UI,
Tailwind CSS). In production the server serves its build from `wwwroot`; there is no server-side rendering.

## Data

- **Web API** (`/api`, `src/BF2WebAdmin.Server/Api/ApiEndpoints.cs`): every query and command. `src/api/client.ts`
  wraps it in typed React Query hooks: `api.useQuery('get', '/api/servers')`, `api.useMutation('post', ...)`. A
  non-2xx response throws an `ApiError`; a 401 anywhere goes to the login page. Login is a cookie
  (`POST /api/auth/login`).
- **Server hub** (`/hubs/server`, SignalR, JSON): live updates only. `src/api/hub.ts` holds the one connection.
  `useHubEvent` listens to an event; `useServerSubscription` joins a server's group. The events patch the React Query
  cache (`src/hooks/use-servers.ts` for the server list, `src/hooks/use-server-live.ts` for a server page). Player
  positions go to a ref that the map canvas draws from, not to the cache.

A server page subscribes first and fetches its snapshot (`GET /api/servers/{id}`) after, so no event falls in
between. After a hub reconnect it subscribes again and refetches.

## Types

`src/api/schema.d.ts` is generated from the server's OpenAPI document. After changing the web API:

```powershell
npm run api    # builds the server with OpenApiGenerateDocuments=true (src/api/openapi.json), then runs openapi-typescript
```

The hub's event types are not in the OpenAPI document: `src/api/events.ts` mirrors `IServerHubClient` by hand.

## Development

Node 24 (pinned for Volta in `package.json`). From the repository root, `aspire run` starts the server and this app's
dev server; Vite proxies `/api` and `/hubs` to the server, so the login cookie works as in production. Without Aspire,
run the server (`http://localhost:5001`) and `npm run dev`.

Page URLs write server ids `ip:port` as `1-2-3-4_16567` (`src/lib/server-id.ts`): a dot in the last path segment would
look like a file to the server's fallback route.

## Build

`npm run build` type-checks and builds into `dist`. `dotnet publish` of the server does this and publishes `dist` as
`wwwroot`; Aspire's publish copies it into the server image (`PublishWithContainerFiles`).
