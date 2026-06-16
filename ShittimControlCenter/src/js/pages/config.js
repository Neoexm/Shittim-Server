import { el, frag, clear, button, input, toggle, field, toast, modal, textarea, confirmDialog } from '../ui.js';
import { store } from '../api.js';

// Defaults mirror Shittim-Server/Configuration/ConfigType/ServerConfig.cs.
// Reset overwrites only these editable ServerConfiguration fields, preserving
// GameVersion, gateway keys, ClientPluginDirectory and the Irc/DataFetcher
// sibling sections in Config.json.
const DEFAULT_SERVER_CONFIG = {
  HostPort: '5000',
  GatewayPort: '5100',
  EnableGateway: true,
  AutoPatchClientMetadata: true, ClientMetadataPath: '',
  AutoPatchClientGameAssemblyIas: false, ClientGameAssemblyPath: '',
  AutoPatchClientGamescaleIas: true, ClientGamescaleCorePath: '',
  AutoPatchClientNexonPlatformIas: true, ClientNexonPlatformModulesPath: '',
  AutoPatchClientInfaceIas: true, ClientInfacePath: '',
  AutoPatchClientInfaceConfig: true, ClientInfaceConfigPath: '',
  AutoManageGrap64: true, ClientGrap64Path: '',
  AutoPatchClientBanners: true, ClientExcelDbPath: '',
  SQLProvider: 'SQLite3',
  SQLConnectionString: 'Data Source=shittim.sqlite3',
  UseEncryption: false,
  BypassAuthentication: false,
  UseCustomExcel: false,
  AutoCheckVersion: true,
  AutoUpdateVersion: true,
  AutoUpdateResources: false,
  OverrideVersionId: null,
  OverrideCdnBaseUrl: null,
  ExcelDbSqlCipherKey: 'efa143094711b6563ec2132d4d6bbe8533d4e291ed4820bdb515b26bb57bb3f0',
  ExcelDbSqlCipherLicense: 'OmNpZDowMDFWSjAwMDAwY3pzaVlZQVE6cGxhdGZvcm06MjY6ZXhwaXJlOm5ldmVyOnZlcnNpb246MTpsaWJ2ZXI6NC4xMC4wOmhtYWM6ODQ1Y2JkMzQ0MDc3YjIxNmRlYTgyOWI3OTIyMzRkM2UwYmUyMzNhYw==',
  ServerInfoUrl: 'https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/433063_Live_77acRXMErRIj8461BJ0KXJP3t.json',
  PacketLogging: { RequestPacket: true, ResponsePacket: false, ErrorPacket: false },
};

// field descriptors grouped for a readable editor over ServerConfiguration
const GROUPS = [
  {
    title: 'Networking', icon: 'server',
    fields: [
      { key: 'HostPort', label: 'API port', type: 'text', hint: 'default 5000' },
      { key: 'GatewayPort', label: 'Gateway port', type: 'text', hint: 'default 5100' },
      { key: 'EnableGateway', label: 'Enable gateway', type: 'bool', desc: 'Serve the encrypted gateway endpoint' },
    ],
  },
  {
    title: 'Behaviour', icon: 'bolt',
    fields: [
      { key: 'UseEncryption', label: 'Packet encryption', type: 'bool', desc: 'Encrypt protocol responses' },
      { key: 'BypassAuthentication', label: 'Bypass authentication', type: 'bool', desc: 'Skip login auth checks' },
      { key: 'UseCustomExcel', label: 'Custom Excel tables', type: 'bool', desc: 'Load overridden game data' },
      { key: 'AutoCheckVersion', label: 'Auto-check version', type: 'bool', desc: 'Resolve latest data version on boot' },
      { key: 'AutoUpdateVersion', label: 'Auto-update version', type: 'bool', desc: 'Apply resolved version automatically' },
      { key: 'AutoUpdateResources', label: 'Auto-update resources', type: 'bool', desc: 'Re-download game data (Excel, HexaMap) when the version changes' },
    ],
  },
  {
    title: 'Database', icon: 'inventory',
    fields: [
      { key: 'SQLProvider', label: 'SQL provider', type: 'text' },
      { key: 'SQLConnectionString', label: 'Connection string', type: 'text' },
    ],
  },
  {
    title: 'Version & data sources', icon: 'clock',
    fields: [
      { key: 'OverrideVersionId', label: 'Override version id', type: 'text', hint: 'blank = auto' },
      { key: 'OverrideCdnBaseUrl', label: 'Override CDN base URL', type: 'text', hint: 'blank = auto' },
      { key: 'ServerInfoUrl', label: 'Server info URL', type: 'text' },
    ],
  },
  {
    title: 'Client auto-patching', icon: 'shield',
    fields: [
      { key: 'AutoPatchClientMetadata', label: 'Patch metadata', type: 'bool', path: 'ClientMetadataPath' },
      { key: 'AutoPatchClientGameAssemblyIas', label: 'Patch GameAssembly IAS', type: 'bool', path: 'ClientGameAssemblyPath' },
      { key: 'AutoPatchClientGamescaleIas', label: 'Patch gamescale.core IAS', type: 'bool', path: 'ClientGamescaleCorePath' },
      { key: 'AutoPatchClientNexonPlatformIas', label: 'Patch Nexon platform IAS', type: 'bool', path: 'ClientNexonPlatformModulesPath' },
      { key: 'AutoPatchClientInfaceIas', label: 'Patch inface IAS', type: 'bool', path: 'ClientInfacePath' },
      { key: 'AutoPatchClientInfaceConfig', label: 'Patch inface config', type: 'bool', path: 'ClientInfaceConfigPath' },
      { key: 'AutoManageGrap64', label: 'Manage grap64', type: 'bool', path: 'ClientGrap64Path' },
      { key: 'AutoPatchClientBanners', label: 'Patch recruitment banners', type: 'bool', path: 'ClientExcelDbPath' },
    ],
  },
  {
    title: 'Packet logging', icon: 'edit', sub: 'PacketLogging',
    fields: [
      { key: 'RequestPacket', label: 'Log requests', type: 'bool' },
      { key: 'ResponsePacket', label: 'Log responses', type: 'bool' },
      { key: 'ErrorPacket', label: 'Log errors', type: 'bool' },
    ],
  },
];

export default {
  id: 'config',
  title: 'Configuration',
  subtitle: 'Edit the server configuration file (Config.json)',
  icon: 'config',
  needsTarget: false,

  async mount(root) {
    const cfg = await window.host.configRead();
    if (!cfg.ok) {
      root.appendChild(frag(`<div class="empty"><b>No configuration found</b><span><span class="mono" data-selectable style="word-break:break-all">${cfg.path}</span><br>It is generated the first time the server runs.</span></div>`));
      const b = button('Open containing folder', { variant: 'ghost', iconName: 'folder', onClick: async () => {
        const p = await window.host.paths(); window.host.openPath(p.exeBaseDir);
      }});
      root.appendChild(el('div', { style: { textAlign: 'center', marginTop: '14px' } }, b));
      return;
    }

    const data = cfg.data;
    const sc = data.ServerConfiguration = data.ServerConfiguration || {};
    const pl = sc.PacketLogging = sc.PacketLogging || {};

    // ---- top action bar
    const restartHint = store.get().online
      ? frag('<span class="pill warn"><span class="dot"></span>Restart server to apply</span>')
      : frag('<span class="pill"><span class="dot"></span>Server offline</span>');

    const saveBtn = button('Save configuration', { variant: 'primary', iconName: 'save', onClick: save });
    const reloadBtn = button('Reload', { variant: 'ghost', iconName: 'refresh', onClick: () => ctxReload() });
    const rawBtn = button('Edit raw JSON', { variant: 'ghost', iconName: 'edit', onClick: editRaw });
    const openBtn = button('Open file', { variant: 'ghost', iconName: 'external', onClick: () => window.host.openPath(cfg.path) });
    const resetBtn = button('Reset to defaults', { variant: 'ghost', iconName: 'refresh', onClick: resetDefaults });

    const bar = el('div.card', { style: { marginBottom: '18px' } },
      el('div.card-body', { style: { display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' } },
        saveBtn, reloadBtn, rawBtn, openBtn, resetBtn, el('div.spacer', {}), restartHint));
    root.appendChild(bar);
    root.appendChild(frag(`<div class="row wrap" style="margin:-4px 0 16px;min-width:0"><span class="mono" data-selectable style="font-size:11.5px;color:var(--ink-2);min-width:0;word-break:break-all;line-height:1.5">${cfg.path}</span></div>`));

    // ---- grouped editor
    const grid = el('div.grid-2', { style: { alignItems: 'start' } });

    for (const g of GROUPS) {
      const target = g.sub === 'PacketLogging' ? pl : sc;
      const body = el('div', {});
      for (const f of g.fields) {
        if (f.type === 'bool') {
          const row = buildToggleRow(target, f);
          body.appendChild(row);
          if (f.path) body.appendChild(buildPathField(sc, f));
        } else {
          body.appendChild(field(f.label, bindInput(target, f.key), f.hint));
        }
      }
      grid.appendChild(el('div.card', { style: { minWidth: '0' } },
        el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: g.title }),
          g.sub ? el('span.sub', { text: g.sub } ) : null),
        el('div.card-body', { style: { minWidth: '0' } }, body)));
    }
    root.appendChild(grid);

    // ---- advanced (cipher) collapsible card
    const advBody = el('div', {});
    advBody.appendChild(field('Excel DB SQLCipher key', bindInput(sc, 'ExcelDbSqlCipherKey')));
    advBody.appendChild(field('Excel DB SQLCipher license', bindInput(sc, 'ExcelDbSqlCipherLicense')));
    root.appendChild(el('div.card', { style: { marginTop: '18px' } },
      el('div.card-head', {}, el('span.tab-mark', {}), el('h3', { text: 'Advanced — Excel decryption' }),
        el('span.sub', { text: 'change only if your data dump differs' })),
      el('div.card-body', {}, advBody)));

    // ---- handlers
    function bindInput(obj, key) {
      const i = input({ value: obj[key] ?? '', placeholder: '—' });
      i.addEventListener('input', () => { obj[key] = i.value; });
      return i;
    }
    function buildToggleRow(obj, f) {
      const row = el('div.toggle-row', {},
        el('div.tr-text', {}, el('b', { text: f.label }), f.desc ? el('span', { text: f.desc }) : null));
      const t = toggle(!!obj[f.key], (on) => { obj[f.key] = on; });
      row.appendChild(t);
      return row;
    }
    function buildPathField(obj, f) {
      const wrap = el('div', { style: { margin: '-4px 0 8px', paddingLeft: '2px', minWidth: '0' } });
      const row = el('div.input-row', { style: { minWidth: '0' } });
      const i = input({ value: obj[f.path] ?? '', placeholder: 'path (optional override)' });
      i.addEventListener('input', () => { obj[f.path] = i.value; });
      const browse = button('…', { variant: 'ghost', onClick: async () => {
        const picked = await window.host.pickFile();
        if (picked) { i.value = picked; obj[f.path] = picked; }
      }});
      browse.style.flex = '0 0 auto';
      row.appendChild(i); row.appendChild(browse);
      wrap.appendChild(row);
      return wrap;
    }

    async function save() {
      const r = await window.host.configWrite(data);
      toast(r.ok ? 'Configuration saved' : (r.error || 'Save failed'), r.ok ? 'good' : 'bad', r.ok ? 'Saved' : 'Error');
    }
    function ctxReload() { location.hash = '#/config'; document.querySelector('.nav-item[data-id="config"]').click(); }

    async function resetDefaults() {
      const ok = await confirmDialog({ title: 'Reset to defaults', confirmLabel: 'Reset & save',
        message: 'Restore every setting on this page to its default value and save it to Config.json? GameVersion, gateway keys and the database are left untouched. If the server is running, restart it to apply.' });
      if (!ok) return;
      Object.assign(sc, DEFAULT_SERVER_CONFIG, { PacketLogging: { ...DEFAULT_SERVER_CONFIG.PacketLogging } });
      const r = await window.host.configWrite(data);
      if (r.ok) { toast('Configuration reset to defaults', 'good', 'Defaults restored'); ctxReload(); }
      else toast(r.error || 'Reset failed', 'bad');
    }

    function editRaw() {
      const ta = textarea({ value: JSON.stringify(data, null, 2), style: { minHeight: '52vh', maxWidth: '100%', fontFamily: 'var(--font-mono)', fontSize: '12.5px', whiteSpace: 'pre-wrap', wordBreak: 'break-word' } });
      const apply = button('Apply', { variant: 'primary', iconName: 'check' });
      const cancel = button('Cancel', { variant: 'ghost' });
      const ref = modal({ title: 'Raw configuration', wide: true, body: ta, footer: [cancel, apply] });
      cancel.addEventListener('click', ref.close);
      apply.addEventListener('click', async () => {
        try {
          const parsed = JSON.parse(ta.value);
          const r = await window.host.configWrite(parsed);
          if (r.ok) { ref.close(); toast('Configuration saved', 'good'); ctxReload(); }
          else toast(r.error, 'bad');
        } catch (e) { toast('Invalid JSON: ' + e.message, 'bad'); }
      });
    }
  },
};
