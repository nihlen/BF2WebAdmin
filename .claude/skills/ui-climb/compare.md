# A/B brief: pick between two builds of the BF2WebAdmin web UI

The orchestrator fills in `{persona}`, `{judgeId}`, `{model}`, `{x}` and `{y}` (two shot folders) and `{output}`, and
sends everything below the line as the subagent's prompt.

---

You are comparing two builds, **X** and **Y**, of the web UI of **BF2WebAdmin**, a web admin for Battlefield 2 game
servers. Its users are server owners and their admins: they watch live matches (mostly 2v2 helicopter duels), chat
with players, warn, kick and ban, change maps, check the server's health, manage chat-command modules and player auth
levels per server group, place objects with the map builder and watch matches in a 3D viewer. They set it up once
and then use it during matches, on a desktop or a phone.

**Your persona:** {persona}

## What you have

- `{x}` and `{y}`: the same shots of each build, with the same fixed demo data. `checks.json` in each lists the shots
  (`results[]`: `id`, `file`, and `parts`, viewport-high slices of long pages that stay readable) and measured checks
  (console errors, horizontal overflow, axe-core violations, failed capture steps).
- A shot can differ in layout or even in which page it lands on when a build reorganised the UI; judge what a user
  gets for the same task.
- Striped panels labelled "3D view (WebGL, not scored)" cover the 3D views: ignore what is under them.

The order X/Y is random. There is no "before" and "after": do not try to work out which build is newer, from files,
dates, git or anything else. Read nothing outside `{x}` and `{y}` except, if you need it, the UI source in
`src/BF2WebAdmin.Web/src`. Do not edit any file except `{output}`.

## How to judge

Look at every shot pair (read the `parts` of long pages). For each area pick the build you would rather ship, or
`same` when the difference does not matter. Areas: `login`, `dashboard`, `setup` (`server-add*`, `server-edit`),
`server-group`, `server` (the other `server*` shots), `viewer`, `builder`, `navigation`. Then pick overall: the build
you would ship to the users above, all things considered. A build that changes more is not better for that alone;
regressions weigh as much as improvements. Name the concrete differences that decided it.

## Output

Write `{output}` as JSON, exactly this shape, then answer with one line: your overall pick and why.

```json
{
  "judge": "{judgeId}",
  "model": "{model}",
  "areas": [ { "area": "server", "pick": "X|Y|same", "confidence": 1, "why": "..." } ],
  "overall": { "pick": "X|Y", "confidence": 1, "why": "Two to four sentences." },
  "regressions": [ { "build": "X|Y", "shots": ["..."], "what": "Something that got worse or broke in that build" } ]
}
```

`confidence`: 1 slight, 2 clear, 3 strong.
