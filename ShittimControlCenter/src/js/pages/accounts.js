import { el, frag, clear, button, input, select, field, toast, modal, confirmDialog, notifyRestart, num, escapeHtml, emptyState } from '../ui.js';
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
  title: 'Accounts',  icon: 'users',
  needsTarget: false,

  mount(root) {
    return gate(root, { needServer: true }, (root) => {
      const gameSel = select([], { style: { minWidth: '220px' } });
      gameSel.addEventListener('change', async () => {
        const id = Number(gameSel.value);
        try {
          await api.selectAccount(id);
          gameAccountId = id || null;
          const picked = allRows.find((a) => a.serverId === id);
          toast(id ? `Game will log into "${picked ? picked.nickname : id}" from the next launch` : 'Game follows the Steam account again', 'good');
          paintList();
        } catch (e) { toast(e.message, 'bad'); fillGameSel(); }
      });
      root.appendChild(el('div.card', { style: { marginBottom: '18px' } },
        el('div.card-body', { style: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' } },
          el('b', { text: 'Game account', style: { fontFamily: 'var(--font-round)', fontSize: '13px' } }),
          gameSel,
          el('span.muted', { text: 'Applies from the next launch.', style: { fontSize: '12px' } }))));

      const layout = el('div', { style: { display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1.25fr)', gap: '18px', alignItems: 'start' } });
      const listCard = el('div.card', { style: { minWidth: '0' } });
      const detailCard = el('div.card', { style: { minWidth: '0' } });
      layout.appendChild(listCard);
      layout.appendChild(detailCard);
      root.appendChild(layout);

      const searchInput = input({ placeholder: 'Filter...', className: 'input btn-sm', style: { height: '32px', width: '130px', minWidth: '0', flex: '0 1 130px' } });
      const createBtn = button('New', { variant: 'primary', sm: true, iconName: 'plus', onClick: openCreate });
      const refreshBtn = button('', { variant: 'ghost', sm: true, iconName: 'refresh', onClick: loadList });

      listCard.appendChild(el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Roster' }),
        el('div.spacer', {}), searchInput, refreshBtn, createBtn));
      const listBody = el('div.list-scroll', { style: { maxHeight: '64vh' } });
      listCard.appendChild(listBody);

      let allRows = [];
      let gameAccountId = null;
      searchInput.addEventListener('input', () => paintList());

      function paintList() {
        const q = searchInput.value.trim().toLowerCase();
        const rows = allRows.filter((a) => !q || a.nickname.toLowerCase().includes(q) || String(a.serverId).includes(q));
        clear(listBody);
        if (!rows.length) { listBody.appendChild(emptyState(q ? 'No match' : 'No accounts')); return; }
        const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th style="width:74px">ID</th><th>Nickname</th><th style="width:54px">Lvl</th></tr></thead><tbody></tbody></table>');
        const tb = tbl.querySelector('tbody');
        for (const a of rows) {
          const inGame = a.serverId === gameAccountId ? '<span class="tag" style="flex:none">in game</span>' : '';
          const tr = frag(`<tr><td class="num" data-selectable>${a.serverId}</td><td style="max-width:0"><div style="display:flex;align-items:center;gap:6px;min-width:0"><b data-selectable style="font-family:var(--font-round);overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(a.nickname)}</b>${inGame}</div></td><td class="num">${a.level}</td></tr>`);
          if (a.serverId === store.get().targetId) tr.classList.add('sel');
          tr.addEventListener('click', () => { store.set({ targetId: a.serverId }); paintList(); loadDetail(a.serverId); });
          tb.appendChild(tr);
        }
        listBody.appendChild(tbl);
      }

      function fillGameSel() {
        clear(gameSel);
        for (const o of [{ value: 0, label: 'Follow the Steam account' }, ...allRows.map((a) => ({ value: a.serverId, label: `${a.nickname} (#${a.serverId})` }))]) {
          const opt = document.createElement('option');
          opt.value = o.value;
          opt.textContent = o.label;
          gameSel.appendChild(opt);
        }
        gameSel.value = String(gameAccountId || 0);
      }

      async function loadList() {
        listBody.innerHTML = `<div class="empty"><div class="spinner"></div></div>`;
        allRows = await reloadAccounts();
        gameAccountId = await api.selectedAccount().then((r) => r.selectedAccountId || null).catch(() => null);
        fillGameSel();
        paintList();
        const t = store.get().targetId;
        if (t) loadDetail(t); else showDetailPlaceholder();
      }

      function showDetailPlaceholder() {
        clear(detailCard);
        detailCard.appendChild(el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Account detail' })));
        detailCard.appendChild(emptyState('Select an account'));
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
        body.appendChild(idGrid);
        const saveId = button('Save identity', { variant: 'primary', iconName: 'save', onClick: async () => {
          const r = await api.accountUpdate({ serverId: d.serverId, nickname: fNick.value, comment: fComment.value, level: Number(fLevel.value), exp: Number(fExp.value), vipLevel: Number(fVip.value) }).then(() => ({ ok: true })).catch((e) => ({ ok: false, error: e.message }));
          toast(r.ok ? 'Account updated' : r.error, r.ok ? 'good' : 'bad');
          if (r.ok) { notifyRestart(); reloadAccounts().then((rows) => { allRows = rows; paintList(); }); }
        }});
        body.appendChild(el('div', { style: { marginTop: '4px' } }, saveId));

        body.appendChild(frag('<div class="hazard" style="margin:20px 0 16px"></div>'));
        body.appendChild(el('div', { text: 'Currencies', style: { fontSize: '12px', fontWeight: '600', color: 'var(--ink-2)', margin: '0 0 10px' } }));
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
          if (n) notifyRestart();
        }});
        body.appendChild(el('div.row.wrap', { style: { marginTop: '4px', gap: '10px' } },
          saveCur,
          button('Max all currencies', { variant: 'ghost', onClick: () => maxCurrencies(d.serverId, edits) })));

        body.appendChild(frag('<div class="hazard" style="margin:20px 0 16px"></div>'));
        body.appendChild(el('div', { text: 'Actions', style: { fontSize: '12px', fontWeight: '600', color: 'var(--ink-2)', margin: '0 0 10px' } }));
        const tools = el('div.row.wrap', { style: { gap: '10px' } });
        tools.appendChild(cmdButton(d.serverId, 'Max all characters', 'max all'));
        tools.appendChild(cmdButton(d.serverId, 'Unlock all characters', 'giveall'));
        tools.appendChild(cmdButton(d.serverId, 'Unlock campaign + story', ['unlockall campaign', 'unlockall story']));
        tools.appendChild(cmdButton(d.serverId, 'Unlock battlepass', 'unlockall battlepass'));
        body.appendChild(tools);

        body.appendChild(frag(`<div class="muted" style="font-size:12px;margin-top:18px">${d.itemCount} items · ${d.characterCount} characters · ${d.mailCount} mails · ${escapeHtml(d.state || '')}</div>`));
        const del = button('Delete account', { variant: 'danger', iconName: 'trash', onClick: async () => {
          const ok = await confirmDialog({ title: 'Delete account', danger: true, confirmLabel: 'Delete permanently',
            message: `This permanently removes "${d.nickname}" (#${d.serverId}) and all of its data.` });
          if (!ok) return;
          try { await api.accountDelete(d.serverId); toast('Account deleted', 'warn'); store.set({ targetId: null }); loadList(); }
          catch (e) { toast(e.message, 'bad'); }
        }});
        body.appendChild(el('div', { style: { marginTop: '16px' } }, del));
      }

      function cmdButton(uid, label, command) {
        const commands = Array.isArray(command) ? command : [command];
        return button(label, { variant: 'ghost', sm: true, onClick: async () => {
          try { for (const c of commands) await api.command(uid, c); toast(label, 'good'); notifyRestart(); }
          catch (e) { toast(e.message, 'bad'); }
        }});
      }
      async function maxCurrencies(id, edits) {
        const MAX = 999999999;
        for (const [cid, e] of Object.entries(edits)) { await api.setCurrency({ accountServerId: id, currencyType: Number(cid), amount: MAX }); e.input.value = MAX; e.orig = MAX; }
        toast('All shown currencies maxed', 'good');
        notifyRestart();
      }

      async function pickProfile() {
        if (!window.host?.pickAccountData) { toast('File picker unavailable', 'bad'); return null; }
        const picked = await window.host.pickAccountData();
        if (!picked || picked.canceled) return null;
        if (!picked.ok) { toast(picked.error || 'Could not read that file', 'bad'); return null; }
        try {
          const up = await api.accountDataUpload({ name: picked.name, content: picked.content });
          return up?.name || picked.name;
        } catch (e) { toast(e.message, 'bad'); return null; }
      }

      function profilePicker(onPick) {
        const chosen = el('span.muted', { text: 'No file chosen', style: { fontSize: '12px' } });
        const browse = button('Browse...', { variant: 'ghost', sm: true, iconName: 'folder',
          onClick: async () => {
            const name = await pickProfile();
            if (!name) return;
            chosen.textContent = name;
            chosen.classList.remove('muted');
            onPick(name);
          } });
        return { row: el('div.row', { style: { gap: '10px', alignItems: 'center' } }, browse, chosen), chosen };
      }

      // The load reports failure in its output rather than as an HTTP error.
      async function loadProfile(uid, file) {
        const r = await api.command(uid, `accountdata load ${file}`);
        const out = String(r?.output || '').trim();
        if (out && !/successfully/i.test(out)) throw new Error(out);
      }

      // Only ever creates a new account: loading over one the client has already logged into leaves it holding stale cached state and the level it shows stops matching the server.
      function openCreate() {
        const nick = input({ value: 'Sensei' });

        let file = null;
        const picker = profilePicker((name) => { file = name; syncHint(); });
        const hint = el('div.muted', { style: { fontSize: '12px', marginTop: '10px' } });
        const syncHint = () => {
          hint.textContent = file
            ? 'This profile is loaded into the new account. Nothing existing is touched.'
            : 'Optional. Leave empty for a fresh account, or pick an exported profile.';
        };
        syncHint();

        const create = button('Create account', { variant: 'primary', iconName: 'plus' });
        const cancel = button('Cancel', { variant: 'ghost' });
        const ref = modal({
          title: 'New account',
          body: el('div', {}, field('Nickname', nick), field('Import a profile', picker.row), hint),
          footer: [cancel, create],
        });
        cancel.addEventListener('click', ref.close);

        create.addEventListener('click', async () => {
          create.disabled = true;
          try {
            const r = await api.accountCreate({ nickname: nick.value.trim() || 'Sensei' });
            if (file) await loadProfile(r.serverId, file);
            ref.close();
            toast(file ? `Created #${r.serverId} from "${file}"` : `Created "${nick.value}" (#${r.serverId})`, 'good');
            store.set({ targetId: r.serverId });
            await loadList();
            if (file) notifyRestart();
          } catch (e) { toast(e.message, 'bad'); create.disabled = false; }
        });
      }

      loadList();
    });
  },
};
