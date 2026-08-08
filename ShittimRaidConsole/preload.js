'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('host', {
  windowControl: (action) => ipcRenderer.send('window:control', action),
  onWindowState: (cb) => ipcRenderer.on('window:state', (_e, d) => cb(d)),
});
