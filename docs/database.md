# Database

WebAdmin keeps its servers, modules, player auth levels, map mods, recorded matches and player joins in one database.
Three are supported:

| Provider | Data access | Schema | For |
|---|---|---|---|
| `sqlite` | EF Core, `BF2Context` | EF migrations in `src/BF2WebAdmin.Data/Migrations`, applied at startup | small installs, development |
| `postgres` | EF Core with Npgsql, `PostgresBF2Context` | EF migrations in `src/BF2WebAdmin.Data/Migrations/Postgres`, applied at startup | production |
| `sqlserver` | Dapper | `src/BF2WebAdmin.Data/Schema.sql` for a new database; `SqlServerSchemaUpdates` applies later changes at startup | the old production setup, kept as the fallback |

## Configuration

```json
{
  "ConnectionStrings": {
    "BF2DB": "Host=db.example.com;Port=5432;Database=bf2webadmin;Username=bf2webadmin;Password=<secret>"
  },
  "Database": {
    "Provider": "postgres"
  }
}
```

As environment variables: `ConnectionStrings__BF2DB` and `Database__Provider`.

`Database:Provider` is `sqlite`, `sqlserver` or `postgres`. Without it the connection string decides, as before:
`.sqlite` in it is SQLite, a `Host=` key (Npgsql's name for the server) is Postgres, anything else is SQL Server. Set the
provider explicitly in production.

| Provider | Example connection string |
|---|---|
| `sqlite` | `Data Source=BF2WebAdmin.sqlite;Cache=Shared` |
| `postgres` | `Host=db.example.com;Port=5432;Database=bf2webadmin;Username=bf2webadmin;Password=<secret>` (add `SSL Mode=Require` when the server has TLS) |
| `sqlserver` | `Server=sql.example.com;Database=bf2;User Id=bf2webadmin;Password=<secret>;TrustServerCertificate=true` |

The Postgres login must own the database (or be allowed to create tables in it): WebAdmin runs its migrations at
startup and stops if it cannot.

## The Postgres schema

Database `bf2webadmin`, `snake_case` singular table names, `timestamptz` in UTC, `text` columns:

| Table | Notes |
|---|---|
| `server`, `server_module`, `server_player_auth`, `map_mod`, `map_mod_object` | As on the other providers |
| `match` | `change_seq` (the export cursor, unique, not null) comes from sequence `match_change_seq`, taken under WebAdmin's match write lock. Index on `match_start desc` |
| `match_round` | `match_id`, `round_start` and `round_end` are required. Index `(match_id, round_start)` |
| `match_round_player` | No `match_id` (a player reaches its match through the round) and no paths. Index on `player_hash` |
| `match_round_player_path` | One row per player per round with a path: `movement_path` and `projectile_paths`, the gzip bytes of the JSON as `bytea`. Read only where a whole match is needed (the export, resuming a match), never by lists |
| `player_join` | The player joins (`JoinHistory` on the other providers), `id bigint` from an identity, no `is_likely_spoof` (BF2v2's, not WebAdmin's) |

In C# the paths stay `MatchRoundPlayer.MovementPathJson` and `ProjectilePathsJson`, base64 strings of the same gzip
bytes; `MatchRepository` converts them.

## Migrations

The two EF providers have their own migrations, one context each:

```powershell
cd src\BF2WebAdmin.Data
dotnet ef migrations add <Name> --context BF2Context --output-dir Migrations                   # SQLite
dotnet ef migrations add <Name> --context PostgresBF2Context --output-dir Migrations/Postgres   # Postgres
```

A model change usually needs both. The Postgres-only shape (table names, the path table, the sequence, required
columns) is in `PostgresBF2Context.OnModelCreating`; SQLite keeps its schema. A change for SQL Server goes into
`Schema.sql` and, for existing databases, into `SqlServerSchemaUpdates`.

## Copying a SQL Server database to Postgres

`--db-copy-from` copies WebAdmin's SQL Server database into the configured Postgres database and exits:

```powershell
$env:ConnectionStrings__BF2DB = "Host=db.example.com;Database=bf2webadmin;Username=bf2webadmin;Password=<secret>"
$env:Database__Provider = "postgres"
BF2WebAdmin.Server.exe --db-copy-from "Server=sql.example.com;Database=bf2;User Id=<user>;Password=<secret>;TrustServerCertificate=true"
```

```sh
docker run --rm \
  -e ConnectionStrings__BF2DB="Host=db.example.com;Database=bf2webadmin;Username=bf2webadmin;Password=<secret>" \
  -e Database__Provider=postgres \
  -e ConnectionStrings__CopySource="Server=sql.example.com;Database=bf2;User Id=<user>;Password=<secret>;TrustServerCertificate=true" \
  nihlen/bf2-webadmin:<version> --db-copy-from
```

Without a value after the flag, the source is `ConnectionStrings:CopySource` (env `ConnectionStrings__CopySource`), which
keeps the password out of the command line and the shell history. `appsecrets.json` is optional for this command.

What it does:

1. Applies the Postgres migrations to the target, then refuses to continue unless every table is empty.
2. Copies, in one transaction, with binary `COPY` in batches, streaming row by row (it never holds a table in memory):
   `Server`, `ServerModule`, `ServerPlayerAuth`, `MapMod`, `MapModObject`, `Match`, `MatchRound`, `MatchRoundPlayer`
   (the paths decoded from base64 into `match_round_player_path`) and `JoinHistory` into `player_join`. Every id stays:
   match, round and map ids, the join ids, and every match's `ChangeSeq`.
3. Moves `match_change_seq` above the highest `ChangeSeq` (and above the SQL Server sequence `MatchChangeSeq`) and
   `player_join`'s identity above the highest join id.
4. Counts every target table and prints the source, skipped, copied and target rows per table. It commits only when
   they all match; otherwise it rolls back (the target stays empty) and exits with code 1.

The source is only read. Data quirks are handled and listed as notes in the output:

| Quirk | Handling |
|---|---|
| Matches without `ChangeSeq` (a database WebAdmin 182bf9b or later never started on), or no `ChangeSeq` column at all | Numbered as WebAdmin's startup update would: in start order, above the highest number. Without the column a consumer of the export has to start from 0 |
| A round whose match is missing | Left out, with its players and paths |
| A round without `RoundStart` or `RoundEnd` | The missing one is set from the other (or the match start). Left out if there is no time at all |
| A player whose `MatchId` differs from its round's | Copied with its round (`match_round_player` has no `match_id`) |
| A path that is not base64 | Dropped; a player without any readable path gets no path row |
| NUL characters in text | Removed (Postgres text cannot hold them) |
| Null text in `JoinHistory` | Written as an empty string (`player_join`'s columns are NOT NULL); a join without a date is left out |
| `JoinHistory.IsLikelySpoof` | Not copied (BF2v2 keeps its own spoof list) |

Speed on a local test (LocalDB to Postgres 18 in Docker on the same machine, 20,000 round players with 1.45 GB of base64
paths in SQL Server): 29 s, 18.6 MB/s of decoded path bytes. The paths are about three eighths of their size in SQL Server
(UTF-16 base64 becomes bytes), so an 8 GB database copies in a few minutes plus the network.

## Switching production from SQL Server to Postgres

A generic runbook. Replace the host names, the image version and the secrets with your own.

1. **Before.** Run a WebAdmin version with the export API (182bf9b or later) on SQL Server once, so that every match
   has a `ChangeSeq`. Create the empty Postgres database `bf2webadmin` and its owner login. Have a backup of the SQL
   Server database.
2. **Rehearse.** Restore a recent backup of the SQL Server database somewhere else, copy it into a scratch Postgres
   database with `--db-copy-from`, read the notes, and start a WebAdmin against that database (with another admin
   channel port and no game servers) to look at the matches in the web UI and the export.
3. **Stop WebAdmin.** The copy needs a database nobody writes: stop WebAdmin (scale it to 0) and anything else that
   writes the SQL Server database. Matches played meanwhile are not recorded; joins of players connecting meanwhile are
   missed.
4. **Copy.** Run `--db-copy-from` with the production SQL Server as the source and the empty `bf2webadmin` as the
   target (the WebAdmin image, a one-off container or job). Check that it ends with `Committed.` and exit code 0, and
   keep its output.
5. **Switch.** Set WebAdmin's `ConnectionStrings__BF2DB` to the Postgres database and `Database__Provider=postgres`,
   and start it. It logs `Database: Postgres` and `Running Postgres migrations`.
6. **Check.** The web UI lists the servers and the recent matches; the game servers connect; a new match gets a
   `changeSeq` above the copied ones (`GET /api/export/v1/matches?since=<the highest before>`); a player connect adds a
   `player_join` row with the next id. A consumer of the export keeps its cursors: the copy keeps every `changeSeq` and
   join id.
7. **Rollback.** Point `ConnectionStrings__BF2DB` back at SQL Server (and `Database__Provider=sqlserver`) and restart.
   SQL Server has nothing written after the switch: matches and joins recorded on Postgres are not in it, and its
   `changeSeq` numbers may repeat ones the consumers already read on Postgres, so a consumer should read from 0 again
   after a rollback. Keep SQL Server, unused, until Postgres has run for a while.
