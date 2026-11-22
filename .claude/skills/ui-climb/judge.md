# Judge brief: review one build of the BF2WebAdmin web UI

The orchestrator fills in `{persona}`, `{judgeId}`, `{model}`, `{shots}` (a folder) and `{output}` (a JSON file), and
sends everything below the line as the subagent's prompt.

---

You are reviewing the web UI of **BF2WebAdmin**, a web admin for Battlefield 2 game servers. Its users are server
owners and their admins: they watch live matches (mostly 2v2 helicopter duels on small maps, and some public servers),
chat with players, warn, kick and ban, change maps, restart rounds, check the server's health (tick timing, CPU, the
players' connections), manage the chat commands' modules and player auth levels per server group, place objects on the
running map with the map builder, and watch the match in a 3D viewer. They set it up once (log in, add a server, give
the game server the admin key) and then use it during matches, on a desktop or a phone.

**Your persona:** {persona}

Judge the product as that person would, against the best tools you know for similar jobs: BattleMetrics RCON and
Pterodactyl (game server admin), Grafana (health and charts), Linear and Vercel's dashboard (navigation, craft,
density), Discord's server settings (setting up groups, roles and bots).

## What you have

- `{shots}/checks.json`: the list of shots (`results[]`: `id` such as `server--desktop`, the `path`, `viewport`
  desktop 1440 px or phone 390 px, `file`, and `parts`: viewport-high slices of long pages) plus measured checks per
  shot (console errors, horizontal overflow in px, axe-core accessibility violations, failed capture steps).
- The PNG files in `{shots}`. Read every shot. For a long page, read its `parts` (they are readable) instead of the
  full-page file. All shots use the same fixed demo data.
- Striped panels labelled "3D view (WebGL, not scored)" cover the 3D views of the 3D viewer and the map builder: do
  not judge what is under them, judge everything around them (panels, controls, layout, how the page is organised).
- You may read the UI's source in `src/BF2WebAdmin.Web/src` to learn what a control does or what exists behind a
  menu. Read nothing else in the repository, and nothing under `artifacts/` except `{shots}`.

Do not try to find out whether this build is old or new, or how it scored before: there is nothing to compare with,
judge it on its own. Do not edit any file except `{output}`.

## How to score

Score 0-10 with one decimal, where 10 is as good as the best tool you know for that job, 7 is decent but clearly
behind it, 5 is usable with real problems, 3 is hard to use. Be strict and specific: a score without a concrete reason
is useless. Base it on what you see, and say when you are unsure.

Score each **area** on five criteria:

- `clarity`: can the user tell what this is, what state things are in and what to do next
- `density`: the right amount of information per screen, no wasted space, no crowding
- `efficiency`: how few steps and how little hunting the common tasks take
- `feedback`: loading, empty, error, offline, success and confirmation states
- `craft`: alignment, spacing, typography, colour, consistency, polish, the phone layout

Areas (each covers the shots whose id starts with it): `login`, `dashboard` (with `dashboard-empty`), `setup`
(`server-add`, `server-add-tested`, `server-edit`), `server-group`, `server` (every `server` and `server-*` shot,
except the setup ones above), `viewer`, `builder`, `navigation` (the sidebar, `nav-open`, and moving between pages).

Then score the **app** as a whole: `structure` (is the information architecture right: which pages exist, what is on
which page, how the server page, the map builder and the 3D viewer relate, where settings, modules and auth live),
`setupFlow` (from the first login to a connected, configured server), `consistency`, `mobile`.

## Changes

Rank up to 15 changes that would raise the scores most, most valuable first. Big ones are welcome when the
structure is the problem: merging or splitting pages, moving things between pages, a different navigation, a
different setup flow. Each change: what is wrong (on which shots), what to do instead, concretely enough for a
developer to build it, and its scope:

- `frontend`: can be done in the web UI with the data the pages already have
- `backend`: needs new data or a new endpoint from the server (say what)

## Output

Write `{output}` as JSON, exactly this shape, then answer with one line: your overall score and your top change.

```json
{
  "judge": "{judgeId}",
  "model": "{model}",
  "areas": [
    { "area": "server", "clarity": 7.0, "density": 6.5, "efficiency": 6.0, "feedback": 7.5, "craft": 6.5, "overall": 6.7,
      "notes": "Two or three sentences with the main reasons, naming the shots." }
  ],
  "app": { "structure": 6.0, "setupFlow": 5.5, "consistency": 7.0, "mobile": 5.0, "overall": 6.0, "notes": "..." },
  "overall": 6.3,
  "strengths": ["what works and must be kept", "..."],
  "changes": [
    { "rank": 1, "title": "Short imperative title", "shots": ["server--desktop"], "problem": "...", "proposal": "...",
      "scope": "frontend", "size": "S|M|L" }
  ]
}
```
