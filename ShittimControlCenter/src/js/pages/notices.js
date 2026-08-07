import { el, frag, clear, button, input, select, toggle, field, toast, confirmDialog } from '../ui.js';
import { api } from '../api.js';
import { gate } from './_util.js';

// Blurbs keyed by ServerNotificationFlag member name. A bit the server enum grows past this list still gets a row, just without a description.
const FLAG_DESC = {
  NewMailArrived: 'Pops the new-mail toast in the lobby.',
  HasUnreadMail: 'Lights the mailbox badge. The server already raises this by itself whenever the account has unread mail.',
  NewToastDetected: 'Client fetches and shows its queued toast notifications.',
  CanReceiveArenaDailyReward: 'Badges the arena daily reward as claimable.',
  CanReceiveRaidReward: 'Badges Total Assault rewards as claimable.',
  ServerMaintenance: 'Read on every reply, but the maintenance screen itself comes from the gate on the right, not from this bit.',
  CannotReceiveMail: 'Mailbox is full, so the client stops offering to claim.',
  InventoryFullRewardMail: 'Tells the player rewards went to mail because the inventory was full.',
  CanReceiveClanAttendanceReward: 'Badges the club attendance reward.',
  HasClanApplicant: 'The club has pending join requests.',
  HasFriendRequest: 'Friend requests are waiting.',
  CheckConquest: 'Conquest state changed, refetch it.',
  CanReceiveEliminateRaidReward: 'Badges Grand Assault rewards as claimable.',
  CanReceiveMultiFloorRaidReward: 'Badges Final Restriction rewards as claimable.',
  CanReceiveProductDailyRecordReward: 'Badges the monthly pass daily reward.',
  HasUnreadSemiPermanentMail: 'Unread mail sitting in the long-retention box.',
};

// The error codes the client has a dedicated screen for. Everything else in WebAPIErrorCode lands on its generic popup, which is what "Other error code" is for.
const GATE_CODES = [
  { value: 28001, name: 'ServerIsUnderMaintenance', desc: 'Full maintenance screen, nobody gets past the title.' },
  { value: 28002, name: 'ServerMaintenanceSoon', desc: 'Shutting-down-shortly warning.' },
  { value: 28003, name: 'AccountIsNotInWhiteList', desc: 'Closed-beta gate - the player is not on the list.' },
  { value: 28005, name: 'ServerContentsLock', desc: 'Content is locked.' },
  { value: 27000, name: 'AccountBanned', desc: 'Ban screen.' },
  { value: 903, name: 'ClientUpdateRequire', desc: 'Forced-update prompt.' },
  { value: 3, name: 'InvalidSession', desc: 'Kicks back to the title screen for a relogin.' },
];

export default {
  id: 'notices',
  title: 'Notices',  icon: 'info',
  needsTarget: false,

  mount(root, { rerender }) {
    return gate(root, { needServer: true }, async (root) => {
      const state = await api.notice();
      let flags = state.flags | 0;

      const flagTag = el('span', {});
      const gateTag = el('span', {});

      const flagBody = el('div', {});
      for (const f of state.availableFlags) {
        const row = el('div.toggle-row', {},
          el('div.tr-text', {}, el('b', { text: f.name }), el('span', { text: FLAG_DESC[f.name] || `bit ${f.value}` })));
        row.appendChild(toggle((flags & f.value) !== 0, (on) => {
          flags = on ? (flags | f.value) : (flags & ~f.value);
          paintFlags();
        }));
        flagBody.appendChild(row);
      }

      const flagCard = el('div.card', { style: { minWidth: '0' } },
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Notification flags' }),
          el('span.sub', { text: 'stamped on every response' }), el('div.spacer', {}), flagTag),
        el('div.card-body', { style: { minWidth: '0' } }, flagBody,
          frag('<p class="muted" style="font-size:12px;margin:10px 0 0;line-height:1.6">These OR onto whatever the server works out for itself, so a bit left off here can still be raised by real game state. Pre-login requests and the four protocols that skip notification are never stamped.</p>')));

      const codeSel = select(
        [{ value: '0', label: 'Off - serve normally' }]
          .concat(GATE_CODES.map((c) => ({ value: String(c.value), label: `${c.name} - ${c.value}` })))
          .concat([{ value: 'custom', label: 'Other error code...' }]));
      const customCode = input({ type: 'number', value: '' });
      const customField = field('Error code', customCode, 'any WebAPIErrorCode value');
      const msg = input({ value: state.gateMessage || '', placeholder: 'Server is under maintenance' });
      const codeNote = el('p.muted', { style: { fontSize: '12px', margin: '-2px 0 14px', lineHeight: '1.6' } });

      const known = GATE_CODES.some((c) => c.value === state.gateError);
      codeSel.value = state.gateError ? (known ? String(state.gateError) : 'custom') : '0';
      if (state.gateError && !known) customCode.value = state.gateError;

      codeSel.addEventListener('change', paintGate);
      customCode.addEventListener('input', paintGate);

      const gateCard = el('div.card', { style: { minWidth: '0' } },
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Close the server' }),
          el('div.spacer', {}), gateTag),
        el('div.card-body', { style: { minWidth: '0' } },
          field('Answer every request with', codeSel),
          codeNote,
          customField,
          field('Message', msg, 'carried in the error packet'),
          frag('<p class="muted" style="font-size:12px;margin:10px 0 0;line-height:1.6">The client draws its own screen for the codes above, so the message mostly shows up in logs. Only the crypto handshake stays open, because without it the client cannot decrypt the error and the maintenance screen degrades into a connection failure.</p>')));

      const applyBtn = button('Apply', { variant: 'primary', iconName: 'save', onClick: apply });
      const clearBtn = button('Clear everything', { variant: 'ghost', iconName: 'refresh', onClick: clearAll });
      const bar = el('div.card', { style: { marginTop: '18px' } },
        el('div.card-body', { style: { display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' } },
          applyBtn, clearBtn, el('div.spacer', {}),
          frag('<span class="pill blue"><span class="dot"></span>Takes effect on the next request</span>')));

      root.appendChild(el('div.grid-2', { style: { alignItems: 'start' } }, flagCard, gateCard));
      root.appendChild(bar);
      paintFlags();
      paintGate();

      function currentCode() {
        return parseInt(codeSel.value === 'custom' ? customCode.value : codeSel.value, 10) || 0;
      }
      function paintFlags() {
        clear(flagTag);
        const set = state.availableFlags.filter((f) => flags & f.value).length;
        flagTag.appendChild(frag(`<span class="pill ${flags ? 'blue' : ''}"><span class="dot"></span>ServerNotification = ${flags}${set ? ` - ${set} set` : ''}</span>`));
      }
      function paintGate() {
        const code = currentCode();
        customField.style.display = codeSel.value === 'custom' ? '' : 'none';
        const c = GATE_CODES.find((x) => x.value === code);
        codeNote.textContent = c ? c.desc
          : code ? 'The client has no dedicated screen for this one and will fall back to its generic error popup.'
          : '';
        clear(gateTag);
        gateTag.appendChild(frag(`<span class="pill ${code ? 'warn' : 'good'}"><span class="dot"></span>${code ? 'Closed - ' + code : 'Open'}</span>`));
      }

      async function apply() {
        const code = currentCode();
        if (code && code !== state.gateError) {
          const c = GATE_CODES.find((x) => x.value === code);
          const ok = await confirmDialog({ title: 'Close the server', confirmLabel: 'Close the server', danger: true,
            message: `Every request but the crypto handshake gets answered with ${c ? c.name : 'error ' + code}, for every account, until you turn this back off. Anyone already playing hits it on their next request.` });
          if (!ok) return;
        }
        try {
          await api.setNotice({ flags, gateError: code, gateMessage: msg.value });
          state.gateError = code;
          toast(code ? 'Clients now get the error screen' : 'Notification settings applied', code ? 'warn' : 'good', code ? 'Server closed' : 'Saved');
        } catch (e) { toast(e.message, 'bad'); }
      }
      async function clearAll() {
        try {
          await api.setNotice({ flags: 0, gateError: 0, gateMessage: msg.value });
          toast('All flags cleared and the server reopened', 'good');
          rerender();
        } catch (e) { toast(e.message, 'bad'); }
      }
    });
  },
};
