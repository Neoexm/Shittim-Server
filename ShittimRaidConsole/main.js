'use strict';

const path = require('path');
const { app, BrowserWindow, ipcMain } = require('electron');

function createWindow() {
  const win = new BrowserWindow({
    width: 1180,
    height: 780,
    minWidth: 900,
    minHeight: 600,
    show: false,
    frame: false,
    backgroundColor: '#0d1826',
    title: 'Shittim Raid Console',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  win.loadFile(path.join(__dirname, 'src', 'index.html'));
  win.once('ready-to-show', () => win.show());

  win.on('maximize', () => win.webContents.send('window:state', { maximized: true }));
  win.on('unmaximize', () => win.webContents.send('window:state', { maximized: false }));
  return win;
}

ipcMain.on('window:control', (e, action) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (!win) return;
  if (action === 'minimize') win.minimize();
  else if (action === 'maximize') win.isMaximized() ? win.unmaximize() : win.maximize();
  else if (action === 'close') win.close();
});

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
