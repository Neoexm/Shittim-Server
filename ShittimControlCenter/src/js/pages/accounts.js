import { el, frag, clear, button, input, field, toast, modal, confirmDialog, num, escapeHtml, emptyState } from '../ui.js';
import { icon } from '../icons.js';
import { api, store, reloadAccounts, CURRENCY_ID, CURRENCY_NAME, PRIMARY_CURRENCIES } from '../api.js';
import { gate, loadInto } from './_util.js';

function normalizeCurrencies(dict) {
  const out = {};
  for (const [k, v] of Object.entries(dict || {})) {
    let id = Number(k);
    if (Number.isNaN(id)) id = CURRENCY_ID[k];
    if (id != null) out[id] = Number(v) || 0;
  }
  return out;
}

export default {
  id: 'accounts',
  title: 'Accounts',
  subtitle: 'Create, edit and administer player accounts',
  icon: 'users',
  needsTarget: false,

  mount(root) {
    return gate(root, {}, { needServer: true }, (root) => {
      const layout = el('div', { style: { display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1.25fr)', gap: '18px', alignItems: 'start' } });
      const listCard = el('div.card', { style: { minWidth: '0' } });
      const detailCard = el('div.card', { style: { minWidth: '0' } });
      layout.appendChild(listCard);
      layout.appendChild(detailCard);
      root.appendChild(layout);

      const searchInput = input({ placeholder: 'Filter…', className: 'input btn-sm', style: { height: '32px', width: '130px', minWidth: '0', flex: '0 1 130px' } });
      const createBtn = button('New', { variant: 'primary', sm: true, iconName: 'plus', onClick: openCreate });
      const refreshBtn = button('', { variant: 'ghost', sm: true, iconName: 'refresh', onClick: loadList });

      listCard.appendChild(el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Roster' }),
        el('div.spacer', {}), searchInput, refreshBtn, createBtn));
      const listBody = el('div.list-scroll', { style: { maxHeight: '64vh' } });
      listCard.appendChild(listBody);

      let allRows = [];
      searchInput.addEventListener('input', () => paintList());

      function paintList() {
        const q = searchInput.value.trim().toLowerCase();
        const rows = allRows.filter((a) => !q || a.nickname.toLowerCase().includes(q) || String(a.serverId).includes(q));
        clear(listBody);
        if (!rows.length) { listBody.appendChild(emptyState('No accounts', q ? 'No match for this filter' : 'Create one to begin')); return; }
        const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th style="width:74px">ID</th><th>Nickname</th><th style="width:54px">Lvl</th></tr></thead><tbody></tbody></table>');
        const tb = tbl.querySelector('tbody');
        for (const a of rows) {
          const tr = frag(`<tr><td class="num" data-selectable>${a.serverId}</td><td style="max-width:0"><b data-selectable style="font-family:var(--font-round);display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(a.nickname)}</b></td><td class="num">${a.level}</td></tr>`);
          if (a.serverId === store.get().targetId) tr.classList.add('sel');
          tr.addEventListener('click', () => { store.set({ targetId: a.serverId }); paintList(); loadDetail(a.serverId); });
          tb.appendChild(tr);
        }
        listBody.appendChild(tbl);
      }

      async function loadList() {
        listBody.innerHTML = `<div class="empty"><div class="spinner"></div></div>`;
        allRows = await reloadAccounts();
        paintList();
        const t = store.get().targetId;
        if (t) loadDetail(t); else showDetailPlaceholder();
      }

      function showDetailPlaceholder() {
        clear(detailCard);
        detailCard.appendChild(el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Account detail' })));
        detailCard.appendChild(emptyState('Select an account', 'Pick a sensei from the roster'));
      }

      async function loadDetail(id) {
        clear(detailCard);
        detailCard.appendChild(el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Account detail' }),
          el('span.sub', { text: `#${id}`, 'data-selectable': '' })));
        const body = el('div.card-body', {});
        detailCard.appendChild(body);
        await loadInto(body, () => api.accountDetail(id), (body, d) => renderDetail(body, d));
      }

      function renderDetail(body, d) {
        // identity fields
        const fNick = input({ value: d.nickname || '' });
        const fComment = input({ value: d.comment || '' });
        const fLevel = input({ value: d.level ?? 1, type: 'number' });
        const fExp = input({ value: d.exp ?? 0, type: 'number' });
        const fVip = input({ value: d.vipLevel ?? 0, type: 'number' });

        const idGrid = el('div.grid-2', {},
          field('Nickname', fNick),
          field('Comment', fComment),
          field('Level', fLevel),
          field('Experience', fExp),
          field('VIP level', fVip));
        body.appendChild(sectionTag('Identity'));
        body.appendChild(idGrid);
        const saveId = button('Save identity', { variant: 'primary', iconName: 'save', onClick: async () => {
          const r = await api.accountUpdate({ serverId: d.serverId, nickname: fNick.value, comment: fComment.value, level: Number(fLevel.value), exp: Number(fExp.value), vipLevel: Number(fVip.value) }).then(() => ({ ok: true })).catch((e) => ({ ok: false, error: e.message }));
          toast(r.ok ? 'Account updated' : r.error, r.ok ? 'good' : 'bad');
          if (r.ok) reloadAccounts().then((rows) => { allRows = rows; paintList(); });
        }});
        body.appendChild(el('div', { style: { marginTop: '4px' } }, saveId));

        // currencies
        body.appendChild(frag('<div class="hazard" style="margin:20px 0 16px"></div>'));
        body.appendChild(sectionTag('Currencies'));
        const cur = normalizeCurrencies(d.currencies);
        const curGrid = el('div.grid-2', {});
        const edits = {};
        for (const cid of PRIMARY_CURRENCIES) {
          const i = input({ value: cur[cid] ?? 0, type: 'number' });
          edits[cid] = { input: i, orig: cur[cid] ?? 0 };
          curGrid.appendChild(field(CURRENCY_NAME[cid], i));
        }
        body.appendChild(curGrid);
        const saveCur = button('Apply currencies', { variant: 'primary', iconName: 'coin', onClick: async () => {
          let n = 0;
          for (const [cid, e] of Object.entries(edits)) {
            const val = Number(e.input.value);
            if (val !== e.orig) { await api.setCurrency({ accountServerId: d.serverId, currencyType: Number(cid), amount: val }); e.orig = val; n++; }
          }
          toast(n ? `Updated ${n} ${n === 1 ? 'currency' : 'currencies'}` : 'No changes', n ? 'good' : 'warn');
        }});
        body.appendChild(el('div.row.wrap', { style: { marginTop: '4px', gap: '10px' } },
          saveCur,
          button('Max all currencies', { variant: 'gold', iconName: 'bolt', onClick: () => maxCurrencies(d.serverId, edits) })));

        // power tools
        body.appendChild(frag('<div class="hazard gold" style="margin:20px 0 16px"></div>'));
        body.appendChild(sectionTag('Power tools'));
        const tools = el('div.row.wrap', { style: { gap: '10px' } });
        tools.appendChild(cmdButton(d.serverId, 'Max all characters', 'star', 'max all', 'gold'));
        tools.appendChild(cmdButton(d.serverId, 'Unlock all characters', 'users', 'giveall'));
        tools.appendChild(cmdButton(d.serverId, 'Unlock campaign', 'shield', 'unlockall campaign'));
        tools.appendChild(cmdButton(d.serverId, 'Unlock battlepass', 'ticket', 'unlockall battlepass'));
        body.appendChild(tools);

        // meta + delete
        body.appendChild(frag(`<div class="row wrap" style="gap:8px;margin-top:18px">
          <span class="tag grey">items ${d.itemCount}</span>
          <span class="tag grey">characters ${d.characterCount}</span>
          <span class="tag grey">mails ${d.mailCount}</span>
          <span class="tag grey">state ${escapeHtml(d.state || '')}</span></div>`));
        const del = button('Delete account', { variant: 'danger', iconName: 'trash', onClick: async () => {
          const ok = await confirmDialog({ title: 'Delete account', danger: true, confirmLabel: 'Delete permanently',
            message: `This permanently removes “${d.nickname}” (#${d.serverId}) and all of its data. This cannot be undone.` });
          if (!ok) return;
          try { await api.accountDelete(d.serverId); toast('Account deleted', 'warn'); store.set({ targetId: null }); loadList(); }
          catch (e) { toast(e.message, 'bad'); }
        }});
        body.appendChild(el('div', { style: { marginTop: '16px' } }, del));
      }

      function cmdButton(uid, label, ic, command, variant = 'ghost') {
        return button(label, { variant, sm: true, iconName: ic, onClick: async () => {
          try { const r = await api.command(uid, command); toast(`${label} ✓`, 'good'); }
          catch (e) { toast(e.message, 'bad'); }
        }});
      }
      async function maxCurrencies(id, edits) {
        const MAX = 999999999;
        for (const [cid, e] of Object.entries(edits)) { await api.setCurrency({ accountServerId: id, currencyType: Number(cid), amount: MAX }); e.input.value = MAX; e.orig = MAX; }
        toast('All shown currencies maxed', 'good');
      }

      function openCreate() {
        const nick = input({ placeholder: 'Nickname', value: 'Sensei' });
        const create = button('Create account', { variant: 'primary', iconName: 'plus' });
        const cancel = button('Cancel', { variant: 'ghost' });
        const ref = modal({ title: 'New account', body: el('div', {}, field('Nickname', nick),
          frag('<p style="font-size:12.5px;color:var(--ink-2);margin:4px 0 0;line-height:1.6">A fully initialised, client-loadable account is created with the default starter parcels and characters.</p>')),
          footer: [cancel, create] });
        cancel.addEventListener('click', ref.close);
        create.addEventListener('click', async () => {
          create.disabled = true;
          try {
            const r = await api.accountCreate({ nickname: nick.value.trim() || 'Sensei' });
            ref.close(); toast(`Created “${nick.value}” (#${r.serverId})`, 'good');
            store.set({ targetId: r.serverId });
            await loadList();
          } catch (e) { toast(e.message, 'bad'); create.disabled = false; }
        });
      }

      loadList();
    });
  },
};

function sectionTag(text) {
  return frag(`<div class="section-tag"><span class="plus"></span>${escapeHtml(text)}</div>`);
}
