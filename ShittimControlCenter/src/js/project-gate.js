import { el, frag, clear, button, toast, escapeHtml } from './ui.js';
import { icon } from './icons.js';

const BRAND_IMG = '../Sprite/Common_Icon_Setting_Account.png';

function fmtBytes(n) {
  if (!n) return '0 MB';
  const mb = n / (1024 * 1024);
  return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`;
}

// First-run / recovery screen shown when no server project is present. Offers
// two routes: download a fresh copy of the repo from GitHub, or point at an
// existing folder. On success the whole app reloads so every module re-resolves
// its paths against the newly-set project.
export function renderProjectGate(appRoot, status, { titlebar }) {
  clear(appRoot);
  appRoot.appendChild(titlebar);

  // target folder for a download (a parent + /Shittim-Server); user can change.
  const sep = (status.defaultDir || '').includes('\\') ? '\\' : '/';
  let targetDir = status.defaultDir || '';
  let busy = false;

  const wrap = el('div', {
    style: {
      gridColumn: '1 / -1', gridRow: '2', minHeight: '0', overflow: 'auto',
      display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '34px 26px',
    },
  });
  const col = el('div', { style: { width: '100%', maxWidth: '600px', minWidth: '0' } });
  wrap.appendChild(col);
  appRoot.appendChild(wrap);

  // ---- header ---------------------------------------------------------------
  col.appendChild(frag(`
    <div style="display:flex;align-items:center;gap:14px;margin-bottom:6px">
      <img class="brand-img" src="${BRAND_IMG}" alt="" style="height:46px;width:auto">
      <div style="min-width:0">
        <h2 style="font-size:20px;font-weight:800;color:var(--ink);line-height:1.15;margin:0">Server project not found</h2>
        <p style="font-size:13px;color:var(--ink-2);margin:4px 0 0;line-height:1.5">
          The control center needs the Shittim-Server project to run. Download the
          latest copy from GitHub, or point it at a folder you already have.
        </p>
      </div>
    </div>`));
  col.appendChild(frag(`<div class="hazard" style="margin:18px 0"></div>`));

  // ---- download card --------------------------------------------------------
  const targetLabel = el('span.mono', {
    text: targetDir,
    'data-selectable': true,
    style: { fontSize: '12px', color: 'var(--blue-ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', minWidth: '0' },
  });
  const changeBtn = button('Change…', { variant: 'ghost', sm: true, iconName: 'folder', onClick: async () => {
    if (busy) return;
    const picked = await window.host.pickFolder();
    if (!picked) return;
    targetDir = picked.replace(/[\\/]+$/, '') + sep + 'Shittim-Server';
    targetLabel.textContent = targetDir;
  }});

  const dlBtn = button('Download latest project', { variant: 'primary', iconName: 'download', onClick: doDownload });

  const progressWrap = el('div', { style: { display: 'none', marginTop: '14px' } });
  const progressBar = el('div', { style: { height: '100%', width: '0%', background: 'linear-gradient(90deg, var(--blue), var(--blue-ink))', borderRadius: '999px', transition: 'width .15s ease' } });
  const progressTrack = el('div', { style: { height: '8px', background: 'var(--surface-2)', border: '1px solid var(--line)', borderRadius: '999px', overflow: 'hidden' } }, progressBar);
  const progressText = el('div', { style: { fontSize: '12px', color: 'var(--ink-2)', marginTop: '8px', display: 'flex', justifyContent: 'space-between', gap: '12px' } });
  progressWrap.appendChild(progressTrack);
  progressWrap.appendChild(progressText);

  const downloadCard = el('div.card', {},
    el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Download latest' }),
      el('span.sub', { text: 'Neoexm/Shittim-Server · main' }), el('div.spacer', {})),
    el('div.card-body', {},
      el('p', {
        html: 'Fetches a zip of the latest commit from GitHub and unpacks it into the folder below.',
        style: { fontSize: '13px', color: 'var(--ink-2)', margin: '0 0 14px', lineHeight: '1.6' },
      }),
      el('div', { style: { display: 'flex', alignItems: 'center', gap: '10px', minWidth: '0', padding: '10px 12px', background: 'var(--surface-2)', border: '1px solid var(--line)', borderRadius: 'var(--r-sm)' } },
        el('span', { text: 'Install to', style: { fontSize: '11.5px', fontWeight: '700', color: 'var(--ink-3)', flex: 'none' } }),
        targetLabel, el('div.spacer', { style: { flex: '1' } }), changeBtn),
      el('div', { style: { marginTop: '16px' } }, dlBtn),
      progressWrap));

  col.appendChild(downloadCard);

  // ---- locate card ----------------------------------------------------------
  const locateBtn = button('Locate folder…', { variant: 'ghost', iconName: 'folder', onClick: doLocate });
  const locateCard = el('div.card', { style: { marginTop: '16px' } },
    el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Use an existing folder' })),
    el('div.card-body', {},
      el('p', {
        html: 'Already have the project? Choose the repo folder (the one that contains <b>Shittim-Server</b>) or the <b>Shittim-Server</b> project folder itself.',
        style: { fontSize: '13px', color: 'var(--ink-2)', margin: '0 0 14px', lineHeight: '1.6' },
      }),
      locateBtn));
  col.appendChild(locateCard);

  // ---- actions --------------------------------------------------------------

  function setBusy(on) {
    busy = on;
    dlBtn.disabled = on;
    locateBtn.disabled = on;
    changeBtn.disabled = on;
  }

  function showProgress(pct, label) {
    progressWrap.style.display = 'block';
    progressBar.style.width = `${Math.max(2, Math.min(100, pct))}%`;
    clear(progressText);
    progressText.appendChild(el('span', { text: label }));
  }

  let unsub = null;
  async function doDownload() {
    if (busy) return;
    setBusy(true);
    showProgress(2, 'Starting…');
    unsub = window.host.onProjectProgress((d) => {
      if (d.phase === 'download') {
        const pct = d.total ? (d.recv / d.total) * 100 : 0;
        showProgress(d.total ? pct : 8, d.total ? `Downloading… ${fmtBytes(d.recv)} / ${fmtBytes(d.total)}` : `Downloading… ${fmtBytes(d.recv)}`);
      } else if (d.phase === 'resolve') {
        showProgress(4, 'Resolving latest commit…');
      } else if (d.phase === 'extract') {
        showProgress(92, 'Extracting…');
      } else if (d.phase === 'install') {
        showProgress(97, 'Installing files…');
      } else if (d.phase === 'done') {
        showProgress(100, 'Done');
      } else if (d.phase === 'error') {
        showProgress(100, d.message || 'Failed');
      }
    });
    try {
      const res = await window.host.projectDownload({ targetDir });
      if (unsub) { unsub(); unsub = null; }
      if (res && res.ok) {
        showProgress(100, `Installed ${res.sha || ''} — starting…`);
        toast('Project downloaded', 'good', 'Ready');
        setTimeout(() => location.reload(), 500);
      } else {
        toast((res && res.error) || 'Download failed', 'bad', 'Download failed');
        showProgress(100, (res && res.error) || 'Download failed');
        setBusy(false);
      }
    } catch (e) {
      if (unsub) { unsub(); unsub = null; }
      toast(String(e.message || e), 'bad', 'Download failed');
      setBusy(false);
    }
  }

  async function doLocate() {
    if (busy) return;
    const picked = await window.host.pickFolder();
    if (!picked) return;
    setBusy(true);
    try {
      const res = await window.host.projectSetPath(picked);
      if (res && res.ok) {
        toast('Project located', 'good', 'Ready');
        setTimeout(() => location.reload(), 350);
      } else {
        toast((res && res.error) || 'No project found in that folder.', 'bad', 'Not found');
        setBusy(false);
      }
    } catch (e) {
      toast(String(e.message || e), 'bad');
      setBusy(false);
    }
  }
}
