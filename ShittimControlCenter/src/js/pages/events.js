import { el, frag, clear, button, toast, confirmDialog, shortDate, escapeHtml, emptyState } from '../ui.js';
import { api, targetAccount } from '../api.js';
import { gate, loadInto } from './_util.js';

const TYPES = [
  { key: 'total', title: 'Total Assault', icon: 'shield', sub: 'Raid season' },
  { key: 'grand', title: 'Grand Assault', icon: 'bolt', sub: 'Eliminate raid' },
  { key: 'drill', title: 'Joint Firing Drill', icon: 'clock', sub: 'Time-attack dungeon' },
  { key: 'final', title: 'Final Restriction', icon: 'flask', sub: 'Multi-floor raid' },
];

export default {
  id: 'events',
  title: 'Events',
  subtitle: 'Activate raid and challenge seasons for the selected account',
  icon: 'events',
  needsTarget: true,

  mount(root) {
    return gate(root, {}, { needServer: true, needTarget: true }, (root) => {
      const acc = targetAccount();
      const uid = acc.serverId;

      root.appendChild(frag(`<div class="row wrap" style="margin:-2px 0 16px;gap:8px">
        <span class="pill blue"><span class="dot"></span>Applying to ${escapeHtml(acc.nickname)} · #${uid}</span>
        <span class="muted" style="font-size:12px;min-width:0">Seasons are server-side; changing one resets the matching content lobby for this account.</span></div>`));

      const grid = el('div.grid-2', { style: { alignItems: 'start' } });
      root.appendChild(grid);

      loadInto(grid, () => api.eventSeasons(), (grid, data) => {
        for (const t of TYPES) {
          const seasons = data[t.key] || [];
          const body = el('div.list-scroll', { style: { maxHeight: '40vh' } });
          const card = el('div.card', { style: { minWidth: '0' } },
            el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: t.title }), el('span.sub', { text: t.sub })),
            body);

          if (!seasons.length) {
            body.appendChild(emptyState('No seasons defined', 'This content has no Excel seasons'));
          } else {
            const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th>Season</th><th style="width:104px">Window</th><th style="width:72px"></th></tr></thead><tbody></tbody></table>');
            const tb = tbl.querySelector('tbody');
            for (const s of seasons) {
              const tr = frag(`<tr>
                <td style="max-width:0"><b style="font-family:var(--font-round)" data-selectable>#${escapeHtml(String(s.seasonId))}</b><div class="muted" style="font-size:11px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(s.boss || '')}</div></td>
                <td class="muted" style="font-size:11.5px;white-space:nowrap">${fmt(s.start)}<br>→ ${fmt(s.end)}</td>
                <td style="text-align:right;white-space:nowrap"></td></tr>`);
              const apply = button('Apply', { variant: 'primary', sm: true, iconName: 'check' });
              apply.style.height = '28px'; apply.style.padding = '0 12px';
              apply.addEventListener('click', async (e) => {
                e.stopPropagation();
                const ok = await confirmDialog({ title: `${t.title} → season ${s.seasonId}`, confirmLabel: 'Apply season',
                  message: `Set ${acc.nickname}'s ${t.title} to season ${s.seasonId}? Pending battles in this mode will be closed.` });
                if (!ok) return;
                try { await api.command(uid, `setseason ${t.key} ${s.seasonId}`); toast(`${t.title} set to season ${s.seasonId}`, 'good'); }
                catch (err) { toast(err.message, 'bad'); }
              });
              tr.lastElementChild.appendChild(apply);
              tb.appendChild(tr);
            }
            body.appendChild(tbl);
          }
          grid.appendChild(card);
        }
      });
    });
  },
};

function fmt(s) {
  if (!s) return '—';
  // season excel dates may be ISO or "yyyy-MM-dd HH:mm:ss"
  return shortDate(String(s).replace(' ', 'T'));
}
