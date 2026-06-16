import { el, frag, clear, button, escapeHtml } from '../ui.js';
import { icon } from '../icons.js';
import { store } from '../api.js';
import { logBuffer, onLog, clearLog } from '../bus.js';

function bigButton(label, iconName, variant, onClick) {
  const b = frag(`<button class="btn btn-${variant} btn-skew"><span>${icon(iconName)}</span><span>${label}</span></button>`);
  b.addEventListener('click', onClick);
  return b;
}

function statusPill(state) {
  const map = {
    running: ['good', 'Running'], starting: ['warn', 'Starting'],
    stopped: ['', 'Stopped'], failed: ['bad', 'Failed'],
  };
  const [cls, label] = map[state] || ['', 'Stopped'];
  return frag(`<span class="pill ${cls}"><span class="dot"></span>${label}</span>`);
}

// One of the three honest server states, derived from the probe fields on the
// store: ready (online) > live-but-not-ready (starting) > offline.
function serverPhase(s) {
  if (s.online) return { cls: 'up', pill: 'good', label: 'Online', detail: 'DB-ready · serving admin API' };
  if (s.live || s.procServer === 'starting' || s.procServer === 'running') {
    return { cls: 'starting', pill: 'warn', label: 'Starting', detail: 'Host is up — waiting for the database' };
  }
  return { cls: 'down', pill: '', label: 'Offline', detail: 'Server process is not running' };
}

function relAgo(ts) {
  if (!ts) return 'never';
  const secs = Math.max(0, Math.floor((Date.now() - ts) / 1000));
  if (secs < 2) return 'just now';
  if (secs < 60) return `${secs}s ago`;
  const m = Math.floor(secs / 60);
  if (m < 60) return `${m}m ago`;
  return `${Math.floor(m / 60)}h ago`;
}

export default {
  id: 'server',
  title: 'Server Control',
  subtitle: 'Launch, stop and monitor the game server and traffic proxy',
  icon: 'server',
  needsTarget: false,

  mount(root) {
    // ---- status strip (3 honest states) -----------------------------------
    const stripLed = el('span.led', {});
    const stripLabel = el('b', {});
    const stripDetail = el('span', {});
    const strip = el('div.rail-status', { style: { flex: '1', minWidth: '0' } },
      stripLed,
      el('div.st-text', { style: { minWidth: '0' } }, stripLabel, stripDetail));

    const metaTarget = el('span.mono', { 'data-selectable': true, style: { color: 'var(--ink-2)' } });
    const metaPid = el('span.mono', { 'data-selectable': true, style: { color: 'var(--ink-2)' } });
    const metaChecked = el('span', { style: { color: 'var(--ink-3)' } });
    const stripMeta = el('div', {
      style: {
        display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px 16px',
        minWidth: '0', fontSize: '12px', justifyContent: 'flex-end',
      },
    },
      el('span', { style: { display: 'inline-flex', gap: '6px', minWidth: '0' } },
        el('span', { text: 'Target', style: { color: 'var(--ink-3)' } }), metaTarget),
      el('span', { style: { display: 'inline-flex', gap: '6px', minWidth: '0' } },
        el('span', { text: 'PID', style: { color: 'var(--ink-3)' } }), metaPid),
      el('span', { style: { display: 'inline-flex', gap: '6px', minWidth: '0' } },
        el('span', { text: 'Checked', style: { color: 'var(--ink-3)' } }), metaChecked));

    const statusCard = el('div.card', {},
      el('div.card-body', {
        style: { display: 'flex', alignItems: 'center', gap: '18px', flexWrap: 'wrap' },
      }, strip, stripMeta));

    // ---- launch deck -------------------------------------------------------
    const serverState = el('span', { style: { minWidth: '0' } });
    const mitmState = el('span', { style: { minWidth: '0' } });

    // single power control — one button drives BOTH the game server and the proxy
    const isUp = (s) => s.online || s.procServer === 'running' || s.procServer === 'starting'
      || s.procMitm === 'running' || s.procMitm === 'starting';
    const powerBtn = bigButton('Start Server + Proxy', 'play', 'primary', async () => {
      const { toast } = await import('../ui.js');
      const up = isUp(store.get());
      powerBtn.disabled = true;
      try {
        if (up) { await window.host.systemStop(); toast('Stopping server + proxy…', 'warn'); }
        else { await window.host.systemStart(); toast('Starting server + proxy…', 'good'); }
      } finally { powerBtn.disabled = false; }
    });
    const powerIcon = powerBtn.querySelector('span:first-child');
    const powerLabel = powerBtn.querySelector('span:last-child');

    const stateLine = (label, node) => el('div.row', { style: { gap: '9px', minWidth: '0' } },
      el('span', { text: label, style: { fontSize: '12.5px', color: 'var(--ink-2)', fontWeight: '600' } }), node);

    const deck = el('div.card', {},
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Launch deck' }),
        el('span.sub', { text: 'server + wireguard proxy · BlueArchive.exe' }), el('div.spacer', {})),
      el('div.card-body', {},
        el('div.row.wrap', { style: { gap: '16px', minWidth: '0' } }, powerBtn),
        frag('<div class="hazard" style="margin:18px 0"></div>'),
        el('div.row.wrap', { style: { gap: '22px', minWidth: '0' } },
          stateLine('Game server', serverState),
          stateLine('Traffic proxy', mitmState))));

    // ---- console -----------------------------------------------------------
    const consoleEl = el('div.console', {});
    let following = true;
    let filter = 'all';

    const followBtn = button('Auto-scroll', { variant: 'ghost', sm: true, iconName: 'bolt', onClick: () => {
      following = !following; followBtn.classList.toggle('btn-primary', following);
      if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
    }});
    followBtn.classList.add('btn-primary');
    const clearBtn = button('Clear', { variant: 'ghost', sm: true, iconName: 'trash', onClick: () => { clearLog(); repaintLog(); } });
    const srcSel = frag(`<select class="select btn-sm" style="height:32px;width:120px">
      <option value="all">All output</option><option value="server">Server</option><option value="mitm">Proxy</option></select>`);
    srcSel.addEventListener('change', () => { filter = srcSel.value; repaintLog(); });

    const consoleCard = el('div.card', {
      style: { display: 'flex', flexDirection: 'column', flex: '1', minHeight: '0' },
    },
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Live console' }),
        el('div.spacer', {}), srcSel, followBtn, clearBtn),
      el('div.card-body.tight', {
        style: { display: 'flex', flexDirection: 'column', flex: '1', minHeight: '0' },
      }, consoleEl));

    function lineNode(entry) {
      const isSys = entry.line.startsWith('>');
      const cls = isSys ? 'sys' : entry.source;
      return frag(`<div class="ln ${cls}"><span class="src">${entry.source}</span>${escapeHtml(entry.line)}</div>`);
    }
    function repaintLog() {
      clear(consoleEl);
      const rows = logBuffer.filter((e) => filter === 'all' || e.source === filter);
      if (!rows.length) { consoleEl.appendChild(frag('<div class="ln" style="color:var(--ink-3)">— no output yet — launch the server to see logs —</div>')); return; }
      for (const e of rows) consoleEl.appendChild(lineNode(e));
      if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
    }

    const unsubLog = onLog((entry) => {
      if (!entry) { repaintLog(); return; }
      if (filter !== 'all' && entry.source !== filter) return;
      consoleEl.appendChild(lineNode(entry));
      while (consoleEl.childElementCount > 1400) consoleEl.removeChild(consoleEl.firstChild);
      if (following) consoleEl.scrollTop = consoleEl.scrollHeight;
    });
    consoleEl.addEventListener('scroll', () => {
      const atBottom = consoleEl.scrollTop + consoleEl.clientHeight >= consoleEl.scrollHeight - 30;
      if (!atBottom && following) { following = false; followBtn.classList.remove('btn-primary'); }
    });

    // ---- shortcuts ---------------------------------------------------------
    const shortcutBody = el('div.row.wrap', { style: { gap: '10px', minWidth: '0' } });
    const folders = [
      ['Server folder', 'folder', (p) => p.serverDir],
      ['Proxy scripts', 'folder', (p) => p.scriptsDir],
      ['Config file', 'config', (p) => p.configPath],
      ['Database', 'inventory', (p) => p.dbPath],
    ];
    for (const [label, ic, pick] of folders) {
      shortcutBody.appendChild(button(label, { variant: 'ghost', sm: true, iconName: ic, onClick: async () => {
        const p = await window.host.paths();
        window.host.openPath(pick(p));
      }}));
    }
    const shortcuts = el('div.card', {},
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Shortcuts' })),
      el('div.card-body', {}, shortcutBody));

    // ---- assemble: flex column so the console fills remaining height -------
    // root is the .page-enter wrapper inside the scroll viewport; make it a
    // full-height flex column so the console card can grow into the leftover
    // space (and never push the shell into a horizontal scroll).
    root.style.display = 'flex';
    root.style.flexDirection = 'column';
    root.style.minHeight = '100%';
    root.style.minWidth = '0';

    const page = el('div', {
      style: { display: 'flex', flexDirection: 'column', gap: '18px', flex: '1', minHeight: '0', minWidth: '0' },
    });
    page.appendChild(statusCard);
    page.appendChild(deck);
    page.appendChild(consoleCard);
    page.appendChild(shortcuts);
    root.appendChild(page);

    function paintState() {
      const s = store.get();

      // status strip
      const ph = serverPhase(s);
      strip.className = 'rail-status ' + ph.cls;
      strip.style.flex = '1';
      strip.style.minWidth = '0';
      stripLabel.textContent = ph.label;
      stripDetail.textContent = ph.detail;
      metaTarget.textContent = s.probeTarget || '—';
      metaPid.textContent = s.serverPid != null ? String(s.serverPid) : '—';
      metaChecked.textContent = relAgo(s.lastCheckedTs);

      // single power button reflects combined server+proxy state
      const up = isUp(s);
      powerLabel.textContent = up ? 'Stop Server + Proxy' : 'Start Server + Proxy';
      powerIcon.innerHTML = icon(up ? 'stop' : 'play');
      powerBtn.classList.toggle('btn-primary', !up);
      powerBtn.classList.toggle('btn-danger', up);

      // launch-deck pills
      const srv = s.online ? 'running' : s.procServer;
      clear(serverState); serverState.appendChild(statusPill(srv));
      clear(mitmState); mitmState.appendChild(statusPill(s.procMitm));
    }
    paintState();
    repaintLog();
    const unsubStore = store.subscribe(paintState);

    // keep the "checked … ago" label honest between probes
    const tick = setInterval(() => { metaChecked.textContent = relAgo(store.get().lastCheckedTs); }, 1000);

    return () => { unsubLog(); unsubStore(); clearInterval(tick); };
  },
};
