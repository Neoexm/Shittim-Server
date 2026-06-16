// HTTP client for the server's /api/admin surface plus a tiny reactive store
// shared across pages. The renderer reaches the server over loopback only.

const host = window.host;

// Stable currency map (mirrors Schale.FlatData.CurrencyTypes) so the Accounts
// page can render balances regardless of how enum dict keys are serialized.
export const CURRENCIES = [
  [1, 'Gold'], [2, 'GemPaid'], [3, 'GemBonus'], [4, 'Gem'], [5, 'ActionPoint'],
  [6, 'AcademyTicket'], [7, 'ArenaTicket'], [8, 'RaidTicket'],
  [9, 'WeekDungeonChaserATicket'], [10, 'WeekDungeonFindGiftTicket'], [11, 'WeekDungeonBloodTicket'],
  [12, 'WeekDungeonChaserBTicket'], [13, 'WeekDungeonChaserCTicket'],
  [14, 'SchoolDungeonATicket'], [15, 'SchoolDungeonBTicket'], [16, 'SchoolDungeonCTicket'],
  [17, 'TimeAttackDungeonTicket'], [18, 'MasterCoin'],
  [19, 'WorldRaidTicketA'], [20, 'WorldRaidTicketB'], [21, 'WorldRaidTicketC'],
  [22, 'ChaserTotalTicket'], [23, 'SchoolDungeonTotalTicket'],
  [24, 'EliminateTicketA'], [25, 'EliminateTicketB'], [26, 'EliminateTicketC'], [27, 'EliminateTicketD'],
];
export const CURRENCY_ID = Object.fromEntries(CURRENCIES.map(([id, name]) => [name, id]));
export const CURRENCY_NAME = Object.fromEntries(CURRENCIES.map(([id, name]) => [id, name]));
// "primary" currencies surfaced prominently
export const PRIMARY_CURRENCIES = [4, 1, 5, 7, 8, 18]; // Gem, Gold, AP, Arena, Raid, MasterCoin

let apiPort = 5000;

async function refreshBase() {
  try {
    const cfg = await host.configRead();
    const p = cfg?.data?.ServerConfiguration?.HostPort;
    if (p) apiPort = parseInt(p, 10) || 5000;
  } catch { /* keep default */ }
  return apiPort;
}
function base() { return `http://127.0.0.1:${apiPort}`; }

async function req(method, pathname, body, { timeout = 12000 } = {}) {
  const ctrl = new AbortController();
  const tid = setTimeout(() => ctrl.abort(), timeout);
  try {
    const res = await fetch(base() + pathname, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      signal: ctrl.signal,
    });
    const text = await res.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { data = text; }
    if (!res.ok) {
      const msg = (data && (data.error || data.Message || data.title)) || `HTTP ${res.status}`;
      throw new Error(msg);
    }
    return data;
  } finally {
    clearTimeout(tid);
  }
}

export const api = {
  refreshBase,
  port: () => apiPort,
  get: (p, opts) => req('GET', p, null, opts),
  post: (p, body, opts) => req('POST', p, body, opts),

  hostPort: () => `127.0.0.1:${apiPort}`,

  async health() {
    try { const r = await req('GET', '/health', null, { timeout: 1800 }); return r?.status === 'ok'; }
    catch { return false; }
  },

  // Two-stage probe so the UI never lies about being "online":
  //   live  = the web host answers /health (process is up / port bound)
  //   ready = /api/admin/status answers — that handler hits the DB, so a 200
  //           means the server is genuinely able to serve, not just listening.
  // Only `ready` should ever render as "Online".
  async probe() {
    let live = false;
    try { const r = await req('GET', '/health', null, { timeout: 1500 }); live = r?.status === 'ok'; }
    catch { return { live: false, ready: false, status: null }; }
    if (!live) return { live: false, ready: false, status: null };
    try {
      const status = await req('GET', '/api/admin/status', null, { timeout: 2500 });
      return { live: true, ready: true, status };
    } catch {
      return { live: true, ready: false, status: null };
    }
  },

  // accounts
  status: () => req('GET', '/api/admin/status'),
  accounts: () => req('GET', '/api/admin/accounts'),
  accountDetail: (id) => req('GET', `/api/admin/account/${id}/detail`),
  accountCreate: (b) => req('POST', '/api/admin/account/create', b),
  accountUpdate: (b) => req('POST', '/api/admin/account/update', b),
  accountDelete: (id) => req('POST', '/api/admin/account/delete', { serverId: id }),
  currencies: (id) => req('GET', `/api/admin/account/${id}/currencies`),
  setCurrency: (b) => req('POST', '/api/admin/currency/set', b),

  // inventory
  items: (id) => req('GET', `/api/admin/account/${id}/items`),
  giveItem: (b) => req('POST', '/api/admin/items/give', b),
  removeItem: (b) => req('POST', '/api/admin/items/remove', b),
  characters: (id) => req('GET', `/api/admin/account/${id}/characters`),

  // mail
  mails: (id) => req('GET', `/api/admin/account/${id}/mails`),
  sendMail: (b) => req('POST', '/api/admin/mail/send', b),
  deleteMail: (b) => req('POST', '/api/admin/mail/delete', b),

  // commands
  command: (uid, command) => req('POST', '/api/admin/command', { uid, command }),

  // static
  staticItems: (q) => req('GET', `/api/admin/static/items?limit=400&search=${encodeURIComponent(q || '')}`),
  staticCharacters: (q) => req('GET', `/api/admin/static/characters?limit=600&search=${encodeURIComponent(q || '')}`),
  staticEquipment: (q) => req('GET', `/api/admin/static/equipment?limit=400&search=${encodeURIComponent(q || '')}`),
  staticCurrencies: () => req('GET', '/api/admin/static/currencies'),
  parcelTypes: () => req('GET', '/api/admin/meta/parceltypes'),

  // gacha + events
  gachaConfig: () => req('GET', '/api/admin/gacha/config'),
  setGachaConfig: (b) => req('POST', '/api/admin/gacha/config', b),
  gachaBanners: () => req('GET', '/api/admin/gacha/banners'),
  eventSeasons: () => req('GET', '/api/admin/events/seasons'),
};

// ---------------------------------------------------------------- store

function makeStore(initial) {
  let state = initial;
  const subs = new Set();
  return {
    get: () => state,
    set(patch) { state = { ...state, ...patch }; subs.forEach((f) => f(state)); },
    subscribe(f) { subs.add(f); return () => subs.delete(f); },
  };
}

export const store = makeStore({
  procServer: 'stopped',   // process lifecycle state from main
  procMitm: 'stopped',
  serverPid: null,         // pid of the server child we spawned (if any)
  online: false,           // server is READY (db-backed /api/admin/status answered)
  live: false,             // web host answered /health but may not be ready yet
  status: null,            // /api/admin/status payload
  lastCheckedTs: 0,        // epoch ms of the last completed probe
  probeTarget: '127.0.0.1:5000', // host:port the probe is hitting
  accounts: [],            // [{serverId,nickname,level,...}]
  targetId: null,          // selected account
});

export async function reloadAccounts() {
  try {
    const list = await api.accounts();
    store.set({ accounts: list || [] });
    const cur = store.get().targetId;
    if ((!cur || !list.some((a) => a.serverId === cur)) && list.length) {
      store.set({ targetId: list[0].serverId });
    }
    return list;
  } catch {
    store.set({ accounts: [] });
    return [];
  }
}

export function targetAccount() {
  const s = store.get();
  return s.accounts.find((a) => a.serverId === s.targetId) || null;
}
