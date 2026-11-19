# Map builder

The map builder places, moves and removes static objects, spawn points and helipads on a running server, and saves what
is built as a layout of the map. The web UI's builder is the page `/server/<id>/builder` (the "Map builder" button on a
server's page). The in-game commands `.o`, `.undo`, `.clear`, `.save` and `.load` (`MapModule`) act on the same
objects.

## What a player notices

- **Adding** spawns the object at once on the server and on every client.
- **Removing** deletes it on the server at once, so collisions and hits are right from then on. Clients keep their own
  copy of a spawned static object until they are resynced: the admin channel's `ResyncClient` (openbf2-server-ext
  `docs/admin-channel.md`, "Client resync") makes a client drop every spawned object and get them again from the
  server, without a round restart. WebAdmin resyncs each human client once per apply.
  - A resync is a short hitch for the player: about one round trip plus 60 ms without state for its own vehicle. The
    spawned objects come back at 120 a second.
  - It waits while the player is dead: the client would drop its spawn point selection.
  - It waits while the player has a missile in the air: the gunner would lose its guidance.
  - The page shows the players still waiting ("Reloading objects for N players").
- **Moving** is removing and adding.
- **A round restart** destroys every spawned object; WebAdmin spawns what is built again a second after the round
  starts. A new map clears the builder (its layouts stay).

A module without `ResyncClient` (capability bit 7) can only add: the page says "Add only", Apply refuses moves and
removals of objects and helipads, and Clear and the in-game `.undo` restart the round as `.clear` always did (the scores
are put back afterwards).

## The page

- The 3D view is the 3D viewer's world (`maps/<map>/map.glb`, BF2v2 `tools/replay-map/build_map.py`). A map without
  one gets a flat ground at the default sea level. The maps, helicopter models and object templates are not in git:
  they are made of the game's files and load from `https://static.nihlen.net/bf2/viewer/v1/` (`VIEWER_ASSETS` sets
  another base; tools/builder-assets/README.md).
- The keys are HeliGame's build mode's:
  - Holding the right mouse button looks around and flies with WASD, with Q and E for down and up.
  - Without the right button, W picks the move tool and E the rotate tool.
  - Space and Ctrl go up and down, the arrow keys fly, Shift is faster and Alt slower, either way.
  - Ctrl while dragging the gizmo snaps to 1 m and 15°.
  - End stands the selection on the ground, F flies to it, Del removes it, Esc deselects.
  - Ctrl+W closes the tab and no page can stop it, so with unapplied drafts the page asks before it closes.
- The left button selects and drags the gizmo. An object, spawn point or helipad dragged from the lists onto the map
  goes on the surface under the pointer; a click on it instead puts it on the surface in the middle of the view. Either
  way the new draft is selected, ready to move or turn.
- Every change is a draft on this browser until Apply, which sends them in one request:
  - drafts are outlined amber, removals red, items that are built but not in the game (their spawn failed) purple;
  - an apply made against an older state of the builder (another admin applied first) is refused with 409, and the
    page reloads the state and keeps the drafts.
- The tabs:
  - **Objects**: the template palette, from `templates/catalog.json` with one GLB per template, built by
    `tools/builder-assets` (its README has the command, the catalog's format and the frame convention). It shows only
    the templates the running level uses (below, "Only the level's templates"). Thumbnails are rendered in the
    browser. A placed object stands on its base: most meshes are centred on their bounds, so the pivot goes the
    catalog's `-min[1]` above the surface.
  - **Spawns**: helipads and spawn points per team. A helipad comes with its own spawn point into its helicopter;
    spawn points are optional extra places to spawn on foot, each a place of its own on the spawn screen. Players can
    pick them at once; with an older module, one they cannot pick yet is marked "next round" (`!admin.restartMap` in
    the chat makes a round start).
  - **Built**: everything built and drafted.
  - **Layouts**: save, load and delete layouts of the map.
- The players are drawn where they are (the server page's 250 ms positions).
- **Chat** (the header's toggle) shows the server's chat beside the 3D view, to read and write while building. The
  button counts the messages that came while it was hidden; this browser remembers whether it was open.

## Engine objects

`BuilderSpawning` (`src/BF2WebAdmin.Server/Builder/`) makes each kind of item:

| Kind | Made of | Removed by |
|---|---|---|
| Object | `SpawnObject` of the template, after console lines that give the template a networkable info with a negative base priority that only the server has: clients create the object from their own template (it reaches them once, as a static ghost). A template that still has no networkable is deleted again and reported: no client would see it | `DeleteObject`, then a resync |
| Spawn point | console lines: a `SpawnPoint` template of its own with `setControlPointId` of the team's control point nearest to it and a `group` of its own (below), `Object.create`. The player picks a spawn group on the spawn screen and the server picks one of the group's points at random, so a point in the control point's own group 0 would rarely be the one (Dalian 2v2's bases have four each); alone in its group it always is | `DeleteObject`; no resync (spawn points are server only) |
| Helipad | `concreteblock_helipad` as an object (its tweak adds the repair and rearm, for the team of the nearest control point); an `ObjectSpawner` of its own for the team's helicopter (the item's template; `ahe_z10` for team 1 and `ahe_ah1z` for team 2 by default); and a spawn point on the pad like the above with `setEnterOnSpawn 1` and `setAIEnterOnSpawn 1`, so a player who picks it always gets the helicopter: the engine seats a player spawning there in the nearest free seat within 15 m, or leaves it on foot on the pad when the helicopter is away. All made with console lines | the spawner first, then the helicopter if it is on the pad and empty (an occupied one is left; without its spawner it decays), then the pad and the spawn point, then a resync |

**Spawn groups.** Each spawn point and helipad has a group number from 1001 up, on the team's nearest control point.
The engine keeps a group for each (control point, number) pair until the map changes, and a removed item's group stays
there empty (the spawn screen hides it). The builder gives a free number already used on the same control point to the
next item there: that group has its team, so the item works at once. It makes at most 48 groups per level and refuses
more until the map changes.

### Only the level's templates

The server knows every object template, so it spawns any of them, but a template the level does not use can crash the
game: the clients load what the level needs. So the builder spawns only templates the running level placed objects of,
for the web page and the in-game `.o` alike (`LevelTemplates`):

- A template counts when `ListObjects` finds an object of it that the builder did not spawn. Objects in the object
  cache, at 0/0/0, are not counted, because a deleted object is pooled there with a new console id.
- Each answer is kept until the map changes.
- The page sends its catalog's names to `POST templates` once per map. The first call asks about all 852, 16 at a
  time, which took 1.8 s on Dalian.
- A helipad needs `concreteblock_helipad` on the level, and a helicopter the level has an object of now. A no for a
  helicopter is asked again next time, in case they were all destroyed just then.
- On `dalian_2_v_2` (2026-11-18), the server's 128 templates were exactly the catalog's templates among the 133 that
  the level's `StaticObjects.con` creates.
- A WebAdmin restarted in the middle of a level does not know what an earlier run spawned. Objects that run left in
  the game count as the level's until the next map.

Rules found in OpenBF2's source (verified on a test server unless noted):

- Spawn points and spawners must be made with the console's `Object.create`, never `SpawnObject`: `SpawnObject` goes
  through the object cache, which pools a deleted object instead of destroying it, so a spawn point would stay
  registered at the origin.
- The object cache does pool the static objects and helicopters that `DeleteObject` removes: they stay in the grid under
  the terrain at 0/0/0 with a new console id, and the next spawn of that template may reuse them. They are out of play.
- A new spawn group has no team until its control point's team is set (`ControlPoint::setTeam`), which the engine does
  at every round start. Until then no player can pick it, and it shows on no spawn screen. So a new spawn point or
  helipad would work only from the next round restart on; the group stays for the rest of the level, and the builder
  makes the point again after every restart. Nothing on the console sets a control point's team (Python's
  `cp_setParam(cp, 'team', n)` does, but WebAdmin runs no Python), so the admin channel has `SetControlPointTeam`
  (openbf2-server-ext `2a920c0`, a contract draft, capability bit 7 with `ResyncClient`): right after making a point in
  a group without a team, the builder sets the control point to the team it has. That sends no HUD event
  (`ControlPoint.cpp:318-331`) and gives the control point's groups and spawners its team (`:374-388`), the new group
  too, so players can pick it at once. A module without it leaves the item marked "next round".
- A point in an existing group (the control point's group 0, which has its team) works at once, but a player who picks
  the group gets one of its points at random (`SpawnGroup::getSpawnPoint`, `rand() % n`); the spawn screen shows the
  group at the average of its points. That is how the first spawn points were made; a test on 2026-11-18 spawned on
  the base's own points.
- Creating control points at runtime breaks clients (their template ids differ), so every point uses an existing one.
- Spawn groups: each (control point, group) pair takes a group id for the rest of the level. More than 63 static groups
  collide with squad and vehicle groups, and the engine aborts past 127; the level uses a few.
- A spawner's team must be 1 or 2 (the engine indexes three-element arrays with it). Spawners outlive round restarts;
  spawn points and spawned objects do not.
- Template names made by WebAdmin carry a random prefix per process (`bf2waXXXX_sp_<id>`, `bf2waXXXX_heli_<id>`):
  templates last until the map changes, and one from an earlier run keeps its settings.

Verified on 2026-11-17 on a localtest test instance (OpenBF2 `2324a5d`, module `1bccc5d`) without a human client:
objects, spawn points and helipads were added, moved and removed; a helicopter spawned on a new pad and none came back
after its removal; a standing bot of team 2 spawned on a new spawn point; bots filling team 2's spawns did not use a new helipad's group
before a round restart and were seated in its helicopter after one; layouts saved, refused a duplicate name,
cleared and loaded; a round restart was followed by the built items spawning again. What a retail client sees of a
resync is measured in the contract (above); the builder's own resyncs have not been run with a human client yet.

## Layout votes

Any player can start a vote on loading a layout of the running map with `.vote <name>` (or `.votemap`), and a user of
another site with `POST /api/servers/{serverId}/builder/layouts/{layoutId}/vote` (BF2v2's layout list; below). Admins
load a layout directly with `.load` or the page. `LayoutVoteModule` runs the vote ([admin-channel.md](admin-channel.md),
"Layout votes"):

- The players see "VOTE: Load layout '<name>'? (PgUp yes / PgDn no, 30 s left)" and answer with PgUp or PgDn. A vote
  lasts 30 s.
- It passes with enough yes votes and more yes than no:
  - 2 yes, or 1 when a single player is on the server, and every human player on a team votes;
  - 3 yes while a 2v2 match is played (a running match with at least three of its players on the server), and only
    the match players vote.
- It ends as soon as its result is certain; WebAdmin then announces "VOTE PASSED" or "VOTE FAILED" with the counts,
  and loads the layout when it passed.
- One vote runs at a time, and whoever started one (a player by CD-key hash, a web user by id) waits two minutes
  before the next.
- Refused: a layout of another map, nobody on the server, and a server whose module has no client votes.
- `.vote default` (or `clear`, `reset`) votes the same way on removing what is built, as an admin's `.clear` does, so
  players can leave a layout they no longer want; refused when nothing is built. No layout may have these names.

The ballots have been tested only without a retail client (the module refuses a vote with no human player); a vote with
players pressing PgUp and PgDn has not been run yet.

## API

Under `/api/servers/{serverId}/builder` (the admin cookie), in `Builder/BuilderEndpoints.cs`:

| Request | Does |
|---|---|
| `GET /` | the state: map, revision, whether connected and able to resync, the loaded layout, the items, the players waiting for a resync |
| `POST /apply` | `{revision, add, update, remove}`: drafts made against `revision`; 409 when the builder has changed since |
| `POST /clear` | removes everything; `true` when it had to restart the round |
| `POST /templates` | a list of template names (at most 4096): those the running level uses, the only ones that can be built |
| `GET /layouts` | the layouts of the running map, and those from before the builder (no map), offered on every map |
| `POST /layouts` | `{name, overwrite}`: saves what is built; 409 for an existing name without overwrite |
| `POST /layouts/{id}/load` | replaces what is built with the layout |
| `DELETE /layouts/{id}` | deletes the layout |
| `POST /layouts/{id}/vote` | `{starterId, starterName}`: starts a players' vote on loading the layout ([Layout votes](#layout-votes)); 204, or 409 with the reason it did not start |

Positions are BF2's `[x, height, z]` (z north), rotations `[yaw, pitch, roll]` in degrees. The hub sends
`BuilderEvent` with the state to a server's subscribers whenever it changes.

Layouts are the `MapMod` and `MapModObject` tables (`map_mod`, `map_mod_object` on Postgres). The builder added
`MapMod.Map` and `MapModObject.Kind` and `Team` (migration `MapBuilder` for each provider). A layout saved in game before
has no map and is listed on every map.
