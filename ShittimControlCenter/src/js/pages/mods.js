import { el, frag, clear, button, input, field, toast, confirmDialog, modal, openPicker, escapeHtml } from '../ui.js';
import { api } from '../api.js';
import { gate } from './_util.js';

const SECTIONS = [
  { id: 'characters', name: 'Custom characters', desc: 'Clone a student onto a free id and edit her profile, stats and school.' },
];

export default {
  id: 'mods',
  title: 'Mods',  icon: 'flask',
  needsTarget: false,

  mount(root) {
    let view = 'menu';
    let openId = null;
    let host = null;

    return gate(root, { needServer: true }, (r) => { host = r; paint(); });

    function paint() {
      clear(host);
      if (view === 'menu') menu();
      else if (view === 'characters') characters();
      else editor(openId);
    }

    function go(next, id) { view = next; openId = id; paint(); }

    function menu() {
      const list = el('div.picker-list', {});
      for (const s of SECTIONS) {
        const row = frag(`<div class="picker-item"><span class="pi-name">${escapeHtml(s.name)}</span><span class="muted" style="font-size:12px;flex:2;min-width:0">${escapeHtml(s.desc)}</span></div>`);
        row.addEventListener('click', () => go(s.id));
        list.appendChild(row);
      }
      host.appendChild(el('div.card', {},
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Mods' }), el('span.sub', { text: 'edits the game data, not the account' })),
        el('div.card-body', {}, list,
          frag('<p class="muted" style="font-size:12px;margin:12px 0 0;line-height:1.6">Everything here writes to the ExcelDB the server reads and to the copy inside the game install, so the server has to be restarted and the game relaunched before a change shows up.</p>'))));
    }

    function characters() {
      const back = button('Back', { variant: 'ghost', sm: true, iconName: 'x', onClick: () => go('menu') });
      const add = button('Add character', { variant: 'primary', sm: true, iconName: 'plus', onClick: importFlow });
      const body = el('div.card-body', {});
      host.appendChild(el('div.card', {},
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Custom characters' }), el('div.spacer', {}), back, add),
        body));

      body.innerHTML = '<div class="empty"><div class="spinner"></div></div>';
      api.modsCharacters().then((data) => {
        clear(body);
        if (!data.characters.length) {
          body.appendChild(frag('<div class="empty"><b>No custom characters</b><span>Add one from a zip and it gets cloned onto a free student id.</span></div>'));
        } else {
          const list = el('div.picker-list', {});
          for (const c of data.characters) {
            const row = frag(`<div class="picker-item"><span class="pi-id">${c.id}</span><span class="pi-name">${escapeHtml(c.name || 'unnamed')}</span><span class="tag grey">from ${c.donorId}</span>${c.assets.length ? `<span class="tag">${c.assets.length} file${c.assets.length === 1 ? '' : 's'}</span>` : ''}</div>`);
            row.addEventListener('click', () => go('editor', c.id));
            list.appendChild(row);
          }
          body.appendChild(list);
        }
        body.appendChild(frag(`<p class="muted" style="font-size:12px;margin:12px 0 0;line-height:1.6">Writing to ${data.databases} ExcelDB cop${data.databases === 1 ? 'y' : 'ies'}. A backup is taken next to each one the first time a mod is installed.</p>`));
      }).catch((e) => { body.innerHTML = `<div class="empty"><b>Couldn't load</b><span>${escapeHtml(String(e.message || e))}</span></div>`; });
    }

    async function importFlow() {
      const zipPath = await window.host.pickFile([{ name: 'Character mod', extensions: ['zip'] }]);
      if (!zipPath) return;

      let info;
      try { info = await api.modsInspect(zipPath); }
      catch (e) { toast(e.message, 'bad'); return; }

      let donorId = info.donorId || null;
      let donorName = null;

      const name = input({ value: info.name || '', placeholder: 'Shirakami Suzu' });
      const id = input({ type: 'number', placeholder: 'next free id' });
      const donorLabel = el('div', {});
      const pickDonor = button('Choose donor', { variant: 'ghost', sm: true, iconName: 'users', onClick: () => {
        openPicker({ title: 'Borrow from', loader: (q) => api.staticCharacters(q).then((r) => r.map((x) => ({ id: x.id, name: x.name, sub: `★${x.maxStar}` }))),
          onPick: (it) => { donorId = it.id; donorName = it.name; paintDonor(); } });
      }});
      function paintDonor() {
        clear(donorLabel);
        if (donorId) donorLabel.appendChild(frag(`<div class="chip"><div class="chip-ic">${'★'}</div><div class="chip-main"><b>${escapeHtml(donorName || ('Character ' + donorId))}</b><span>id ${donorId}</span></div></div>`));
        else donorLabel.appendChild(frag('<div class="muted" style="font-size:12.5px">Pick the student whose rows the new one is built from</div>'));
      }
      paintDonor();

      const staged = info.assets.length
        ? `<p class="muted" style="font-size:12px;margin:12px 0 0;line-height:1.6">${info.assets.length} art/audio file${info.assets.length === 1 ? '' : 's'} in the zip get copied into the mods folder but are <b>not</b> installed - a new asset path cannot be registered without repacking the game's addressable catalog, so she draws the donor's art.</p>`
        : '<p class="muted" style="font-size:12px;margin:12px 0 0;line-height:1.6">The zip carries no art, so she draws the donor\'s.</p>';

      const install = button('Install', { variant: 'primary', iconName: 'download' });
      const cancel = button('Cancel', { variant: 'ghost' });
      const ref = modal({
        title: 'Add custom character', wide: true,
        body: el('div', {},
          field('Name', name, 'shown on the card and in the student list'),
          el('div', { style: { display: 'flex', gap: '10px', alignItems: 'center', margin: '0 0 14px' } }, donorLabel, el('div.spacer', {}), pickDonor),
          field('Character id', id, 'leave blank to take the next free one'),
          frag(`<div class="muted mono" data-selectable style="font-size:11px;overflow-wrap:anywhere">${escapeHtml(zipPath)}</div>`),
          frag(staged)),
        footer: [cancel, install],
      });
      cancel.addEventListener('click', ref.close);
      install.addEventListener('click', async () => {
        if (!donorId) { toast('Pick a donor student first', 'warn'); return; }
        install.disabled = true;
        try {
          const made = await api.modsImport({ zipPath, donorId, id: id.value ? parseInt(id.value, 10) : null, name: name.value.trim(), overrides: info.overrides || {} });
          ref.close();
          toast(`${made.name} installed as ${made.id}`, 'good', 'Restart the server');
          paint();
        } catch (e) { install.disabled = false; toast(e.message, 'bad'); }
      });
    }

    function editor(id) {
      const back = button('Back', { variant: 'ghost', sm: true, iconName: 'x', onClick: () => go('characters') });
      const body = el('div.card-body', {});
      const head = el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Character ' + id }), el('div.spacer', {}), back);
      host.appendChild(el('div.card', {}, head, body));

      body.innerHTML = '<div class="empty"><div class="spinner"></div></div>';
      api.modsCharacter(id).then((d) => {
        clear(body);
        head.querySelector('h3').textContent = `${d.name || 'unnamed'} - ${id}`;

        const name = input({ value: d.name || '' });
        const inputs = { character: {}, profile: {}, stat: {} };

        function group(title, values, bag, numeric) {
          if (!values) return null;
          const grid = el('div.grid-3', {});
          for (const [key, value] of Object.entries(values)) {
            const box = input({ value: value == null ? '' : value, type: numeric ? 'number' : 'text' });
            bag[key] = { box, was: value == null ? '' : String(value) };
            grid.appendChild(field(key, box));
          }
          return el('div', {}, el('div', { text: title, style: { fontSize: '13px', fontWeight: '600', margin: '18px 0 10px' } }), grid);
        }

        body.appendChild(field('Display name', name, 'the LocalizeEtc row this character points at'));
        body.appendChild(el('div', {},
          group('Character', d.character, inputs.character, false),
          group('Profile', d.profile, inputs.profile, false),
          group('Stats', d.stat, inputs.stat, true)));

        const save = button('Save', { variant: 'primary', iconName: 'save', onClick: async () => {
          const payload = { name: name.value.trim() };
          for (const part of ['character', 'profile', 'stat']) {
            const changed = {};
            for (const [key, held] of Object.entries(inputs[part])) {
              if (held.box.value !== held.was) changed[key] = held.box.value;
            }
            if (Object.keys(changed).length) payload[part] = changed;
          }
          save.disabled = true;
          try {
            await api.modsUpdate(id, payload);
            toast('Saved - restart the server for it to take', 'good');
            go('characters');
          } catch (e) { save.disabled = false; toast(e.message, 'bad'); }
        }});
        const remove = button('Delete character', { variant: 'danger', iconName: 'trash', onClick: async () => {
          const ok = await confirmDialog({ title: 'Delete character', confirmLabel: 'Delete', danger: true,
            message: `Every row cloned for ${id} is dropped from all ExcelDB copies. Accounts that already own her keep a row pointing at an id that no longer exists.` });
          if (!ok) return;
          try { await api.modsRemove(id); toast('Deleted', 'warn'); go('characters'); }
          catch (e) { toast(e.message, 'bad'); }
        }});

        body.appendChild(el('div.row.wrap', { style: { gap: '10px', marginTop: '20px' } }, save, remove, el('div.spacer', {}),
          d.assets.length ? frag(`<span class="tag grey">${d.assets.length} staged file${d.assets.length === 1 ? '' : 's'}</span>`) : null,
          d.donorId ? frag(`<span class="tag">cloned from ${d.donorId}</span>`) : null));
      }).catch((e) => { body.innerHTML = `<div class="empty"><b>Couldn't load</b><span>${escapeHtml(String(e.message || e))}</span></div>`; });
    }
  },
};
