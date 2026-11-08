# Web UI: recommended improvements

## Status (2026-11-03)

Done, without database changes (no new tables, columns or repository methods):

- **Feedback:** `src/lib/pending-actions.ts` keeps one notification per action until a hub event confirms it: map
  change and next map (the round start), restart map, pause (game state; WebAdmin now sends `ServerUpdateEvent` on
  every game state change), switch team, kick and ban (the player leaves), restart server (down, then connected).
  Buttons and map rows show a spinner while their action runs. `.` commands return what they said in game
  (`GameTextCapture`, `POST /commands` answers `{ command, output }`).
- **Typed admin channel commands:** map change (`Map` by name, mode and size), restart map, next map, and pause
  (console `gameLogic.togglePause`) are web API actions now, not rcon. A refusal (such as a map that is not in the
  rotation) comes back as a 400 with the reason.
- **Restart server:** `.restartserver` (the button, Discord and chat) quits cleanly with the console's `quit` instead of
  `.crash`. See "Restarting a server" below. `.crash` is unchanged.
- **Player auth:** `GET` and `PUT /api/server-groups/{group}/auth`; a player menu submenu, auth badges, the player
  details sheet, and a "Player auth levels" section on the server group page. Removing sets the default level All (10),
  so no delete method was needed; the list hides entries at All.
- **Safety:** confirmations for restart server, restart map, map change, next map and unban; the reason dialog is red
  only for kick and ban.
- **Missing features:** ban length (Round, 1 hour, 1 day, 1 week, 30 days, Perm), game mode and search in the map list,
  run next map, a Commands tab built from `GET /api/servers/{id}/commands`, chat history (↑/↓) and Tab completion of
  commands and player names, a player details sheet (with the bans that match), an Activity tab (an in-memory log of
  what web users sent, `GET /api/servers/{id}/log` and `CommandLogEvent`), and a dashboard empty state and totals.
- **Smaller fixes:** labels on icon buttons, password fields with a show button, inline "Test connection" results with
  the error, a visible live-updates banner, team colours by team id, formatted and sorted bans, narrower tables on small
  screens, "always on" modules, "Save and reload", and a warning before leaving the settings form with changes.

Not done:

- A known-players table (names for offline hashes, past names), a persistent audit log, match history and web logins
  in the database: they need database changes.
- Showing or setting the next map without running it: the admin channel's `Map` command has no such action, and the
  server does not send the next map.
- "Offline for N minutes" on the dashboard: WebAdmin does not keep the time of the last disconnect.
- Timed bans pass the number of seconds to `iga ban`. That is the ModManager ban manager's format as I understand it,
  but it has not been tried on a server.

### Restarting a server

`.crash` spawns an `air_j10` at 0/0/0 to crash the engine, and relies on the container restarting the process. The
admin channel has no restart command, but its `Console` command reaches the engine console, whose `quit` (and `exit`)
calls `BF2Engine::quitNextFrame` (OpenBF2 `IO/Console/Console.cpp:524`, `Game/Main/BF2Engine.cpp:2826`). The engine
then leaves its main loop, deletes the game (module unload, the admin channel closes normally) and `main` returns 0.

That needs a container restart policy that restarts on exit 0: `always` or `unless-stopped`. `on-failure` restarts only
a crash. The localtest's `docker-compose.openbf2.yml` and the AppHost's `openbf2` container have no restart policy, so
there a quit (like a crash) leaves the server stopped. `quit` also needs `adminAllowConsole 1` (the default); with 0 the
web UI shows "disabled in its serverext.cfg".

A restart that does not depend on the container would need a module change in openbf2-server-ext: for example a typed
`Restart` command that re-executes the server process, or that exits with a code a wrapper script loops on.

A review of `src/BF2WebAdmin.Web` on the `admin-channel` branch (2026-11-03). It does not cover the 3D viewer, which
is still being worked on. The items are grouped by topic, and the order at the end says what to do first. Many items
need a small web API change as well; those say so.

## 1. Feedback on actions

Most buttons send a request and show nothing more. Today the result shows in one of these places:

| Action | What the user sees today |
|---|---|
| Change map (Maps tab) | Nothing. The rcon response goes only to the Requests tab, and the map badge changes when `MapChangeEvent` arrives, which can take many seconds |
| Restart map, toggle pause | Nothing, unless they open the Requests tab |
| Switch team, kill player, restart server (`.` commands) | Nothing. `POST /commands` returns 204, and the module's answer (`SendText`) goes to the game chat |
| Warn, kick, ban, unban | A toast with the rcon response (`notifyCommands`, `pages/server.tsx:25`) |
| Errors | A toast from the mutation cache. This one works |

Recommendations:

- **Track pending game changes until the game confirms them.** Clicking a map should mark that row "Changing…" with a
  spinner, disable the list, and show a toast (sonner's `toast.promise` or `toast.loading`) that moves through
  *Sent → Loading map → Map changed to X*. It ends when a `MapChangeEvent` with that map arrives, or fails with "No map
  change after 60 s". `GameStateEvent` already gives the loading step. Use the same pattern for restart map
  (wait for the game state to change), pause (the paused game state) and switch team (`PlayerTeamEvent` for that
  player). A small `usePendingAction(match, timeout)` hook that watches hub events would cover all of them.
- **Show the result of every command, not only some.** Show a toast for each command with the rcon response, and
  replace `notifyCommands` with a short list of commands to keep quiet instead.
- **Return the output of `.` commands to the web.** `HandleCustomCommandAsync` (`WebModule.cs:235`) is fire-and-forget.
  Collect what the command sends with `SendText` while it runs (or give the fake message a reply callback) and return
  it from `POST /commands`, the way `/rcon` returns a `RconResponse`. Then "Player not found" and "Insufficient
  permissions" show up in the UI.
- **Show the state on the button.** Disable the button and show a spinner while its request runs. Today
  `custom.mutate` and `rcon.mutateAsync` (`pages/server.tsx:159-161`) share one mutation, so `isPending` is the same for
  all buttons. Give each action its own pending state.
- **Keep the Requests tab.** It is component state (`pages/server.tsx:135`) and empties when the user goes to another
  page. Put it in the query cache per server, or better, save it on the server as a per-server audit log (see 4).
- **Show what changed on the forms.** The server group page saves and reloads in separate steps with no dirty state.
  Show "Unsaved changes" and a single "Save and reload" button. The settings form should warn before the user leaves
  with unsaved edits.

## 2. Player auth levels in the UI

Today auth is only `.auth <name> <level>` and `.auth <name>` in the chat (`Commands/AuthCommands.cs`,
`BF2Module.cs:501-544`). This has limits:

- It works only for a player who is online, found by name.
- The answer goes to the game chat, so a web user does not see if it worked.
- There is no list of who has which level. An admin cannot be removed while offline.
- The levels are numbers (`Auth` in `BF2WebAdmin.Common/Enums.cs`: 10 All, 20 Trusted, 50 Admin, 75 SuperAdmin,
  100 God) that the user has to remember.

The data is already there: `ServerPlayerAuth` (server group, player hash, level) with `GetPlayerAuthAsync` and
`SetPlayerAuthAsync`. Recommendations:

- **Web API:** `GET /api/server-groups/{group}/auth` returns hash, level and the last known name.
  `PUT /api/server-groups/{group}/auth/{hash}` takes `{ level }`. `DELETE` removes the entry, which needs a new
  repository method. After a change, call `ModManager.GetAuthPlayersAsync()` on every server in the group, as the chat
  command does. The web user is God (`WebAdminHashGod`), so the chat command's "lower than your own level" rule does not
  limit it, but the API should still refuse level 100 unless it is meant to be allowed.
- **Names for hashes:** auth rows have only the hash. Fill in names from players online now, and from
  `MatchRoundPlayer` (which has `PlayerHash` and `PlayerName`). A small `KnownPlayer` table (hash, last name, last
  seen, server) updated on join would make this reliable and help with bans too.
- **Server group page:** add an "Admins" section beside Modules. It lists the players with their level as a select
  (Trusted / Admin / SuperAdmin / God), a remove button, and "Add by hash" for players who are not online.
- **Server page:** in the player menu, add "Set auth level ▸" with the named levels. Show a small badge next to the name
  of each player who has a level, so admins are easy to see. `PlayerDto.Hash` is already sent; the snapshot needs the
  group's auth list (or a level on each player) and a hub event when it changes.
- **Web logins:** web users are in `Authentication:Admins` in the configuration, with plain-text passwords, and they all
  have the same role. This is fine for one or two people. If more people get access, consider users stored in the
  database with hashed passwords, a "change password" page, and a read-only role (can watch, cannot kick or ban). That
  is a bigger change and can come later.

## 3. Safety: ask before destructive actions

- **Restart server** (`.crash`, `pages/server.tsx:92`) runs on one click, with no question, next to Restart map. Use
  `ConfirmButton` for it.
- **Map change** runs on one click on a list item. With players on the server, ask "Change map to X? N players are
  online", or use a select plus a "Change map" button.
- **Unban** (`pages/server.tsx:455`) runs on one click. Ask first, and show the player's name in the question.
- **Kill / switch team** are fine on one click; a toast with an undo is not possible, so feedback is enough.
- The reason dialog's submit is always red (`input-dialog.tsx:47`), also for Warn. Pass the variant in the request.

## 4. Missing features

Ordered by how useful they likely are:

1. **Ban length.** Ban always sends `Perm` (`pages/server.tsx:234`). Add a length choice (round, 1 day, 1 week, perm)
   to the reason dialog.
2. **Game mode in the map list.** The map command always uses `gpm_cq` (`pages/server.tsx:309`), but the server knows
   each map's mode (`Map.GameMode` in `AdminStateApplier.cs:94`); `MapDto` leaves it out. Add `gameMode` to `MapDto`,
   show it, and use it in the command. A search box above the list helps when the list is long.
3. **Next map / map rotation.** Show the next map, set it without changing now, and run "Run next map".
4. **Module controls.** Many chat commands have no button: Heli2v2 (`switch`, `2v2`, `pad`, `nasa`, `mg`, `tvmissile`,
   `nofences`), map mods (`mapload`, `mapsave`, `mapclear`, `mapundo`), `gravity`, `score reset`, `jail`/`free`,
   `teleport`. Add a "Modules" panel on the server page with the commands of the active modules. A
   `GET /api/servers/{id}/commands` that returns the `[Command]` attributes (pattern, auth level, module) would let
   the UI build forms and also offer autocomplete in the chat box.
5. **Chat box.** Arrow-up history, autocomplete for `.` commands and player names (from the list above), and a channel
   selector if team messages are wanted.
6. **Player details.** A side panel when a name is clicked: hash, country, ping, auth level, time on the server, past
   names, past bans, and the actions. The player menu would then have room for more actions.
7. **Audit log.** Who ran which command, on which server, when, and what the answer was. Server side, per server and
   shown in the Requests tab. Useful once more than one admin uses the web UI.
8. **Match history.** `Match` / `MatchRound` / `MatchRoundPlayer` are stored for Heli2v2 but cannot be seen in the web
   UI. A page per server group with results and players.
9. **Dashboard.** An empty state ("No servers yet, add one") and a total player count. A server that is offline should
   say for how long.

## 5. Smaller fixes

- **Icon-only buttons** (settings cog, player menu `⋮`, unban) have no `aria-label`, and the cog has no tooltip.
- **Secrets on the settings form.** Rcon password and Discord bot token are plain text inputs
  (`pages/server-settings.tsx:125-126`). Use `type="password"` with a show toggle, as the admin key already does.
- **"Test connection"** shows only "OK" or "error" per type. Show the error text, and keep the result next to the
  button instead of in a toast that goes away.
- **"Live updates offline"** is small grey text in the header. When it is offline the data on screen is old; make it a
  visible banner on the server page, with "reconnecting in N s".
- **Team colours.** Chat (`ChatLine`) uses the team *name* `US` for blue, and the map uses team id 2 for blue. Use one
  rule, since team names differ between maps.
- **Bans table** shows the raw `datetime` string. Format it like the log times, and sort with the newest first.
- **Small screens:** the score table has 7 columns, and the page buttons wrap badly. Hide K/D below `sm` and put the
  three server actions in a menu on mobile.
- **Server group page:** say why default modules are checked and cannot be changed ("always on"), and make it clear that
  "Reload modules for all servers" means the servers in this group.

## Suggested order

1. Confirm dialogs for Restart server, map change and unban (3). Small, and they prevent accidents during a game.
2. Pending state and result toasts for map change, restart map and pause, using hub events (1). This is the main
   feedback problem.
3. Return `.` command output from `POST /commands` (1). After this, every action can show its result.
4. Auth management: the API, the Admins section on the server group page, and the player menu item (2).
5. Game mode in the map list, and ban length (4.1, 4.2).
6. The command list endpoint, followed by module controls and chat autocomplete (4.4, 4.5).
7. The rest as needed.
