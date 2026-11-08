// BF2's team 1 is the opposing force (red) and team 2 the US-side team (blue) on every map, while the team names
// change from map to map. Colour by team id, never by name.

export const teamTextColor = (teamId: number | null | undefined) => (teamId === 2 ? 'text-blue-400' : teamId === 1 ? 'text-red-400' : undefined)

export const teamCanvasColor = (teamId: number) => (teamId === 2 ? '#60a5fa' : '#f87171')

const gameModes: Record<string, string> = {
  gpm_cq: 'Conquest',
  gpm_coop: 'Co-op',
  gpm_ctf: 'Capture the flag',
  gpm_ti: 'Titan',
}

/** A game mode such as "gpm_cq" as a name ("Conquest"); unknown modes as they are. */
export const gameModeName = (mode: string | null | undefined) => (mode ? (gameModes[mode.toLowerCase()] ?? mode) : '')
