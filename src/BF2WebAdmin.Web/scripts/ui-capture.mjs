// Screenshots of the web UI in its fixture mode, for the UI review loop (.claude/skills/ui-climb). Every page and state in
// the shot list, at desktop and phone width, with the browser's clock fixed to the fixtures' time, animations off and
// WebGL canvases covered (they are not scored). Each shot also gets measured checks: console errors, horizontal scroll
// and axe-core's accessibility rules.
//
//   UI_FIXTURES=1 npx vite --port 5199 --strictPort        (in another terminal)
//   node scripts/ui-capture.mjs --out <dir> [--base http://localhost:5199] [--only server,login]
//
// Writes <dir>/<shot>.png (full page), <dir>/<shot>.part<N>.png (viewport-high slices of long pages, for reading),
// and <dir>/checks.json. Uses the installed Chrome (playwright-core, no browser download).
//
// The shot list is the review's contract: keep every shot. When the UI changes, update the steps (selectors) so each
// shot still shows the same state; a step that fails is recorded in checks.json and the shot is taken anyway.

import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'
import { chromium } from 'playwright-core'

const require = createRequire(import.meta.url)
const args = Object.fromEntries(
  process.argv.slice(2).reduce((pairs, arg, i, all) => (arg.startsWith('--') ? [...pairs, [arg.slice(2), all[i + 1]?.startsWith('--') ? 'true' : (all[i + 1] ?? 'true')]] : pairs), []),
)
const base = (args.base ?? 'http://localhost:5199').replace(/\/$/, '')
const out = path.resolve(args.out ?? 'ui-shots')
const only = args.only ? new Set(args.only.split(',')) : null
const fixtureNow = Date.parse('2026-11-21T20:30:00Z')

const desktop = { width: 1440, height: 900 }
const phone = { width: 390, height: 844 }

const heli1 = '/server/198-51-100-20_16570'
const mayhem = '/server/203-0-113-40_16570'
const offline = '/server/203-0-113-41_16570'

/** A step that may fail when the UI changed: the error goes to the shot's checks, the shot is still taken */
const tryStep = async (log, name, fn) => {
  try {
    await fn()
  } catch (error) {
    log.push(`step "${name}" failed: ${String(error?.message ?? error).split('\n')[0]}`)
  }
}

const openNav = async (page, log) =>
  tryStep(log, 'open navigation', async () => {
    await page.locator('[data-sidebar="trigger"], button[aria-label*="sidebar" i], button:has-text("Toggle Sidebar")').first().click({ timeout: 4000 })
    await page.waitForTimeout(600)
  })

const fillAddForm = async (page, log) =>
  tryStep(log, 'fill add-server form', async () => {
    const fill = async (selector, value) => {
      const field = page.locator(selector).first()
      if (await field.count()) await field.fill(value, { timeout: 3000 })
    }
    // The group is a select of the existing groups since r01 (an input when there are none)
    const group = page.locator('#serverGroup').first()
    if ((await group.evaluate((el) => el.tagName).catch(() => '')) === 'INPUT') await group.fill('2v2', { timeout: 3000 })
    else {
      await group.click({ timeout: 3000 })
      await page.getByRole('option', { name: '2v2', exact: true }).click({ timeout: 3000 })
    }
    await fill('#ipAddress, input[name="ipAddress"]', '198.51.100.30')
    await fill('#gamePort, input[name="gamePort"]', '16570')
    await fill('#queryPort, input[name="queryPort"]', '29900')
  })

/** Clicks a button (by its accessible name) and waits for what it opens */
const clickButton = (name, label = name) => async (page, log) =>
  tryStep(log, label, async () => {
    await page.getByRole('button', { name }).first().click({ timeout: 4000 })
    await page.waitForTimeout(600)
  })

const testConnection = async (page, log) => {
  await fillAddForm(page, log)
  await tryStep(log, 'test connection', async () => {
    // Exact since r03: the Optional section's toggle mentions the test-server flag
    await page.getByRole('button', { name: /^Test connection$/ }).first().click({ timeout: 4000 })
    await page.waitForTimeout(1200)
  })
}

const addServer = async (page, log) => {
  await fillAddForm(page, log)
  await tryStep(log, 'add server', async () => {
    await page.getByRole('button', { name: /^Add server$/ }).last().click({ timeout: 4000 })
    await page.getByText(/Waiting for 198\.51\.100\.30:16570/).waitFor({ timeout: 4000 })
  })
}

const addedServerConnects = async (page, log) =>
  tryStep(log, 'the new server connects', async () => {
    await page.evaluate(() =>
      // eslint-disable-next-line no-undef
      window.__fixtureHub.emit('ServerUpdateEvent', {
        id: '198.51.100.30:16570',
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
        timeStamp: new Date().toISOString(),
      }),
    )
    await page.getByText(/is connected$/).waitFor({ timeout: 4000 })
    await page.waitForTimeout(400)
  })

/** Opens the header's round menu (the split button's chevron, since r06) and picks an item from it */
const roundMenuItem = (name, label) => async (page, log) =>
  tryStep(log, label, async () => {
    await page.getByRole('button', { name: /^More round actions/ }).first().click({ timeout: 4000 })
    await page.waitForTimeout(400)
    await page.getByRole('menuitem', { name }).first().click({ timeout: 4000 })
    await page.waitForTimeout(600)
  })

/** On a phone, opens Live's Players view (since r06 Live opens on the chat there); on desktop the teams are always shown */
const playersView = async (page, log) =>
  tryStep(log, 'open the players view', async () => {
    const players = page.getByRole('tab', { name: /^Players/ })
    if (await players.count()) await players.click({ timeout: 4000 })
    await page.waitForTimeout(300)
  })

/** Presses a button of the add server flow and waits for the next step's heading text */
const addFlowStep = (button, label) => async (page, log) =>
  tryStep(log, label, async () => {
    await page.getByRole('button', { name: button }).last().click({ timeout: 4000 })
    await page.waitForTimeout(800)
  })

/** The shots: name, path, scenario (ui-fixture cookie), viewports, and the steps to the state */
const shots = [
  { name: 'login', path: '/login', scenario: 'loggedout', viewports: ['desktop', 'phone'] },
  {
    name: 'login-error',
    path: '/login',
    scenario: 'loggedout',
    viewports: ['desktop'],
    steps: [
      async (page, log) =>
        tryStep(log, 'wrong password', async () => {
          const inputs = page.locator('input')
          await inputs.nth(0).fill('admin', { timeout: 3000 })
          await page.locator('input[type="password"]').first().fill('wrong', { timeout: 3000 })
          await page.locator('button[type="submit"]').first().click({ timeout: 3000 })
          await page.waitForTimeout(800)
        }),
    ],
  },
  { name: 'dashboard', path: '/', viewports: ['desktop', 'phone'] },
  { name: 'dashboard-empty', path: '/', scenario: 'empty', viewports: ['desktop', 'phone'] },
  // Since r03 the setup steps are one reminder row above the list, which opens into the servers' checklists
  { name: 'dashboard-setup', path: '/', viewports: ['desktop'], steps: [clickButton(/^Show steps$/, 'show setup steps')] },
  { name: 'nav-open', path: '/', viewports: ['phone'], overlay: true, steps: [openNav] },
  { name: 'server-add', path: '/server/add', viewports: ['desktop', 'phone'] },
  { name: 'server-add-tested', path: '/server/add', viewports: ['desktop'], steps: [testConnection] },
  // Since r02: "New group" beside the group list opens the name field; with no servers the field has suggestions
  { name: 'server-add-new-group', path: '/server/add', viewports: ['desktop'], steps: [clickButton(/^New group$/, 'new group')] },
  { name: 'server-add-first', path: '/server/add', scenario: 'empty', viewports: ['desktop', 'phone'] },
  // Since r03: Add server saves and goes to step 2, the lines for the game server and a live wait for it to connect
  { name: 'server-add-connect', path: '/server/add', viewports: ['desktop', 'phone'], steps: [addServer] },
  // ... which turns into the connected state when the hub reports the new server (the fake hub sends it)
  { name: 'server-add-connected', path: '/server/add', viewports: ['desktop', 'phone'], steps: [addServer, addedServerConnects] },
  // Since r06 the add flow goes on after connecting: the group's modules, a first admin, then done. In the 2v2 group,
  // which has its modules and admins already, so since r07 the connected step offers Finish, and Review modules for the
  // modules and admins steps
  { name: 'server-add-modules', path: '/server/add', viewports: ['desktop', 'phone'], steps: [addServer, addedServerConnects, addFlowStep(/^Review modules$/, 'review modules')] },
  {
    name: 'server-add-admins',
    path: '/server/add',
    viewports: ['desktop', 'phone'],
    steps: [addServer, addedServerConnects, addFlowStep(/^Review modules$/, 'review modules'), addFlowStep(/^Continue$/, 'continue to admins')],
  },
  {
    name: 'server-add-done',
    path: '/server/add',
    viewports: ['desktop'],
    steps: [addServer, addedServerConnects, addFlowStep(/^Finish$/, 'finish')],
  },
  // The first server, in a new group, with connecting skipped: the modules step asks for the kind of server and ticks
  // the modules it usually runs
  { name: 'server-add-modules-new-group', path: '/server/add', scenario: 'empty', viewports: ['desktop'], steps: [addServer, addFlowStep(/^Skip for now$/, 'skip connecting')] },
  { name: 'server-edit', path: `${heli1}/edit`, viewports: ['desktop'] },
  { name: 'server-group', path: '/servergroup/2v2', viewports: ['desktop', 'phone'] },
  // The unsaved-changes bar sticks to the window's bottom, so the shot is of the screen
  {
    name: 'server-group-unsaved',
    path: '/servergroup/2v2',
    viewports: ['desktop', 'phone'],
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'turn on Chopper Mayhem', async () => {
          await page.getByRole('checkbox', { name: /Chopper Mayhem/ }).click({ timeout: 4000 })
          await page.waitForTimeout(400)
        }),
    ],
  },
  // Since r04: the Discord module turned on in a group whose server has no bot token warns, with a link to its settings
  {
    name: 'server-group-discord',
    path: '/servergroup/fun',
    viewports: ['desktop', 'phone'],
    steps: [
      async (page, log) =>
        tryStep(log, 'turn on Discord', async () => {
          await page.getByRole('checkbox', { name: /^Discord/ }).click({ timeout: 4000 })
          await page.getByText(/has no bot token/).waitFor({ timeout: 4000 })
        }),
    ],
  },
  // Since r04: the modules filter finds a module by a command it adds
  {
    name: 'server-group-filter',
    path: '/servergroup/2v2',
    viewports: ['desktop'],
    steps: [
      async (page, log) =>
        tryStep(log, 'filter by a command', async () => {
          await page.getByRole('searchbox', { name: /filter the modules/i }).fill('.mg', { timeout: 4000 })
          await page.waitForTimeout(400)
        }),
    ],
  },
  // Since r04: on a phone, Modules and Admin levels are two tabs
  {
    name: 'server-group-admins',
    path: '/servergroup/2v2',
    viewports: ['phone'],
    steps: [
      async (page, log) =>
        tryStep(log, 'admin levels tab', async () => {
          await page.getByRole('tab', { name: 'Admin levels' }).click({ timeout: 4000 })
          await page.waitForTimeout(400)
        }),
    ],
  },
  { name: 'server', path: heli1, viewports: ['desktop', 'phone'] },
  // Since r03 the maps are a dialog; since r04 "Change map…" is a button of its own in the header
  // Since r06 "Change map…" is in the Restart round split button's menu
  { name: 'server-tab-maps', path: heli1, viewports: ['desktop'], overlay: true, steps: [roundMenuItem(/^Change map/, 'change map')] },
  // Since r03 the commands are a palette, opened by Commands beside the chat box (or Ctrl+K)
  { name: 'server-tab-commands', path: heli1, viewports: ['desktop'], overlay: true, steps: [clickButton(/^Commands$/, 'open commands')] },
  // Since r04: the selected command's arguments beside the list, a player argument with the online players to pick
  {
    name: 'server-command-args',
    path: heli1,
    viewports: ['desktop'],
    overlay: true,
    steps: [
      clickButton(/^Commands$/, 'open commands'),
      async (page, log) =>
        tryStep(log, 'pick .flip and a player', async () => {
          await page.getByRole('combobox', { name: /Search commands/ }).fill('flip', { timeout: 4000 })
          await page.getByRole('combobox', { name: /Search commands/ }).press('Enter')
          await page.getByRole('group', { name: /Online players/ }).getByRole('button', { name: 'Kestrel' }).click({ timeout: 4000 })
          await page.waitForTimeout(300)
        }),
    ],
  },
  // Chat that comes while it is not in view. Since r03 chat is always on Live on desktop, so the shot scrolls the log up
  // and the new lines show as a "2 new messages" jump; on a phone, the Players segment is open and the Chat segment
  // counts them. The fixtures' chat fits the desktop log, so the fake hub first sends earlier lines until it scrolls,
  // then the two new ones
  {
    name: 'server-chat-unread',
    path: heli1,
    viewports: ['desktop', 'phone'],
    steps: [
      // Since r06 a phone opens Live on the chat view: the Players view keeps the new lines out of sight
      playersView,
      async (page, log) =>
        tryStep(log, 'scroll the chat up', async () => {
          const chatLog = page.locator('h2:text-is("Chat") + ul')
          if (!(await chatLog.count())) return
          await chatLog.evaluate((el) => {
            const said = ['ready?', 'go', 'nice shot', 'lag again', 'tv on the left', 'one more round', 'swap sides?', 'ok']
            if (el.scrollHeight > el.clientHeight + 80) return
            for (let i = 0; i < said.length; i++)
              // eslint-disable-next-line no-undef
              window.__fixtureHub.emit('ChatEvent', {
                message: { id: `00000000-0000-4000-b000-00000000000${i}`, type: 'Player', channel: 'Global', flags: '', playerId: i % 2 ? 1 : 3, playerName: i % 2 ? 'mr.Hellcat' : 'GhostRotor', teamName: i % 2 ? 'China' : 'USA', text: said[i] },
                timeStamp: new Date().toISOString(),
              })
          })
          await page.waitForTimeout(300)
          await chatLog.evaluate((el) => el.scrollTo({ top: 0 }))
          await page.waitForTimeout(300)
        }),
      async (page, log) =>
        tryStep(log, 'two new chat lines', async () => {
          await page.evaluate(() => {
            const lines = [
              { playerId: 2, playerName: 'Vortex|Ace', teamName: 'USA', text: 'gg, rematch?' },
              { playerId: 0, playerName: 'Kestrel', teamName: 'China', text: 'sure, same teams' },
            ]
            lines.forEach((line, i) =>
              // eslint-disable-next-line no-undef
              window.__fixtureHub.emit('ChatEvent', {
                message: { id: `00000000-0000-4000-a000-00000000000${i}`, type: 'Player', channel: 'Global', flags: '', ...line },
                timeStamp: new Date().toISOString(),
              }),
            )
          })
          await page.waitForTimeout(400)
        }),
    ],
  },
  // Since r03 the phone's tab bar is at the bottom: Live, Health, 3D, Builder and More with the rest
  { name: 'server-tabs-more', path: heli1, viewports: ['phone'], overlay: true, steps: [clickButton(/More pages/, 'open More')] },
  // The header's actions menu. Since r04 the round actions are buttons of their own on Live, and since r05 the server
  // actions are a "⋯" menu (Settings, Copy address, the group's settings, Restart server); on a phone all of them are
  // one menu button beside the name
  { name: 'server-round-menu', path: heli1, viewports: ['desktop'], overlay: true, steps: [clickButton('Server actions', 'server menu')] },
  // Since r05: away from Live the round actions are one labelled Round menu
  // Since r06 every tab has the same round actions: Restart round… with Pause and Change map… in its split menu
  { name: 'server-round-tab-menu', path: `${heli1}/health`, viewports: ['desktop'], overlay: true, steps: [clickButton(/^More round actions/, 'round menu')] },
  // Since r06 a phone has the same header actions as desktop; since r07 they are one "⋯" menu beside the name (the
  // round actions, then the server's), which this opens
  { name: 'server-actions', path: heli1, viewports: ['phone'], overlay: true, steps: [clickButton(/^Round and server actions/, 'actions menu')] },
  // Since r06: the group's modules and admin levels as a tab of the server
  { name: 'server-tab-group', path: `${heli1}/group`, viewports: ['desktop', 'phone'] },
  // Bans, activity and health are pages of their own under the server's tab bar since r01
  { name: 'server-tab-bans', path: `${heli1}/bans`, viewports: ['desktop', 'phone'] },
  // Since r03: the search field over names, hashes, addresses and reasons
  {
    name: 'server-tab-bans-search',
    path: `${heli1}/bans`,
    viewports: ['desktop'],
    steps: [
      async (page, log) =>
        tryStep(log, 'search the bans', async () => {
          await page.getByRole('searchbox', { name: /search the bans/i }).fill('spawn', { timeout: 4000 })
          await page.waitForTimeout(400)
        }),
    ],
  },
  // Since r04 the web's commands and the game's events are one timeline with filter chips
  { name: 'server-tab-activity', path: `${heli1}/activity`, viewports: ['desktop', 'phone'] },
  {
    name: 'server-tab-activity-filtered',
    path: `${heli1}/activity`,
    viewports: ['desktop'],
    steps: [
      async (page, log) =>
        tryStep(log, 'show web actions only', async () => {
          await page.getByRole('button', { name: /^Web actions/ }).click({ timeout: 4000 })
          await page.waitForTimeout(400)
        }),
    ],
  },
  // Since r04: Warn, Kick and Ban on a scoreboard row while the pointer is over it (desktop)
  {
    name: 'server-row-actions',
    path: heli1,
    viewports: ['desktop'],
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'hover mr.Hellcat', async () => {
          await page.getByRole('row').filter({ hasText: 'mr.Hellcat' }).first().hover({ timeout: 4000 })
          await page.waitForTimeout(300)
        }),
    ],
  },
  { name: 'server-health', path: `${heli1}/health`, viewports: ['desktop', 'phone'] },
  {
    name: 'server-player',
    path: heli1,
    viewports: ['desktop', 'phone'],
    overlay: true,
    steps: [
      playersView,
      async (page, log) =>
        tryStep(log, 'open player Kestrel', async () => {
          await page.getByText('Kestrel', { exact: true }).first().click({ timeout: 4000 })
          await page.waitForTimeout(800)
        }),
    ],
  },
  {
    name: 'server-confirm',
    path: heli1,
    viewports: ['desktop'],
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'restart map dialog', async () => {
          await page.getByRole('button', { name: /restart map|restart round/i }).first().click({ timeout: 4000 })
          await page.waitForTimeout(600)
        }),
    ],
  },
  // Since r02: warn, kick and ban in one dialog with preset reasons and, for a ban, its length as chips
  {
    name: 'server-moderate',
    path: heli1,
    viewports: ['desktop', 'phone'],
    overlay: true,
    steps: [
      playersView,
      clickButton('Actions for mr.Hellcat', 'player menu'),
      async (page, log) =>
        tryStep(log, 'ban with a reason and a length', async () => {
          await page.getByRole('menuitem', { name: /ban player/i }).click({ timeout: 4000 })
          await page.waitForTimeout(500)
          await page.getByRole('button', { name: 'Team killing' }).click({ timeout: 3000 })
          await page.getByRole('button', { name: '1 day' }).click({ timeout: 3000 })
          await page.waitForTimeout(300)
        }),
    ],
  },
  { name: 'account-menu', path: heli1, viewports: ['desktop'], overlay: true, steps: [clickButton(/^Account/, 'account menu')] },
  { name: 'server-busy', path: mayhem, viewports: ['desktop'] },
  { name: 'server-offline', path: offline, viewports: ['desktop', 'phone'] },
  // Since r07 an offline server that was connected before reads as lost: when it was last seen, what to check
  { name: 'server-offline-lost', path: offline, scenario: 'lost', viewports: ['desktop', 'phone'] },
  { name: 'viewer', path: `${heli1}/viewer`, viewports: ['desktop', 'phone'], wait: 2500 },
  { name: 'builder', path: `${heli1}/builder`, viewports: ['desktop', 'phone'], wait: 2500 },
  // Since r02: the chat opens under the 3D view on a phone. The map in software WebGL keeps the page too busy for a
  // pointer click to pass Playwright's checks in time, so the step sends the click event itself
  {
    name: 'builder-chat',
    path: `${heli1}/builder`,
    viewports: ['phone'],
    wait: 2500,
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'open chat', async () => {
          await page.getByRole('button', { name: /^Chat/ }).first().dispatchEvent('click', {}, { timeout: 4000 })
          await page.waitForTimeout(800)
        }),
    ],
  },
  // Since r03: below lg the builder's panels (objects, spawns, built, layouts) are a bottom sheet over the 3D view
  {
    name: 'builder-panels',
    path: `${heli1}/builder`,
    viewports: ['phone'],
    wait: 2500,
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'open the panels', async () => {
          await page.getByRole('button', { name: /^Objects, spawns and layouts/ }).first().dispatchEvent('click', {}, { timeout: 4000 })
          await page.waitForTimeout(1500)
        }),
    ],
  },
  // Since r03: "Clear all built" is in the Apply card's menu, behind a confirmation that names the count
  {
    name: 'builder-clear',
    path: `${heli1}/builder`,
    viewports: ['desktop'],
    wait: 2500,
    overlay: true,
    steps: [
      async (page, log) =>
        tryStep(log, 'clear all built', async () => {
          await page.getByRole('button', { name: 'More builder actions' }).first().dispatchEvent('click', {}, { timeout: 4000 })
          await page.waitForTimeout(600)
          await page.getByRole('menuitem', { name: /Clear all built/ }).first().dispatchEvent('click', {}, { timeout: 4000 })
          await page.waitForTimeout(800)
        }),
    ],
  },
]

// Animations, transitions and the caret off, so two runs give the same pixels
// The React Query devtools button is a development tool, not part of the UI
const stillCss = `*, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; scroll-behavior: auto !important; }
.tsqd-parent-container, .tsqd-open-btn-container { display: none !important; }`

// External images (the minimaps on static.nihlen.net) are cached on disk: the same bytes every run, and no wait for them
const cacheDir = path.resolve(import.meta.dirname, '../../../artifacts/ui-climb/.cache')
async function cachedRoute(route) {
  const url = new URL(route.request().url())
  const file = path.join(cacheDir, url.hostname, ...url.pathname.split('/').filter(Boolean))
  if (!fs.existsSync(file)) {
    // Node's fetch: Cloudflare refuses headless Chrome's requests
    const response = await fetch(url)
    if (!response.ok) return route.fulfill({ status: response.status })
    fs.mkdirSync(path.dirname(file), { recursive: true })
    fs.writeFileSync(file, Buffer.from(await response.arrayBuffer()))
  }
  return route.fulfill({ path: file, headers: { 'access-control-allow-origin': '*' } })
}

/** Covers every WebGL canvas (one that has no 2D context) with a labelled panel: the 3D view is not scored */
const coverWebGl = () => {
  let covered = 0
  for (const canvas of document.querySelectorAll('canvas')) {
    let webgl = false
    try {
      webgl = canvas.getContext('2d') === null
    } catch {
      webgl = true
    }
    if (!webgl) continue
    const r = canvas.getBoundingClientRect()
    if (r.width < 10 || r.height < 10) continue
    const cover = document.createElement('div')
    cover.setAttribute('data-ui-capture', 'webgl-cover')
    Object.assign(cover.style, {
      position: 'absolute',
      left: `${r.left + window.scrollX}px`,
      top: `${r.top + window.scrollY}px`,
      width: `${r.width}px`,
      height: `${r.height}px`,
      background: 'repeating-linear-gradient(45deg, #1f2937 0 12px, #273244 12px 24px)',
      color: '#9ca3af',
      font: '600 14px system-ui, sans-serif',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      // Above the page, below what floats over it (the phone's tab bar at z-40, dialogs, sheets and menus at z-50)
      zIndex: '30',
      pointerEvents: 'none',
    })
    cover.textContent = '3D view (WebGL, not scored)'
    document.body.appendChild(cover)
    covered++
  }
  // A state the page shows over its 3D view (such as the viewer's "waiting for frames") is part of the UI: above the cover
  for (const keep of document.querySelectorAll('[data-ui-capture-keep]')) keep.style.zIndex = '31'
  return covered
}

async function main() {
  fs.mkdirSync(out, { recursive: true })
  const axeSource = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8')
  const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] })
  const results = []

  for (const shot of shots) {
    if (only && !only.has(shot.name)) continue
    for (const viewportName of shot.viewports) {
      const viewport = viewportName === 'desktop' ? desktop : phone
      const id = `${shot.name}--${viewportName}`
      const context = await browser.newContext({ viewport, deviceScaleFactor: 1, reducedMotion: 'reduce', colorScheme: 'dark', locale: 'en-GB', timezoneId: 'Europe/London' })
      await context.addCookies([{ name: 'ui-fixture', value: shot.scenario ?? 'default', url: base }])
      await context.route('https://static.nihlen.net/**', cachedRoute)
      const page = await context.newPage()
      await page.clock.setFixedTime(fixtureNow)
      const log = []
      const consoleErrors = []
      page.on('console', (m) => m.type() === 'error' && consoleErrors.push(m.text().slice(0, 300)))
      page.on('pageerror', (e) => consoleErrors.push(`pageerror: ${String(e.message).slice(0, 300)}`))

      const started = Date.now()
      try {
        await page.goto(base + shot.path, { waitUntil: 'networkidle', timeout: 30000 })
      } catch (error) {
        log.push(`navigation: ${String(error?.message ?? error).split('\n')[0]}`)
      }
      await page.addStyleTag({ content: stillCss }).catch(() => {})
      await page.waitForTimeout(shot.wait ?? 1200)
      for (const step of shot.steps ?? []) await step(page, log)
      await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {})
      await page.waitForTimeout(300)
      await page.addStyleTag({ content: stillCss }).catch(() => {})
      const layout = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        innerWidth: window.innerWidth,
        scrollHeight: document.documentElement.scrollHeight,
        url: location.pathname,
      }))

      // A long page is shot with the viewport as tall as the page, so fixed parts (the sidebar, the phone's tab bar)
      // reach its bottom as they do while scrolling. A part sized by the viewport (the 3D views, in svh) grows with it,
      // so the viewport follows the page until it fits. This comes before the 3D views are covered and frozen, so the
      // cover fits the resized view. A shot of an overlay (sheet, dialog, menu) shows the screen as the user sees it
      const tall = !shot.overlay && layout.scrollHeight > viewport.height
      for (let i = 0, height = viewport.height; tall && i < 4 && layout.scrollHeight > height; i++) {
        height = Math.min(layout.scrollHeight, 8000)
        await page.setViewportSize({ width: viewport.width, height })
        await page.waitForTimeout(500)
        layout.scrollHeight = await page.evaluate(() => document.documentElement.scrollHeight)
      }

      const covered = await page.evaluate(coverWebGl).catch((e) => (log.push(`cover: ${e.message}`), 0))
      await page.waitForTimeout(200)
      // A page with a 3D view is frozen: its render loop would keep the screenshot waiting. The 2D minimap keeps drawing
      if (covered > 0) {
        await page.evaluate(() => (window.requestAnimationFrame = () => 0)).catch(() => {})
        await page.waitForTimeout(100)
      }

      const file = path.join(out, `${id}.png`)
      try {
        await page.screenshot({ path: file, fullPage: !shot.overlay, timeout: 60000 })
      } catch (error) {
        log.push(`screenshot: ${String(error?.message ?? error).split('\n')[0]}`)
        await page.screenshot({ path: file, timeout: 60000 }).catch(() => {})
      }
      // Long pages also in viewport-high slices, which stay readable when a reviewer opens them
      const parts = []
      const sliceCount = Math.min(8, Math.ceil(layout.scrollHeight / viewport.height))
      if (tall && sliceCount > 1) {
        for (let i = 0; i < sliceCount; i++) {
          const y = i * viewport.height
          const height = Math.min(viewport.height, layout.scrollHeight - y)
          if (height < 80) continue
          const partFile = path.join(out, `${id}.part${i + 1}.png`)
          const taken = await page
            .screenshot({ path: partFile, fullPage: true, clip: { x: 0, y, width: viewport.width, height }, timeout: 60000 })
            .then(() => true)
            .catch(() => false)
          if (taken) parts.push(path.basename(partFile))
        }
      }

      let axe = { violations: [] }
      try {
        await page.addScriptTag({ content: axeSource })
        axe = await page.evaluate(async () => {
          // eslint-disable-next-line no-undef
          const r = await window.axe.run(document, { exclude: [['canvas'], ['[data-ui-capture]']], resultTypes: ['violations'] })
          return { violations: r.violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.length, help: v.help })) }
        })
      } catch (error) {
        log.push(`axe: ${String(error?.message ?? error).split('\n')[0]}`)
      }

      results.push({
        id,
        shot: shot.name,
        viewport: viewportName,
        path: shot.path,
        scenario: shot.scenario ?? 'default',
        file: path.basename(file),
        parts,
        finalUrl: layout.url,
        horizontalOverflow: layout.scrollWidth > layout.innerWidth + 1 ? layout.scrollWidth - layout.innerWidth : 0,
        pageHeight: layout.scrollHeight,
        consoleErrors,
        stepFailures: log,
        axe: axe.violations,
        ms: Date.now() - started,
      })
      process.stdout.write(`${id}: ${consoleErrors.length} errors, ${axe.violations.length} axe rules, ${log.length} failed steps\n`)
      await context.close()
    }
  }

  await browser.close()
  const summary = {
    shots: results.length,
    consoleErrors: results.reduce((n, r) => n + r.consoleErrors.length, 0),
    stepFailures: results.reduce((n, r) => n + r.stepFailures.length, 0),
    horizontalOverflow: results.filter((r) => r.horizontalOverflow > 0).map((r) => r.id),
    axeSeriousOrCritical: results.reduce((n, r) => n + r.axe.filter((v) => v.impact === 'serious' || v.impact === 'critical').reduce((m, v) => m + v.nodes, 0), 0),
    axeTotalNodes: results.reduce((n, r) => n + r.axe.reduce((m, v) => m + v.nodes, 0), 0),
  }
  fs.writeFileSync(path.join(out, 'checks.json'), JSON.stringify({ base, capturedAt: new Date().toISOString(), summary, results }, null, 2))
  console.log(JSON.stringify(summary))
}

main().catch((error) => {
  console.error(error)
  process.exit(1)
})
