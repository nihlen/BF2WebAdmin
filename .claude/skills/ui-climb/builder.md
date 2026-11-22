# Builder brief: make one round's changes to the web UI

The orchestrator fills in `{round}`, `{branch}`, `{plan}`, `{run}` and `{report}`, and sends everything below the
line as the subagent's prompt.

---

You are improving the web UI of BF2WebAdmin (`src/BF2WebAdmin.Web`: Vite, React 19, TypeScript, TanStack Query and
Router, shadcn/ui on Base UI, Tailwind CSS 4; read its `README.md` and the repository's `CLAUDE.md` first). Reviewers
scored screenshots of the UI and ranked changes; `{plan}` lists the ones for this round ({round}). Make them, well.

You are on git branch `{branch}`; stay on it. Nobody can answer questions: decide yourself, and write down what you
decided and why.

## Scope

- Change only `src/BF2WebAdmin.Web`. Pages, components, navigation, routes, layout, copy, styling: all allowed,
  including bigger restructuring (merging or splitting pages, a different navigation or setup flow) when the plan
  asks for it.
- The backend (`src/BF2WebAdmin.Server` and the other .NET projects) must not change. When an item needs data or an
  endpoint the API does not have, build what you can without it and append a suggestion to
  `{run}/backend-suggestions.md` (round, what the UI needs, the endpoint or field, why).
- Use only the web API endpoints in `src/api/schema.d.ts` and the hub events in `src/api/events.ts`. Do not change
  `schema.d.ts`, `openapi.json` or `events.ts`.
- The UI must keep working against the real server, not only against the fixtures: keep the data flow of
  `src/api/hub.ts`, `src/hooks/use-server-live.ts` and `src/hooks/use-servers.ts` (subscriptions, the snapshot
  refetch after reconnects, the cache updates from hub events). Moving their callers is fine.
- No new npm dependencies. shadcn/ui components may be added to `src/components/ui` (copy them in the project's
  style; the existing ones show it).
- Keep the code's style: comments say why, in plain sentences; no dead code.

## The test bed

The screenshots come from the fixture mode: `UI_FIXTURES=1 npx vite --port 5199 --strictPort` serves the API from
`src/fixtures/data.ts` and fakes the hub (`src/fixtures/signalr.ts`). Start your own on a free port (for example
5210) in the background, pass `--base http://localhost:5210` to the capture, and stop it when you are done; port 5199
is the orchestrator's. `node scripts/ui-capture.mjs --out <dir>` (from `src/BF2WebAdmin.Web`)
takes the screenshots; use a scratch folder under `{run}/{round}/scratch` to look at your work (read the PNGs), never
the round's `shots` folder.

- You may add fixture data for endpoints that exist, when a page you build needs it. Do not change or remove the
  existing data to make a page look better: the shots must keep showing the same situation (including the bad health
  windows, the lossy player, the offline server, the long server name).
- `scripts/ui-capture.mjs` defines the shots. Keep every shot. When you move or rename things, update the steps so
  each shot still shows the same state (the same tab, dialog or sheet). When you add a page, add a shot for it (desktop
  and phone).

## Done

1. `npm run build` passes (type check and build) and `npm run lint` shows no errors (warnings that were there
   before are fine).
2. A capture into your scratch folder runs with no console errors and no failed steps, and you have looked at the
   shots of what you changed, on desktop and phone.
3. Commit on `{branch}` with explicit paths (`git add <paths>`, never `-A`), message `ui-climb {round}: <summary>`,
   one commit or a few.
4. Write `{report}` (Markdown): each plan item with done / partly / not done and why, the structural decisions you
   made, anything you noticed but left alone, and the backend suggestions you added. Answer with a two-line summary.
