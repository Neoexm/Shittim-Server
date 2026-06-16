import { el, frag, clear, button, input, textarea, select, field, toast, confirmDialog, openPicker, num, shortDate, escapeHtml, emptyState } from '../ui.js';
import { icon } from '../icons.js';
import { api, targetAccount } from '../api.js';
import { gate, loadInto } from './_util.js';

const PARCEL_KINDS = [
  { value: 'Item', label: 'Item', loader: (q) => api.staticItems(q), amount: true },
  { value: 'Currency', label: 'Currency', loader: () => api.staticCurrencies(), amount: true },
  { value: 'Equipment', label: 'Equipment', loader: (q) => api.staticEquipment(q), amount: true },
  { value: 'Character', label: 'Character', loader: (q) => api.staticCharacters(q), amount: false },
];

export default {
  id: 'mail',
  title: 'Mail',
  subtitle: 'Compose reward mail and review the player inbox',
  icon: 'mail',
  needsTarget: true,

  mount(root) {
    return gate(root, {}, { needServer: true, needTarget: true }, (root) => {
      const acc = targetAccount();
      const uid = acc.serverId;
      const rewards = [];

      const layout = el('div', { style: { display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1fr)', gap: '18px', alignItems: 'start' } });
      root.appendChild(layout);

      // ---- composer
      const fSender = input({ value: 'Plana' });
      const fComment = textarea({ value: 'A gift from the management team.', placeholder: 'Message body…' });
      const fExpire = input({ value: 30, type: 'number' });

      const kindSel = select(PARCEL_KINDS.map((k) => ({ value: k.value, label: k.label })));
      const addReward = button('Add reward', { variant: 'ghost', sm: true, iconName: 'plus', onClick: pickReward });
      const chipList = el('div.chips', {});
      paintChips();

      const sendBtn = button('Send mail', { variant: 'primary', iconName: 'send', onClick: send });

      const composer = el('div.card', { style: { minWidth: '0' } },
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Compose' }),
          el('span.sub', { text: `to ${acc.nickname} · #${uid}`, style: { minWidth: '0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } })),
        el('div.card-body', {},
          field('Sender', fSender),
          field('Message', fComment),
          field('Expires in (days)', fExpire),
          frag('<div class="hazard" style="margin:6px 0 14px"></div>'),
          frag('<div class="section-tag"><span class="plus"></span>Attachments</div>'),
          el('div.input-row', { style: { marginBottom: '12px' } }, kindSel, addReward),
          chipList,
          el('div', { style: { marginTop: '16px' } }, sendBtn)));
      layout.appendChild(composer);

      function pickReward() {
        const kind = PARCEL_KINDS.find((k) => k.value === kindSel.value);
        openPicker({ title: `Pick ${kind.label.toLowerCase()}`,
          loader: (q) => kind.loader(q).then((r) => r.map((x) => ({ id: x.id, name: x.name, sub: x.icon || x.devName }))),
          onPick: (it) => {
            const amount = kind.amount ? Math.max(1, prompt(`Amount of ${it.name}?`, '1') | 0 || 1) : 1;
            rewards.push({ type: kind.value, id: it.id, name: it.name, amount });
            paintChips();
          } });
      }
      function paintChips() {
        clear(chipList);
        if (!rewards.length) { chipList.appendChild(frag('<div class="muted" style="font-size:12.5px;padding:6px 2px">No attachments yet — add items, currency, equipment or students.</div>')); return; }
        rewards.forEach((r, i) => {
          const chip = frag(`<div class="chip"><div class="chip-ic">${icon(r.type === 'Currency' ? 'coin' : r.type === 'Character' ? 'users' : r.type === 'Equipment' ? 'shield' : 'box')}</div>
            <div class="chip-main"><b>${escapeHtml(r.name)}</b><span data-selectable>${r.type} · id ${r.id} · ×${num(r.amount)}</span></div></div>`);
          const x = frag('<button class="chip-x">✕</button>');
          x.addEventListener('click', () => { rewards.splice(i, 1); paintChips(); });
          chip.appendChild(x);
          chipList.appendChild(chip);
        });
      }
      async function send() {
        if (!rewards.length) { toast('Add at least one attachment', 'warn'); return; }
        const days = Math.max(1, Number(fExpire.value) || 30);
        const expireDate = new Date(Date.now() + days * 86400000).toISOString();
        sendBtn.disabled = true;
        try {
          await api.sendMail({ accountServerId: uid, sender: fSender.value || 'Plana', comment: fComment.value,
            parcels: rewards.map((r) => ({ type: r.type, id: r.id, amount: r.amount })), expireDate });
          toast('Mail sent', 'good', 'Delivered');
          rewards.length = 0; paintChips(); reloadInbox();
        } catch (e) { toast(e.message, 'bad'); }
        sendBtn.disabled = false;
      }

      // ---- inbox
      const clearBtn = button('Clear all', { variant: 'ghost', sm: true, iconName: 'trash', onClick: async () => {
        const ok = await confirmDialog({ title: 'Clear inbox', danger: true, confirmLabel: 'Delete all', message: 'Delete every mail for this account?' });
        if (!ok) return;
        try { await api.deleteMail({ accountServerId: uid, clearAll: true }); toast('Inbox cleared', 'warn'); reloadInbox(); }
        catch (e) { toast(e.message, 'bad'); }
      }});
      const refreshBtn = button('', { variant: 'ghost', sm: true, iconName: 'refresh', onClick: () => reloadInbox() });
      const inboxCard = el('div.card', { style: { minWidth: '0' } },
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Inbox' }), el('div.spacer', {}), refreshBtn, clearBtn));
      const inboxBody = el('div.list-scroll', { style: { maxHeight: '66vh' } });
      inboxCard.appendChild(inboxBody);
      layout.appendChild(inboxCard);

      async function reloadInbox() {
        await loadInto(inboxBody, () => api.mails(uid), (body, mails) => {
          if (!mails.length) { body.appendChild(emptyState('Inbox empty', 'Sent mail appears here')); return; }
          const wrap = el('div', { style: { padding: '12px', display: 'flex', flexDirection: 'column', gap: '10px' } });
          for (const m of mails) {
            const parcels = (m.parcels || []).map((p) => `<span class="tag grey" data-selectable>${escapeHtml(p.type)} ${p.id} ×${num(p.amount)}</span>`).join(' ');
            const card = frag(`<div class="banner-card" style="gap:8px">
              <div class="bc-top"><b style="font-family:var(--font-round);min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(m.sender)}</b>
                <span class="bc-id mono" data-selectable>#${m.serverId}</span><div class="spacer" style="flex:1"></div>
                <span class="pill ${m.collected ? '' : 'blue'}" style="flex:none"><span class="dot"></span>${m.collected ? 'Collected' : 'Unread'}</span></div>
              <div style="font-size:12.5px;color:var(--ink-2);min-width:0;overflow-wrap:anywhere">${escapeHtml(m.comment || '')}</div>
              <div class="bc-feat">${parcels || '<span class="muted" style="font-size:12px">no attachments</span>'}</div>
              <div class="muted" style="font-size:11.5px;min-width:0;overflow-wrap:anywhere">sent ${shortDate(m.sendDate)} · expires ${shortDate(m.expireDate)}</div></div>`);
            const del = frag(`<button class="chip-x" style="position:absolute;top:12px;right:12px">✕</button>`);
            card.style.position = 'relative';
            del.addEventListener('click', async () => { await api.deleteMail({ accountServerId: uid, mailServerId: m.serverId }); toast('Mail deleted', 'warn'); reloadInbox(); });
            card.appendChild(del);
            wrap.appendChild(card);
          }
          body.appendChild(wrap);
        });
      }
      reloadInbox();
    });
  },
};
