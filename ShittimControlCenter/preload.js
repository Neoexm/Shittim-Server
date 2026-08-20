'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('host', {
  paths: () => ipcRenderer.invoke('paths:resolve'),
  pickAccountData: () => ipcRenderer.invoke('accountdata:pick'),
  settingsRead: () => ipcRenderer.invoke('settings:read'),
  settingsWrite: (patch) => ipcRenderer.invoke('settings:write', patch),

  configRead: () => ipcRenderer.invoke('config:read'),
  configWrite: (payload) => ipcRenderer.invoke('config:write', payload),

  serverStart: () => ipcRenderer.invoke('server:start'),
  serverStop: () => ipcRenderer.invoke('server:stop'),
  mitmStart: () => ipcRenderer.invoke('mitm:start'),
  mitmStop: () => ipcRenderer.invoke('mitm:stop'),
  systemStart: () => ipcRenderer.invoke('system:start'),
  systemStop: () => ipcRenderer.invoke('system:stop'),
  systemStartOffline: () => ipcRenderer.invoke('system:startOffline'),
  offlineStatus: () => ipcRenderer.invoke('offline:status'),
  offlineHosts: (on) => ipcRenderer.invoke('offline:hosts', on),
  procStatus: () => ipcRenderer.invoke('proc:status'),

  envCheck: () => ipcRenderer.invoke('env:check'),

  setupInstall: (which) => ipcRenderer.invoke('setup:install', which),
  onSetupProgress: (cb) => {
    const fn = (_e, d) => cb(d);
    ipcRenderer.on('setup:progress', fn);
    return () => ipcRenderer.removeListener('setup:progress', fn);
  },

  projectStatus: () => ipcRenderer.invoke('project:status'),
  projectDownload: (opts) => ipcRenderer.invoke('project:download', opts),
  projectSetPath: (dir) => ipcRenderer.invoke('project:setPath', dir),

  updatesCheck: () => ipcRenderer.invoke('updates:check'),
  updatesApply: () => ipcRenderer.invoke('updates:apply'),
  updatesRebuild: () => ipcRenderer.invoke('updates:rebuild'),

  updatesCheckSelf: () => ipcRenderer.invoke('updates:checkSelf'),
  onSelfUpdate: (cb) => {
    const fn = (_e, d) => cb(d);
    ipcRenderer.on('update:self', fn);
    return () => ipcRenderer.removeListener('update:self', fn);
  },
  // periodic "server source is behind origin" notice from the main process
  onServerUpdate: (cb) => {
    const fn = (_e, d) => cb(d);
    ipcRenderer.on('update:server', fn);
    return () => ipcRenderer.removeListener('update:server', fn);
  },

  pickFolder: () => ipcRenderer.invoke('dialog:pickFolder'),
  pickFile: (filters) => ipcRenderer.invoke('dialog:pickFile', filters),
  openPath: (p) => ipcRenderer.invoke('shell:openPath', p),
  openExternal: (url) => ipcRenderer.invoke('shell:openExternal', url),
  revealPath: (p) => ipcRenderer.invoke('shell:showItem', p),

  exportLogs: () => ipcRenderer.invoke('logs:export'),

  windowControl: (action) => ipcRenderer.send('window:control', action),

  onProcLog: (cb) => ipcRenderer.on('proc:log', (_e, d) => cb(d)),
  onProcState: (cb) => ipcRenderer.on('proc:state', (_e, d) => cb(d)),
  onWindowState: (cb) => ipcRenderer.on('window:state', (_e, d) => cb(d)),
  // download/extract progress for project acquisition + updates
  onProjectProgress: (cb) => {
    const fn = (_e, d) => cb(d);
    ipcRenderer.on('project:progress', fn);
    return () => ipcRenderer.removeListener('project:progress', fn);
  },
});
