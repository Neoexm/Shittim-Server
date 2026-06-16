import { el, frag, clear, button, input, field, toast, modal, confirmDialog, openPicker, num, stars, escapeHtml, emptyState } from '../ui.js';
import { icon } from '../icons.js';
import { api, targetAccount } from '../api.js';
import { gate, loadInto } from './_util.js';

export default {
  id: 'inventory',
  title: 'Inventory',
  subtitle: 'Grant items and manage the character roster',
  icon: 'inventory',
  needsTarget: true,

  mount(root) {
    return gate(root, {}, { needServer: true, needTarget: true }, (root) => {
      const acc = targetAccount();
      const uid = acc.serverId;

      // quick bulk actions
      const bulk = el('div.card', { style: { marginBottom: '18px' } },
        el('div.card-head', { style: { flexWrap: 'wrap', rowGap: '4px' } }, el('span.tab-mark', {}), el('h3', { text: 'Bulk grants' }),
          el('span.sub', { style: { minWidth: '0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } },
            `for ${acc.nickname} · `,
            el('span.mono', { text: `#${uid}`, 'data-selectable': '' }))),
        el('div.card-body', { style: { display: 'flex', gap: '10px', flexWrap: 'wrap', minWidth: '0' } },
          cmdBtn(uid, 'All items', 'box', 'inventory add items', 'ghost', () => itemsCard && reloadItems()),
          cmdBtn(uid, 'All equipment', 'shield', 'giveallequip', 'ghost'),
          cmdBtn(uid, 'All characters', 'users', 'giveall', 'ghost', () => reloadChars()),
          cmdBtn(uid, 'Max characters', 'star', 'max all', 'gold', () => reloadChars()),
          dangerCmd(uid, 'Clear inventory', 'trash', 'clearinventory', () => reloadItems())));
      root.appendChild(bulk);

      const grid = el('div.grid-2', { style: { alignItems: 'start' } });
      root.appendChild(grid);

      // ---- items
      const itemSearch = input({ placeholder: 'Filter…', className: 'input btn-sm', style: { height: '32px', width: '120px', minWidth: '0', flex: '0 1 120px' } });
      const giveItemBtn = button('Give item', { variant: 'primary', sm: true, iconName: 'plus', onClick: giveItem });
      const itemsCard = el('div.card', {},
        el('div.card-head', { style: { flexWrap: 'wrap', rowGap: '8px' } }, el('span.tab-mark', {}), el('h3', { text: 'Items' }),
          el('div.spacer', {}), itemSearch, giveItemBtn));
      const itemsBody = el('div.list-scroll', { style: { maxHeight: '58vh' } });
      itemsCard.appendChild(itemsBody);
      grid.appendChild(itemsCard);
      let itemRows = [];
      itemSearch.addEventListener('input', paintItems);

      function paintItems() {
        const q = itemSearch.value.trim().toLowerCase();
        const rows = itemRows.filter((r) => !q || r.name.toLowerCase().includes(q) || String(r.uniqueId).includes(q));
        clear(itemsBody);
        if (!rows.length) { itemsBody.appendChild(emptyState('No items', 'Grant some with “Give item”')); return; }
        const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th style="width:74px">ID</th><th>Name</th><th style="width:72px">Qty</th><th style="width:44px"></th></tr></thead><tbody></tbody></table>');
        const tb = tbl.querySelector('tbody');
        for (const r of rows) {
          const tr = frag(`<tr><td class="num mono" data-selectable>${r.uniqueId}</td><td style="min-width:0;max-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" title="${escapeHtml(r.name)}">${escapeHtml(r.name)}</td><td class="num">${num(r.stackCount)}</td><td></td></tr>`);
          const x = button('', { variant: 'ghost', sm: true, iconName: 'trash' });
          x.style.height = '26px'; x.style.padding = '0 8px';
          x.addEventListener('click', async (e) => { e.stopPropagation(); await api.removeItem({ accountServerId: uid, uniqueId: r.uniqueId }); toast('Item removed', 'warn'); reloadItems(); });
          tr.lastElementChild.appendChild(x);
          tb.appendChild(tr);
        }
        itemsBody.appendChild(tbl);
      }
      async function reloadItems() { await loadInto(itemsBody, () => api.items(uid), (_b, rows) => { itemRows = rows; paintItems(); }); }

      function giveItem() {
        openPicker({ title: 'Pick an item', loader: (q) => api.staticItems(q).then((r) => r.map((x) => ({ id: x.id, name: x.name, sub: x.icon }))),
          onPick: (it) => promptAmount(it, async (amount) => {
            await api.giveItem({ accountServerId: uid, uniqueId: it.id, amount });
            toast(`Gave ${num(amount)}× ${it.name}`, 'good'); reloadItems();
          }) });
      }

      // ---- characters
      const charSearch = input({ placeholder: 'Filter…', className: 'input btn-sm', style: { height: '32px', width: '120px', minWidth: '0', flex: '0 1 120px' } });
      const addCharBtn = button('Add character', { variant: 'primary', sm: true, iconName: 'plus', onClick: addChar });
      const charsCard = el('div.card', {},
        el('div.card-head', { style: { flexWrap: 'wrap', rowGap: '8px' } }, el('span.tab-mark', {}), el('h3', { text: 'Characters' }),
          el('div.spacer', {}), charSearch, addCharBtn));
      const charsBody = el('div.list-scroll', { style: { maxHeight: '58vh' } });
      charsCard.appendChild(charsBody);
      grid.appendChild(charsCard);
      let charRows = [];
      charSearch.addEventListener('input', paintChars);

      function paintChars() {
        const q = charSearch.value.trim().toLowerCase();
        const rows = charRows.filter((r) => !q || r.name.toLowerCase().includes(q) || String(r.uniqueId).includes(q));
        clear(charsBody);
        if (!rows.length) { charsBody.appendChild(emptyState('No characters', 'Add students with the button above')); return; }
        const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th style="width:74px">ID</th><th>Name</th><th style="width:84px">Grade</th><th style="width:54px">Lvl</th></tr></thead><tbody></tbody></table>');
        const tb = tbl.querySelector('tbody');
        for (const r of rows) {
          const tr = frag(`<tr><td class="num mono" data-selectable>${r.uniqueId}</td><td style="min-width:0;max-width:0"><b style="font-family:var(--font-round);display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" title="${escapeHtml(r.name)}">${escapeHtml(r.name)}</b></td><td class="stars">${stars(r.starGrade)}</td><td class="num">${r.level}</td></tr>`);
          tr.title = 'Click to max this student';
          tr.addEventListener('click', async () => {
            const ok = await confirmDialog({ title: 'Max student', confirmLabel: 'Max out', message: `Max ${r.name} (level 90, max stars, skills, gear)?` });
            if (!ok) return;
            const target = r.devName || r.name;
            try { await api.command(uid, `max ${target}`); toast(`${r.name} maxed`, 'good'); reloadChars(); }
            catch (e) { toast(e.message, 'bad'); }
          });
          tb.appendChild(tr);
        }
        charsBody.appendChild(tbl);
      }
      async function reloadChars() { await loadInto(charsBody, () => api.characters(uid), (_b, rows) => { charRows = rows; paintChars(); }); }

      function addChar() {
        openPicker({ title: 'Pick a student', loader: (q) => api.staticCharacters(q).then((r) => r.map((x) => ({ id: x.id, name: x.name, sub: `★${x.maxStar}` }))),
          onPick: (it) => {
            const lvl = input({ value: 'max', placeholder: 'max / basic / ue30 / ue50' });
            const add = button('Add', { variant: 'primary', iconName: 'plus' });
            const cancel = button('Cancel', { variant: 'ghost' });
            const ref = modal({ title: `Add ${it.name}`, body: el('div', {}, field('Preset', lvl, 'barebone · basic · ue30 · ue50 · max')), footer: [cancel, add] });
            cancel.addEventListener('click', ref.close);
            add.addEventListener('click', async () => {
              const opt = (lvl.value.trim() || 'max').toLowerCase();
              try { await api.command(uid, `character add ${it.id} ${opt}`); ref.close(); toast(`Added ${it.name}`, 'good'); reloadChars(); }
              catch (e) { toast(e.message, 'bad'); }
            });
          } });
      }

      reloadItems();
      reloadChars();

      function cmdBtn(uid, label, ic, command, variant, after) {
        return button(label, { variant, sm: true, iconName: ic, onClick: async () => {
          try { await api.command(uid, command); toast(`${label} ✓`, 'good'); after && after(); }
          catch (e) { toast(e.message, 'bad'); }
        }});
      }
      function dangerCmd(uid, label, ic, command, after) {
        return button(label, { variant: 'danger', sm: true, iconName: ic, onClick: async () => {
          const ok = await confirmDialog({ title: label, danger: true, confirmLabel: label, message: 'This wipes inventory items and unbound equipment for this account.' });
          if (!ok) return;
          try { await api.command(uid, command); toast(`${label} ✓`, 'warn'); after && after(); }
          catch (e) { toast(e.message, 'bad'); }
        }});
      }
    });
  },
};

function promptAmount(it, onConfirm) {
  const amt = input({ value: 1, type: 'number' });
  const ok = button('Grant', { variant: 'primary', iconName: 'check' });
  const cancel = button('Cancel', { variant: 'ghost' });
  const ref = modal({ title: `Give ${it.name}`, body: el('div', {}, field('Amount', amt)), footer: [cancel, ok] });
  cancel.addEventListener('click', ref.close);
  ok.addEventListener('click', () => { ref.close(); onConfirm(Math.max(1, Number(amt.value) || 1)); });
  setTimeout(() => { amt.focus(); amt.select(); }, 50);
}
