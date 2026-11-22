import type { CommandInfo } from '@/api/client'

// The game modules by class name (src/BF2WebAdmin.Server/Modules), in words. GET /api/modules returns the class names
// only (backend-suggestions.md asks for names and descriptions from the server); a module not listed here shows its
// name without a description.

const descriptions: Record<string, string> = {
  BF2Module: 'The base of every server: join messages, info commands such as .ping and .time, and the admin, server control and fun commands.',
  AdminModule: 'Kick, warn, ban, switch and kill players, and auth levels, in game.',
  ServerControlModule: 'Restart the game server, gravity, script mods and the position timer.',
  FunModule: 'Things admins do to players: dance, freeze, flip, repair, teleport.',
  Heli2v2Module: '2v2 helicopter matches: rounds, scores and results in game and on Discord, saved for the statistics, with the practice commands.',
  MapModule: 'The in-game map builder commands (.o, .undo, .clear, .save, .load), the same objects as the web map builder.',
  LayoutVoteModule: 'Players vote to load a saved layout, clear the map, reset the round or restart the server.',
  ChopperMayhemModule: 'The Chopper Mayhem game mode on Dalian Plant, with its layouts.',
  DiscordModule: "The server's Discord bot: the game in the Discord channels and admin commands from there. Needs a bot token in the server's settings.",
  JailModule: '.jail keeps a player at the jail for an hour.',
  LogModule: 'Writes the game events to log files on the WebAdmin host.',
  DictionaryModule: 'Looks words up in Urban Dictionary from the chat.',
  WebModule: 'Connects the server to this web UI: chat, commands and live updates.',
  TestModule: 'Commands for testing the modules. Not for a public server.',
}

/** "Heli2v2Module" → "Heli 2v2", "ChopperMayhemModule" → "Chopper Mayhem": without "Module", words split. */
export function moduleName(className: string) {
  return className
    .replace(/Module$/, '')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    // A word before a number, but not the v of 2v2
    .replace(/([a-z]{2,})(\d)/g, '$1 $2')
}

export const moduleDescription = (className: string): string | undefined => descriptions[className]

// The module list in kinds, so an owner finds the game mode or the integration without reading every line. Each kind
// has a line on what its modules have in common.
const groups = [
  { name: 'Core', description: 'The base of a server and its link to WebAdmin.', modules: ['BF2Module', 'AdminModule', 'ServerControlModule', 'LogModule', 'WebModule'] },
  { name: 'Match modes', description: 'Game modes, layouts and the map builder.', modules: ['Heli2v2Module', 'ChopperMayhemModule', 'LayoutVoteModule', 'MapModule'] },
  { name: 'Fun', description: 'Commands for players and admins to play with.', modules: ['FunModule', 'JailModule', 'DictionaryModule'] },
  { name: 'Integrations', description: 'Services outside the game.', modules: ['DiscordModule'] },
  { name: 'Testing', description: 'Not for a public server.', modules: ['TestModule'] },
]

/** The modules by kind, in the order above; a module the list does not know goes to Other, at the end. */
export function groupModules(classNames: string[]) {
  const known = new Set(groups.flatMap((g) => g.modules))
  const result = groups
    .map((g) => ({ name: g.name, description: g.description, modules: classNames.filter((m) => g.modules.includes(m)) }))
    .filter((g) => g.modules.length > 0)
  const other = classNames.filter((m) => !known.has(m))
  if (other.length > 0) result.push({ name: 'Other', description: 'Modules this page has no description for.', modules: other })
  return result
}

// Modules that the server loads with another one (LoadedWith in src/BF2WebAdmin.Server/Modules): GET /api/modules
// leaves them out, so the commands they handle belong, on the group page, to the module that brings them
const loadedWith: Record<string, string> = {
  AdminModule: 'BF2Module',
  ServerControlModule: 'BF2Module',
  FunModule: 'BF2Module',
  PracticeModule: 'Heli2v2Module',
  RecordingModule: 'Heli2v2Module',
  ReplayModule: 'Heli2v2Module',
  SeatModule: 'Heli2v2Module',
  TvMissileModule: 'Heli2v2Module',
  LayoutVoteModule: 'MapModule',
}

/**
 * The chat commands each listed module adds, from the command lists of one or more servers (a command names the
 * modules that handle it). A module that is not listed counts for the module it is loaded with. Each command is there
 * once, by its first alias: ".ping" and ".ping <Name>" are one chip.
 */
export function commandsByModule(lists: CommandInfo[][], listed: string[]) {
  const result = new Map<string, string[]>()
  for (const commands of lists)
    for (const command of commands) {
      const alias = command.aliases[0]
      if (!alias) continue
      for (const module of command.modules) {
        const owner = listed.includes(module) ? module : loadedWith[module]
        if (!owner || !listed.includes(owner)) continue
        const aliases = result.get(owner) ?? []
        if (!aliases.includes(alias)) aliases.push(alias)
        result.set(owner, aliases)
      }
    }
  return result
}

/** The kinds of server the group page marks modules as recommended for. */
export type ServerKind = '2v2' | 'public'

// Hand-written from how the modules are used: a 2v2 helicopter server runs the matches and the map builder its layouts
// are built with; a public server, the jail for players who keep breaking the rules and the layout votes for its
// players; both, the Discord bot, so admins can read the game and act from Discord when nobody is in game. The
// modules every server runs (BF2Module, LogModule, WebModule) need no mark
const recommended: Record<string, ServerKind[]> = {
  Heli2v2Module: ['2v2'],
  MapModule: ['2v2'],
  DiscordModule: ['2v2', 'public'],
  JailModule: ['public'],
  LayoutVoteModule: ['public'],
}

/** "Recommended", "Recommended for 2v2" or "Recommended for public servers"; undefined when it is not marked. */
export function recommendation(className: string) {
  const kinds = recommended[className]
  if (!kinds) return undefined
  if (kinds.length > 1) return 'Recommended'
  return kinds[0] === '2v2' ? 'Recommended for 2v2' : 'Recommended for public servers'
}
