import fs from 'node:fs'
import path from 'node:path'
import type { Plugin } from 'vite'
import { fixtureResponse, parseCommands, scenarioFromCookie } from './src/fixtures/data.ts'

/**
 * The fixture mode (UI_FIXTURES=1): the dev server answers the web API from src/fixtures/data.ts instead of proxying it
 * to BF2WebAdmin.Server, and the hub is src/fixtures/signalr.ts. No server is needed, and the data is the same on every
 * run, for screenshots that can be compared (scripts/ui-capture.mjs). The cookie ui-fixture picks a scenario.
 */
export function fixtures(): Plugin {
  const commandsFile = path.resolve(import.meta.dirname, '../BF2WebAdmin.Server/commands.txt')
  const commands = fs.existsSync(commandsFile) ? parseCommands(fs.readFileSync(commandsFile, 'utf8')) : []

  return {
    name: 'ui-fixtures',
    apply: 'serve',
    enforce: 'pre',
    resolveId(source) {
      if (source === '@microsoft/signalr') return path.resolve(import.meta.dirname, 'src/fixtures/signalr.ts')
      return null
    },
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const url = new URL(req.url ?? '/', 'http://fixtures')
        if (!url.pathname.startsWith('/api/')) return next()
        const chunks: Buffer[] = []
        req.on('data', (chunk: Buffer) => chunks.push(chunk))
        req.on('end', () => {
          const text = Buffer.concat(chunks).toString('utf8')
          let body: unknown
          try {
            body = text ? JSON.parse(text) : undefined
          } catch {
            body = text
          }
          const answer = fixtureResponse({
            method: req.method ?? 'GET',
            path: url.pathname,
            query: url.searchParams,
            body,
            scenario: scenarioFromCookie(req.headers.cookie),
            commands,
          })
          res.statusCode = answer.status
          if (answer.body === undefined) return res.end()
          const json = typeof answer.body === 'string' && answer.status >= 400
          res.setHeader('Content-Type', json ? 'text/plain' : 'application/json')
          res.end(json ? answer.body : JSON.stringify(answer.body))
        })
      })
    },
  }
}
