import { icon } from './icons.js';

const BRAND_IMG = '../Sprite/Common_Icon_Setting_Account.png';
import { el, frag, clear, select, button, toast, escapeHtml, batched } from './ui.js';
import { api, store, reloadAccounts } from './api.js';
import { serverHealth } from './health.js';
import { renderProjectGate } from './project-gate.js';

import overview from './pages/overview.js';
import config from './pages/config.js';
import accounts from './pages/accounts.js';
import inventory from './pages/inventory.js';
import mail from './pages/mail.js';
import events from './pages/events.js';
import rates from './pages/rates.js';
import updates from './pages/updates.js';

const PAGES = [overview, accounts, inventory, mail, events, rates, config, updates];
const NAV = PAGES.map((p) => p.id);
const byId = Object.fromEntries(PAGES.map((p) => [p.id, p]));

let currentId = null;
let cleanup = null;

// process-log ring buffer, shared between the main-process stream and the dock
const LOG_MAX = 1200;
const logBuffer = [];
const logSubs = new Set();
function pushLog(entry) {
  logBuffer.push(entry);
  if (logBuffer.length > LOG_MAX) logBuffer.splice(0, logBuffer.length - LOG_MAX);
  logSubs.forEach((f) => f(entry));
}
function onLog(f) { logSubs.add(f); return () => logSubs.delete(f); }
function clearLog() { logBuffer.length = 0; logSubs.forEach((f) => f(null)); }

// The frameless window carries its own titlebar (drag region + min/max/close). Shared by the shell and the first-run project gate.
function buildTitlebar() {
  const titlebar = frag(`
    <div class="titlebar">
      <div class="brand-mini"><img class="brand-img" src="${BRAND_IMG}" alt=""><span>SHITTIM</span><span class="tb-sub">Control Center</span></div>
      <div class="spacer"></div>
      <div class="win-btns">
        <button class="win-btn" data-w="minimize" title="Minimize">${icon('win_min', 'ico', 1.15)}</button>
        <button class="win-btn" data-w="maximize" title="Maximize">${icon('win_max', 'ico', 1.15)}</button>
        <button class="win-btn close" data-w="close" title="Close">${icon('win_close', 'ico', 1.15)}</button>
      </div>
    </div>`);
  titlebar.querySelectorAll('[data-w]').forEach((b) =>
    b.addEventListener('click', () => window.host.windowControl(b.dataset.w)));
  return titlebar;
}

function buildShell() {
  const app = document.getElementById('app');
  clear(app);

  const titlebar = buildTitlebar();
  const notice = el('div.notice-slot', { id: 'restartSlot' });

  const rail = el('div.rail', {});
  rail.appendChild(frag(`
    <div class="rail-brand">
      <img class="brand-img" src="${BRAND_IMG}" alt="">
      <div class="bt"><h1>Shittim</h1><span>Control Center</span></div>
    </div>`));
  rail.appendChild(el('div.hazard.rail-hazard', {}));

  const nav = el('div.nav', {});
  for (const id of NAV) {
    const item = frag(`<div class="nav-item" data-id="${id}"><span>${byId[id].title}</span></div>`);
    item.addEventListener('click', () => navigate(id));
    nav.appendChild(item);
  }
  rail.appendChild(nav);

  // main - a slim target bar (only for account-scoped pages) above the scroller
  const main = el('div.main', {},
    el('div.page-bar', { id: 'pageBar' }),
    el('div.page-scroll', { id: 'pageScroll' }));

  app.appendChild(titlebar);
  app.appendChild(notice);
  app.appendChild(rail);
  app.appendChild(main);
  app.appendChild(buildDock());
  app.appendChild(buildStatusBar());
}

function setActiveNav(id) {
  document.querySelectorAll('.nav-item').forEach((n) =>
    n.classList.toggle('active', n.dataset.id === id));
}

// account picker - shown in the page bar only on pages scoped to one account
function renderPageBar(page) {
  const bar = document.getElementById('pageBar');
  clear(bar);
  if (page.needsTarget) { bar.style.display = ''; bar.appendChild(buildTargetPicker()); }
  else bar.style.display = 'none';
}

function buildTargetPicker() {
  const wrap = el('div.target-pick', {}, el('span.tp-label', { text: 'Account' }));
  const sel = select(
    store.get().accounts.map((a) => ({ value: a.serverId, label: `${a.nickname} - ${a.serverId}` })),
    { value: store.get().targetId ?? '' });
  if (!store.get().accounts.length) {
    sel.appendChild(frag('<option value="">No accounts</option>'));
    sel.disabled = true;
  }
  sel.addEventListener('change', () => {
    store.set({ targetId: Number(sel.value) });
    if (currentId && byId[currentId].needsTarget) navigate(currentId, true);
  });
  wrap.appendChild(sel);
  return wrap;
}

function refreshTargetPicker() {
  if (currentId && byId[currentId].needsTarget) renderPageBar(byId[currentId]);
}

let dockHeight = 200;      // body height in px; persisted
let dockCollapsed = false; // header-only when true
let following = true;
let logFilter = 'all';
let consoleEl = null;
let dockToggleBtn = null;
let logFeed = null;

function lineNode(entry) {
  const cls = entry.line.startsWith('>') ? 'sys' : entry.source;
  return frag(`<div class="ln ${cls}"><span class="src">${entry.source}</span>${escapeHtml(entry.line)}</div>`);
}

function repaintLog() {
  if (logFeed) logFeed.discard(); // lines waiting on a frame were filtered under the old selection
  clear(consoleEl);
  const rows = logBuffer.filter((e) => logFilter === 'all' || e.source === logFilter);
  if (!rows.length) { consoleEl.appendChild(frag('<div class="ln muted">- no output yet - start the server to see logs -</div>')); return; }
  // one reflow for the whole buffer instead of one per line
  const batch = document.createDocumentFragment();
  for (const e of rows) batch.appendChild(lineNode(e));
  consoleEl.appendChild(batch);
  if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
}

function buildDock() {
  const dock = el('div.dock' + (dockCollapsed ? '.collapsed' : ''), { id: 'dock' });
  dock.style.setProperty('--dock-h', dockHeight + 'px');

  consoleEl = el('div.console', {});

  const srcSel = frag(`<select class="select dock-sel">
    <option value="all">All output</option><option value="server">Server</option><option value="mitm">Proxy</option></select>`);
  srcSel.value = logFilter;
  srcSel.addEventListener('change', () => { logFilter = srcSel.value; repaintLog(); });

  const followBtn = button('Follow', { variant: 'ghost', sm: true, onClick: () => {
    following = !following; followBtn.classList.toggle('btn-primary', following);
    if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
  }});
  if (following) followBtn.classList.add('btn-primary');
  const clearBtn = button('Clear', { variant: 'ghost', sm: true, onClick: () => { clearLog(); repaintLog(); } });

  dockToggleBtn = frag(`<button class="dock-toggle" title="Collapse console">${dockCollapsed ? '▴' : '▾'}</button>`);
  dockToggleBtn.addEventListener('click', (e) => { e.stopPropagation(); setDockCollapsed(!dockCollapsed); });

  const head = el('div.dock-head', {},
    el('span.dock-title', { text: 'Console' }),
    el('div.spacer', {}), srcSel, followBtn, clearBtn, dockToggleBtn);
  head.addEventListener('mousedown', startDockDrag);
  head.addEventListener('dblclick', () => setDockCollapsed(!dockCollapsed));

  dock.appendChild(head);
  dock.appendChild(el('div.dock-body', {}, consoleEl));

  // 10Hz, not a frame each: a frame each is more often than anyone reads and at a few hundred lines a second it is 5000 layout passes a minute over a scrollback whose rows all wrap. Measured at a sixth the layout work and 60MB less renderer.
  logFeed = batched((rows) => {
    const batch = document.createDocumentFragment();
    for (const e of rows) batch.appendChild(lineNode(e));
    consoleEl.appendChild(batch);
    while (consoleEl.childElementCount > 1400) consoleEl.removeChild(consoleEl.firstChild);
    if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
  }, { cap: LOG_MAX, schedule: (fn) => setTimeout(() => requestAnimationFrame(fn), 100) });

  onLog((entry) => {
    if (!entry) { repaintLog(); return; }
    if (logFilter !== 'all' && entry.source !== logFilter) return;
    logFeed.push(entry);
  });
  consoleEl.addEventListener('scroll', () => {
    const atBottom = consoleEl.scrollTop + consoleEl.clientHeight >= consoleEl.scrollHeight - 30;
    if (!atBottom && following) { following = false; followBtn.classList.remove('btn-primary'); }
  });
  repaintLog();
  return dock;
}

function setDockCollapsed(v) {
  dockCollapsed = v;
  const dock = document.getElementById('dock');
  if (dock) dock.classList.toggle('collapsed', v);
  if (dockToggleBtn) { dockToggleBtn.textContent = v ? '▴' : '▾'; dockToggleBtn.title = v ? 'Show console' : 'Collapse console'; }
  if (!v && following && consoleEl) consoleEl.scrollTop = consoleEl.scrollHeight;
  persistDock();
}

function startDockDrag(e) {
  if (e.target.closest('button, select') || dockCollapsed) return;
  e.preventDefault();
  const dock = document.getElementById('dock');
  const startY = e.clientY;
  const startH = dockHeight;
  document.body.style.cursor = 'row-resize';
  const onMove = (ev) => {
    const max = Math.round(window.innerHeight * 0.6);
    dockHeight = Math.max(64, Math.min(max, startH + (startY - ev.clientY)));
    dock.style.setProperty('--dock-h', dockHeight + 'px');
    if (following && consoleEl) consoleEl.scrollTop = consoleEl.scrollHeight;
  };
  const onUp = () => {
    document.removeEventListener('mousemove', onMove);
    document.removeEventListener('mouseup', onUp);
    document.body.style.cursor = '';
    persistDock();
  };
  document.addEventListener('mousemove', onMove);
  document.addEventListener('mouseup', onUp);
}

function persistDock() {
  try { window.host.settingsWrite({ dockHeight, dockCollapsed }); } catch { /* non-fatal */ }
}

const isUp = (s) => s.online || s.procServer === 'running' || s.procServer === 'starting'
  || s.procMitm === 'running' || s.procMitm === 'starting';

const health = (s) => serverHealth({ proc: s.procServer, startedAt: s.serverStartedAt, live: s.live, ready: s.online, now: Date.now(), grace: s.serverGraceMs });

function statusPill(state) {
  const map = { online: ['good', 'Running'], running: ['good', 'Running'], starting: ['warn', 'Starting'], unhealthy: ['warn', 'Not ready'], unresponsive: ['bad', 'No response'], stopped: ['', 'Stopped'], failed: ['bad', 'Failed'] };
  const [cls, label] = map[state] || ['', 'Stopped'];
  return frag(`<span class="pill ${cls}"><span class="dot"></span>${label}</span>`);
}

let sbLed = null, sbTitle = null, sbSub = null, sbServer = null, sbProxy = null, sbPower = null;

async function togglePower() {
  const up = isUp(store.get());
  sbPower.disabled = true;
  try {
    if (up) { await window.host.systemStop(); toast('Stopping server + proxy...', 'warn'); }
    else { await window.host.systemStart(); toast('Starting server + proxy...', 'good'); }
  } finally { sbPower.disabled = false; }
}

function buildStatusBar() {
  sbLed = el('span.sb-led', {});
  sbTitle = el('b.sb-title', {});
  sbSub = el('span.sb-sub', {});
  sbServer = el('span.sb-state', {}, el('span.sb-tag', { text: 'Server' }));
  sbProxy = el('span.sb-state', {}, el('span.sb-tag', { text: 'Proxy' }));
  sbPower = frag(`<button class="sb-power"><span class="ico-slot">${icon('play')}</span><span class="pw-label">Start</span></button>`);
  sbPower.addEventListener('click', togglePower);

  return el('div.statusbar', {},
    sbLed, sbTitle, sbSub,
    el('div.spacer', {}),
    sbServer, sbProxy,
    el('span.sb-div', {}),
    sbPower);
}

function paintStatusBar() {
  if (!sbLed) return;
  const s = store.get();

  const h = health(s);
  let cls, title, sub;
  if (h === 'online') {
    cls = 'up'; title = 'Online';
    sub = s.status ? `v${s.status.gameVersion} · :${s.status.apiPort} · ${s.status.accountCount} acct` : `Ready · ${s.probeTarget}`;
  } else if (h === 'starting') {
    cls = 'starting'; title = 'Starting'; sub = s.live ? `Listening on ${s.probeTarget}` : 'Booting server';
  } else if (h === 'unhealthy') {
    cls = 'starting'; title = 'Not ready'; sub = `Listening on ${s.probeTarget} but /api/admin/status is still failing`;
  } else if (h === 'unresponsive') {
    cls = 'bad'; title = 'Not responding'; sub = `Process up, nothing on ${s.probeTarget} after ${Math.round(s.serverGraceMs / 1000)}s`;
  } else if (h === 'failed') {
    cls = 'bad'; title = 'Start failed'; sub = 'The server process did not spawn - see the console';
  } else {
    cls = 'down'; title = 'Offline'; sub = 'Not running';
  }
  sbLed.className = 'sb-led ' + cls;
  sbTitle.textContent = title;
  sbSub.textContent = sub;

  clear(sbServer); sbServer.append(el('span.sb-tag', { text: 'Server' }), statusPill(h));
  clear(sbProxy); sbProxy.append(el('span.sb-tag', { text: 'Proxy' }), statusPill(s.procMitm));

  const up = isUp(s);
  sbPower.querySelector('.pw-label').textContent = up ? 'Stop' : 'Start';
  sbPower.querySelector('.ico-slot').innerHTML = icon(up ? 'stop' : 'play');
  sbPower.classList.toggle('stop', up);
}

function navigate(id, force = false) {
  const page = byId[id] || byId.overview;
  if (id === currentId && !force) return;
  if (cleanup) { try { cleanup(); } catch {} cleanup = null; }
  currentId = page.id;
  location.hash = `#/${page.id}`;
  setActiveNav(page.id);
  renderPageBar(page);

  const scroll = document.getElementById('pageScroll');
  clear(scroll);
  const root = el('div.view', {});
  scroll.appendChild(root);
  scroll.scrollTop = 0;

  Promise.resolve(page.mount(root, { rerender: () => navigate(page.id, true) }))
    .then((c) => { cleanup = typeof c === 'function' ? c : null; })
    .catch((e) => { root.appendChild(frag(`<div class="empty"><b>Page failed</b><span>${String(e.message || e)}</span></div>`)); });
}

let wasReady = false;
async function poll() {
  let pStatus = null;
  try { pStatus = await window.host.procStatus(); } catch { /* ignore */ }
  const { live, ready, status } = await api.probe();
  if (ready) {
    if (!wasReady) { await api.refreshBase(); await reloadAccounts(); refreshTargetPicker(); }
  }
  wasReady = ready;
  const patch = {
    online: ready,
    live,
    status: ready ? status : null,
    lastCheckedTs: Date.now(),
    probeTarget: api.hostPort(),
  };
  // proc:state only ever announces starting/stopped/failed, so a child that is simply up is only ever visible from here
  if (pStatus) {
    patch.procServer = pStatus.server;
    patch.procMitm = pStatus.mitm;
    patch.serverPid = pStatus.serverPid;
    patch.serverStartedAt = pStatus.serverStartedAt;
    patch.serverGraceMs = pStatus.serverGraceMs;
  }
  store.set(patch);
}

// Adaptive scheduler: probe hard while the server is booting (that's when the user is watching), relax once state is stable, and back off when the window is hidden.
let pollTimer = null;
let pollBusy = false;

function pollDelay() {
  const s = store.get();
  if (document.hidden) return 15000;                       // minimized - relax
  if (health(s) === 'starting') return 1500;               // booting - tight loop
  if (s.online) return 5000;                               // stable online
  return 7000;                                             // offline idle
}

function schedulePoll(immediate = false) {
  clearTimeout(pollTimer);
  pollTimer = setTimeout(async () => {
    if (pollBusy) { schedulePoll(); return; }
    pollBusy = true;
    try { await poll(); } catch { /* probe never throws, but stay safe */ }
    pollBusy = false;
    schedulePoll();
  }, immediate ? 0 : pollDelay());
}

// When the window comes back, refresh immediately instead of waiting out a long hidden-state delay.
document.addEventListener('visibilitychange', () => { if (!document.hidden) schedulePoll(true); });

async function boot() {
  // dock geometry is remembered between sessions
  try {
    const s = await window.host.settingsRead();
    if (s && Number.isFinite(s.dockHeight)) dockHeight = Math.max(64, s.dockHeight);
    if (s && typeof s.dockCollapsed === 'boolean') dockCollapsed = s.dockCollapsed;
  } catch { /* defaults */ }

  // First-run gate: with no server project present there is nothing for the shell to drive, so offer to download it from GitHub or locate an existing copy.
  // The gate reloads the app once a project is set.
  let project = null;
  try { project = await window.host.projectStatus(); } catch { /* treat as found */ }
  if (project && !project.found) {
    renderProjectGate(document.getElementById('app'), project, { titlebar: buildTitlebar() });
    return;
  }

  buildShell();

  window.host.onProcState((d) => {
    const prevServer = store.get().procServer;
    if (d.server) store.set({ procServer: d.server });
    if (d.mitm) store.set({ procMitm: d.mitm });
    if (d.serverPid !== undefined) store.set({ serverPid: d.serverPid });
    // lifecycle flip (start/stop) - re-probe right away so the UI reacts
    if (d.server && d.server !== prevServer) schedulePoll(true);
  });
  window.host.onProcLog((d) => pushLog(d));

  // Passive "server update available" notice (checked at launch + every few hours by the main process). Applying stays manual on the Updates page.
  window.host.onServerUpdate((d) => {
    const n = d.behind === 1 ? '1 commit' : `${d.behind} commits`;
    toast(`Server update available - ${n} behind (${d.remoteShort}: ${d.remoteSubject}). Open the Updates page to install.`, 'good', 'Server update');
  });

  store.subscribe(paintStatusBar);
  paintStatusBar();

  const start = (location.hash || '').replace('#/', '') || 'overview';
  navigate(byId[start] ? start : 'overview');

  await api.refreshBase();
  store.set({ probeTarget: api.hostPort() });
  schedulePoll(true);
}

// Module scripts are deferred, so the DOM is already parsed here.
boot();
