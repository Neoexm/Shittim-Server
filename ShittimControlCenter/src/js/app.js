import { icon } from './icons.js';

const BRAND_IMG = '../Sprite/Common_Icon_Setting_Account.png';
import { el, frag, clear, select, button, toast, escapeHtml } from './ui.js';
import { api, store, reloadAccounts } from './api.js';
import { pushLog } from './bus.js';
import { renderProjectGate } from './project-gate.js';

import dashboard from './pages/dashboard.js';
import server from './pages/server.js';
import config from './pages/config.js';
import accounts from './pages/accounts.js';
import inventory from './pages/inventory.js';
import mail from './pages/mail.js';
import events from './pages/events.js';
import gacha from './pages/gacha.js';
import rates from './pages/rates.js';
import updates from './pages/updates.js';

const PAGES = [dashboard, server, config, updates, accounts, inventory, mail, events, gacha, rates];
const NAV = [
  { group: 'Server', items: ['dashboard', 'server', 'config', 'updates'] },
  { group: 'Management', items: ['accounts', 'inventory', 'mail'] },
  { group: 'Content', items: ['events', 'gacha', 'rates'] },
];
const byId = Object.fromEntries(PAGES.map((p) => [p.id, p]));

let currentId = null;
let cleanup = null;

// --------------------------------------------------------------- theme

function applyTheme(theme, animate = false) {
  const t = theme === 'light' ? 'light' : 'dark';
  const html = document.documentElement;
  if (animate) {
    html.classList.add('theme-anim');
    setTimeout(() => html.classList.remove('theme-anim'), 320);
  }
  html.dataset.theme = t;
  const btn = document.getElementById('themeBtn');
  if (btn) {
    // show the icon of the mode you'd switch TO
    btn.innerHTML = icon(t === 'dark' ? 'sun' : 'moon');
    btn.title = t === 'dark' ? 'Switch to light theme' : 'Switch to dark theme';
  }
}

async function initTheme() {
  let theme = 'dark';
  try { const s = await window.host.settingsRead(); if (s && s.theme) theme = s.theme; } catch { /* default */ }
  applyTheme(theme);
}

function toggleTheme() {
  const next = document.documentElement.dataset.theme === 'light' ? 'dark' : 'light';
  applyTheme(next, true);
  try { window.host.settingsWrite({ theme: next }); } catch { /* non-fatal */ }
}

// --------------------------------------------------------------- shell

// The frameless window has no OS chrome, so every view must carry the custom
// titlebar (drag region + theme toggle + min/max/close). Shared by the shell
// and the first-run project gate.
function buildTitlebar() {
  const titlebar = frag(`
    <div class="titlebar">
      <div class="brand-mini"><img class="brand-img" src="${BRAND_IMG}" alt=""><span>SHITTIM</span><span class="tb-sub">Control Center</span></div>
      <div class="spacer"></div>
      <div class="tb-actions">
        <button class="tb-btn" id="themeBtn" title="Toggle theme">${icon('sun')}</button>
      </div>
      <div class="win-btns">
        <button class="win-btn" data-w="minimize" title="Minimize">${icon('win_min', 'ico', 1.15)}</button>
        <button class="win-btn" data-w="maximize" title="Maximize">${icon('win_max', 'ico', 1.15)}</button>
        <button class="win-btn close" data-w="close" title="Close">${icon('win_close', 'ico', 1.15)}</button>
      </div>
    </div>`);
  titlebar.querySelector('#themeBtn').addEventListener('click', toggleTheme);
  titlebar.querySelectorAll('[data-w]').forEach((b) =>
    b.addEventListener('click', () => window.host.windowControl(b.dataset.w)));
  return titlebar;
}

function buildShell() {
  const app = document.getElementById('app');
  clear(app);

  // titlebar
  const titlebar = buildTitlebar();

  // rail
  const rail = el('div.rail', {});
  rail.appendChild(frag(`
    <div class="rail-brand">
      <img class="brand-img" src="${BRAND_IMG}" alt="">
      <div class="bt"><h1>Shittim</h1><span>Control Center</span></div>
    </div>`));
  rail.appendChild(el('div.hazard.rail-hazard', {}));

  const nav = el('div.nav', {});
  let navIdx = 0; // stagger index for the entrance animation
  for (const grp of NAV) {
    nav.appendChild(el('div.nav-group', { text: grp.group }));
    for (const id of grp.items) {
      const p = byId[id];
      const item = frag(`<div class="nav-item" data-id="${id}" style="--i:${navIdx++}">${icon(p.icon)}<span>${p.title}</span></div>`);
      item.addEventListener('click', () => navigate(id));
      nav.appendChild(item);
    }
  }
  rail.appendChild(nav);

  const railFoot = frag(`
    <div class="rail-foot">
      <div class="rail-status down" id="railStatus">
        <span class="led"></span>
        <div class="st-text"><b id="railStTitle">Server offline</b><span id="railStSub">Not running</span></div>
      </div>
    </div>`);
  rail.appendChild(railFoot);

  // main
  const main = el('div.main', {},
    el('div.page-head', { id: 'pageHead' }),
    el('div.page-scroll', { id: 'pageScroll' }));

  app.appendChild(titlebar);
  app.appendChild(rail);
  app.appendChild(main);
}

function setActiveNav(id) {
  document.querySelectorAll('.nav-item').forEach((n) =>
    n.classList.toggle('active', n.dataset.id === id));
}

function renderHeader(page) {
  const head = document.getElementById('pageHead');
  clear(head);
  // NOTE: frag() returns only the first element of its template, so the icon
  // and the text block must be appended as two separate fragments — otherwise
  // the title/subtitle are silently dropped and the icon floats alone.
  head.appendChild(frag(`<div class="ph-icon">${icon(page.icon)}</div>`));
  head.appendChild(frag(`<div class="ph-text"><h2>${page.title}</h2><p>${page.subtitle}</p></div>`));
  const actions = el('div.ph-actions', { id: 'phActions' });
  head.appendChild(actions);

  if (page.needsTarget) actions.appendChild(buildTargetPicker());
  return actions;
}

function buildTargetPicker() {
  const wrap = el('div.target-pick', {}, el('span.tp-label', { text: 'Account' }));
  const sel = select(
    store.get().accounts.map((a) => ({ value: a.serverId, label: `${a.nickname} · ${a.serverId}` })),
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

// --------------------------------------------------------------- routing

function navigate(id, force = false) {
  const page = byId[id] || byId.dashboard;
  if (id === currentId && !force) return;
  if (cleanup) { try { cleanup(); } catch {} cleanup = null; }
  currentId = page.id;
  location.hash = `#/${page.id}`;
  setActiveNav(page.id);
  renderHeader(page);

  const scroll = document.getElementById('pageScroll');
  clear(scroll);
  const root = el('div.page-enter', {});
  scroll.appendChild(root);
  scroll.scrollTop = 0;

  Promise.resolve(page.mount(root, { navigate, rerender: () => navigate(page.id, true) }))
    .then((c) => { cleanup = typeof c === 'function' ? c : null; })
    .catch((e) => { root.appendChild(frag(`<div class="empty"><b>Page failed</b><span>${String(e.message || e)}</span></div>`)); });
}

// --------------------------------------------------------------- live status

// Cache the last rendered strings so the 2–8s poll loop doesn't touch the DOM
// (classList + two text nodes) unless something actually changed.
let railPrev = { cls: '', title: '', sub: '' };

function applyRailStatus() {
  const s = store.get();
  const node = document.getElementById('railStatus');
  if (!node) return;

  let cls, title, sub;
  if (s.online) {
    // genuinely ready (db-backed status answered)
    cls = 'up';
    title = 'Server online';
    sub = s.status
      ? `v${s.status.gameVersion} · :${s.status.apiPort} · ${s.status.accountCount} acct`
      : `Ready · ${s.probeTarget}`;
  } else if (s.live) {
    // web host answers /health but DB-backed status is not ready yet
    cls = 'starting';
    title = 'Starting…';
    sub = `Listening · not ready · ${s.probeTarget}`;
  } else if (s.procServer === 'starting') {
    cls = 'starting';
    title = 'Starting…';
    sub = 'Booting server';
  } else if (s.procServer === 'running') {
    cls = 'starting';
    title = 'Process up';
    sub = `No response on ${s.probeTarget}`;
  } else {
    cls = 'down';
    title = 'Server offline';
    sub = 'Not running';
  }

  if (cls === railPrev.cls && title === railPrev.title && sub === railPrev.sub) return;
  railPrev = { cls, title, sub };
  node.classList.remove('up', 'down', 'starting');
  node.classList.add(cls);
  document.getElementById('railStTitle').textContent = title;
  document.getElementById('railStSub').textContent = sub;
}

let wasReady = false;
async function poll() {
  let pStatus = null;
  try { pStatus = await window.host.procStatus(); } catch { /* ignore */ }
  const { live, ready, status } = await api.probe();
  if (live && !ready && status == null) { /* keep prior status null below */ }
  if (ready) {
    if (!wasReady) { await api.refreshBase(); await reloadAccounts(); refreshTargetPicker(); }
  }
  wasReady = ready;
  store.set({
    online: ready,
    live,
    status: ready ? status : null,
    lastCheckedTs: Date.now(),
    probeTarget: api.hostPort(),
    serverPid: pStatus ? pStatus.serverPid : store.get().serverPid,
  });
}

// Adaptive scheduler: probe hard while the server is booting (that's when the
// user is watching), relax once state is stable, and back off when the window
// is hidden. Replaces a flat 4s setInterval.
let pollTimer = null;
let pollBusy = false;

function pollDelay() {
  const s = store.get();
  if (document.hidden) return 15000;                       // minimized — relax
  if (s.live && !s.online) return 1500;                    // booting — tight loop
  if (s.procServer === 'starting') return 1500;
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

// When the window comes back, refresh immediately instead of waiting out a
// long hidden-state delay.
document.addEventListener('visibilitychange', () => { if (!document.hidden) schedulePoll(true); });

function refreshTargetPicker() {
  const actions = document.getElementById('phActions');
  if (actions && currentId && byId[currentId].needsTarget) {
    clear(actions);
    actions.appendChild(buildTargetPicker());
  }
}

// --------------------------------------------------------------- boot

async function boot() {
  await initTheme();

  // First-run gate: with no server project present there is nothing for the
  // shell to drive, so offer to download it from GitHub or locate an existing
  // copy. The gate reloads the app once a project is set.
  let project = null;
  try { project = await window.host.projectStatus(); } catch { /* treat as found */ }
  if (project && !project.found) {
    renderProjectGate(document.getElementById('app'), project, { titlebar: buildTitlebar() });
    applyTheme(document.documentElement.dataset.theme); // sync toggle glyph now #themeBtn exists
    return;
  }

  buildShell();
  applyTheme(document.documentElement.dataset.theme); // sync the toggle button glyph

  window.host.onProcState((d) => {
    const prevServer = store.get().procServer;
    if (d.server) store.set({ procServer: d.server });
    if (d.mitm) store.set({ procMitm: d.mitm });
    if (d.serverPid !== undefined) store.set({ serverPid: d.serverPid });
    // lifecycle flip (start/stop) — re-probe right away so the UI reacts
    if (d.server && d.server !== prevServer) schedulePoll(true);
  });
  window.host.onProcLog((d) => pushLog(d));

  // Passive "server update available" notice (checked at launch + every few
  // hours by the main process). Applying stays manual on the Updates page.
  window.host.onServerUpdate((d) => {
    const n = d.behind === 1 ? '1 commit' : `${d.behind} commits`;
    toast(`Server update available — ${n} behind (${d.remoteShort}: ${d.remoteSubject}). Open the Updates page to install.`, 'good', 'Server update');
  });

  store.subscribe(() => applyRailStatus());

  const start = (location.hash || '').replace('#/', '') || 'dashboard';
  navigate(byId[start] ? start : 'dashboard');

  await api.refreshBase();
  store.set({ probeTarget: api.hostPort() });
  schedulePoll(true);
}

// Module scripts are deferred, so the DOM is already parsed here.
boot();
