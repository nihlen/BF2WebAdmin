# Database

WebAdmin keeps its servers, modules, player auth levels, map mods, recorded matches and player joins in one database.
Three are supported:

| Provider | Context | Migrations | For |
|---|---|---|---|
| `sqlite` | `BF2Context` | `src/BF2WebAdmin.Data/Migrations` | small installs, development |
| `postgres` | `PostgresBF2Context` (Npgsql) | `src/BF2WebAdmin.Data/Migrations/Postgres` | larger installs |
| `sqlserver` | `SqlServerBF2Context` | `src/BF2WebAdmin.Data/Migrations/SqlServer` | existing SQL Server installs |

All three run on EF Core with the same repositories (`src/BF2WebAdmin.Data/Repositories`, one short context per call);
WebAdmin applies the provider's migrations at startup.

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

`Database:Provider` is `sqlite`, `sqlserver` or `postgres`. Without it the connection string decides:
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
| `server_ban` | Who banned whom and why (`ServerBan` on the other providers), key `(server_id, identifier)`; the bans themselves are the game servers' own lists (docs/admin-channel.md). |

In C# the paths stay `MatchRoundPlayer.MovementPathJson` and `ProjectilePathsJson`, base64 strings of the same gzip
bytes; `MatchRepository` converts them.

## The SQL Server schema

The tables that WebAdmin's SQL Server databases have had since 2022 (`dbo`, singular names: `Server`, `ServerModule`,
`ServerPlayerAuth`, `MapMod`, `MapModObject`, `Match`, `MatchRound`, `MatchRoundPlayer`, `JoinHistory`, `ServerBan`),
with `Match.ChangeSeq` from sequence `MatchChangeSeq`. Until 0.1.0 WebAdmin used Dapper there, with the schema in
`Schema.sql`. The first EF migration (`InitialSqlServer`) creates that schema with statements that check what exists, so
on a database from before EF Core it only adds what is missing (the export cursor and its numbers for older matches,
`ServerBan`, three foreign key indexes) and keeps the data. A `JoinHistory` that is already there stays as it is.
`Server.ServerId` is `nvarchar(20)`, so an `adminServerId` can have at most 20 characters.

## Migrations

Each provider has its own migrations and context:

```powershell
cd src\BF2WebAdmin.Data
dotnet ef migrations add <Name> --context BF2Context --output-dir Migrations                   # SQLite
dotnet ef migrations add <Name> --context PostgresBF2Context --output-dir Migrations/Postgres   # Postgres
dotnet ef migrations add <Name> --context SqlServerBF2Context --output-dir Migrations/SqlServer # SQL Server
```

A model change usually needs all three. The Postgres-only shape (table names, the path table, the sequence, required
columns) is in `PostgresBF2Context.OnModelCreating`, the SQL Server one in `SqlServerBF2Context.OnModelCreating`;
SQLite keeps its schema.
