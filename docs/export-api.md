# Export API

A read-only feed of what WebAdmin records, for a statistics site, a Discord bot or anything else that keeps its own
copy: the matches (with rounds, players and movement paths), the player joins and the server list. The consumer polls
with a cursor, so it can be down for a while and catch up, and WebAdmin keeps no state about it.

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
- `changeSeq` values are only comparable within one WebAdmin database. The move from SQL Server to Postgres with
  `--db-copy-from` ([database.md](database.md)) keeps every match's `changeSeq` and every join id and continues above
  them, so a consumer keeps its cursors and does not need to start again from 0. After any other move (or a rollback
  to the old database) they may start over; read again from `since=0` then.

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

## `GET /api/export/v1/servers`

All configured servers, without secrets (no rcon password, admin key or Discord settings). Not paged.

| Field | Type |
|---|---|
| `serverId` | string, `ip:port` |
| `serverGroup` | string |
| `ipAddress` | string or null |
| `gamePort`, `queryPort` | integer |
| `isTestServer` | boolean |
