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
