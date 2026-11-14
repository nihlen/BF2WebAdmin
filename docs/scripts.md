# Game scripts

A game script is a small TypeScript program that WebAdmin sends to a game server, where the server's module
(`libserverext.so`) runs it on every tick. Scripts are for what cannot wait for a round trip to WebAdmin: holding a
helicopter under a ceiling, turning something smoothly, anything that must act in the tick it sees. Everything else
stays in the C# modules.

A script changes without restarting the game server: WebAdmin sends the source every time it starts one.

The wire messages, the limits and the host API are defined in openbf2-server-ext `docs/admin-channel-draft.md`,
section "Scripts". This document covers WebAdmin's side.

## How it fits together

```
GameScripts/src/nasa.ts ── npm run build ──> GameScripts/dist/nasa.js ── embedded in BF2WebAdmin.Server
C# module ── GameServer.Scripts.StartAsync("nasa", args) ── StartScript ──> module ── runs it every tick
C# module <── ScriptDataEvent, ScriptLogEvent, ScriptFinishedEvent <── ScriptEvent, ScriptFinished ── module
```

| Part | Where |
|---|---|
| The scripts, their types and tests | `src/BF2WebAdmin.Server/GameScripts/` (an npm project: `src/*.ts`, `game.d.ts`, `test/`, `build.mjs`) |
| Starting, stopping and following scripts | `IScriptRunner` and `IScriptRun` (`Abstractions/IScriptRunner.cs`), reached as `GameServer.Scripts`; implemented by `AdminChannel/ScriptRunner.cs` |
| The messages | `AdminChannel/AdminProtocol.cs`: `CmdStartScript`, `CmdStopScript`, `CmdScriptMessage`, `CmdListScripts`, `ScriptList`, `EvScriptFinished`, `EvScriptEvent`, and `Welcome.CapScripts` |
| The scripts in use | `nasa` (`.nasa`, `PracticeModule`) and `dance` (`.dance`, `FunModule`) |

## Servers that cannot run scripts

A module says in its `Welcome` whether it runs scripts (capability bit 3). It does not when it was built before
scripts existed, or when its `serverext.cfg` has `adminAllowScripts 0`. WebAdmin never sends a script message to such
a server: an unknown message type would close the link.

So a module that uses a script checks `GameServer.Scripts.Available` first and keeps another way:

| Feature | With scripts | Without |
|---|---|---|
| `.nasa <metres>` | The script `nasa`: every tick, whatever is above the altitude is made to sink back towards it (faster the higher it is, at most 30 m/s) and keeps its horizontal speed | A teleport back to the altitude when a position update (4 a second by default) shows a player above it, which stops the vehicle |
| `.dance`, `.dance <name>` | The script `dance`: the player is held in place and turned one full turn a second with a gentle wobble, then put back level | A random rotation sent every 25 ms |

The same fallback is used when a script cannot be started, and for `.nasa` after its script failed.

## Writing a script

1. Add `GameScripts/src/<name>.ts`. The name is lower-case letters, digits and `-`.
2. Write strict global code against the two globals `script` and `game` (`GameScripts/game.d.ts` has every member with
   its meaning). Register handlers at the top level:

   ```ts
   // Says once a second how many players are alive.
   let ticks = 0;

   script.onTick(() => {
       if (++ticks % game.tickRate !== 0)
           return;

       const alive = game.players().filter(id => game.player(id)?.alive).length;
       script.emit({ alive });
   });
   ```

3. Add a test in `GameScripts/test/<name>.test.mjs` (below).
4. Start it from a C# module:

   ```csharp
   if (GameServer.Scripts.Available)
   {
       var script = await GameServer.Scripts.StartAsync("alive");
       var result = await script.Finished;   // when the module needs to know that it ended
   }
   ```

Rules a script must follow:

- **One self-contained file.** No `import` or `export`; every script is its own program on the game server, with its
  own globals.
- **Only syntax that disappears with the types** (`erasableSyntaxOnly`): no `enum`, no `namespace`, no parameter
  properties. The build removes the types by blanking them out, so line and column numbers are those of the `.ts`
  file.
- **The engine is small.** It has the base objects (`Math`, `Array`, `Object`, `String`, ...), JSON, `Map`/`Set`, typed
  arrays and promises. It has no `Date`, no regular expressions, no `eval`, no `Proxy`, no timers and no `console`.
  Use `game.tick` or `game.time` for time and `script.log` for output. The build fails when a script names one of
  the missing globals.
- **At most 60000 bytes** of JavaScript; the build checks it.
- **No timers.** Promises work, but nothing resolves one later: count ticks in `onTick`.

### The host API

`game.d.ts` is the reference. In short:

| Member | Meaning |
|---|---|
| `script.args` | The arguments of `StartAsync`, as parsed JSON (`null` without any). Property names are camel case |
| `script.onTick(fn)` | `fn(tick)` on every tick of the game server, 30 a second |
| `script.onMessage(fn)` | `fn(message)` for every `IScriptRun.SendAsync` |
| `script.onStop(fn)` | `fn(reason)` before the script ends by `finish`, a stop, a level unload or a closing link; the place to undo what it did. Not called after an error |
| `script.finish(result?)` | Ends the script when the current handler returns |
| `script.emit(data)` | Sends data to WebAdmin: a `ScriptDataEvent` for the modules |
| `script.log(...parts)` | A line in the game server's log and in WebAdmin's |
| `game.tick`, `game.tickRate`, `game.time`, `game.map` | The tick number, 30, the engine's time in seconds, the map's name |
| `game.players()`, `game.player(id)` | The player ids, and one player: name, team, alive, bot, seat, the root object's id, template, position, rotation and velocity |
| `game.teleport(id, position, rotation)` | Moves or turns the player's root object (its vehicle when seated); a position resets its speed |
| `game.setVelocity(id, velocity)`, `game.setAngularVelocity(id, velocity)` | Set the root object's speed (m/s) or rotational speed (rad/s) |
| `game.say(text, player?)` | A chat line to everyone or to one player |

Positions are `[x, y, z]` in metres with `y` up; rotations are `[yaw, pitch, roll]` in degrees. The crew of one
vehicle shares its root object (`rootId`): a script that acts on vehicles handles each `rootId` once.

### Limits on the game server

The module enforces them; a script cannot switch them off.

| Limit | Value | What happens |
|---|---|---|
| Scripts at once on a server | 8 | `StartAsync` fails |
| Memory per script | 4 MiB | The script ends with an error |
| CPU time of one call into the script | 5 ms (50 ms for the top-level code) | The script ends, reason over budget |
| CPU time over any 30 ticks | 30 ms (1 ms a tick on average) | The same |
| `emit` in one tick | 16, each at most 8192 bytes of JSON | `emit` throws a `RangeError` |
| A message to a script | 8192 bytes of JSON | `SendAsync` throws |

## The life of a script

- It starts when `StartAsync` returns: the script's top-level code has run on the game server.
- It ends when it calls `script.finish`, when the module that started it calls `StopAsync`, when it fails, when the
  level unloads (a map change or a restarted round), or when the link to the game server closes.
- **Scripts do not outlive their link or their level.** A C# module that wants a script to keep running starts it again
  after a reconnect (`SocketStateChangedEvent` with `Connected`) and after a level unload (`MapChangedEvent`), as
  `PracticeModule` does for `.nasa`. While WebAdmin is disconnected, no script runs.
- A module reload ends nothing by itself: a module stops its scripts when its token is cancelled (`StopScriptAsync` in
  `BaseModule`).

`IScriptRun.Finished` completes with a `ScriptResult` in every case and never fails:

| Reason | Meaning | `Text` |
|---|---|---|
| `Finished` | The script called `finish` | The JSON of its result (also as `Value`) |
| `Stopped` | `StopAsync` | Empty |
| `Error` | A handler threw, a promise was rejected, or it ran out of memory | The error with its stack: lines of the `.ts` file |
| `OverBudget` | It used too much CPU time | The same |
| `LevelUnload` | The level unloaded | Empty |
| `LinkClosed` | The link closed | Empty |
| `Lost` | Events were lost and the script no longer runs on the server | Empty |

The result also has the number of calls into the script and its CPU time, total and longest call, which WebAdmin logs
when a script ends. That is the way to see what a script costs.

The modules also get three events, on their own thread like every game event: `ScriptDataEvent` (an `emit`),
`ScriptLogEvent` (a `script.log` line) and `ScriptFinishedEvent`. They are for every script on the server, so a
handler compares `e.Script` with its own run.

## Errors and logs

- **A script that does not compile, or throws in its top-level code**, is refused: `StartAsync` throws
  `AdminCommandException` with the engine's message and line.
- **A script that fails later** ends with reason `Error` or `OverBudget`. WebAdmin logs a warning with the server id,
  the script's name and the error; the stack names lines and columns of the `.ts` file.
- **`script.log`** lines are in the game server's log (`[serverext]` lines) and in WebAdmin's log as
  `Script <name> on <server id>: <line>`.
- **Starts and ends** are logged by WebAdmin at Information.

## Build

`dotnet build` of the server runs `npm run build` in `GameScripts` when a script, `game.d.ts` or a build file is newer
than the last build (target `BuildGameScripts`; `npm ci` first when `node_modules` is missing), and embeds
`dist/<name>.js` as the resource `GameScripts/<name>.js`. It needs the Node version pinned in
`GameScripts/package.json` (Volta picks it up). `-p:SkipGameScripts=true` skips the npm step and embeds what `dist`
already holds.

`npm run build` is two steps: `tsc` type checks the scripts against `game.d.ts`, then `build.mjs` removes the types
with Node's own type stripping (`module.stripTypeScriptTypes`, which Node 24 has without a flag but marks as
experimental) and checks each script's size and that it names nothing the engine lacks.

## Testing and debugging

```powershell
cd src\BF2WebAdmin.Server\GameScripts
npm test          # builds, then runs test/*.test.mjs with node:test
```

- **Unit tests in Node.** `test/harness.mjs` runs a built script against a fake `script` and `game`: the test sets up
  players, runs ticks, sends messages and reads what the script did (`calls`, `emitted`, `logs`, `result`). The
  globals the engine lacks are removed there too. `test/nasa.test.mjs` and `test/dance.test.mjs` are the examples.
- **The C# side** is tested with a fake runner (`test/BF2WebAdmin.Tests/ScriptModuleTests.cs`) and against a fake
  module over a real link (`ScriptRunnerTests.cs`).
- **On a game server**, change the script, publish WebAdmin and start the script again: the game server keeps running.
  `script.log` is the print statement. There is no debugger: a breakpoint would stop the game server's main thread.
- **Without a player**, a chat command can be sent through the web API: `POST /api/servers/{id}/chat` with
  `{"message": ".nasa 100"}` and the API key in the `X-API-KEY` header (as the localtest's `tools\console.ps1` does for
  `/rcon`). `.nasa` is `PracticeModule`'s, which loads with `Heli2v2Module`; `.dance` is on every server.
- **A script alone, without WebAdmin**: openbf2-server-ext's
  `tools/adminclient/adminclient.py --scenario runscript --script-file GameScripts/dist/<name>.js` starts it on a test
  server and prints its events, errors and CPU time.

## What was tested on a game server

On 2026-11-14, with this WebAdmin (`f2ca979`) and the module of openbf2-server-ext `a0ba53b` on a WSL test server with
a bot helicopter hovering at 213.9 m (the setup and all numbers are in openbf2-server-ext `docs/findings/scripts.md`,
"End to end with BF2WebAdmin"):

| Command | What happened |
|---|---|
| `.nasa 60` | The script held the helicopter at 200.15 m (the ceiling is 200 m: the command's 60 plus the 140 m offset) while its pilot kept climbing. It cost 25 µs of the game server's CPU per tick |
| `.nasa 50`, `.nasa 0` | A message moved the ceiling to 190 m without a new script; 0 stopped the script and the helicopter climbed back |
| `.dance` | Position held within 0.01 m, one turn of yaw a second, back level at the old yaw after 5 s |
| A map change with `nasa` running | The script ended with `LevelUnload`; the server went on |
| The same against a module with `adminAllowScripts 0` | No script was started; `.nasa 60` teleported the helicopter back to 200.0 m four times a second (it climbed to 200.5 m in between), `.dance` used the old loop |

Not tested: what a player sees (there was no human client), more than one player, a link that drops while a script
runs (only in the unit tests), and the production servers, whose modules are older and get the fallback until they
are updated.
