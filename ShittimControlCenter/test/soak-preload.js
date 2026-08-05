'use strict';

// Stands in for the real preload so the soak drives the actual page without a server behind it. Same window.host
// surface, every answer canned, plus window.__log to push lines at the console the way the main process would.
const noop = () => {};
const logSubs = [];
const stateSubs = [];

window.__log = (d) => logSubs.forEach((f) => f(d));
window.__procState = (d) => stateSubs.forEach((f) => f(d));

window.host = {
  paths: async () => ({ projectDir: 'C:\\soak', dataDir: 'C:\\soak\\data' }),
  settingsRead: async () => ({}),
  settingsWrite: async () => ({ ok: true }),
  configRead: async () => ({ data: { ServerConfiguration: { HostPort: '5599' } } }),
  configWrite: async () => ({ ok: true }),
  serverStart: async () => ({ ok: true }),
  serverStop: async () => ({ ok: true }),
  mitmStart: async () => ({ ok: true }),
  mitmStop: async () => ({ ok: true }),
  systemStart: async () => ({ ok: true }),
  systemStop: async () => ({ ok: true }),
  systemStartOffline: async () => ({ ok: true }),
  offlineStatus: async () => ({ ok: true, hosts: false }),
  offlineHosts: async () => ({ ok: true }),
  procStatus: async () => ({ server: 'stopped', mitm: 'stopped', serverPid: null, mitmPid: null }),
  envCheck: async () => ({ dotnet: { ok: true }, mitmproxy: { ok: true } }),
  setupInstall: async () => ({ ok: true }),
  onSetupProgress: () => noop,
  projectStatus: async () => ({ found: true, path: 'C:\\soak' }),
  projectDownload: async () => ({ ok: true }),
  projectSetPath: async () => ({ ok: true }),
  updatesCheck: async () => ({ ok: true, behind: 0 }),
  updatesApply: async () => ({ ok: true }),
  updatesRebuild: async () => ({ ok: true }),
  updatesCheckSelf: async () => ({ ok: true, available: false }),
  onSelfUpdate: () => noop,
  onServerUpdate: () => noop,
  pickFolder: async () => null,
  pickFile: async () => null,
  openPath: noop,
  openExternal: noop,
  revealPath: noop,
  exportLogs: async () => ({ ok: true, path: '', name: '', count: 0 }),
  windowControl: noop,
  onProcLog: (cb) => { logSubs.push(cb); return noop; },
  onProcState: (cb) => { stateSubs.push(cb); },
  onWindowState: noop,
  onProjectProgress: () => noop,
};
