import { api, type ModuleInfo, type Server } from '@/api/client'

/** The modules a group can enable, with their names, descriptions and commands in words (GET /api/modules). */
export const modulesQuery = api.queryOptions('get', '/api/modules')

/**
 * A module's name in words: the server's display name when GET /api/modules lists it, else the class name without
 * "Module" and split into words, as the server makes it ("TvMissileModule" → "Tv Missile"). A command can name a module
 * the list leaves out, one that comes with another (AdminModule with BF2Module).
 */
export function moduleName(className: string, modules?: ModuleInfo[]) {
  const listed = modules?.find((m) => m.name === className)
  if (listed) return listed.displayName
  return className
    .replace(/Module$/, '')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    // A word before a number, but not the v of 2v2
    .replace(/([a-z]{2,})(\d)/g, '$1 $2')
}

/** "Admin, Fun and Server control": the modules that come with one, in a sentence. */
export function namesText(names: string[]) {
  if (names.length <= 1) return names.join('')
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`
}

// The module list in kinds, so an owner finds the game mode or the integration without reading every line. Each kind
// has a line on what its modules have in common. The server has no kinds; a module not named here goes to Other
const groups = [
  { name: 'Core', description: 'The base of a server and its link to WebAdmin.', modules: ['BF2Module', 'LogModule', 'WebModule'] },
  { name: 'Match modes', description: 'Game modes, layouts and the map builder.', modules: ['Heli2v2Module', 'ChopperMayhemModule', 'MapModule'] },
  { name: 'Fun', description: 'Commands for players and admins to play with.', modules: ['JailModule', 'DictionaryModule'] },
  { name: 'Integrations', description: 'Services outside the game.', modules: ['DiscordModule'] },
  { name: 'Testing', description: 'Not for a public server.', modules: ['TestModule'] },
]

/** The modules by kind, in the order above; a module the list does not know goes to Other, at the end. */
export function groupModules(modules: ModuleInfo[]) {
  const known = new Set(groups.flatMap((g) => g.modules))
  const result = groups
    .map((g) => ({ name: g.name, description: g.description, modules: modules.filter((m) => g.modules.includes(m.name)) }))
    .filter((g) => g.modules.length > 0)
  const other = modules.filter((m) => !known.has(m.name))
  if (other.length > 0) result.push({ name: 'Other', description: 'Modules of a newer WebAdmin than this list of kinds.', modules: other })
  return result
}

/** The kinds of server the group page marks modules as recommended for. */
export type ServerKind = '2v2' | 'public'

// Hand-written from how the modules are used: a 2v2 helicopter server runs the matches and the map builder its layouts
// are built with; a public server, the jail for players who keep breaking the rules and the map builder for the layout
// votes that come with it; both, the Discord bot, so admins can read the game and act from Discord when nobody is in
// game. The modules every server runs (BF2Module, LogModule, WebModule) need no mark
const recommended: Record<string, ServerKind[]> = {
  Heli2v2Module: ['2v2'],
  MapModule: ['2v2', 'public'],
  DiscordModule: ['2v2', 'public'],
  JailModule: ['public'],
}

// Modules that make a server a game mode of its own, which neither list above was written for
const gameModeModules = ['Heli2v2Module', 'ChopperMayhemModule']

// The game's own modes (ServerDto.gameMode, as the server last ran it): a server in one of them that runs no game-mode
// module is a public server
const standardModes = ['gpm_cq', 'gpm_ctf', 'gpm_coop']

/**
 * What kind of server a group's are, as far as the UI can tell: 2v2 when the group runs the Heli2v2 module or a server
 * of it is on a 2v2 map (dalian_2_v_2: its mode is plain conquest, gpm_cq, so the mode alone does not tell); public when
 * the group runs no game-mode module and every server whose mode is known (since it connected) runs a standard mode.
 * Anything else (a Chopper Mayhem group, a group whose servers have not connected yet) is undefined, and then no module
 * is marked: a mark the page cannot back up would be a guess.
 */
export function groupKind(servers: Pick<Server, 'map' | 'gameMode'>[], modules: string[]): ServerKind | undefined {
  if (modules.includes('Heli2v2Module') || servers.some((s) => s.map && /2_?v_?2/i.test(s.map))) return '2v2'
  const known = servers.filter((s) => s.gameMode)
  if (known.length > 0 && known.every((s) => standardModes.includes(s.gameMode!.toLowerCase())) && !modules.some((m) => gameModeModules.includes(m))) return 'public'
  return undefined
}

/** Whether a module is one a server of this kind usually runs. */
export const isRecommended = (className: string, kind: ServerKind | undefined) => !!kind && !!recommended[className]?.includes(kind)

/** The kind in words, for the mark's tooltip: "a 2v2 server", "a public server". */
export const kindText = (kind: ServerKind) => (kind === '2v2' ? 'a 2v2 server' : 'a public server')
