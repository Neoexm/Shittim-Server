'use strict';

const { contextBridge, ipcRenderer } = require('electron');

// A deliberately small, audited surface bridged into the renderer. The renderer
// never touches Node or Electron internals directly — everything it can do is
// listed here.
contextBridge.exposeInMainWorld('host', {
  // path + settings
  paths: () => ipcRenderer.invoke('paths:resolve'),
  settingsRead: () => ipcRenderer.invoke('settings:read'),
  settingsWrite: (patch) => ipcRenderer.invoke('settings:write', patch),

  // server config file
  configRead: () => ipcRenderer.invoke('config:read'),
  configWrite: (payload) => ipcRenderer.invoke('config:write', payload),

  // process lifecycle
  serverStart: () => ipcRenderer.invoke('server:start'),
  serverStop: () => ipcRenderer.invoke('server:stop'),
  mitmStart: () => ipcRenderer.invoke('mitm:start'),
  mitmStop: () => ipcRenderer.invoke('mitm:stop'),
  systemStart: () => ipcRenderer.invoke('system:start'),
  systemStop: () => ipcRenderer.invoke('system:stop'),
  procStatus: () => ipcRenderer.invoke('proc:status'),

  // diagnostics
  envCheck: () => ipcRenderer.invoke('env:check'),

  // project location + first-run acquisition
  projectStatus: () => ipcRenderer.invoke('project:status'),
  projectDownload: (opts) => ipcRenderer.invoke('project:download', opts),
  projectSetPath: (dir) => ipcRenderer.invoke('project:setPath', dir),

  // self-update (GitHub REST — no git required)
  updatesCheck: () => ipcRenderer.invoke('updates:check'),
  updatesApply: () => ipcRenderer.invoke('updates:apply'),
  updatesRebuild: () => ipcRenderer.invoke('updates:rebuild'),

  // dialogs + shell
  pickFolder: () => ipcRenderer.invoke('dialog:pickFolder'),
  pickFile: (filters) => ipcRenderer.invoke('dialog:pickFile', filters),
  openPath: (p) => ipcRenderer.invoke('shell:openPath', p),
  openExternal: (url) => ipcRenderer.invoke('shell:openExternal', url),

  // window chrome
  windowControl: (action) => ipcRenderer.send('window:control', action),

  // event streams
  onProcLog: (cb) => ipcRenderer.on('proc:log', (_e, d) => cb(d)),
  onProcState: (cb) => ipcRenderer.on('proc:state', (_e, d) => cb(d)),
  onWindowState: (cb) => ipcRenderer.on('window:state', (_e, d) => cb(d)),
  // download/extract progress for project acquisition + updates; returns an
  // unsubscribe so callers can detach the listener when their view goes away.
  onProjectProgress: (cb) => {
    const fn = (_e, d) => cb(d);
    ipcRenderer.on('project:progress', fn);
    return () => ipcRenderer.removeListener('project:progress', fn);
  },
});
