import { el, frag, clear, button, input, select, toggle, field, toast, confirmDialog, escapeHtml, emptyState, shortDate, notifyRestart, modal, num } from '../ui.js';
import { api, targetAccount } from '../api.js';
import { gate, loadInto } from './_util.js';

// EventContentType values that are their own minigame rather than a stage/shop/mission attached to one.
const MINIGAME_TYPES = {
  MiniGameRhythm: 'Rhythm',
  MinigameRhythmEvent: 'Rhythm',
  MiniGameShooting: 'Shooting',
  MiniGameTBG: 'Board game',
  MiniGameDefense: 'Tower defense',
  MinigameDreamMaker: 'Dream Maker',
  MiniGameRoad: 'Road puzzle',
  MiniGameCCG: 'Card battle',
  DiceRace: 'Dice race',
  Treasure: 'Treasure hunt',
  Conquest: 'Conquest',
  Field: 'Field',
  EventLocation: 'Location',
  CardShop: 'Card shop',
  BoxGacha: 'Box gacha',
  FortuneGachaShop: 'Fortune gacha',
};

const FILTERS = [
  { value: 'all', label: 'Everything' },
  { value: 'minigame', label: 'Has a minigame' },
  { value: 'on', label: 'Currently forced open' },
  { value: 'rerun', label: 'Reruns' },
  { value: 'rail', label: 'Shows a lobby icon' },
  { value: 'unnamed', label: 'Never localized' },
];

const SORTS = [
  { value: 'new', label: 'Newest first' },
  { value: 'old', label: 'Oldest first' },
  { value: 'name', label: 'Name' },
];

// Past this many lobby icons the dot bar under the rail keeps growing at a fixed pixel per dot and walks off the edge of the screen.
const RAIL_COMFORTABLE = 8;

// The event's own item names already read well enough ("Baddie's Apology Letter"), so the type is only there to say which of the three token slots it is.
const ITEM_TYPES = {
  EventPoint: 'Points',
  EventToken1: 'Token 1',
  EventToken2: 'Token 2',
  EventToken3: 'Token 3',
  EventToken4: 'Token 4',
  EventToken5: 'Token 5',
  EventMeetUpTicket: 'Outing pass',
  EventEtcItem: 'Item',
  Concentration: 'Concentration',
};

export default {
  id: 'schedule',
  title: 'Events',  icon: 'play',
  needsTarget: false,

  mount(root) {
    return gate(root, { needServer: true }, (root) => {
      const body = el('div', {});
      root.appendChild(body);

      loadInto(body, () => api.eventSchedule(), (body, data) => {
        const events = data.events || [];
        const on = new Set(data.enabled || []);
        const byId = new Map(events.map((e) => [e.id, e]));

        // A rerun and the run it repeats are the same event to a player and only differ by which dates they carry, so they belong on adjacent rows under one heading rather than 89 ids apart in an id-sorted list.
        const families = new Map();
        for (const e of events) {
          const head = byId.has(e.original) ? e.original : e.id;
          if (!families.has(head)) families.set(head, []);
          families.get(head).push(e);
        }
        for (const members of families.values()) members.sort((a, b) => a.iconOrder - b.iconOrder || a.id - b.id);

        const search = input({ placeholder: 'Name, id, student or minigame' });
        const filter = select(FILTERS);
        const sort = select(SORTS);
        const count = el('span', {});
        const tb = el('tbody', {});
        // ticking a box must not re-run the filter under the cursor, so the only thing a toggle repaints is the count and the both-halves-on warnings
        const warns = [];

        const tbl = frag('<table class="tbl" style="table-layout:fixed"><thead><tr><th>Event</th><th style="width:200px">Featured</th><th style="width:170px">Contents</th><th style="width:118px">Ran</th><th style="width:58px">Lobby</th><th style="width:64px">Open</th></tr></thead></table>');
        tbl.appendChild(tb);

        const list = el('div.list-scroll', { style: { maxHeight: '52vh' } }, tbl);
        const applyBtn = button('Apply', { variant: 'primary', iconName: 'save', sm: true, onClick: apply });
        const clearBtn = button('Close everything', { variant: 'ghost', iconName: 'refresh', sm: true, onClick: closeAll });
        const card = el('div.card', {},
          el('div.card-head', { style: { flexWrap: 'wrap', rowGap: '8px' } }, el('span.tab-mark', {}), el('h3', { text: 'Events and minigames' }),
            el('span.sub', { text: `${events.length} in this client version` }), el('div.spacer', {}), count, applyBtn, clearBtn),
          el('div.card-body', { style: { paddingBottom: '8px' } },
            el('div.row.wrap', { style: { gap: '10px' } },
              el('div', { style: { flex: '1', minWidth: '220px' } }, field('Find', search)),
              el('div', { style: { width: '190px' } }, field('Show', filter)),
              el('div', { style: { width: '160px' } }, field('Sort', sort)))),
          list);

        body.appendChild(card);
        body.appendChild(frag('<p class="muted" style="font-size:12px;margin:14px 2px 0;line-height:1.6">The game reads its event table when it launches, so restart the client after applying. Nothing about an event\'s schedule is on the wire: the client works out for itself whether one is running by comparing the clock against the dates in its own table. Switching an event on here rewrites those dates in the installed game so they read as permanently open, and switching it off puts the shipped dates back. Any number can run at once - what an event switched on gets you is its icon on the lobby rail and its own menus, not a slot in the rotating banner on the front page. The schedule is re-applied every time the server starts, so a game update that replaces the table does not quietly close everything again.</p>'));

        search.addEventListener('input', paint);
        filter.addEventListener('change', paint);
        sort.addEventListener('change', paint);
        paint();

        function matches(e) {
          const q = search.value.trim().toLowerCase();
          const f = filter.value;
          if (f === 'minigame' && !minigames(e).length) return false;
          if (f === 'on' && !on.has(e.id)) return false;
          if (f === 'rerun' && !e.isReturn) return false;
          if (f === 'rail' && !e.rail) return false;
          if (f === 'unnamed' && !e.name.startsWith('Event #')) return false;
          if (!q) return true;
          return String(e.id).includes(q)
            || String(e.original || '').includes(q)
            || (e.name || '').toLowerCase().includes(q)
            || (e.key || '').toLowerCase().includes(q)
            || (e.students || []).some((s) => s.toLowerCase().includes(q))
            || (e.currency || []).some((c) => c.toLowerCase().includes(q))
            || e.types.some((t) => t.toLowerCase().includes(q) || (MINIGAME_TYPES[t] || '').toLowerCase().includes(q));
        }

        function paint() {
          clear(tb);
          warns.length = 0;

          const shown = [];
          for (const [rootId, members] of families) {
            const hit = members.filter(matches);
            if (hit.length) shown.push([rootId, hit]);
          }

          const dir = sort.value;
          shown.sort(([, a], [, b]) => {
            if (dir === 'name') return (a[0].name || '').localeCompare(b[0].name || '');
            // IconOrder counts down as events get newer, so ascending is newest-first.
            const key = (m) => Math.min(...m.map((x) => x.iconOrder));
            return dir === 'old' ? key(b) - key(a) : key(a) - key(b);
          });

          paintCount();

          if (!shown.length) { tb.appendChild(el('tr', {}, el('td', { colSpan: 6 }, emptyState('Nothing matches')))); return; }

          for (const [, members] of shown) {
            members.forEach((e, i) => tb.appendChild(row(e, i > 0, members)));
          }
        }

        function row(e, indented, family) {
          const mg = minigames(e);
          const contents = mg.length ? mg : (e.stages ? [`${e.stages} stages`] : []);
          const featured = (e.students || []).length ? e.students.join(', ') : (e.currency || []).join(', ');
          const tag = e.isReturn ? 'Rerun' : e.releaseType !== 'None' ? 'Permanent' : '';

          const tr = frag(`<tr>
            <td style="max-width:0;${indented ? 'padding-left:26px' : ''}"><b style="font-family:var(--font-round);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;display:block">${escapeHtml(e.name)}</b><div class="muted" data-sub style="font-size:11px"><span data-selectable>#${e.id}</span>${tag ? ` - ${tag}` : ''}</div></td>
            <td class="muted" style="font-size:11.5px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(featured || '-')}</td>
            <td class="muted" style="font-size:11.5px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(contents.length ? [...new Set(contents)].join(', ') : 'Story only')}</td>
            <td class="muted" style="font-size:11.5px;white-space:nowrap">${fmt(e.open)}<br>→ ${fmt(e.close)}</td>
            <td style="font-size:11px">${e.rail ? '<span class="pill">Icon</span>' : '<span class="muted">Menu only</span>'}</td>
            <td style="text-align:right"></td></tr>`);

          if (!indented && family.length > 1) {
            const warn = el('span', {});
            tr.querySelector('[data-sub]').appendChild(warn);
            const refresh = () => {
              clear(warn);
              if (family.filter((m) => on.has(m.id)).length > 1)
                warn.appendChild(frag('<span class="pill warn" style="margin-left:6px">Two runs at once</span>'));
            };
            refresh();
            warns.push(refresh);
          }

          const sw = toggle(on.has(e.id), (isOn) => {
            if (isOn) on.add(e.id); else on.delete(e.id);
            warns.forEach((f) => f());
            paintCount();
          });
          sw.addEventListener('click', (ev) => ev.stopPropagation());
          tr.lastElementChild.appendChild(sw);

          tr.style.cursor = 'pointer';
          tr.addEventListener('click', () => openUnlock(e, on.has(e.id)));
          return tr;
        }

        function railCount() {
          let n = 0;
          for (const id of on) { const e = byId.get(id); if (e && e.rail) n++; }
          return n;
        }

        function paintCount() {
          clear(count);
          const rail = railCount();
          const crowded = rail > RAIL_COMFORTABLE;
          count.appendChild(frag(`<span class="pill ${crowded ? 'warn' : on.size ? 'good' : ''}" title="${crowded ? 'The row of dots under the lobby icons grows one dot per icon and runs off the screen past about ten' : ''}"><span class="dot"></span>${on.size ? `${on.size} forced open${rail ? `, ${rail} on the rail` : ''}` : 'All on their shipped dates'}</span>`));
        }

        async function apply() {
          try {
            const out = await api.setEventSchedule({ enabled: [...on] });
            toast(on.size ? `${on.size} event(s) forced open across ${out.rows} table rows` : 'Every event is back on its shipped dates', 'good', 'Client table rewritten');
            notifyRestart();
          } catch (e) { toast(e.message, 'bad'); }
        }

        async function closeAll() {
          const ok = await confirmDialog({ title: 'Close everything', confirmLabel: 'Close everything',
            message: 'Every event goes back to the dates it originally ran on, so anything long expired disappears from the client again. Progress already saved on the server is untouched.' });
          if (!ok) return;
          on.clear();
          try {
            await api.setEventSchedule({ enabled: [] });
            toast('Every event is back on its shipped dates', 'good');
            notifyRestart();
            paint();
          } catch (e) { toast(e.message, 'bad'); }
        }
      });
    });
  },
};

// Everything in here is written straight into one account's save, so it is the only part of this page that needs a target - the schedule itself is client-side and account-independent.
function openUnlock(e, isOn) {
  const acc = targetAccount();
  const body = el('div', {});
  const go = button('Unlock', { variant: 'primary', iconName: 'check' });
  const cancel = button('Cancel', { variant: 'ghost' });
  const ref = modal({ title: e.name, body, footer: [cancel, go] });
  cancel.addEventListener('click', ref.close);

  if (!acc) {
    body.appendChild(frag('<p class="muted" style="font-size:13.5px;line-height:1.6;margin:0">Pick an account from the header first. Event points, stage clears and shop resets all belong to one save.</p>'));
    go.disabled = true;
    return;
  }

  go.disabled = true;
  const amounts = new Map();
  const picks = { stages: false, missions: false, shop: false, collections: false, minigame: false, minigameStages: false };

  loadInto(body, () => api.eventUnlocks(e.id, acc.serverId), (body, d) => {
    go.disabled = false;

    body.appendChild(frag(`<p class="muted" style="font-size:12px;line-height:1.6;margin:0 0 14px">Writing to <b>${escapeHtml(acc.nickname)}</b> - #${acc.serverId}.${isOn ? '' : ' This event is not forced open, so none of it is reachable in the client until you switch it on and restart.'}</p>`));

    if (d.currency.length) {
      const grid = el('div', { style: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px 14px' } });
      for (const c of d.currency) {
        const box = input({ type: 'number', min: '0', placeholder: '0' });
        amounts.set(c.itemId, box);
        const cost = c.costMax ? ` - a run costs ${c.costMin === c.costMax ? num(c.costMin) : `${num(c.costMin)}-${num(c.costMax)}`}` : '';
        grid.appendChild(field(c.name || `Item ${c.itemId}`, box, `${ITEM_TYPES[c.type] || c.type} - holds ${num(c.held)}${cost}`));
      }
      body.appendChild(el('div', { style: { marginBottom: '16px' } },
        el('h4', { text: 'Event currency', style: { margin: '0 0 8px', fontSize: '13px' } }), grid));
    }

    const rows = [
      d.stages.total && ['stages', 'Clear every stage', `${d.stages.total} stages, ${d.stages.cleared} already cleared. Everything gated behind stage progress opens with them.`],
      d.missions.total && ['missions', 'Complete every mission', `${d.missions.total} missions, ${d.missions.done} already done. Rewards still have to be claimed in-game.`],
      d.shop.total && ['shop', 'Reset shop purchase limits', `${d.shop.total} products, ${d.shop.bought} with a purchase on record.`],
      d.collections.total && ['collections', 'Unlock the collection', `${d.collections.total} entries, ${d.collections.owned} already owned.`],
      d.minigame.total && ['minigame', 'Unlock the minigame', `${d.minigame.names.map((n) => MINIGAME_TYPES[n] || n).join(', ')} is padlocked behind a story stage belonging to the event's rerun, so clearing this event's own stages never opens it. ${d.minigame.cleared} of ${d.minigame.total} already cleared.`],
      d.minigameStages.total && ['minigameStages', 'Clear every minigame stage', `${d.minigameStages.kinds.join(' and ')} - ${d.minigameStages.total} stages, ${d.minigameStages.cleared} already cleared. Unlocking the minigame only opens the front door; this plays it through.`],
    ].filter(Boolean);

    if (!rows.length && !d.currency.length) {
      body.appendChild(emptyState('Nothing to unlock', 'This event carries no stages, missions, shop or currency of its own'));
      go.disabled = true;
      return;
    }

    for (const [key, label, hint] of rows) {
      const sw = toggle(false, (v) => { picks[key] = v; });
      body.appendChild(el('div.row', { style: { alignItems: 'flex-start', gap: '12px', padding: '10px 0', borderTop: '1px solid var(--line)' } },
        el('div', { style: { flex: '1', minWidth: '0' } },
          el('b', { text: label, style: { fontSize: '13.5px' } }),
          el('div', { text: hint, style: { color: 'var(--ink-2)', fontSize: '11.5px', lineHeight: '1.55', marginTop: '2px' } })),
        sw));
    }
  });

  go.addEventListener('click', async () => {
    const currency = {};
    for (const [id, box] of amounts) {
      const v = Number(box.value) || 0;
      if (v > 0) currency[id] = v;
    }
    if (!Object.keys(currency).length && !Object.values(picks).some(Boolean)) { toast('Nothing selected', 'warn'); return; }

    go.disabled = true;
    try {
      const r = await api.eventUnlock({
        accountServerId: acc.serverId, eventContentId: e.id,
        clearStages: picks.stages, completeMissions: picks.missions,
        resetShop: picks.shop, unlockCollections: picks.collections, unlockMinigame: picks.minigame,
        clearMinigames: picks.minigameStages, currency,
      });
      ref.close();
      const done = [
        r.stages && `${r.stages} stages cleared`,
        r.missions && `${r.missions} missions completed`,
        r.shop && `${r.shop} shop limits reset`,
        r.collections && `${r.collections} collection entries`,
        r.items && `${r.items} item stacks`,
        r.minigame && 'minigame unlocked',
        r.minigameStages && `${r.minigameStages} minigame stages cleared`,
      ].filter(Boolean);
      toast(done.length ? done.join(', ') : 'Nothing changed - it was all unlocked already', done.length ? 'good' : 'warn', e.name);
    } catch (err) { go.disabled = false; toast(err.message, 'bad'); }
  });
}

// The server's minigames list is derived from the type name, which misses the ones whose type reads as a mode rather than a minigame (Conquest, Treasure, Field), so the label falls back to the full type list.
function minigames(e) {
  const named = (e.minigames || []).map((t) => MINIGAME_TYPES[t] || t);
  return named.length ? named : e.types.filter((t) => MINIGAME_TYPES[t]).map((t) => MINIGAME_TYPES[t]);
}

function fmt(s) {
  if (!s) return '-';
  // season excel dates may be ISO or "yyyy-MM-dd HH:mm:ss"
  return shortDate(String(s).replace(' ', 'T'));
}
