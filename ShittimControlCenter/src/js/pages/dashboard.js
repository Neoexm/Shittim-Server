import { el, frag, clear, button, num, relTime, toast } from '../ui.js';
import { icon } from '../icons.js';
import { store } from '../api.js';

function stat(iconName, value, label) {
  return frag(`<div class="stat">
    <div class="stat-ico">${icon(iconName)}</div>
    <div class="stat-val">${value}</div>
    <div class="stat-label">${label}</div>
  </div>`);
}

function diagRow(name, info, fixBtn) {
  const status = info?.status || 'missing';
  const row = frag(`<div class="diag-row">
    <span class="d-led ${status}"></span>
    <span class="d-name">${name}</span>
    <span class="d-detail">${(info?.detail || '').replace(/</g, '&lt;')}</span>
  </div>`);
  if (fixBtn) row.appendChild(fixBtn);
  return row;
}

export default {
  id: 'dashboard',
  title: 'Overview',
  subtitle: 'Operational readiness and server status at a glance',
  icon: 'dashboard',
  needsTarget: false,

  mount(root) {
    const statsRow = el('div.grid-3', { style: { gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' } });
    const diagBody = el('div.diag', { style: { minWidth: '0' } });
    const probeBody = el('div', { style: { minWidth: '0' } });
    const actionsBody = el('div', { style: { minWidth: '0' } });

    const left = el('div', { style: { display: 'flex', flexDirection: 'column', gap: '18px', minWidth: '0' } });
    const right = el('div', { style: { display: 'flex', flexDirection: 'column', gap: '18px', minWidth: '0' } });

    // readiness card
    let busy = false; // a setup install is in flight — gate the buttons
    const refreshBtn = button('Re-check', { variant: 'ghost', sm: true, iconName: 'refresh', onClick: () => loadDiag() });
    const setupBtn = button('Install missing', { variant: 'primary', sm: true, iconName: 'download', onClick: () => runSetup('all') });
    left.appendChild(cardWith('Environment readiness', 'Toolchain & data prerequisites', [setupBtn, refreshBtn], diagBody));

    // connection / probe detail
    right.appendChild(cardWith('Connection', 'Live readiness probe', [], probeBody));

    // quick actions
    right.appendChild(cardWith('Quick actions', null, [], actionsBody));
    const actions = buildActions(actionsBody);

    root.appendChild(statsRow);
    root.appendChild(el('div.grid-2', { style: { gridTemplateColumns: 'minmax(0, 1.3fr) minmax(0, 1fr)', alignItems: 'start' } }, left, right));

    function paintStats() {
      const s = store.get();
      clear(statsRow);
      // store.online == DB-ready (status answered), not merely listening.
      const online = s.online;
      const subLabel = online ? 'Database ready' : s.live ? 'Listening, not ready' : 'Server not running';
      statsRow.appendChild(stat('server', online ? 'Online' : 'Offline', subLabel));
      statsRow.appendChild(stat('users', num(s.status?.accountCount ?? s.accounts.length ?? 0), 'Registered accounts'));
      statsRow.appendChild(stat('bolt', s.status?.gameVersion ? `v${s.status.gameVersion}` : '—', 'Game data version'));
      statsRow.appendChild(stat('clock', s.status ? relTime(s.status.uptimeSeconds) : '—', 'Server uptime'));
      // colour the first tile: ready=good, live-but-not-ready=warn, down=muted
      const first = statsRow.firstElementChild.querySelector('.stat-val');
      if (first) first.style.color = online ? 'var(--good)' : s.live ? 'var(--warn)' : 'var(--ink-3)';

      // single power button reflects combined server+proxy state
      if (actions && actions.power) {
        const up = actions.isUp(s);
        const lbl = actions.power.querySelector('span');
        if (lbl) lbl.textContent = up ? 'Stop server + proxy' : 'Start server + proxy';
        const svg = actions.power.querySelector('svg');
        if (svg) svg.outerHTML = icon(up ? 'stop' : 'play');
        actions.power.classList.toggle('btn-primary', !up);
        actions.power.classList.toggle('btn-danger', up);
      }

      paintProbe(s);
    }

    function paintProbe(s) {
      clear(probeBody);
      const online = s.online;
      const kind = online ? 'good' : s.live ? 'warn' : 'bad';
      const stateLabel = online ? 'Online' : s.live ? 'Not ready' : 'Offline';
      const pill = frag(`<span class="pill ${kind}"><span class="dot"></span>${stateLabel}</span>`);

      const checkedAgo = (() => {
        if (!s.lastCheckedTs) return 'never';
        const secs = Math.max(0, Math.round((Date.now() - s.lastCheckedTs) / 1000));
        return `${relTime(secs)} ago`;
      })();

      const rows = el('div', { style: { display: 'flex', flexDirection: 'column', gap: '10px', minWidth: '0' } });
      rows.appendChild(probeRow('State', pill));
      rows.appendChild(probeRow('Probe target', el('span.mono', { text: s.probeTarget || '—', style: { fontSize: '12.5px', color: 'var(--ink-2)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', minWidth: '0' } })));
      rows.appendChild(probeRow('Last checked', el('span', { text: checkedAgo, style: { fontSize: '12.5px', color: 'var(--ink-2)' } })));
      if (s.serverPid != null) {
        rows.appendChild(probeRow('Server PID', el('span.mono', { text: String(s.serverPid), style: { fontSize: '12.5px', color: 'var(--ink-2)' } })));
      }
      probeBody.appendChild(rows);
    }

    // A small "Install" / "Fix" button for an installable prerequisite, shown
    // only when that check isn't already satisfied.
    function fixBtn(step, info) {
      if (busy) return null;
      const ready = (info?.status || 'missing') === 'ready';
      if (ready) return null;
      const label = step === 'certificate' ? 'Trust cert' : 'Install';
      return button(label, { variant: 'ghost', sm: true, iconName: 'download', onClick: () => runSetup(step) });
    }

    async function loadDiag() {
      diagBody.innerHTML = `<div class="empty"><div class="spinner"></div></div>`;
      try {
        const env = await window.host.envCheck();
        clear(diagBody);
        diagBody.appendChild(diagRow('.NET SDK', env.dotnet, fixBtn('dotnet', env.dotnet)));
        diagBody.appendChild(diagRow('Server build', env.server));
        diagBody.appendChild(diagRow('Game database', env.database));
        diagBody.appendChild(diagRow('mitmproxy', env.mitmproxy, fixBtn('mitmproxy', env.mitmproxy)));
        diagBody.appendChild(diagRow('CA certificate', env.certificate, fixBtn('certificate', env.certificate)));
        diagBody.appendChild(diagRow('Redirect script', env.redirect));
        // gate the header "Install missing" button on there being something to do
        const anyMissing = ['dotnet', 'mitmproxy', 'certificate'].some((k) => (env[k]?.status || 'missing') !== 'ready');
        setupBtn.disabled = busy || !anyMissing;
      } catch (e) {
        diagBody.innerHTML = `<div class="empty"><b>Check failed</b><span>${String(e.message || e)}</span></div>`;
      }
    }

    // Drive setupInstall for one step or 'all', streaming progress to toasts and
    // re-checking the environment when it finishes. mitmproxy/.NET install
    // per-user (silent); trusting the CA raises one Windows elevation prompt.
    async function runSetup(which) {
      if (busy) return;
      busy = true;
      setupBtn.disabled = true; refreshBtn.disabled = true;
      const labels = { dotnet: '.NET 10 SDK', mitmproxy: 'mitmproxy', certificate: 'CA certificate', all: 'prerequisites' };
      toast(`Installing ${labels[which] || which}… this can take a few minutes.`, 'good', 'Setup started');
      const unsub = window.host.onSetupProgress((d) => {
        if (d.status === 'done') toast(d.message || `${labels[d.step] || d.step} ready`, 'good');
        else if (d.status === 'failed') toast(d.message || `${labels[d.step] || d.step} failed`, 'bad');
        else if (d.status === 'running' && d.message) toast(d.message, 'good');
        // reflect each phase change live in the readiness list
        if (d.status === 'done' || d.status === 'failed') loadDiag();
      });
      try {
        const res = await window.host.setupInstall(which);
        if (res.ok) toast('All prerequisites are ready.', 'good', 'Setup complete');
        else {
          const failed = Object.entries(res.results || {}).filter(([, r]) => r && !r.ok).map(([k]) => labels[k] || k);
          toast(failed.length ? `Couldn’t complete: ${failed.join(', ')}.` : (res.error || 'Setup did not finish.'), 'bad', 'Setup incomplete');
        }
      } catch (e) {
        toast(String(e.message || e), 'bad', 'Setup failed');
      } finally {
        unsub();
        busy = false;
        refreshBtn.disabled = false;
        await loadDiag();
      }
    }

    paintStats();
    loadDiag();
    const unsub = store.subscribe(paintStats);
    return unsub;
  },
};

function cardWith(title, sub, actions, body) {
  const head = el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: title }),
    sub ? el('span.sub', { text: sub }) : null, el('div.spacer', {}), ...actions);
  return el('div.card', {}, head, el('div.card-body', {}, body));
}

// One label/value line in the Connection card. The value column carries
// min-width:0 so long values (host:port, pid) ellipsis instead of clipping.
function probeRow(label, valueNode) {
  return el('div', { style: { display: 'flex', alignItems: 'center', gap: '12px', minWidth: '0' } },
    el('span', { text: label, style: { fontSize: '12px', fontWeight: '600', color: 'var(--ink-3)', flex: 'none', width: '108px' } }),
    el('div', { style: { display: 'flex', justifyContent: 'flex-end', flex: '1', minWidth: '0', overflow: 'hidden' } }, valueNode));
}

function buildActions(container) {
  clear(container);
  const isUp = (s) => s.online || s.procServer === 'running' || s.procServer === 'starting'
    || s.procMitm === 'running' || s.procMitm === 'starting';

  const power = button('Start server + proxy', { variant: 'primary', iconName: 'play', block: true, onClick: async () => {
    const up = isUp(store.get());
    power.disabled = true;
    try {
      if (up) { await window.host.systemStop(); toast('Stopping server + proxy…', 'warn'); }
      else { await window.host.systemStart(); toast('Starting server + proxy…', 'good'); }
    } finally { power.disabled = false; }
  }});
  const folder = button('Open server folder', { variant: 'ghost', iconName: 'folder', block: true, onClick: async () => {
    const p = await window.host.paths();
    window.host.openPath(p.serverDir);
  }});

  const grid = el('div', { style: { display: 'flex', flexDirection: 'column', gap: '10px' } }, power, folder);
  container.appendChild(grid);
  container.appendChild(el('div', { style: { height: '12px' } }));
  container.appendChild(frag(`<div class="hazard"></div>`));
  container.appendChild(el('p', { html: 'One button controls the server and proxy together. The live console and shortcuts live in <b>Server Control</b>.', style: { fontSize: '12.5px', color: 'var(--ink-3)', marginTop: '12px', marginBottom: '0' } }));

  return { power, isUp };
}
