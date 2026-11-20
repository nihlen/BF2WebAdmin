# Kill replay (`.replay`)

`.replay` replays a kill shot in the game, at the place it happened. WebAdmin spawns objects for it and the game script
`replay` moves them every tick; the players watch from wherever they are (a dead player can fly the free camera there).

| Command | Replays |
|---|---|
| `.replay`, `.replay <speed>` | In a 2v2 match, between rounds: the match's last kill, if a TV missile made it, with the missile and the round's four helicopters (empty, without collision) where they were. Without a match: your own last TV missile, the missile alone |
| `.replayhit`, `.replayhit <speed>` | The same, with the helicopters the trace has where the server tested the hit: rewound by the shooter's lag compensation |

The speed is 0.05 to 4 (1 is real time, 0.5 half speed). The replay holds the first frame for half a second, plays to
the impact and holds the impact for ten seconds when the missile hit a vehicle (`KillReplay.HitsVehicle`: its first
collision was a vehicle the trace follows or a part with a player in it), for two seconds when it hit the terrain or a
static object or nothing; then the objects are removed. One replay runs at a time, never during
a round. `ReplayModule` (`Modules/Heli2v2/`) loads with `Heli2v2Module`; anyone may use it.

## Where the frames come from

- **The missile**: its `ProjectileTrace` (openbf2-server-ext `docs/admin-channel.md`, "Hit/miss replay"), kept by
  `ViewerFeed` for the last 20 TV missiles: its position every tick, its direction from its velocity. The frames end at
  its first collision, at the collision point.
- **The helicopters**: the trace has every vehicle within 1000 m of the launch except the shooter's own, every tick. The
  shooter's helicopter comes from the player stream (`ViewerFeed.Poses`, `RootPoseHistory`, the last 30 seconds),
  blended between its frames: at the tracker rate, every 250 ms by default, every tick while a 3D viewer is open.
- **The kill**: `ReplayModule` notes the last kill of a round's player; its missile is the killer's traced hit whose
  flight covers the kill's time. `KillReplay.Build` makes the frames.

## What the game server needs

- Script API 2 (`game.teleportObject`, `game.activateProjectile`): openbf2-server-ext `f295284` and `badb3c0`, built
  against OpenBF2 with `activateProjectileOnClients` (R9, `97977b0`). A module with API 1 refuses the script and the
  player is told the module is too old.
- `adminAllowObjects 1` (SpawnObject, DeleteObject) and `adminAllowScripts 1`.

## How it shows on the clients

From the engine's source; not yet seen with a game client:

- Spawned helicopters and the TV missile are ghosted with linear prediction: a client draws them 100-200 ms behind,
  blended between the poses it receives (up to 20 a second), so a replay moves smoothly, in slow motion too. While held,
  the script moves everything a millimetre every other tick: a pose that does not change is not sent, and a client
  would go on moving the object at its last speed for a moment.
- The missile's trail and sound start when the replay starts moving (`game.activateProjectile`). The trail's smoke
  lives 3 s of real time, so at a low speed it is shorter, and it fades during the hold; no effect can be frozen.
- The rotors of the empty helicopters stand still: a client turns a rotor only for a vehicle it saw a player enter.
- The missile cannot hurt anyone: the server's copy is never activated, and clients never detonate on their own.
- Hydra and S-8 rockets are not replayed: they have no networkable, so a spawned one reaches no client.
