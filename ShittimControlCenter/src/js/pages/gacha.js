import { el, frag, button, toast, shortDate, escapeHtml } from '../ui.js';
import { api } from '../api.js';
import { gate, loadInto } from './_util.js';

export default {
  id: 'gacha',
  title: 'Gacha Banners',
  subtitle: 'Browse the recruitment banners defined in the game data',
  icon: 'gacha',
  needsTarget: false,

  mount(root) {
    return gate(root, {}, { needServer: true }, (root) => {
      root.appendChild(frag(`<div class="row wrap" style="margin:-2px 0 16px;gap:8px">
        <span style="font-size:12.5px;color:var(--ink-2);min-width:0">Banner definitions are baked into the Excel game data (read-only). Tune drop rates and the guaranteed pickup in
        <b style="color:var(--blue-ink);cursor:pointer" id="goRates">Gacha Rates →</b></span></div>`));
      root.querySelector('#goRates')?.addEventListener('click', () => document.querySelector('.nav-item[data-id="rates"]').click());

      const grid = el('div', { style: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(100%, 300px), 1fr))', gap: '16px', minWidth: '0' } });
      root.appendChild(grid);

      loadInto(grid, () => api.gachaBanners(), (grid, banners) => {
        if (!banners.length) { grid.appendChild(frag('<div class="empty"><b>No banners found</b><span>Excel recruitment data is unavailable</span></div>')); return; }
        for (const b of banners) {
          const flags = [];
          if (b.isNewbie) flags.push('<span class="tag gold">Newbie</span>');
          if (b.isSelect) flags.push('<span class="tag">Selector</span>');
          const feat = (b.featured || []).slice(0, 8)
            .map((f) => `<span class="tag">${escapeHtml(f.name)}</span>`).join(' ') || '<span class="muted" style="font-size:12px">no featured students</span>';
          grid.appendChild(frag(`<div class="banner-card">
            <div class="bc-top">
              <b style="font-family:var(--font-round);font-size:14.5px;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" data-selectable>Banner ${escapeHtml(String(b.id))}</b>
              <span class="bc-id" data-selectable>order ${escapeHtml(String(b.displayOrder))}</span>
              <div style="flex:1;min-width:8px"></div>${flags.join(' ')}
            </div>
            <div class="bc-feat">${feat}</div>
            <div class="muted" style="font-size:11.5px;color:var(--ink-3);overflow-wrap:anywhere" data-selectable>${b.saleFrom ? `${escapeHtml(b.saleFrom)} → ${escapeHtml(b.saleTo || '')}` : 'no sale window'}</div>
          </div>`));
        }
      });
    });
  },
};
