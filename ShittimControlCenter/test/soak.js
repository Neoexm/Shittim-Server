'use strict';

// A007. Runs the real page under a sustained log flood and fails if anything that should be bounded is not, or if the console goes back to laying out once per frame. Not part of npm test: it needs Electron and a minute of wall clock, and the numbers it reads are only meaningful on an idle machine. npm run soak.
//
// The counts come from CDP Performance.getMetrics, which is the only place live node/listener/document counts are exposed. performance.memory is quantized to uselessness and process._getActiveHandles() reports 0 under Electron, so neither says anything about a leak here.
const { app, BrowserWindow } = require('electron');
const path = require('path');

const SECONDS = Number(process.env.SOAK_SECONDS || 60);
const PER_SECOND = Number(process.env.SOAK_RATE || 400);
const wait = (ms) => new Promise((r) => setTimeout(r, ms));

const DRIVER = (perSecond) => `(() => {
  window.__soak = { blocked: [], lines: 0, navs: 0 };
  new PerformanceObserver((l) => { for (const e of l.getEntries()) window.__soak.blocked.push(+e.duration.toFixed(1)); }).observe({ entryTypes: ['longtask'] });

  const pages = ['overview', 'accounts', 'inventory', 'mail', 'events', 'rates', 'config', 'updates'];
  const every = Math.max(1, Math.round(1000 / ${perSecond}));
  const burst = Math.max(1, Math.round(${perSecond} / (1000 / every)));
  window.__soakTimers = [
    setInterval(() => {
      for (let i = 0; i < burst; i++) {
        const n = window.__soak.lines++;
        window.__log({ source: n % 3 === 0 ? 'mitm' : 'server', line: '[' + new Date().toISOString() + '] INF request ' + n + ' handled in ' + (n % 97) + 'ms' });
      }
    }, every),
    setInterval(() => {
      const p = pages[window.__soak.navs++ % pages.length];
      const item = document.querySelector('.nav-item[data-id="' + p + '"]');
      if (!item) throw new Error('no nav item ' + p);
      item.click();
    }, 3000),
    setInterval(() => {
      const sel = document.querySelector('.dock-sel');
      const opts = ['all', 'server', 'mitm'];
      sel.value = opts[(opts.indexOf(sel.value) + 1) % opts.length];
      sel.dispatchEvent(new Event('change'));
    }, 7000),
  ];
  return true;
})()`;

const SAMPLE = `(() => {
  const b = window.__soak.blocked.splice(0);
  return {
    blocked: Math.round(b.reduce((a, x) => a + x, 0)),
    worst: b.length ? Math.max(...b) : 0,
    rows: document.querySelectorAll('.console .ln').length,
    lines: window.__soak.lines,
    navs: window.__soak.navs,
  };
})()`;

const WANT = ['TaskDuration', 'LayoutCount', 'Nodes', 'JSEventListeners', 'Documents', 'DetachedScriptStates'];

// Measured on the current code at 400 lines a second: 10 layouts a second, 1400 rows, 38 listeners, and a node count swinging between 4k and 24k with no trend - Nodes counts text nodes, so it moves with where in the repaint cycle the sample lands rather than with anything that accumulates. Taking the throttle back out of the console feed puts layouts at 108 a second, which is the number this is really here to catch.
const LIMITS = { layoutsPerSecond: 40, rows: 1400, nodes: 40000, listeners: 400, documents: 8, detached: 0, worstBlock: 2000, growthMb: 40 };

app.whenReady().then(async () => {
  const win = new BrowserWindow({
    x: -4000, y: -4000, width: 1280, height: 860, show: true, opacity: 0,
    webPreferences: { preload: path.join(__dirname, 'soak-preload.js'), contextIsolation: false, nodeIntegration: false, backgroundThrottling: false },
  });
  await win.loadFile(path.join(__dirname, '..', 'src', 'index.html'));
  await wait(1500);

  const dbg = win.webContents.debugger;
  dbg.attach('1.3');
  await dbg.sendCommand('Performance.enable');
  const metrics = async () => {
    const { metrics: m } = await dbg.sendCommand('Performance.getMetrics');
    return Object.fromEntries(m.filter((x) => WANT.includes(x.name)).map((x) => [x.name, x.value]));
  };

  const first = await metrics();
  let prev = first;
  await win.webContents.executeJavaScript(DRIVER(PER_SECOND));

  const samples = [];
  for (let s = 5; s <= SECONDS; s += 5) {
    await wait(5000);
    const now = await metrics();
    const proc = app.getAppMetrics().find((m) => m.pid === win.webContents.getOSProcessId());
    samples.push({
      at: s,
      ...(await win.webContents.executeJavaScript(SAMPLE)),
      layouts: now.LayoutCount - prev.LayoutCount,
      taskMs: Math.round((now.TaskDuration - prev.TaskDuration) * 1000),
      nodes: now.Nodes,
      listeners: now.JSEventListeners,
      documents: now.Documents,
      detached: now.DetachedScriptStates,
      workingSetMb: proc ? +(proc.memory.workingSetSize / 1024).toFixed(1) : 0,
    });
    prev = now;
  }
  await win.webContents.executeJavaScript('window.__soakTimers.forEach(clearInterval); true');

  const peak = (k) => Math.max(...samples.map((s) => s[k]));
  const layoutsPerSecond = +(samples.reduce((a, s) => a + s.layouts, 0) / SECONDS).toFixed(1);
  // the last third against the first third, so a plateau after the initial fill does not read as growth
  const third = Math.max(1, Math.round(samples.length / 3));
  const early = samples.slice(0, third).reduce((a, s) => a + s.workingSetMb, 0) / third;
  const late = samples.slice(-third).reduce((a, s) => a + s.workingSetMb, 0) / third;

  const failures = [];
  const check = (what, value, limit) => {
    console.log(`  ${what.padEnd(20)} ${String(value).padStart(8)}  limit ${limit}`);
    if (value > limit) failures.push(`${what} ${value} over ${limit}`);
  };

  console.log(`${samples[samples.length - 1].lines} lines and ${samples[samples.length - 1].navs} page switches over ${SECONDS}s at ${PER_SECOND}/s`);
  check('layouts per second', layoutsPerSecond, LIMITS.layoutsPerSecond);
  check('console rows', peak('rows'), LIMITS.rows);
  check('live nodes', peak('nodes'), LIMITS.nodes);
  check('listeners', peak('listeners'), LIMITS.listeners);
  check('documents', peak('documents'), LIMITS.documents);
  check('detached scripts', peak('detached'), LIMITS.detached);
  check('worst block ms', peak('worst'), LIMITS.worstBlock);
  check('working set growth', +(late - early).toFixed(1), LIMITS.growthMb);

  if (failures.length) {
    console.error('soak failed:\n  ' + failures.join('\n  '));
    console.error(JSON.stringify(samples, null, 2));
  }
  app.exit(failures.length ? 1 : 0);
}).catch((e) => {
  console.error(String((e && e.stack) || e));
  app.exit(1);
});
