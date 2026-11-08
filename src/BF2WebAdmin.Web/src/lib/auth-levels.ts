// The player auth levels of the chat commands (Auth in src/BF2WebAdmin.Common/Enums.cs). A player without an entry has All.

export const authLevels = [
  { value: 0, name: 'Untrusted', description: 'Can run no commands' },
  { value: 10, name: 'All', description: 'The default: public commands only' },
  { value: 20, name: 'Trusted', description: 'Position and follow commands' },
  { value: 50, name: 'Admin', description: 'Kick, kill, switch, map mods and most module commands' },
  { value: 75, name: 'SuperAdmin', description: 'Ban and TV missile settings' },
  { value: 100, name: 'God', description: 'Every command, including noclip and script mods' },
] as const

export const defaultAuthLevel = 10

/** The name of a level, such as "Admin"; a level between the named ones shows as its number. */
export function authLevelName(level: number) {
  return authLevels.find((l) => l.value === level)?.name ?? String(level)
}
