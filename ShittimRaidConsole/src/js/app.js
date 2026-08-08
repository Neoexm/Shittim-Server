import { SEASONS } from './seasons.js';

let baseUrl = localStorage.getItem('raidconsole.url') || 'http://127.0.0.1:5200';
let token = localStorage.getItem('raidconsole.token') || '';
let connected = false;
let overview = null;
let pollTimer = null;

let draft = emptyDraft();

function emptyDraft() {
  return { seasonId: 0, name: '', exposed: '', open: '', close: '', extension: '', minServerVersion: '', bosses: [] };
}

function frag(html) {
  const t = document.createElement('template');
  t.innerHTML = html.trim();
  return t.content;
}

function esc(s) {
  return String(s == null ? '' : s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function toast(message, kind = '') {
  const host = document.getElementById('toasts');
  const t = frag(`<div class="toast ${kind}"><div>${esc(message)}</div></div>`).firstElementChild;
  host.appendChild(t);
  setTimeout(() => {
    t.style.transition = 'opacity .25s, transform .25s';
    t.style.opacity = '0';
    t.style.transform = 'translateX(20px)';
    setTimeout(() => t.remove(), 260);
  }, 3200);
}

function fmtHP(n) {
  if (n >= 1e12) return trim2(n / 1e12) + 'T';
  if (n >= 1e9) return trim2(n / 1e9) + 'B';
  if (n >= 1e6) return trim2(n / 1e6) + 'M';
  return Number(n).toLocaleString();
}
function trim2(x) { return String(Math.round(x * 100) / 100); }

function pad(n) { return String(n).padStart(2, '0'); }
function dutc(d) { return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`; }
// manifest datetimes go over the wire as "yyyy-MM-dd HH:mm:ss" in utc - every server converts to its own clock so the raid flips at the same instant worldwide. The inputs hold the utc value directly, no local conversion.
function toInput(wire) { return wire ? wire.slice(0, 16).replace(' ', 'T') : ''; }
function toWire(input) { return input ? input.replace('T', ' ') + ':00' : ''; }

async function api(method, path, body) {
  const res = await fetch(baseUrl.replace(/\/+$/, '') + path, {
    method,
    headers: Object.assign({ 'X-Admin-Token': token }, body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (res.status === 401) throw new Error('the coordinator rejected the admin token');
  if (!res.ok) throw new Error(`coordinator answered ${res.status}`);
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : null;
}

function buildShell() {
  const app = document.getElementById('app');
  app.append(frag(`
    <div class="titlebar">
      <div class="brand-mini"><span>SHITTIM</span><span class="tb-sub">Raid Console</span></div>
      <div class="spacer"></div>
      <div class="win-btns">
        <button class="win-btn" data-w="minimize" title="Minimize"><svg viewBox="0 0 10 10"><path d="M1 5h8" stroke="currentColor" stroke-width="1.2" fill="none"/></svg></button>
        <button class="win-btn" data-w="maximize" title="Maximize"><svg viewBox="0 0 10 10"><rect x="1.5" y="1.5" width="7" height="7" stroke="currentColor" stroke-width="1.2" fill="none"/></svg></button>
        <button class="win-btn close" data-w="close" title="Close"><svg viewBox="0 0 10 10"><path d="M1.5 1.5l7 7M8.5 1.5l-7 7" stroke="currentColor" stroke-width="1.2" fill="none"/></svg></button>
      </div>
    </div>
    <div class="connect-bar">
      <input class="input url" id="c-url" placeholder="http://coordinator:5200" spellcheck="false">
      <input class="input tok" id="c-token" type="password" placeholder="admin token" spellcheck="false">
      <button class="btn btn-primary" id="c-go">Connect</button>
      <span class="pill" id="c-pill"><span class="dot"></span><span id="c-pill-text">offline</span></span>
    </div>
    <div class="main-scroll">
      <div class="cols">
        <div class="card">
          <div class="card-head"><span class="tab-mark"></span><h3>Manifest</h3><div class="spacer"></div><span class="sub" id="m-live"></span></div>
          <div class="card-body" id="editor"></div>
        </div>
        <div class="card">
          <div class="card-head"><span class="tab-mark"></span><h3>World state</h3><div class="spacer"></div><span class="sub" id="s-meta"></span></div>
          <div class="card-body" id="statebox"></div>
        </div>
      </div>
    </div>`));

  app.querySelectorAll('[data-w]').forEach((b) =>
    b.addEventListener('click', () => window.host.windowControl(b.dataset.w)));

  const urlBox = document.getElementById('c-url');
  const tokBox = document.getElementById('c-token');
  urlBox.value = baseUrl;
  tokBox.value = token;
  document.getElementById('c-go').addEventListener('click', () => connect(urlBox.value, tokBox.value));
  tokBox.addEventListener('keydown', (e) => { if (e.key === 'Enter') connect(urlBox.value, tokBox.value); });
}

function setPill(kind, text) {
  const pill = document.getElementById('c-pill');
  pill.className = 'pill' + (kind ? ' ' + kind : '');
  document.getElementById('c-pill-text').textContent = text;
}

async function connect(url, tok) {
  baseUrl = url.trim();
  token = tok.trim();
  localStorage.setItem('raidconsole.url', baseUrl);
  localStorage.setItem('raidconsole.token', token);
  setPill('warn', 'connecting');
  try {
    overview = await api('GET', '/admin/overview');
  } catch (e) {
    connected = false;
    setPill('bad', 'offline');
    renderState();
    toast(String(e.message || e), 'bad');
    return;
  }
  connected = true;
  setPill('good', 'connected');
  if (overview.manifest) draft = draftFromManifest(overview.manifest);
  renderEditor();
  renderState();
  if (pollTimer) clearInterval(pollTimer);
  pollTimer = setInterval(refresh, 10000);
}

async function refresh() {
  if (!connected) return;
  try {
    overview = await api('GET', '/admin/overview');
    setPill('good', 'connected');
  } catch {
    setPill('bad', 'unreachable');
    return;
  }
  renderState();
}

function draftFromManifest(m) {
  return {
    seasonId: m.seasonId,
    name: m.name || '',
    exposed: toInput(m.exposed),
    open: toInput(m.open),
    close: toInput(m.close),
    extension: toInput(m.extension),
    minServerVersion: m.minServerVersion || '',
    bosses: (m.bosses || []).map((b) => ({ groupId: b.groupId, hp: b.totalHP, spawn: toInput(b.spawnTime), eliminate: toInput(b.eliminateTime) })),
  };
}

function loadPreset(seasonId) {
  const preset = SEASONS.find((s) => s.seasonId === seasonId);
  if (!preset) return;
  const open = new Date();
  open.setUTCHours(11, 0, 0, 0);
  const close = new Date(open.getTime() + preset.days * 86400000);
  close.setUTCHours(10, 59, 0, 0);
  // phased presets carry day offsets from open; spawns land on the 11:00 utc rollover, eliminations just before it
  const dayOffset = (day, h, m) => { const d = new Date(open.getTime() + day * 86400000); d.setUTCHours(h, m, 0, 0); return dutc(d); };
  draft = {
    seasonId: preset.seasonId,
    name: `Season ${preset.seasonId}`,
    exposed: dutc(open),
    open: dutc(open),
    close: dutc(close),
    extension: dutc(new Date(close.getTime() + 7 * 86400000)),
    minServerVersion: preset.minServerVersion || '',
    bosses: preset.bosses.map((b) => ({
      groupId: b.groupId,
      hp: b.hp,
      spawn: b.spawnDay == null ? '' : dayOffset(b.spawnDay, 11, 0),
      eliminate: b.elimDay == null ? '' : dayOffset(b.elimDay, 10, 59),
    })),
  };
  renderEditor();
}

function renderEditor() {
  const box = document.getElementById('editor');
  box.innerHTML = '';

  const live = document.getElementById('m-live');
  live.textContent = overview && overview.manifest ? `live: season ${overview.manifest.seasonId}` : 'no raid published';

  const presetOpts = SEASONS.map((s) => `<option value="${s.seasonId}">${esc(s.label)}</option>`).join('');
  box.append(frag(`
    <div class="field">
      <label>Start from a shipped season</label>
      <div class="row">
        <select class="select" id="e-preset" style="flex:1"><option value="">pick one</option>${presetOpts}</select>
        <button class="btn btn-sm" id="e-load">Load</button>
      </div>
    </div>
    <div class="date-grid">
      <div class="field"><label>Season id</label><input class="input" id="e-season" type="number" value="${draft.seasonId || ''}"></div>
      <div class="field"><label>Name (console only)</label><input class="input" id="e-name" value="${esc(draft.name)}"></div>
      <div class="field"><label>Exposed from (UTC)</label><input class="input" id="e-exposed" type="datetime-local" value="${draft.exposed}"></div>
      <div class="field"><label>Open (UTC)</label><input class="input" id="e-open" type="datetime-local" value="${draft.open}"></div>
      <div class="field"><label>Close (UTC)</label><input class="input" id="e-close" type="datetime-local" value="${draft.close}"></div>
      <div class="field"><label>Extension until (UTC)</label><input class="input" id="e-ext" type="datetime-local" value="${draft.extension}"></div>
      <div class="field"><label>Min server version</label><input class="input" id="e-minver" placeholder="leave empty to allow all" value="${esc(draft.minServerVersion)}"></div>
    </div>
    <div class="boss-scroll">
      <table class="tbl boss-tbl">
        <thead><tr><th>Group</th><th>World HP</th><th></th><th>Spawns (UTC)</th><th>Until (UTC)</th><th></th></tr></thead>
        <tbody id="e-bosses"></tbody>
      </table>
    </div>
    <div class="row" style="margin-top:10px; gap:8px">
      <button class="btn btn-sm" id="e-add">Add boss</button>
      <div class="spacer"></div>
      <button class="btn btn-danger btn-sm" id="e-end">End raid</button>
      <button class="btn btn-primary btn-skew" id="e-publish"><span>Publish</span></button>
    </div>`));

  const tbody = box.querySelector('#e-bosses');
  draft.bosses.forEach((b, i) => {
    const tr = frag(`
      <tr>
        <td><input class="input gid" type="number" value="${b.groupId || ''}"></td>
        <td><input class="input hp" value="${b.hp}"></td>
        <td class="hp-hint">${b.hp > 0 ? fmtHP(b.hp) : '-'}</td>
        <td><input class="input when" type="datetime-local" value="${b.spawn}"></td>
        <td><input class="input when" type="datetime-local" value="${b.eliminate}"></td>
        <td><button class="btn btn-ghost btn-sm">remove</button></td>
      </tr>`).firstElementChild;
    const [gid, hp, , spawn, eliminate] = tr.querySelectorAll('input, .hp-hint');
    const hint = tr.querySelector('.hp-hint');
    gid.addEventListener('input', () => { b.groupId = Number(gid.value) || 0; });
    hp.addEventListener('input', () => {
      b.hp = Number(hp.value.replace(/[,_\s]/g, '')) || 0;
      hint.textContent = b.hp > 0 ? fmtHP(b.hp) : '-';
    });
    spawn.addEventListener('input', () => { b.spawn = spawn.value; });
    eliminate.addEventListener('input', () => { b.eliminate = eliminate.value; });
    tr.querySelector('button').addEventListener('click', () => { draft.bosses.splice(i, 1); renderEditor(); });
    tbody.appendChild(tr);
  });
  if (!draft.bosses.length)
    tbody.appendChild(frag('<tr><td colspan="6" class="muted" style="padding:10px">no bosses - load a preset or add rows</td></tr>'));

  box.querySelector('#e-load').addEventListener('click', () => {
    const v = Number(box.querySelector('#e-preset').value);
    if (v) loadPreset(v);
  });
  box.querySelector('#e-add').addEventListener('click', () => { draft.bosses.push({ groupId: 0, hp: 0, spawn: '', eliminate: '' }); renderEditor(); });

  const bind = (id, key) => box.querySelector(id).addEventListener('input', (e) => { draft[key] = e.target.value; });
  bind('#e-name', 'name');
  bind('#e-exposed', 'exposed');
  bind('#e-open', 'open');
  bind('#e-close', 'close');
  bind('#e-ext', 'extension');
  bind('#e-minver', 'minServerVersion');
  box.querySelector('#e-season').addEventListener('input', (e) => { draft.seasonId = Number(e.target.value) || 0; });

  box.querySelector('#e-publish').addEventListener('click', publish);
  box.querySelector('#e-end').addEventListener('click', endRaid);
}

async function publish() {
  if (!connected) { toast('connect to the coordinator first', 'warn'); return; }
  if (!draft.seasonId) { toast('season id is required', 'warn'); return; }
  if (!draft.open || !draft.close) { toast('open and close are required', 'warn'); return; }
  const bosses = draft.bosses.filter((b) => b.groupId > 0);
  if (!bosses.length) { toast('declare at least one boss group', 'warn'); return; }

  const manifest = {
    seasonId: draft.seasonId,
    name: draft.name,
    open: toWire(draft.open),
    close: toWire(draft.close),
    exposed: toWire(draft.exposed || draft.open),
    extension: toWire(draft.extension || draft.close),
    minServerVersion: draft.minServerVersion.trim(),
    bosses: bosses.map((b) => ({ groupId: b.groupId, totalHP: b.hp, spawnTime: toWire(b.spawn), eliminateTime: toWire(b.eliminate) })),
  };
  const sameSeason = overview && overview.manifest && overview.manifest.seasonId === manifest.seasonId;
  if (!sameSeason && overview && overview.manifest &&
      !confirm(`Season ${overview.manifest.seasonId} is live. Publishing season ${manifest.seasonId} throws its pool away and starts over. Continue?`))
    return;
  try {
    const r = await api('PUT', '/admin/manifest', manifest);
    overview = { manifest: r.manifest, state: r.state, servers: overview ? overview.servers : 0 };
    toast(sameSeason ? 'manifest updated, running pools kept' : `season ${manifest.seasonId} is live`, 'good');
    renderEditor();
    renderState();
  } catch (e) {
    toast(String(e.message || e), 'bad');
  }
}

async function endRaid() {
  if (!connected) { toast('connect to the coordinator first', 'warn'); return; }
  if (!overview || !overview.manifest) { toast('nothing is published', 'warn'); return; }
  if (!confirm(`End season ${overview.manifest.seasonId}? Every server falls back to its shipped dates on the next poll.`)) return;
  try {
    await api('DELETE', '/admin/manifest');
    overview = { manifest: null, state: null, servers: 0 };
    toast('raid ended', 'good');
    renderEditor();
    renderState();
  } catch (e) {
    toast(String(e.message || e), 'bad');
  }
}

function renderState() {
  const box = document.getElementById('statebox');
  // an HP edit in flight survives the 10s poll; the numbers underneath refresh next round
  if (box.querySelector('.hp-edit')) return;
  box.innerHTML = '';

  const meta = document.getElementById('s-meta');
  if (!connected) {
    meta.textContent = '';
    box.append(frag('<div class="empty"><p>Point this console at your coordinator and connect.</p></div>'));
    return;
  }
  if (!overview || !overview.state) {
    meta.textContent = '';
    box.append(frag('<div class="empty"><p>No raid running. Publish a manifest to start one.</p></div>'));
    return;
  }

  meta.textContent = `${overview.servers} server${overview.servers === 1 ? '' : 's'} contributing`;

  const totals = {};
  (overview.manifest && overview.manifest.bosses || []).forEach((b) => { totals[b.groupId] = b.totalHP; });

  for (const [gid, boss] of Object.entries(overview.state.bosses)) {
    const total = totals[gid] || boss.remainingHP || 1;
    const pct = Math.max(0, Math.min(100, total > 0 ? (boss.remainingHP / total) * 100 : 0));
    const dead = boss.remainingHP <= 0;
    const row = frag(`
      <div class="hp-row" data-gid="${gid}">
        <span class="gid">${gid}</span>
        <div class="hp-meter${dead ? ' dead' : ''}"><i style="width:${pct}%"></i></div>
        <span class="hp-num">${dead ? 'down' : fmtHP(boss.remainingHP)}</span>
        <span class="hp-sub">${boss.participants || 0} srv</span>
        <button class="btn btn-ghost btn-sm">set</button>
      </div>`).firstElementChild;
    row.querySelector('button').addEventListener('click', () => beginHpEdit(row, Number(gid), boss));
    box.appendChild(row);
  }

  box.append(frag(`
    <div class="row" style="margin-top:12px; gap:8px">
      <span class="muted" style="font-size:12px">Set a boss to 0 to open its decisive battle everywhere.</span>
      <div class="spacer"></div>
      <button class="btn btn-sm" id="s-reseed">Reseed pools</button>
    </div>`));
  box.querySelector('#s-reseed').addEventListener('click', reseed);
}

function beginHpEdit(row, gid, boss) {
  const num = row.querySelector('.hp-num');
  const btn = row.querySelector('button');
  const input = frag(`<input class="input hp-edit" value="${boss.remainingHP}">`).firstElementChild;
  num.replaceWith(input);
  btn.textContent = 'apply';
  input.focus();
  input.select();

  const done = async () => {
    const value = Number(input.value.replace(/[,_\s]/g, ''));
    if (!Number.isFinite(value) || value < 0) { toast('that is not an HP value', 'warn'); return; }
    const next = { seasonId: overview.state.seasonId, bosses: {} };
    for (const [g, b] of Object.entries(overview.state.bosses))
      next.bosses[g] = { remainingHP: Number(g) === gid ? value : b.remainingHP, participants: b.participants };
    try {
      const state = await api('PUT', '/admin/state', next);
      overview.state = state;
      toast(`boss ${gid} set to ${value === 0 ? '0 - decisive open' : fmtHP(value)}`, 'good');
    } catch (e) {
      toast(String(e.message || e), 'bad');
    }
    renderStateForced();
  };
  btn.replaceWith(cloneButton(btn, done));
  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') done();
    if (e.key === 'Escape') renderStateForced();
  });
}

// the stale listener on the original button would re-enter edit mode, so swap in a fresh one
function cloneButton(btn, onClick) {
  const fresh = btn.cloneNode(true);
  fresh.addEventListener('click', onClick);
  return fresh;
}

function renderStateForced() {
  const box = document.getElementById('statebox');
  box.querySelectorAll('.hp-edit').forEach((e) => e.remove());
  renderState();
}

async function reseed() {
  if (!confirm('Refill every boss back to the manifest HP and forget who contributed?')) return;
  try {
    const state = await api('POST', '/admin/reseed');
    overview.state = state;
    toast('pools reseeded', 'good');
    renderStateForced();
  } catch (e) {
    toast(String(e.message || e), 'bad');
  }
}

buildShell();
renderEditor();
renderState();
if (baseUrl && token) connect(baseUrl, token);
