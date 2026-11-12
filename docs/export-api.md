# Export API

A read-only feed of what WebAdmin records, for a statistics site, a Discord bot or anything else that keeps its own
copy: the matches (with rounds, players and movement paths), the player joins and the server list. The consumer polls
with a cursor, so it can be down for a while and catch up, and WebAdmin keeps no state about it. The TV missile
traces of the last ten minutes are a fourth, live-only feed.

Version 1, under `/api/export/v1`. A change that breaks a consumer gets a new version in the path.

## Authentication

The same as the rest of the web API: the `X-API-KEY` header with `Authentication:ApiKey` from WebAdmin's
configuration (or an admin's login cookie). Without a configured key the API is closed to programs.

Responses are JSON (camelCase) and compressed when the request accepts it (`Accept-Encoding: gzip, br`). The match
feed carries the movement paths and is large uncompressed.

## `GET /api/export/v1/matches?since={cursor}&limit={n}`

The matches written after `since`, in the order they were written. `since=0` returns everything from the beginning.
`limit`: 1 to 200, default 50.

```json
{
  "items": [ { "id": "…", "changeSeq": 1043, "...": "…", "rounds": [ … ] } ],
  "next": 1043,
  "hasMore": true
}
```

Read the next page with `since=next` until `hasMore` is false; then keep polling with the last `next`. An empty
page returns `next` equal to `since`.

- Every write of a match gives it a new, higher `changeSeq`: when it starts, when one of its rounds is saved, and when
  it ends (also when WebAdmin closes a match that was left open). A live match therefore comes again after each round.
  **Each item is the whole match**, so replace your copy of it (match, rounds, players) by `id`; reading a match twice
  is harmless.
- The numbers are given and committed in order, so a reader never skips a match that commits late.
- `changeSeq` values are only comparable within one WebAdmin database. After WebAdmin moves to another database they
  may start over; read again from `since=0` then.

### Match

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `changeSeq` | integer | The cursor value of this version of the match |
| `serverId` | string | `ip:port`, the server's id in WebAdmin |
| `serverName` | string | The server's name when the match started |
| `map` | string | |
| `type` | string | `HELI_2V2` |
| `teamAHash`, `teamBHash` | string | The two players' CD-key hashes of the team, joined in ordinal order |
| `teamAName`, `teamBName` | string | |
| `teamAScore`, `teamBScore` | integer | Rounds won. Set when the match ends; 0 while it is live |
| `matchStart` | UTC time | |
| `matchEnd` | UTC time or null | Null while the match is live |
| `rounds` | array | In start order |

A match ends when the next match starts on the server or the 2v2 module stops. One that was left open (WebAdmin
restarted in the middle) is closed by WebAdmin an hour after its last round: `matchEnd` is the end of the last round
and the scores are counted from the rounds.

### Round

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `winningTeamId` | integer | `1` or `2`, a player's `teamId` |
| `positionTrackerInterval` | number | Seconds between two movement samples |
| `roundStart`, `roundEnd` | UTC time | |
| `players` | array | By team, then hash |

### Round player

| Field | Type | Notes |
|---|---|---|
| `playerHash` | string | CD-key hash |
| `playerName` | string | |
| `teamId` | integer | |
| `subVehicle` | string or null | |
| `saidGo` | boolean | |
| `startPosition`, `deathPosition`, `killerPosition` | string or null | `x/height/z` |
| `deathTime` | UTC time or null | |
| `killerHash`, `killerWeapon` | string or null | |
| `movementPath` | array or null | Samples: `"time\|x/height/z\|yaw/pitch/roll\|ping"`, `time` in milliseconds since the round started |
| `projectilePaths` | array or null | One array of samples per fired projectile |

## `GET /api/export/v1/joins?since={id}&limit={n}`

Player joins (a human player connecting to a server) with an id above `since`, in id order. `limit`: 1 to 5000, default
1000. Same page shape as the matches; `next` is the last id. Joins are only added, never changed.

| Field | Type | Notes |
|---|---|---|
| `id` | integer | |
| `name` | string | The player's name with the clan tag |
| `guid` | string | The CD-key hash. Rows written before WebAdmin recorded joins itself may hold other ids (PunkBuster GUIDs) |
| `ipAddress` | string | The player's IP, without the port |
| `serverIpAddress` | string | The server's configured IP |
| `joinDate` | UTC time | |

## `GET /api/export/v1/missiles?since={seq}&limit={n}`

The traced TV missiles (`agm114_hellfire_tv`) of every server from the **last ten minutes**, in the order their traces
arrived. `limit`: 1 to 200, default 50. Unlike the other feeds this one is only kept in memory, for live views:
nothing is stored, a reader that is away for more than ten minutes misses missiles, and a WebAdmin restart starts it
again.

```json
{ "epoch": "…", "items": [ { "seq": 17, "...": "…" } ], "next": 17, "hasMore": false }
```

- `seq` numbers the missiles from 1 within one `epoch`. A new `epoch` means WebAdmin restarted: read again from
  `since=0`.
- A missile comes once, when its flight ends (it hit something or its time to live ran out, 3.5 s on the 2v2
  servers).

### Missile

| Field | Type | Notes |
|---|---|---|
| `seq` | integer | The cursor value |
| `serverId`, `map` | string | |
| `matchId`, `roundId` | uuid or null | The 2v2 match and round being played at `launchTime` (the export's match and round ids); null outside a round |
| `roundNumber` | integer | The round's number in its match, from 1 in start order; 0 outside a round |
| `launchTime` | UTC time | When it was fired (the trace's end minus its ticks) |
| `template` | string | `agm114_hellfire_tv` |
| `shooterHash`, `shooterName` | string | The gunner's CD-key hash and name |
| `shooterTeam` | integer | `1` or `2` |
| `shooterVehicle` | string | The gunner's vehicle template |
| `shooterRoot` | integer | The gunner's vehicle's networkable id: the target with this `netId` is his own helicopter |
| `firstTick` | integer | The server tick of the first sample |
| `flags` | integer | `1` the trace was cut short, `2` it collided |
| `outcome` | string | `hit` (an object or vehicle), `terrain`, `miss` or `cut` |
| `hitPart`, `hitRoot` | string or null | For a hit: the part's and its vehicle's template (the same for the hull) |
| `hitPlayerHash` | string or null | A soldier hit directly |
| `hitRewound` | boolean | The hit part was tested at the rewound pose (the hull, or a soldier) |
| `lagMs` | integer | The shooter's lag compensation at launch |
| `targets` | array | Every vehicle that came within 150 m of the missile |
| `samples` | array | One per tick (30 a second) |
| `collisions` | array | What it touched, in order; the first one stopped it |

Vectors are `[x, y, z]` in BF2 coordinates (y up), rotations `[yaw, pitch, roll]` in degrees.

A **target** is `{ netId, template, team, crew: [{ hash, name, team }], closestTested, closestCurrent, moved }`:
`team` is its crew's (0: none known), `closestTested` and `closestCurrent` the closest the missile's path came to the
target's origin at its tested and at its current pose (metres, -1 without samples), `moved` false for a vehicle that
stood still (parked and empty).

A **sample** is `{ p, v, lag, t }`: the missile's position and velocity at the end of tick `firstTick + k`, the
shooter's lag in seconds, and per target (in `targets` order) 12 numbers: position and rotation now (what the gunner's
client draws), then position and rotation as this tick's sweep tests it (the hull rewound by the lag). The sweep from
sample k to k + 1 meets each target at sample k's tested pose.

A **collision** is `{ tick, kind, flags, root, rootTemplate, partTemplate, partPlayer, p, n, speed, material, latency,
partP, partR, rootP, rootR }`: `kind` 0 terrain, 1 an object; `flags` 1 the vehicle's root part (hull), 2 a soldier, 4
tested at the rewound pose; `p` the contact point and `n` the surface normal; `partP`/`partR` the part's pose as tested.

## `GET /api/export/v1/servers`

All configured servers, without secrets (no rcon password, admin key or Discord settings). Not paged.

| Field | Type |
|---|---|
| `serverId` | string, `ip:port` |
| `serverGroup` | string |
| `ipAddress` | string or null |
| `gamePort`, `queryPort` | integer |
| `isTestServer` | boolean |
