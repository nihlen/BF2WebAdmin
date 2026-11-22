// Fixed data for the web UI's fixture mode (UI_FIXTURES=1, see README.md "Fixture mode"): every web API request
// is answered from here by the Vite dev server (fixtures-plugin.ts), and the hub is faked (signalr.ts). The data is the
// same on every run, so screenshots of two builds can be compared. Times are relative to fixtureNow, the time the
// screenshot script fixes the browser's clock to.
//
// Node (the Vite plugin) and the browser (the fake hub) both import this file: no DOM and no Node APIs here.

import type { components } from '../api/schema.d.ts'

type S = components['schemas']

export const fixtureNow = Date.parse('2026-11-21T20:30:00Z')

/**
 * The scenarios, chosen with the ui-fixture cookie: default, empty (a new install without servers), loggedout, and lost
 * (as default, but the offline Public Conquest server was connected until 25 minutes ago, so WebAdmin still has its
 * map, its events and its last health window)
 */
export type FixtureScenario = 'default' | 'empty' | 'loggedout' | 'lost'

export const scenarioFromCookie = (cookie: string | undefined | null): FixtureScenario => {
  const match = /(?:^|;\s*)ui-fixture=([a-z]+)/.exec(cookie ?? '')
  const value = match?.[1]
  return value === 'empty' || value === 'loggedout' || value === 'lost' ? value : 'default'
}

const iso = (msAgo: number) => new Date(fixtureNow - msAgo).toISOString()
const sec = 1000
const min = 60 * sec
const hour = 60 * min
const day = 24 * hour

/** A small deterministic random generator (mulberry32), so generated series are the same on every run */
function random(seed: number) {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

const hash = (seed: number) => {
  const r = random(seed)
  return Array.from({ length: 32 }, () => Math.floor(r() * 16).toString(16)).join('')
}

const uuid = (seed: number) => {
  const h = hash(seed * 7 + 3)
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(13, 16)}-a${h.slice(17, 20)}-${h.slice(20, 32)}`
}

// ---- Servers ----

const ids = {
  heli1: '198.51.100.20:16570',
  heli2: '198.51.100.20:16571',
  test: '198.51.100.21:16570',
  mayhem: '203.0.113.40:16570',
  public: '203.0.113.41:16570',
  // The server the add-server shots add: not in the list, which the capture's fake hub event extends for one page only
  added: '198.51.100.30:16570',
}

export const mainServerId = ids.heli1

const servers: S['ServerDto'][] = [
  {
    id: ids.heli1,
    name: '2v2 Heli | Dalian #1',
    ipAddress: '198.51.100.20',
    gamePort: 16570,
    queryPort: 29900,
    map: 'dalian_2_v_2',
    players: 4,
    maxPlayers: 16,
    gameState: 'Playing',
    socketState: 'Connected',
    serverGroup: '2v2',
  },
  {
    id: ids.heli2,
    name: '2v2 Heli | Dalian #2',
    ipAddress: '198.51.100.20',
    gamePort: 16571,
    queryPort: 29901,
    map: 'dalian_2_v_2',
    players: 0,
    maxPlayers: 16,
    gameState: 'PreGame',
    socketState: 'Connected',
    serverGroup: '2v2',
  },
  {
    id: ids.test,
    name: '2v2 Heli | Test server with a long name for practice and map building',
    ipAddress: '198.51.100.21',
    gamePort: 16570,
    queryPort: 29900,
    map: 'dalian_2_v_2',
    players: 1,
    maxPlayers: 8,
    gameState: 'Paused',
    socketState: 'Connected',
    serverGroup: '2v2',
  },
  {
    id: ids.mayhem,
    name: 'Chopper Mayhem',
    ipAddress: '203.0.113.40',
    gamePort: 16570,
    queryPort: 29900,
    map: 'dalian_plant',
    players: 12,
    maxPlayers: 32,
    gameState: 'Playing',
    socketState: 'Connected',
    serverGroup: 'fun',
  },
  {
    id: ids.public,
    name: 'Public Conquest 24/7',
    ipAddress: '203.0.113.41',
    gamePort: 16570,
    queryPort: 29900,
    map: null,
    players: 0,
    maxPlayers: 64,
    gameState: 'NotConnected',
    socketState: 'Disconnected',
    serverGroup: 'public',
  },
]

const serverGroups = ['2v2', 'fun', 'public']

// ---- Players ----

type PlayerSeed = { name: string; country: string; team: number; rank: number; kills: number; deaths: number; ping: number; alive?: boolean; x: number; y: number }

function makePlayers(seeds: PlayerSeed[], seed: number): S['PlayerDto'][] {
  return seeds.map((p, index) => ({
    name: p.name,
    index,
    ipAddress: `192.0.2.${10 + index + seed}`,
    hash: hash(seed * 100 + index),
    country: p.country,
    rank: p.rank,
    team: p.team,
    isAlive: p.alive ?? true,
    score: { total: p.kills * 2 - p.deaths, team: p.kills, kills: p.kills, deaths: p.deaths, ping: p.ping },
    position: { x: p.x, y: p.y, z: 140 + index * 3 },
    rotation: { x: (index * 77) % 360, y: 0, z: 0 },
  }))
}

const heliPlayers = makePlayers(
  [
    { name: 'Kestrel', country: 'SE', team: 1, rank: 12, kills: 7, deaths: 4, ping: 38, x: -120, y: 80 },
    { name: 'mr.Hellcat', country: 'DE', team: 1, rank: 8, kills: 5, deaths: 6, ping: 52, x: -60, y: 40 },
    { name: 'Vortex|Ace', country: 'US', team: 2, rank: 16, kills: 9, deaths: 3, ping: 104, x: 140, y: -60 },
    { name: 'GhostRotor', country: 'FI', team: 2, rank: 5, kills: 2, deaths: 10, ping: 61, alive: false, x: 90, y: -120 },
  ],
  1,
)

const testPlayers = makePlayers([{ name: 'Builder', country: 'NL', team: 2, rank: 3, kills: 0, deaths: 0, ping: 22, x: 10, y: 10 }], 5)

const mayhemNames: [string, string][] = [
  ['Blackbird', 'GB'],
  ['Zed', 'PL'],
  ['RotorWash', 'US'],
  ['n00bslayer', 'BR'],
  ['Mistral', 'FR'],
  ['Tanaka', 'JP'],
  ['Viper-7', 'CA'],
  ['Lynx', 'NO'],
  ['Hammerhead', 'AU'],
  ['Sasha', 'UA'],
  ['Pilot_Pete', 'IE'],
  ['Orca', 'DK'],
]

const mayhemPlayers = makePlayers(
  mayhemNames.map(([name, country], i) => ({
    name,
    country,
    team: (i % 2) + 1,
    rank: (i * 5) % 22,
    kills: (i * 7) % 15,
    deaths: (i * 3) % 9,
    ping: 30 + ((i * 37) % 180),
    alive: i % 5 !== 3,
    x: ((i * 173) % 600) - 300,
    y: ((i * 251) % 600) - 300,
  })),
  9,
)

const teams2v2: S['TeamDto'][] = [
  { id: 1, name: 'China' },
  { id: 2, name: 'USA' },
]

const maps2v2: S['MapDto'][] = [
  { index: 0, name: 'dalian_2_v_2', size: 16, gameMode: 'gpm_cq' },
  { index: 1, name: 'dalian_plant', size: 64, gameMode: 'gpm_cq' },
  { index: 2, name: 'dalian_plant', size: 32, gameMode: 'gpm_cq' },
  { index: 3, name: 'kubra_dam', size: 64, gameMode: 'gpm_cq' },
  { index: 4, name: 'strike_at_karkand', size: 64, gameMode: 'gpm_cq' },
  { index: 5, name: 'gulf_of_oman', size: 32, gameMode: 'gpm_ctf' },
]

// ---- Chat and events ----

const msg = (
  i: number,
  agoMs: number,
  text: string,
  player: S['PlayerDto'] | null,
  channel = 'Global',
  type = player ? 'Player' : 'Server',
): S['ChatLogDto'] => ({
  message: {
    id: uuid(1000 + i),
    playerId: player?.index ?? null,
    playerName: player?.name ?? null,
    teamName: player ? (teams2v2.find((t) => t.id === player.team)?.name ?? null) : null,
    type,
    channel,
    flags: '',
    text,
  },
  timestamp: iso(agoMs),
})

const [kestrel, hellcat, vortex, ghost] = heliPlayers

const heliChat: S['ChatLogDto'][] = [
  msg(1, 24 * min, 'Welcome to 2v2 Heli. Type .help for the commands', null, 'ServerMessage'),
  msg(2, 22 * min, 'gl hf', kestrel),
  msg(3, 21 * min + 40 * sec, 'glhf', vortex),
  msg(4, 20 * min, 'Round 1 started. Say go when you are ready', null, 'ServerMessage'),
  msg(5, 19 * min, 'go', hellcat),
  msg(6, 19 * min - 5 * sec, 'go', ghost),
  msg(7, 14 * min, 'take the left side, I have the tower', kestrel, 'Team'),
  msg(8, 13 * min, 'nice tv', vortex),
  msg(9, 11 * min, 'lag?', ghost),
  msg(10, 10 * min + 50 * sec, '.ping', ghost),
  msg(11, 10 * min + 49 * sec, 'GhostRotor: ping 61 ms', null, 'ServerMessage'),
  msg(12, 8 * min, 'Round 1: China wins 3-2', null, 'ServerMessage'),
  msg(13, 7 * min, 'gg', vortex),
  msg(14, 6 * min, 'one more?', hellcat),
  msg(15, 5 * min, 'sure, swap sides', vortex),
  msg(16, 4 * min, 'Round 2 started. Say go when you are ready', null, 'ServerMessage'),
  msg(17, 3 * min, 'go', kestrel),
  msg(18, 2 * min, 'watch the AA at the plant', hellcat, 'Team'),
  msg(19, 40 * sec, 'how do I swap seats again?', ghost),
  msg(20, 20 * sec, '.seat to switch seat while landed', null, 'ServerMessage'),
]

const event = (i: number, agoMs: number, message: string): S['EventLogDto'] => ({ id: uuid(2000 + i), message, timestamp: iso(agoMs) })

const heliEvents: S['EventLogDto'][] = [
  event(1, 26 * min, 'Map dalian_2_v_2 loaded'),
  event(2, 24 * min, 'Kestrel joined (SE)'),
  event(3, 23 * min, 'mr.Hellcat joined (DE)'),
  event(4, 22 * min, 'Vortex|Ace joined (US)'),
  event(5, 21 * min, 'GhostRotor joined (FI)'),
  event(6, 20 * min, 'Round 1 started'),
  event(7, 17 * min, 'Vortex|Ace killed mr.Hellcat (TV missile)'),
  event(8, 15 * min, 'Kestrel killed GhostRotor (Z-10 cannon)'),
  event(9, 12 * min, 'Kestrel killed Vortex|Ace (TV missile)'),
  event(10, 8 * min, 'Round 1 ended: China 3, USA 2'),
  event(11, 4 * min, 'Round 2 started'),
  event(12, 90 * sec, 'Vortex|Ace killed GhostRotor (friendly fire)'),
  event(13, 30 * sec, 'GhostRotor died'),
]

const mayhemChat: S['ChatLogDto'][] = mayhemPlayers.slice(0, 8).map((p, i) => msg(100 + i, (30 - i * 3) * min, ['lol', 'who is in the blackhawk', 'rush the dam!', 'gg', 'ns', 'stop spawn killing pls', 'haha', 'brb'][i], p))

// ---- Snapshots ----

function snapshot(serverId: string): S['ServerSnapshotDto'] | null {
  const server = servers.find((s) => s.id === serverId)
  if (!server) return null
  switch (serverId) {
    case ids.heli1:
      return { server, maps: maps2v2, teams: teams2v2, players: heliPlayers, chatLog: heliChat, eventLog: heliEvents }
    case ids.heli2:
      return { server, maps: maps2v2, teams: teams2v2, players: [], chatLog: [], eventLog: [event(30, 3 * hour, 'Map dalian_2_v_2 loaded')] }
    case ids.test:
      return { server, maps: maps2v2, teams: teams2v2, players: testPlayers, chatLog: [msg(40, 9 * min, '.pause', testPlayers[0])], eventLog: [event(40, 10 * min, 'Builder joined (NL)'), event(41, 9 * min, 'Game paused')] }
    case ids.mayhem:
      return {
        server,
        maps: [
          { index: 0, name: 'dalian_plant', size: 64, gameMode: 'gpm_cq' },
          { index: 1, name: 'kubra_dam', size: 64, gameMode: 'gpm_cq' },
        ],
        teams: teams2v2,
        players: mayhemPlayers,
        chatLog: mayhemChat,
        eventLog: [event(50, 40 * min, 'Map dalian_plant loaded'), event(51, 2 * min, 'Chopper Mayhem layout dalian_mayhem6 loaded')],
      }
    default:
      return { server, maps: [], teams: [], players: [], chatLog: [], eventLog: [] }
  }
}

// The lost scenario's Public Conquest server: what WebAdmin keeps of a server after its connection drops
const lostAgo = 25 * min
const lostServer = (serverId: string, scenario: FixtureScenario) => scenario === 'lost' && serverId === ids.public

function lostSnapshot(snap: S['ServerSnapshotDto']): S['ServerSnapshotDto'] {
  return {
    ...snap,
    server: { ...snap.server, map: 'strike_at_karkand' },
    eventLog: [event(60, 3 * hour, 'Map strike_at_karkand loaded'), event(61, lostAgo + 2 * min, 'Round 4 started')],
  }
}

export function fixtureSnapshot(serverId: string, scenario: FixtureScenario) {
  if (scenario === 'empty') return null
  const snap = snapshot(serverId)
  return snap && lostServer(serverId, scenario) ? lostSnapshot(snap) : snap
}

// ---- Health ----

function healthWindow(serverId: string, index: number, count: number): S['ServerHealthWindowDto'] {
  // index 0 is the oldest; windows are 10 s apart and the newest ends at fixtureNow
  const r = random(index * 31 + serverId.length)
  const agoMs = (count - 1 - index) * 10 * sec
  const loading = index % 120 === 7 // a level load about every 20 minutes
  const spike = !loading && index % 53 === 20
  const warn = !loading && !spike && r() < 0.05
  const cpuMedian = 2400 + Math.floor(r() * 900)
  return {
    time: iso(agoMs),
    tick: 30 * 10 * index + 123456,
    status: loading ? 'Loading' : spike ? 'Bad' : warn ? 'Warn' : 'Good',
    reasons: spike ? ['lateTicks', 'cpu'] : warn ? ['lateTicks'] : [],
    ticks: loading ? 180 : 300,
    wallUs: 10_000_000,
    lostTicks: loading ? 120 : spike ? 6 : 0,
    intervalMedianUs: 33_333,
    intervalP99Us: spike ? 71_000 : warn ? 44_000 : 34_100 + Math.floor(r() * 400),
    intervalMaxUs: spike ? 140_000 : warn ? 52_000 : 35_000 + Math.floor(r() * 1500),
    lateTicks: spike ? 14 : warn ? 3 : 0,
    shortTicks: 0,
    cpuMedianUs: cpuMedian,
    cpuP99Us: spike ? 38_000 : cpuMedian * 2 + Math.floor(r() * 1500),
    cpuMaxUs: spike ? 61_000 : cpuMedian * 3 + Math.floor(r() * 3000),
    handlerP99Us: 180 + Math.floor(r() * 120),
    handlerMaxUs: 400 + Math.floor(r() * 600),
    involuntarySwitches: Math.floor(r() * 30),
    majorFaults: spike ? 2 : 0,
    hostValid: true,
    hostCpus: 4,
    hostBusyPercent: 18 + r() * 9 + (spike ? 40 : 0),
    hostStealPercent: r() * 0.4 + (spike ? 3 : 0),
    hostIoWaitPercent: r() * 0.3,
    residentKib: 412_000 + Math.floor(index * 3.2),
  }
}

function connectionsAt(players: S['PlayerDto'][], index: number, count: number): S['ServerConnectionsDto'] {
  const agoMs = (count - 1 - index) * 10 * sec
  return {
    time: iso(agoMs),
    tick: 30 * 10 * index + 123456,
    players: players.map((p, i) => {
      const r = random(index * 17 + i)
      const lossy = p.name === 'GhostRotor' && index % 9 < 3
      const ping = p.score.ping + Math.floor(r() * 12) - 6
      const lossTo = lossy ? 3.5 + r() * 2 : r() * 0.3
      return {
        playerId: p.index,
        name: p.name,
        status: lossy ? 'Bad' : p.score.ping > 100 ? 'Warn' : 'Good',
        reasons: lossy ? ['lossToPlayer'] : p.score.ping > 100 ? ['pingSpread'] : [],
        partial: false,
        readings: 10,
        ping,
        pingMin: ping - 8,
        pingMax: ping + (p.score.ping > 100 ? 60 : 14),
        packetsToPlayer: 300,
        packetsFromPlayer: 290,
        bytesToPlayer: 182_000 + Math.floor(r() * 9000),
        bytesFromPlayer: 41_000 + Math.floor(r() * 3000),
        lostToPlayer: Math.round(lossTo * 3),
        lostToPlayerPeak: Math.round(lossTo * 6),
        lostFromPlayer: 0,
        lostFromPlayerPeak: 1,
        lossToPlayerPercent: lossTo,
        lossFromPlayerPercent: r() * 0.2,
      }
    }),
  }
}

function healthFor(serverId: string, minutes: number): S['ServerHealthDto'] {
  const snap = snapshot(serverId)
  if (!snap || snap.server.socketState !== 'Connected') return { latest: null, history: [], connections: null }
  const count = Math.max(1, Math.min(360 * 6, minutes * 6))
  const history = Array.from({ length: count }, (_, i) => healthWindow(serverId, i, count))
  return { latest: history[history.length - 1], history, connections: connectionsAt(snap.players, count - 1, count) }
}

function connectionsHistory(serverId: string, minutes: number): S['ServerConnectionsDto'][] {
  const snap = snapshot(serverId)
  if (!snap || snap.server.socketState !== 'Connected' || snap.players.length === 0) return []
  const count = Math.max(1, Math.min(360 * 6, minutes * 6))
  return Array.from({ length: count }, (_, i) => connectionsAt(snap.players, i, count))
}

function roundsFor(serverId: string, minutes: number): S['HealthRoundDto'][] {
  if (serverId !== ids.heli1) return []
  const all: S['HealthRoundDto'][] = []
  const matchCount = 8
  for (let m = 0; m < matchCount; m++) {
    const matchStart = (matchCount - m) * 40 * min
    const matchId = uuid(3000 + m)
    for (let n = 1; n <= 3; n++) {
      const startAgo = matchStart - (n - 1) * 12 * min
      const endAgo = startAgo - 10 * min
      all.push({ matchId, roundId: uuid(3100 + m * 10 + n), number: n, start: iso(startAgo), end: endAgo <= 0 ? null : iso(endAgo) })
    }
  }
  all.push({ matchId: uuid(3999), roundId: uuid(3998), number: 1, start: iso(20 * min), end: iso(8 * min) })
  all.push({ matchId: uuid(3999), roundId: uuid(3997), number: 2, start: iso(4 * min), end: null })
  return all.filter((r) => fixtureNow - Date.parse(r.start) <= minutes * min).sort((a, b) => a.start.localeCompare(b.start))
}

// ---- Bans, command log, commands, auth, modules ----

const bans: S['BanDto'][] = [
  { identifier: hash(901), byCdKey: true, kind: 'Permanent', expiresAt: null, name: 'Aimbot_Andy', bannedBy: 'admin', reason: 'Cheating (aimbot)', bannedAt: iso(12 * day) },
  { identifier: hash(902), byCdKey: true, kind: 'Timed', expiresAt: iso(-3 * day), name: 'SpawnCamper', bannedBy: 'Kestrel', reason: 'Spawn killing after warnings', bannedAt: iso(4 * day) },
  { identifier: '192.0.2.77', byCdKey: false, kind: 'Permanent', expiresAt: null, name: 'xX_griefer_Xx', bannedBy: 'admin', reason: 'Ban evasion', bannedAt: iso(30 * day) },
  { identifier: hash(904), byCdKey: true, kind: 'Round', expiresAt: null, name: 'mr.Hellcat', bannedBy: 'Vortex|Ace', reason: 'Team killing', bannedAt: iso(25 * min) },
  { identifier: hash(905), byCdKey: true, kind: 'Permanent', expiresAt: null, name: null, bannedBy: null, reason: null, bannedAt: null },
]

const commandLog = (serverId: string): S['CommandLogEntryDto'][] =>
  [
    { kind: 'Action', command: 'Restart map', response: null, success: true, ago: 26 * min },
    { kind: 'Chat', command: 'Round 2 in a minute, last chance to switch', response: null, success: true, ago: 5 * min + 10 * sec },
    { kind: 'Command', command: '.ping GhostRotor', response: 'GhostRotor: ping 61 ms', success: true, ago: 10 * min },
    { kind: 'Rcon', command: 'sv.numPlayersNeededToStart', response: '2', success: true, ago: 31 * min },
    { kind: 'Action', command: 'Warn mr.Hellcat: team killing', response: null, success: true, ago: 25 * min },
    { kind: 'Action', command: 'Kick SpawnCamper', response: 'Player not found', success: false, ago: 44 * min },
  ]
    .sort((a, b) => b.ago - a.ago)
    .map((e, i) => ({ id: `log-${i}`, serverId, time: iso(e.ago), user: i % 3 === 0 ? 'admin' : 'nihlen', kind: e.kind as S['CommandKind'], command: e.command, response: e.response, success: e.success }))

/** The chat commands, parsed from src/BF2WebAdmin.Server/commands.txt by the plugin ("kick|k <Args>\tAdmin\t(KickCommand)") */
export function parseCommands(text: string): S['CommandInfoDto'][] {
  const levels: Record<string, number> = { Untrusted: 0, All: 10, Trusted: 20, Admin: 50, SuperAdmin: 75, God: 100 }
  return text
    .split(/\r?\n/)
    .filter((line) => line.trim() && !line.startsWith('#'))
    .map((line) => {
      const [usage, level, owner] = line.split('\t')
      const [aliases, ...params] = usage.trim().split(' ')
      return {
        aliases: aliases.split('|'),
        parameters: params.filter(Boolean).map((p) => p.replace(/[<>]/g, '')),
        authLevel: levels[level?.trim()] ?? 10,
        modules: [owner?.includes('Mayhem') ? 'ChopperMayhemModule' : owner?.includes('Heli') ? 'Heli2v2Module' : 'AdminModule'],
      }
    })
}

const auth = (serverGroup: string): S['PlayerAuthDto'][] =>
  serverGroup === '2v2'
    ? [
        { playerHash: heliPlayers[0].hash, authLevel: 100, name: 'Kestrel', serverId: null },
        { playerHash: heliPlayers[2].hash, authLevel: 50, name: 'Vortex|Ace', serverId: null },
        { playerHash: hash(950), authLevel: 75, name: 'nihlen', serverId: null },
        { playerHash: hash(951), authLevel: 20, name: 'TrustedTom', serverId: ids.heli1 },
        { playerHash: hash(952), authLevel: 0, name: 'Muted_Mike', serverId: null },
      ]
    : []

const allModules = [
  'BF2Module',
  'ChopperMayhemModule',
  'DictionaryModule',
  'DiscordModule',
  'FunModule',
  'Heli2v2Module',
  'JailModule',
  'LayoutVoteModule',
  'LogModule',
  'MapModule',
  'ServerControlModule',
  'TestModule',
  'WebModule',
]

const groupModules = (group: string) =>
  group === '2v2'
    ? ['BF2Module', 'Heli2v2Module', 'MapModule', 'DiscordModule', 'LayoutVoteModule', 'LogModule']
    : group === 'fun'
      ? ['BF2Module', 'ChopperMayhemModule', 'FunModule', 'MapModule']
      : ['BF2Module', 'LogModule']

/** The server the add-server shots add, as the hub reports it once it connects (scripts/ui-capture.mjs sends it) */
const addedServer: S['ServerDto'] = {
  id: ids.added,
  name: '2v2 Heli | Dalian #3',
  ipAddress: '198.51.100.30',
  gamePort: 16570,
  queryPort: 29900,
  map: 'dalian_2_v_2',
  players: 0,
  maxPlayers: 16,
  gameState: 'PreGame',
  socketState: 'Connected',
  serverGroup: '2v2',
}

const settings = (server: S['ServerDto']): S['ServerDataDto'] => ({
  serverId: server.id,
  serverGroup: server.serverGroup,
  ipAddress: server.ipAddress,
  gamePort: server.gamePort,
  queryPort: server.queryPort,
  discordBotToken: server.serverGroup === '2v2' ? 'MTA5ODc2NTQzMjEwOTg3NjU0.GhIjKl.fixture-token-not-real' : '',
  discordAdminChannel: server.serverGroup === '2v2' ? '1098765432109876543' : '',
  discordNotificationChannel: server.serverGroup === '2v2' ? '1098765432109876544' : '',
  discordMatchResultChannel: '',
  adminKey: server.socketState === 'Connected' ? 'fixture-admin-key-0123456789abcdef' : null,
  isTestServer: server.id === ids.test,
})

// ---- Missiles (the 3D viewer's list) ----

const missiles = (serverId: string): S['MissileSummaryDto'][] =>
  serverId !== ids.heli1
    ? []
    : Array.from({ length: 8 }, (_, i) => {
        const shooter = heliPlayers[i % 2 === 0 ? 2 : 0]
        const outcome = ['hit', 'miss', 'hit', 'terrain', 'miss', 'hit', 'static', 'miss'][i]
        return {
          key: 100 + i,
          projectileId: 5000 + i,
          template: 'tv_missile',
          shooter: shooter.index,
          shooterName: shooter.name,
          shooterTeam: shooter.team,
          shooterVehicle: shooter.team === 2 ? 'usaav_ah1z' : 'chthe_z10',
          firstTick: 100000 + i * 900,
          samples: 90 + i * 4,
          flags: 0,
          outcome,
          hitPart: outcome === 'hit' ? 'cockpit' : null,
          hitRoot: outcome === 'hit' ? (shooter.team === 2 ? 'chthe_z10' : 'usaav_ah1z') : null,
          hitPlayer: outcome === 'hit' ? (shooter.team === 2 ? 0 : 2) : -1,
          hitRewound: i === 2,
          lagMs: 40 + i * 9,
          targets: [{ netId: 70 + i, template: shooter.team === 2 ? 'chthe_z10' : 'usaav_ah1z', crew: ['Kestrel', 'mr.Hellcat'], closestTested: 1.2 + i, closestCurrent: 2.4 + i, moved: i % 3 === 0 }],
          time: iso((20 - i * 2) * min),
        }
      })

// ---- Map builder ----

const builderItems: S['BuiltItemDto'][] = [
  { id: 1, kind: 'Helipad', template: 'helipad', team: 1, position: [-210, 152, 160], rotation: [90, 0, 0], spawned: true, spawnReady: true },
  { id: 2, kind: 'Helipad', template: 'helipad', team: 2, position: [215, 150, -170], rotation: [270, 0, 0], spawned: true, spawnReady: true },
  { id: 3, kind: 'SpawnPoint', template: 'spawnpoint', team: 1, position: [-200, 152, 150], rotation: [0, 0, 0], spawned: true, spawnReady: true },
  { id: 4, kind: 'SpawnPoint', template: 'spawnpoint', team: 2, position: [205, 150, -160], rotation: [180, 0, 0], spawned: true, spawnReady: false },
  { id: 5, kind: 'Object', template: 'aircontroltower', team: 0, position: [0, 148, 0], rotation: [45, 0, 0], spawned: true, spawnReady: true },
  { id: 6, kind: 'Object', template: 'air_repairstation', team: 0, position: [-180, 151, 170], rotation: [0, 0, 0], spawned: true, spawnReady: true },
  { id: 7, kind: 'Object', template: 'barrackdbl_01', team: 0, position: [60, 149, -40], rotation: [10, 0, 0], spawned: false, spawnReady: false },
]

const builderState = (serverId: string): S['BuilderStateDto'] => {
  const server = servers.find((s) => s.id === serverId)
  return {
    map: server?.map ?? null,
    revision: 14,
    connected: server?.socketState === 'Connected',
    canResync: true,
    layoutId: serverId === ids.heli1 ? uuid(4001) : null,
    layoutName: serverId === ids.heli1 ? 'Tower duel' : null,
    items: serverId === ids.heli1 || serverId === ids.test ? builderItems : [],
    pendingResync: serverId === ids.heli1 ? [{ index: 3, name: 'GhostRotor', blocker: 'dead' }] : [],
  }
}

const layouts = (server: S['ServerDto']): S['LayoutDto'][] =>
  [
    { name: 'Tower duel', by: 'nihlen', ago: 2 * day },
    { name: 'Classic pads', by: 'Kestrel', ago: 9 * day },
    { name: 'Canyon run', by: 'BF2v2: Vortex|Ace', ago: 20 * day },
    { name: 'Tight quarters (old)', by: 'nihlen', ago: 64 * day },
  ].map((l, i) => ({ id: uuid(4001 + i), name: l.name, map: server.map ?? 'dalian_2_v_2', createdBy: l.by, editedDate: iso(l.ago) }))

// ---- Requests ----

export type FixtureRequest = { method: string; path: string; query: URLSearchParams; body: unknown; scenario: FixtureScenario; commands: S['CommandInfoDto'][] }
export type FixtureResponse = { status: number; body?: unknown }

const ok = (body: unknown): FixtureResponse => ({ status: 200, body })
const noContent: FixtureResponse = { status: 204 }
const notFound: FixtureResponse = { status: 404, body: { title: 'Not found', status: 404 } }
const action = (message: string) => ok({ message })

/** Answers one web API request (path without the query string, such as /api/servers/1.2.3.4:16570) */
export function fixtureResponse(request: FixtureRequest): FixtureResponse {
  const { method, query, scenario } = request
  const path = decodeURIComponent(request.path)
  const list = scenario === 'empty' ? [] : servers
  const minutes = Number(query.get('minutes') ?? 60)

  if (path === '/api/auth/me' && method === 'GET') return scenario === 'loggedout' ? { status: 401 } : ok({ name: 'admin' })
  if (path === '/api/auth/login') {
    const body = request.body as { username?: string; password?: string } | undefined
    return body?.password === 'wrong' ? { status: 401, body: 'Wrong username or password' } : noContent
  }
  if (path === '/api/auth/logout') return noContent
  if (path === '/api/servers' && method === 'GET') return ok(list)
  if (path === '/api/servers' && method === 'POST') return noContent
  if (path === '/api/servers/test') {
    const body = request.body as Partial<S['ServerDataDto']> | undefined
    const reachable = !!body?.ipAddress && !body.ipAddress.startsWith('10.')
    return ok([
      { type: 'Query port', success: reachable, message: reachable ? `Answered: 2v2 Heli, dalian_2_v_2, 4/16 players` : 'No answer on the query port within 3 s' },
      // The real server's words (TestServerAsync in ApiEndpoints.cs)
      {
        type: 'Admin channel',
        success: false,
        message: `${body?.ipAddress ?? ''}:${body?.gamePort ?? ''} is not connected. The server connects by itself once it is saved here with the admin key of its serverext.cfg`,
      },
      { type: 'Discord', success: !!body?.discordBotToken, message: body?.discordBotToken ? 'Logged in as BF2 Bot#1234' : 'No bot token: Discord is off for this server' },
    ] satisfies S['TestServerResult'][])
  }
  if (path === '/api/modules') return ok(allModules)

  let m = /^\/api\/server-groups\/([^/]+)\/(modules|auth)(\/reload)?$/.exec(path)
  if (m) {
    const [, group, kind, reload] = m
    if (reload || method !== 'GET') return noContent
    // A new install has no groups yet: a group named in the add flow starts without modules or admins
    if ((!serverGroups.includes(group) || scenario === 'empty') && group !== 'default') return ok([])
    return ok(kind === 'modules' ? groupModules(group === 'default' ? 'public' : group) : auth(group))
  }

  m = /^\/api\/servers\/([^/]+)(\/.*)?$/.exec(path)
  if (!m) return notFound
  const [, serverId, rest = ''] = m
  // The added server's settings and its health (none yet), which its setup steps and the sidebar's health dot ask for once
  // the fake hub has announced it
  if (serverId === ids.added && scenario === 'default') {
    // As the add form saved it, which leaves Discord out: the 2v2 group's other servers have a bot, this one not yet
    if (rest === '/settings') return ok({ ...settings(addedServer), discordBotToken: '', discordAdminChannel: '', discordNotificationChannel: '' })
    if (rest === '/health') return ok(healthFor(serverId, minutes))
  }
  const server = list.find((s) => s.id === serverId)
  if (!server) return notFound
  const connected = server.socketState === 'Connected'
  const snap = fixtureSnapshot(serverId, scenario)!

  if (rest === '') return method === 'DELETE' ? noContent : ok(snap)
  if (rest === '/settings') return method === 'GET' ? ok(settings(server)) : noContent
  if (rest === '/commands') return method === 'GET' ? ok(request.commands) : ok({ command: String((request.body as { command?: string })?.command ?? ''), output: ['Done'] })
  if (rest === '/log') return ok(commandLog(serverId))
  if (rest === '/chat') return ok({ response: null })
  if (rest === '/rcon') return ok({ request: '', response: '' })
  if (rest === '/bans') return ok(serverId === ids.heli1 || serverId === ids.heli2 ? bans : [])
  if (rest.startsWith('/bans/')) return action('Unbanned')
  if (/^\/players\/\d+\/(warn|kick|ban)$/.test(rest)) return action('Done')
  if (rest === '/map' || rest === '/map/restart' || rest === '/map/next' || rest === '/pause') return connected ? action('Sent') : { status: 400, body: 'The server is not connected' }
  // The last window of the lost server, which WebAdmin keeps after the connection drops: the time it was last seen
  if (rest === '/health' && lostServer(serverId, scenario)) {
    const latest = { ...healthWindow(serverId, 0, 1), time: iso(lostAgo) }
    return ok({ latest, history: minutes * min > lostAgo ? [latest] : [], connections: null } satisfies S['ServerHealthDto'])
  }
  if (rest === '/health') return ok(healthFor(serverId, minutes))
  if (rest === '/health/connections') return ok(connectionsHistory(serverId, minutes))
  if (rest === '/health/rounds') return ok(roundsFor(serverId, minutes))
  if (rest === '/missiles') return connected ? ok(missiles(serverId)) : notFound
  if (rest.startsWith('/missiles/')) return notFound
  if (rest === '/builder') return ok(builderState(serverId))
  if (rest === '/builder/templates') {
    // The templates the level uses: a fixed part of the catalog
    const names = Array.isArray(request.body) ? (request.body as string[]) : []
    return ok(names.filter((n, i) => i % 3 !== 1 || ['aircontroltower', 'air_repairstation', 'barrackdbl_01'].includes(n)))
  }
  if (rest === '/builder/layouts') return method === 'GET' ? ok(layouts(server)) : ok(layouts(server)[0])
  if (rest === '/builder/apply' || /^\/builder\/layouts\/[^/]+\/load$/.test(rest)) return ok({ state: builderState(serverId), added: [], errors: [] })
  if (rest === '/builder/clear') return ok(true)
  if (/^\/builder\/layouts\/[^/]+(\/vote)?$/.test(rest)) return noContent
  return notFound
}
