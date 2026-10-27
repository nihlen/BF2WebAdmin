// Server ids are "ip:port". In page URLs they are written as "1-2-3-4_16567": a dot in the last path segment looks like
// a file name, which the server's fallback to index.html (and Vite's dev server) would not route to the app.

export const encodeServerId = (serverId: string) => serverId.replaceAll('.', '-').replaceAll(':', '_')

export const decodeServerId = (urlId: string) => urlId.replaceAll('-', '.').replaceAll('_', ':')
