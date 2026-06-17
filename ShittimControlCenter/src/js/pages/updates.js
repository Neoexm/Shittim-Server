import { el, frag, clear, button, toast, escapeHtml } from '../ui.js';
import { icon } from '../icons.js';

// Git-free updater. "Check" compares the locally recorded commit (a download
// marker, or — for a real git checkout — HEAD) against origin/<branch> through
// the GitHub API and lists the incoming changelog. "Install" fast-forwards a
// git checkout, or re-downloads the latest source for a plain folder. A rebuild
// action recompiles the .NET server the update may change.
export default {
  id: 'updates',
  title: 'Updates',
  subtitle: 'Compare against GitHub and pull the latest server',
  icon: 'download',
  needsTarget: false,

  mount(root) {
    let last = null; // last check result
    let progUnsub = null;

    const headInfo = el('div', { style: { minWidth: '0' } });
    const resultBody = el('div', { style: { minWidth: '0', marginTop: '14px' } });

    const checkBtn = button('Check for updates', { variant: 'primary', sm: true, iconName: 'refresh', onClick: doCheck });

    const versionCard = el('div.card', {},
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Version' }),
        el('span.sub', { text: 'Neoexm/Shittim-Server · main' }), el('div.spacer', {}), checkBtn),
      el('div.card-body', {}, headInfo, resultBody));

    const rebuildBtn = button('Rebuild server', { variant: 'ghost', iconName: 'bolt', onClick: doRebuild });
    const selfBtn = button('Check for app update', { variant: 'ghost', iconName: 'refresh', onClick: doSelfCheck });
    const maintCard = el('div.card', { style: { marginTop: '18px' } },
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Maintenance' })),
      el('div.card-body', {},
        el('p', {
          html: 'The update above pulls the latest <b>server</b> source — rebuild it afterwards so the new code is compiled in (build output streams to the console in <b>Server Control</b>). The <b>Control Center app</b> updates itself separately from GitHub Releases: it checks on launch and prompts you, or check now below.',
          style: { fontSize: '13px', color: 'var(--ink-2)', margin: '0 0 14px', lineHeight: '1.6' },
        }),
        el('div.row.wrap', { style: { gap: '10px' } }, rebuildBtn, selfBtn)));

    root.appendChild(versionCard);
    root.appendChild(maintCard);

    paintHead(null);
    clear(resultBody);
    resultBody.appendChild(spinnerRow('Checking origin/main…'));
    doCheck();

    // ----------------------------------------------------------------- render

    function sourceTag(info) {
      if (!info || !info.localSource) return null;
      const label = info.localSource === 'git' ? 'git checkout' : 'downloaded';
      return el('span.tag.grey', { text: label });
    }

    function paintHead(info) {
      clear(headInfo);
      const row = el('div', { style: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap', minWidth: '0' } });
      if (!info || !info.ok) {
        row.appendChild(el('span', { text: 'Current project', style: { fontSize: '13px', color: 'var(--ink-2)' } }));
      } else if (info.versionKnown === false) {
        row.appendChild(el('span', { text: 'Installed copy', style: { fontSize: '12.5px', color: 'var(--ink-3)' } }));
        if (info.branch) row.appendChild(el('span.tag', { text: info.branch }));
        row.appendChild(el('span.tag.gold', { text: 'version unknown' }));
        const st = sourceTag(info); if (st) row.appendChild(st);
      } else {
        row.appendChild(el('span', { text: 'On branch', style: { fontSize: '12.5px', color: 'var(--ink-3)' } }));
        row.appendChild(el('span.tag', { text: info.branch || 'main' }));
        if (info.head) row.appendChild(el('span.mono', { text: info.head, 'data-selectable': true, style: { fontSize: '12.5px', color: 'var(--blue-ink)' } }));
        const st = sourceTag(info); if (st) row.appendChild(st);
        if (info.headSubject) {
          row.appendChild(el('span', {
            text: info.headSubject,
            style: { fontSize: '12.5px', color: 'var(--ink-2)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', minWidth: '0' },
          }));
        }
      }
      headInfo.appendChild(row);
    }

    function spinnerRow(text) {
      const label = el('span', { text, style: { color: 'var(--ink-3)', fontSize: '13px' } });
      const row = el('div.row', { style: { gap: '10px', padding: '6px 0' } }, el('div.spinner', {}), label);
      row._label = label;
      return row;
    }

    function remoteLine(r) {
      return frag(`<div style="display:flex;gap:11px;padding:10px 13px;margin-top:12px;border:1px solid var(--line);border-radius:var(--r-sm);min-width:0">
        <span class="mono" data-selectable style="font-size:11px;color:var(--ink-3);flex:none;width:58px">${escapeHtml(r.remoteShort || '')}</span>
        <div style="min-width:0;flex:1">
          <div style="font-size:13px;color:var(--ink);overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(r.remoteSubject || '')}</div>
          <div style="font-size:11px;color:var(--ink-3)">origin/${escapeHtml(r.branch || 'main')}${r.remoteWhen ? ` · ${escapeHtml(r.remoteWhen)}` : ''}</div>
        </div>
      </div>`);
    }

    function updateNote(r) {
      const text = r.localSource === 'git'
        ? 'Installed via git — the update is a fast-forward pull and will never overwrite local edits.'
        : 'Updating re-downloads the latest source from GitHub. Your <b>Config</b>, database and build output are left untouched, but any edits to source files will be replaced.';
      return el('p', {
        html: text,
        style: { fontSize: '12px', color: 'var(--ink-3)', margin: '12px 0 0', lineHeight: '1.6' },
      });
    }

    function renderResult(r) {
      clear(resultBody);
      if (!r || !r.ok) {
        resultBody.appendChild(statusRow('bad', 'Check failed', (r && r.error) || 'Unknown error'));
        return;
      }
      paintHead(r);

      // Can't quantify the gap (no marker, or a commit GitHub can't diff). Offer
      // a clean re-download of the latest source.
      if (r.versionKnown === false || r.compareFailed) {
        const why = r.versionKnown === false
          ? 'This copy has no version marker, so its exact commit is unknown.'
          : 'This copy sits on a commit GitHub can’t diff against the branch (a local build or diverged history).';
        resultBody.appendChild(statusRow('warn', 'Can’t compare versions',
          `${why} Latest on origin/${r.branch} is ${r.remoteShort}${r.remoteWhen ? ` · ${r.remoteWhen}` : ''}.`));
        if (r.remoteSubject) resultBody.appendChild(remoteLine(r));
        resultBody.appendChild(updateNote(r));
        const btn = button('Download latest', { variant: 'primary', iconName: 'download', onClick: () => doInstall(r) });
        resultBody.appendChild(el('div', { style: { marginTop: '16px' } }, btn));
        return;
      }

      if ((r.behind || 0) <= 0) {
        resultBody.appendChild(statusRow('good', 'Up to date',
          r.ahead > 0
            ? `You are ${r.ahead} local commit${r.ahead === 1 ? '' : 's'} ahead of origin/${r.branch}.`
            : 'You have the latest version.'));
        return;
      }

      // behind — offer install
      resultBody.appendChild(statusRow('warn', `${r.behind} update${r.behind === 1 ? '' : 's'} available`,
        `origin/${r.branch} is ${r.behind} commit${r.behind === 1 ? '' : 's'} ahead of your copy.`));
      resultBody.appendChild(updateNote(r));

      // changelog
      if (r.commits && r.commits.length) {
        const list = el('div', { style: { marginTop: '14px', border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', overflow: 'hidden', maxHeight: '40vh', overflowY: 'auto' } });
        for (const c of r.commits) {
          list.appendChild(frag(`<div style="display:flex;gap:11px;padding:10px 13px;border-bottom:1px solid var(--line-2);min-width:0">
            <span class="mono" data-selectable style="font-size:11px;color:var(--ink-3);flex:none;width:58px">${escapeHtml(c.hash || '')}</span>
            <div style="min-width:0;flex:1">
              <div style="font-size:13px;color:var(--ink);overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(c.subject || '')}</div>
              <div style="font-size:11px;color:var(--ink-3)">${escapeHtml(c.author || '')}${c.when ? ` · ${escapeHtml(c.when)}` : ''}</div>
            </div>
          </div>`));
        }
        if (list.lastElementChild) list.lastElementChild.style.borderBottom = 'none';
        resultBody.appendChild(list);
      }

      const installBtn = button(`Install ${r.behind} update${r.behind === 1 ? '' : 's'}`, { variant: 'primary', iconName: 'download', onClick: () => doInstall(r) });
      resultBody.appendChild(el('div', { style: { marginTop: '16px' } }, installBtn));
    }

    function statusRow(kind, title, detail) {
      const wrap = el('div', { style: { display: 'flex', flexDirection: 'column', gap: '8px' } });
      wrap.appendChild(frag(`<span class="pill ${kind}" style="align-self:flex-start"><span class="dot"></span>${escapeHtml(title)}</span>`));
      if (detail) wrap.appendChild(el('p', { text: detail, style: { fontSize: '13px', color: 'var(--ink-2)', margin: '0', lineHeight: '1.6' } }));
      return wrap;
    }

    // ----------------------------------------------------------------- actions

    async function doCheck() {
      checkBtn.disabled = true;
      clear(resultBody);
      resultBody.appendChild(spinnerRow('Checking origin/main…'));
      try {
        last = await window.host.updatesCheck();
        renderResult(last);
      } catch (e) {
        clear(resultBody);
        resultBody.appendChild(statusRow('bad', 'Check failed', String(e.message || e)));
      } finally {
        checkBtn.disabled = false;
      }
    }

    function fmtBytes(n) {
      if (!n) return '0 MB';
      const mb = n / (1024 * 1024);
      return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`;
    }

    async function doInstall(r) {
      clear(resultBody);
      const prog = spinnerRow(r.localSource === 'git' ? 'Pulling origin/main…' : 'Updating from GitHub…');
      resultBody.appendChild(prog);

      // For a re-download, surface live progress on the same row.
      if (r.localSource !== 'git') {
        progUnsub = window.host.onProjectProgress((d) => {
          if (d.phase === 'download') prog._label.textContent = d.total ? `Downloading… ${fmtBytes(d.recv)} / ${fmtBytes(d.total)}` : `Downloading… ${fmtBytes(d.recv)}`;
          else if (d.phase === 'resolve') prog._label.textContent = 'Resolving latest commit…';
          else if (d.phase === 'extract') prog._label.textContent = 'Extracting…';
          else if (d.phase === 'install') prog._label.textContent = 'Installing files…';
          else if (d.phase === 'done') prog._label.textContent = 'Finishing…';
        });
      }

      try {
        const res = await window.host.updatesApply();
        if (progUnsub) { progUnsub(); progUnsub = null; }
        clear(resultBody);
        if (res.ok) {
          toast(`Updated to ${res.head || 'latest'}`, 'good', 'Update installed');
          resultBody.appendChild(statusRow('good', 'Update installed', `Now at ${res.head || 'latest'}. Rebuild the server below, then restart the control center.`));
          const rb = button('Rebuild server now', { variant: 'gold', iconName: 'bolt', onClick: doRebuild });
          resultBody.appendChild(el('div', { style: { marginTop: '14px' } }, rb));
        } else {
          toast('Update could not be applied', 'bad');
          const detail = res.method === 'git'
            ? 'Your local edits or a diverged branch blocked the fast-forward pull. Nothing was changed — commit or stash local changes and try again.'
            : (res.error || 'The download could not be completed. Nothing was changed.');
          resultBody.appendChild(statusRow('bad', res.method === 'git' ? 'Could not fast-forward' : 'Update failed', detail));
          if (res.output) resultBody.appendChild(el('pre.mono', { text: res.output, 'data-selectable': true, style: { marginTop: '12px', padding: '12px', background: 'var(--surface-2)', border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', fontSize: '11.5px', color: 'var(--ink-2)', whiteSpace: 'pre-wrap', wordBreak: 'break-word', maxHeight: '30vh', overflow: 'auto' } }));
          const retry = button('Check again', { variant: 'ghost', iconName: 'refresh', onClick: doCheck });
          resultBody.appendChild(el('div', { style: { marginTop: '14px' } }, retry));
        }
      } catch (e) {
        if (progUnsub) { progUnsub(); progUnsub = null; }
        toast(String(e.message || e), 'bad');
        clear(resultBody);
        resultBody.appendChild(statusRow('bad', 'Update failed', String(e.message || e)));
      }
    }

    async function doSelfCheck() {
      selfBtn.disabled = true;
      try {
        const r = await window.host.updatesCheckSelf();
        if (r.dev) { toast('Running from source — pull the repo and restart to update the app.', 'warn', 'Dev build'); return; }
        if (!r.ok) { toast(r.error || 'Update check failed.', 'bad', 'App update'); return; }
        if (r.available) toast(`Control Center ${r.version} is available — follow the prompt to install.`, 'good', 'Update available');
        else toast(`Control Center is up to date (v${r.current}).`, 'good', 'App update');
      } catch (e) {
        toast(String(e.message || e), 'bad', 'App update');
      } finally {
        selfBtn.disabled = false;
      }
    }

    async function doRebuild() {
      rebuildBtn.disabled = true;
      toast('Rebuilding server… (output in Server Control console)', 'good', 'dotnet build');
      try {
        const res = await window.host.updatesRebuild();
        toast(res.ok ? 'Server rebuilt successfully' : (res.error || `Build failed (code ${res.code})`), res.ok ? 'good' : 'bad');
      } catch (e) {
        toast(String(e.message || e), 'bad');
      } finally {
        rebuildBtn.disabled = false;
      }
    }

    // detach the progress listener if the user navigates away mid-install
    return () => { if (progUnsub) { progUnsub(); progUnsub = null; } };
  },
};
