---
name: ui-climb
description: Improve the web UI (src/BF2WebAdmin.Web) in rounds - screenshots of fixed fixture data, a blind council of Claude judges that scores them and ranks changes, a builder that makes the top changes, and a blind A/B vote that keeps or drops each round. Use when asked to run, resume or report on the UI review loop.
---

# UI climb: the web UI improved in judged rounds

A hill climb on the web UI, after Theo's "Agents Sucked At UI (Until Today)" and the write-ups of people who
repeated it, with Claude models only. Each round: judges score screenshots of the current build and rank changes, a
builder makes the top ones, a blind A/B vote between the old and the new build keeps or drops the round. Every round
is kept (git tag, screenshots, scores) so a person can pick the one they like best afterwards.

You, the session running this skill, are the **orchestrator**: you never edit UI code yourself. You run the scripts,
start the judges and builders (Agent tool, `run_in_background: false`; several judges in one message run at the same
time), merge their reports into a plan, decide by the rules below, and keep the run's files and gallery current.

## The pieces

| Piece | Where |
|---|---|
| Fixture mode: the dev server answers the API from fixed data, the hub is faked, no backend needed | `src/BF2WebAdmin.Web/fixtures-plugin.ts`, `src/fixtures/data.ts`, `src/fixtures/signalr.ts`; `UI_FIXTURES=1 npx vite --port 5199 --strictPort` |
| Screenshots and measured checks (console errors, horizontal scroll, axe-core) | `node scripts/ui-capture.mjs --out <dir>` (in `src/BF2WebAdmin.Web`, needs the fixture dev server) |
| Gallery of a run: scores per round, every shot across rounds side by side | `node scripts/ui-gallery.mjs <run dir>` writes `<run dir>/index.html` |
| Judge brief, A/B brief, builder brief | `judge.md`, `compare.md`, `builder.md` next to this file |

Run files go to `artifacts/ui-climb/<run>/` (ignored by git):

```
state.json                 the run's state; read it first when resuming (after a context compaction too)
log.md                     what happened, one line per step, with times
backend-suggestions.md     backend changes the builders wanted but may not make
index.html                 the gallery
r00/                       the baseline (the build the run started from)
  shots/                   screenshots + checks.json
  judges/<judge>.json      one review per judge (r00 also has <judge>-repeat.json, to measure the noise)
  round.json               round, tag, commit, accepted, summary, items, times
r01/
  plan.md                  the items the builder was given (from the previous accepted build's reviews)
  builder.md               the builder's report
  shots/                   the new build
  ab/X, ab/Y               the old and the new build's shots, under labels that do not say which is which
  ab/<judge>.json          the blind A/B votes
  judges/<judge>.json      the reviews of the new build, when it was accepted (they drive the next round)
  round.json
```

## The judges

Three personas, each in its own fresh subagent (`subagent_type: general-purpose`), told only what `judge.md` says:

| Id | Model | Persona |
|---|---|---|
| `designer` | opus | A strict principal product designer: hierarchy, consistency, typography, spacing, colour, craft |
| `operator` | sonnet | A server admin on duty during a match, often on a phone: how fast the common tasks are |
| `newcomer` | opus for reviews, fable for A/B votes | A first-time owner setting WebAdmin up: login, adding a server, connecting it, modules and auth, and whether the structure (pages, navigation, the server page vs. the map builder and 3D viewer) makes sense |

Fable votes as the newcomer: a model of another family than the other two, so the council does not share one
family's blind spots, at no extra cost per round.

Rules from the people who ran this before, all of them binding:

- **Blind.** Judges never see earlier scores, earlier rounds, the target, the plan or which build is newer. They get
  only the shots, `checks.json`, and the brief. Never mention a score target to anyone.
- **Fresh every time.** A new subagent for every review and every vote; never continue an old one.
- **The A/B vote decides, not the absolute score.** Absolute scores drift and inflate; they are logged for the person
  and used to rank work, never to accept a round.
- **Read-only.** Judges write only their own output file.

## A round

Round N (N >= 1) builds on the last accepted round, A (r00 at the start).

1. **Plan.** Merge the reviews of A (`rA/judges/*.json`) into `rN/plan.md`: the items most judges raised first, then
   high-ranked single-judge items; at most 8 items, frontend only (backend items go to `backend-suggestions.md`). Leave
   out items a rejected round already tried, unless the vote said why it failed and the plan fixes that. Each item:
   the problem, the screens, the judges who raised it, and what done looks like. Structure items (navigation, how pages
   are split, the setup flow) are allowed and welcome when the judges agree on them.
2. **Build.** `git switch -c ui-climb-rNN ui-climb` (from the accepted tip). Start one builder subagent
   (`general-purpose`, model opus) with `builder.md`, the plan path and the run paths. It commits on that branch.
3. **Capture.** `node scripts/ui-capture.mjs --out <run>/rNN/shots`. If the build or the capture fails, give the
   builder one more turn (a new builder with the error); if it still fails, the round is rejected.
4. **Vote.** Copy `rA/shots` and `rNN/shots` to `rNN/ab/X` and `rNN/ab/Y` in a random order (record the mapping only
   in `round.json`). Start the three judges with `compare.md`, in one message.
5. **Decide.** Accept when at least two of the three judges pick the new build overall, and the measured checks are
   not worse (no new console errors or failed steps, no new horizontal scroll, serious+critical axe nodes not up by
   more than 2). Otherwise reject.
   - Accepted: `git switch ui-climb && git merge --ff-only ui-climb-rNN`, tag `ui-climb/rNN`. Fresh reviews
     (`judge.md`, the three judges in one message, on `rNN/shots`) only when the backlog of reviewed changes is used up:
     a round's plan comes from the latest reviews minus what accepted rounds already did. Reviews are the most
     expensive part after the builder; the votes keep each round honest in the meantime.
   - Rejected: tag the branch tip `ui-climb/rNN-rejected`, switch back to `ui-climb`. The next round plans from the
     same reviews of A, minus what was tried.
6. **Record.** Write `rNN/round.json`, a line in `log.md`, update `state.json`, run `ui-gallery.mjs`.

Round 0 is the baseline: tag `ui-climb/r00` on the tooling commit, capture, run the three judges plus `designer` a
second time (`designer-repeat`) to see how much the same judge moves on the same build.

## Stopping

Stop at the first of: the run's deadline (`state.json`; do not start a round that cannot finish by then, a round takes
about an hour), the round limit, or three rejected rounds in a row. Then: kill the fixture dev server, switch back to
`master` (the branch keeps the work), write the gallery, and write `<run>/summary.md`: what each accepted round
changed, the votes, the score trend (with the baseline noise next to it), the backend suggestions, and which rounds
are worth a look.

## Rules of the repository that still apply

- Only `src/BF2WebAdmin.Web` changes, and only on the `ui-climb*` branches. Never touch master, never push, never open
  PRs. Commit with explicit paths.
- No backend changes (`src/BF2WebAdmin.Server`, `.Data`, `.Common`, `.AppHost`, tests): suggestions only.
- No new runtime dependencies. shadcn/ui components may be added to `src/components/ui`.
- The UI must still work against the real server: the fixtures are a test bed, not the contract. The API calls, hub
  events and their handling (`src/api`, `src/hooks/use-server-live.ts`, `use-servers.ts`) keep their behaviour.
