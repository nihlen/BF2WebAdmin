// The gallery of a UI review run (.claude/skills/ui-climb): one HTML page with every round's scores and votes, and
// every shot across the rounds side by side, to pick the build you like best.
//
//   node scripts/ui-gallery.mjs <run dir>        writes <run dir>/index.html (open it from disk)

import fs from 'node:fs'
import path from 'node:path'

const run = path.resolve(process.argv[2] ?? '.')
const readJson = (file) => {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'))
  } catch {
    return null
  }
}
const readText = (file) => (fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '')
const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c])
const jsonFiles = (dir) => (fs.existsSync(dir) ? fs.readdirSync(dir).filter((f) => f.endsWith('.json')).sort() : [])

const rounds = fs
  .readdirSync(run)
  .filter((d) => /^r\d\d$/.test(d))
  .sort()
  .map((name) => {
    const dir = path.join(run, name)
    const meta = readJson(path.join(dir, 'round.json')) ?? {}
    const checks = readJson(path.join(dir, 'shots', 'checks.json'))
    const judges = jsonFiles(path.join(dir, 'judges')).map((f) => ({ file: f.replace(/\.json$/, ''), ...readJson(path.join(dir, 'judges', f)) }))
    const votes = jsonFiles(path.join(dir, 'ab')).map((f) => ({ file: f.replace(/\.json$/, ''), ...readJson(path.join(dir, 'ab', f)) }))
    return { name, dir, meta, checks, judges, votes, plan: readText(path.join(dir, 'plan.md')), report: readText(path.join(dir, 'builder.md')) }
  })

const status = (r) => (r.name === 'r00' ? 'baseline' : r.meta.accepted === true ? 'accepted' : r.meta.accepted === false ? 'rejected' : 'in progress')

/** The votes for the round's new build: the mapping X/Y -> old/new is in round.json */
function newVotes(r) {
  const newLabel = r.meta.ab?.new
  if (!newLabel || r.votes.length === 0) return ''
  const forNew = r.votes.filter((v) => v.overall?.pick === newLabel).length
  return `${forNew}/${r.votes.length}`
}

const score = (n) => (typeof n === 'number' ? n.toFixed(1) : '')
const judgeIds = [...new Set(rounds.flatMap((r) => r.judges.map((j) => j.file)))].sort()

const shotIds = [...new Set(rounds.flatMap((r) => (r.checks?.results ?? []).map((s) => s.id)))]

const summaryRows = rounds
  .map((r) => {
    const s = r.checks?.summary
    return `<tr class="${status(r)}">
      <td><a href="#${r.name}">${r.name}</a></td>
      <td><span class="badge ${status(r)}">${status(r)}</span></td>
      <td><code>${esc(r.meta.tag ?? '')}</code></td>
      <td>${esc(r.meta.summary ?? '')}</td>
      ${judgeIds.map((id) => `<td class="num">${score(r.judges.find((j) => j.file === id)?.overall)}</td>`).join('')}
      <td class="num">${newVotes(r)}</td>
      <td class="num">${s ? `${s.consoleErrors} / ${s.stepFailures} / ${s.horizontalOverflow.length} / ${s.axeSeriousOrCritical}` : ''}</td>
    </tr>`
  })
  .join('\n')

const roundSections = rounds
  .map((r) => {
    const judgesHtml = r.judges
      .map(
        (j) => `<details><summary><b>${esc(j.file)}</b> (${esc(j.model ?? '')}) overall ${score(j.overall)}; app structure ${score(j.app?.structure)}, setup ${score(j.app?.setupFlow)}, mobile ${score(j.app?.mobile)}</summary>
        <table><tr><th>Area</th><th>Clarity</th><th>Density</th><th>Efficiency</th><th>Feedback</th><th>Craft</th><th>Overall</th><th>Notes</th></tr>
        ${(j.areas ?? []).map((a) => `<tr><td>${esc(a.area)}</td><td class="num">${score(a.clarity)}</td><td class="num">${score(a.density)}</td><td class="num">${score(a.efficiency)}</td><td class="num">${score(a.feedback)}</td><td class="num">${score(a.craft)}</td><td class="num"><b>${score(a.overall)}</b></td><td>${esc(a.notes)}</td></tr>`).join('')}
        </table>
        <p>${esc(j.app?.notes ?? '')}</p>
        <ol>${(j.changes ?? []).map((c) => `<li><b>${esc(c.title)}</b> <span class="muted">[${esc(c.scope)}, ${esc(c.size)}]</span><br>${esc(c.problem)}<br><i>${esc(c.proposal)}</i></li>`).join('')}</ol>
      </details>`,
      )
      .join('')
    const votesHtml = r.votes.length
      ? `<p class="muted">New build is <b>${esc(r.meta.ab?.new ?? '?')}</b>.</p>` +
        r.votes
          .map(
            (v) => `<details><summary><b>${esc(v.file)}</b> picks ${esc(v.overall?.pick)} (confidence ${esc(v.overall?.confidence)})</summary>
          <p>${esc(v.overall?.why)}</p>
          <ul>${(v.areas ?? []).map((a) => `<li>${esc(a.area)}: ${esc(a.pick)} - ${esc(a.why)}</li>`).join('')}</ul>
          ${(v.regressions ?? []).length ? `<p>Regressions:</p><ul>${v.regressions.map((g) => `<li>${esc(g.build)}: ${esc(g.what)} (${esc((g.shots ?? []).join(', '))})</li>`).join('')}</ul>` : ''}
        </details>`,
          )
          .join('')
      : ''
    const thumbs = (r.checks?.results ?? [])
      .map((s) => `<a href="${r.name}/shots/${esc(s.file)}" target="_blank" title="${esc(s.id)}"><img loading="lazy" src="${r.name}/shots/${esc(s.parts?.[0] ?? s.file)}"><span>${esc(s.id)}</span></a>`)
      .join('')
    return `<section id="${r.name}">
      <h2>${r.name} <span class="badge ${status(r)}">${status(r)}</span> <code>${esc(r.meta.tag ?? '')}</code></h2>
      ${r.meta.summary ? `<p>${esc(r.meta.summary)}</p>` : ''}
      ${r.meta.reason ? `<p class="muted">Decision: ${esc(r.meta.reason)}</p>` : ''}
      ${r.plan ? `<details><summary>Plan</summary><pre>${esc(r.plan)}</pre></details>` : ''}
      ${r.report ? `<details><summary>Builder's report</summary><pre>${esc(r.report)}</pre></details>` : ''}
      ${votesHtml ? `<h3>Blind A/B vote</h3>${votesHtml}` : ''}
      ${judgesHtml ? `<h3>Reviews of this build</h3>${judgesHtml}` : ''}
      <details><summary>Screenshots (${(r.checks?.results ?? []).length})</summary><div class="thumbs">${thumbs}</div></details>
    </section>`
  })
  .join('\n')

const shotData = Object.fromEntries(
  shotIds.map((id) => [
    id,
    rounds
      .map((r) => {
        const s = r.checks?.results?.find((x) => x.id === id)
        return s ? { round: r.name, status: status(r), file: `${r.name}/shots/${s.file}` } : null
      })
      .filter(Boolean),
  ]),
)

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>UI climb ${esc(path.basename(run))}</title>
<style>
  :root { color-scheme: dark; --bg: #0b0d10; --panel: #14171c; --line: #262b33; --text: #e5e7eb; --muted: #9ca3af; --good: #34d399; --bad: #f87171; --warn: #fbbf24; }
  body { margin: 0; padding: 16px 24px 64px; background: var(--bg); color: var(--text); font: 14px/1.5 system-ui, sans-serif; }
  h1 { font-size: 22px; } h2 { margin-top: 40px; border-top: 1px solid var(--line); padding-top: 16px; } h3 { font-size: 15px; }
  table { border-collapse: collapse; width: 100%; margin: 8px 0; } th, td { border-bottom: 1px solid var(--line); padding: 6px 8px; text-align: left; vertical-align: top; }
  .num { text-align: right; font-variant-numeric: tabular-nums; } .muted { color: var(--muted); }
  .badge { font-size: 12px; padding: 1px 8px; border-radius: 999px; background: var(--line); }
  .badge.accepted { background: #064e3b; color: var(--good); } .badge.rejected { background: #4c1d1d; color: var(--bad); } .badge.baseline { background: #1e3a5f; color: #93c5fd; }
  tr.rejected td { color: var(--muted); }
  details { background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 8px 12px; margin: 8px 0; } summary { cursor: pointer; }
  pre { white-space: pre-wrap; font-size: 12px; }
  .thumbs { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; margin-top: 8px; }
  .thumbs a { color: var(--muted); text-decoration: none; font-size: 12px; } .thumbs img { width: 100%; border: 1px solid var(--line); border-radius: 4px; display: block; }
  .compare { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 8px; } .compare figure { margin: 0; flex: 0 0 auto; }
  .compare img { border: 1px solid var(--line); border-radius: 4px; display: block; } .compare figcaption { font-size: 12px; margin-bottom: 4px; }
  select, button { background: var(--panel); color: var(--text); border: 1px solid var(--line); border-radius: 6px; padding: 4px 8px; font: inherit; }
  a { color: #93c5fd; }
</style></head><body>
<h1>UI climb: ${esc(path.basename(run))}</h1>
<p class="muted">Generated ${esc(new Date().toISOString())}. Judges score blind (they never see earlier rounds or scores); a round is kept when at least 2 of 3 judges pick it over the previous kept build in a blind A/B vote. Absolute scores drift between runs of the same judge; compare them with the baseline's repeat. Check a build out with <code>git checkout &lt;tag&gt;</code> and run <code>UI_FIXTURES=1 npm run dev</code> in src/BF2WebAdmin.Web.</p>
<table><tr><th>Round</th><th>Status</th><th>Tag</th><th>Summary</th>${judgeIds.map((id) => `<th class="num">${esc(id)}</th>`).join('')}<th class="num">Votes for new</th><th class="num">Errors / failed steps / overflow / axe serious</th></tr>
${summaryRows}</table>

<h2>Compare a shot across rounds</h2>
<p><select id="shot">${shotIds.map((id) => `<option>${esc(id)}</option>`).join('')}</select>
<label><input type="checkbox" id="acceptedOnly" checked> baseline and accepted rounds only</label>
<label>width <select id="width"><option>480</option><option selected>640</option><option>900</option><option>1440</option></select></label></p>
<div class="compare" id="compare"></div>

${roundSections}

<script>
const shots = ${JSON.stringify(shotData)}
const show = () => {
  const id = document.getElementById('shot').value
  const acceptedOnly = document.getElementById('acceptedOnly').checked
  const width = document.getElementById('width').value
  const el = document.getElementById('compare')
  el.innerHTML = ''
  for (const s of shots[id] || []) {
    if (acceptedOnly && s.status !== 'accepted' && s.status !== 'baseline') continue
    const fig = document.createElement('figure')
    fig.innerHTML = '<figcaption>' + s.round + ' (' + s.status + ')</figcaption><a target="_blank" href="' + s.file + '"><img loading="lazy" width="' + width + '" src="' + s.file + '"></a>'
    el.appendChild(fig)
  }
}
for (const id of ['shot', 'acceptedOnly', 'width']) document.getElementById(id).addEventListener('change', show)
show()
</script>
</body></html>`

fs.writeFileSync(path.join(run, 'index.html'), html)
console.log(`Wrote ${path.join(run, 'index.html')} (${rounds.length} rounds, ${shotIds.length} shots)`)
