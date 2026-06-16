'use strict';

const { app, BrowserWindow, ipcMain, dialog, shell } = require('electron');
const path = require('path');
const fs = require('fs');
const os = require('os');
const https = require('https');
const { spawn, exec, execFile } = require('child_process');

// ---------------------------------------------------------------- path model
//
// The control center lives at <repoRoot>/ShittimControlCenter. Everything it
// drives (the server project, the database, the mitm scripts) is resolved
// relative to <repoRoot> so the app is portable as long as the layout holds.

const APP_DIR = __dirname;
const SETTINGS_PATH = () => path.join(app.getPath('userData'), 'control-center.json');

function loadSettings() {
  try {
    return JSON.parse(fs.readFileSync(SETTINGS_PATH(), 'utf8'));
  } catch {
    return {};
  }
}
function saveSettings(patch) {
  const next = { ...loadSettings(), ...patch };
  try {
    fs.mkdirSync(app.getPath('userData'), { recursive: true });
    fs.writeFileSync(SETTINGS_PATH(), JSON.stringify(next, null, 2));
  } catch (e) {
    /* non-fatal */
  }
  return next;
}

function resolveRepoRoot() {
  const settings = loadSettings();
  if (settings.repoRoot && fs.existsSync(path.join(settings.repoRoot, 'Shittim-Server'))) {
    return settings.repoRoot;
  }
  // ShittimControlCenter sits directly inside the repo.
  const guess = path.resolve(APP_DIR, '..');
  if (fs.existsSync(path.join(guess, 'Shittim-Server'))) return guess;
  return guess;
}

function firstExisting(paths) {
  for (const p of paths) if (p && fs.existsSync(p)) return p;
  return null;
}

function resolvePaths() {
  const repoRoot = resolveRepoRoot();
  const serverDir = path.join(repoRoot, 'Shittim-Server');
  const csproj = path.join(serverDir, 'Shittim-Server.csproj');

  const debugExe = path.join(serverDir, 'bin', 'Debug', 'net10.0', 'Shittim-Server.exe');
  const releaseExe = path.join(serverDir, 'bin', 'Release', 'net10.0', 'Shittim-Server.exe');
  const exePath = firstExisting([debugExe, releaseExe]);

  // The server reads its config from <exeBaseDir>/Config/Config.json and the
  // gacha overrides from <exeBaseDir>/../gacha_config.json.
  const exeBaseDir = exePath ? path.dirname(exePath) : path.join(serverDir, 'bin', 'Debug', 'net10.0');
  const configPath = path.join(exeBaseDir, 'Config', 'Config.json');
  const gachaConfigPath = path.resolve(path.join(exeBaseDir, '..', 'gacha_config.json'));

  // DB lives in the working directory the server runs from (serverDir).
  const dbPath = path.join(serverDir, 'shittim.sqlite3');

  const scriptsDir = path.join(repoRoot, 'Scripts', 'redirect_server_mitmproxy');
  const redirectScript = path.join(scriptsDir, 'redirect_server.py');

  return {
    repoRoot, serverDir, csproj, exePath, exeBaseDir,
    configPath, gachaConfigPath, dbPath, scriptsDir, redirectScript,
  };
}

// --------------------------------------------------------------- config file

function readConfig() {
  const { configPath } = resolvePaths();
  try {
    const raw = fs.readFileSync(configPath, 'utf8');
    return { ok: true, path: configPath, raw, data: JSON.parse(raw), exists: true };
  } catch (e) {
    return { ok: false, path: configPath, exists: fs.existsSync(configPath), error: String(e.message || e) };
  }
}

function writeConfig(payload) {
  const { configPath } = resolvePaths();
  try {
    const text = typeof payload === 'string' ? payload : JSON.stringify(payload, null, 2);
    JSON.parse(text); // validate
    fs.mkdirSync(path.dirname(configPath), { recursive: true });
    fs.writeFileSync(configPath, text);
    return { ok: true, path: configPath };
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  }
}

// ------------------------------------------------------------ process model

const procs = { server: null, mitm: null };

function broadcast(channel, payload) {
  for (const w of BrowserWindow.getAllWindows()) {
    if (!w.isDestroyed()) w.webContents.send(channel, payload);
  }
}

function pipeLines(child, source) {
  let buf = '';
  const onData = (chunk) => {
    buf += chunk.toString();
    let idx;
    while ((idx = buf.indexOf('\n')) >= 0) {
      const line = buf.slice(0, idx).replace(/\r$/, '');
      buf = buf.slice(idx + 1);
      if (line.length) broadcast('proc:log', { source, line });
    }
  };
  if (child.stdout) child.stdout.on('data', onData);
  if (child.stderr) child.stderr.on('data', onData);
}

function killTree(child) {
  if (!child || child.killed) return;
  if (process.platform === 'win32') {
    try { exec(`taskkill /pid ${child.pid} /T /F`); } catch { /* ignore */ }
  } else {
    try { child.kill('SIGTERM'); } catch { /* ignore */ }
  }
}

function startServer() {
  if (procs.server && !procs.server.killed) return { ok: false, error: 'Server already running' };
  const p = resolvePaths();

  let cmd, args, cwd;
  if (p.exePath) {
    cmd = p.exePath;
    args = [];
    cwd = p.serverDir;
  } else if (fs.existsSync(p.csproj)) {
    cmd = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    args = ['run', '--project', p.csproj];
    cwd = p.serverDir;
  } else {
    return { ok: false, error: 'No server executable or project found. Build the server first.' };
  }

  broadcast('proc:log', { source: 'server', line: `> launching ${path.basename(cmd)} (cwd: ${cwd})` });
  const child = spawn(cmd, args, { cwd, windowsHide: true, env: { ...process.env } });
  procs.server = child;
  broadcast('proc:state', { server: 'starting', serverPid: child.pid });
  pipeLines(child, 'server');

  child.on('exit', (code) => {
    broadcast('proc:log', { source: 'server', line: `> server exited (code ${code})` });
    procs.server = null;
    broadcast('proc:state', { server: 'stopped' });
  });
  child.on('error', (err) => {
    broadcast('proc:log', { source: 'server', line: `> spawn error: ${err.message}` });
    procs.server = null;
    broadcast('proc:state', { server: 'failed' });
  });

  return { ok: true, pid: child.pid };
}

function stopServer() {
  if (!procs.server) return { ok: false, error: 'Server not running' };
  killTree(procs.server);
  procs.server = null;
  broadcast('proc:state', { server: 'stopped' });
  return { ok: true };
}

function startMitm() {
  if (procs.mitm && !procs.mitm.killed) return { ok: false, error: 'mitmproxy already running' };
  const p = resolvePaths();
  if (!fs.existsSync(p.redirectScript)) return { ok: false, error: 'redirect_server.py not found' };

  const args = ['-m', 'wireguard', '--no-http2', '-s', 'redirect_server.py',
    '--set', 'termlog_verbosity=warn', '--mode', 'local:BlueArchive.exe'];
  const cmd = process.platform === 'win32' ? 'mitmweb.exe' : 'mitmweb';

  broadcast('proc:log', { source: 'mitm', line: `> launching mitmweb (cwd: ${p.scriptsDir})` });
  let child;
  try {
    child = spawn(cmd, args, { cwd: p.scriptsDir, windowsHide: true, env: { ...process.env } });
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  }
  procs.mitm = child;
  broadcast('proc:state', { mitm: 'starting' });
  pipeLines(child, 'mitm');

  child.on('exit', (code) => {
    broadcast('proc:log', { source: 'mitm', line: `> mitmproxy exited (code ${code})` });
    procs.mitm = null;
    broadcast('proc:state', { mitm: 'stopped' });
  });
  child.on('error', (err) => {
    broadcast('proc:log', { source: 'mitm', line: `> spawn error: ${err.message} (is mitmproxy installed?)` });
    procs.mitm = null;
    broadcast('proc:state', { mitm: 'failed' });
  });

  return { ok: true, pid: child.pid };
}

function stopMitm() {
  if (!procs.mitm) return { ok: false, error: 'mitmproxy not running' };
  killTree(procs.mitm);
  procs.mitm = null;
  broadcast('proc:state', { mitm: 'stopped' });
  return { ok: true };
}

// ----------------------------------------------------------- env diagnostics

function execCheck(command, timeout = 6000) {
  return new Promise((resolve) => {
    exec(command, { timeout, windowsHide: true }, (err, stdout, stderr) => {
      const out = (stdout || stderr || '').toString().trim();
      resolve({ ok: !err, detail: out.split('\n')[0] || (err ? String(err.message) : '') });
    });
  });
}

async function runEnvChecks() {
  const p = resolvePaths();
  const certPath = path.join(os.homedir(), '.mitmproxy', 'mitmproxy-ca-cert.cer');

  const [dotnet, mitm] = await Promise.all([
    execCheck('dotnet --version'),
    execCheck('mitmweb --version'),
  ]);

  return {
    dotnet: { status: dotnet.ok ? 'ready' : 'missing', detail: dotnet.ok ? `SDK ${dotnet.detail}` : '.NET SDK not found in PATH' },
    mitmproxy: { status: mitm.ok ? 'ready' : 'missing', detail: mitm.ok ? mitm.detail : 'mitmproxy not found in PATH' },
    certificate: { status: fs.existsSync(certPath) ? 'ready' : 'warning', detail: fs.existsSync(certPath) ? certPath : 'CA certificate not generated yet' },
    database: { status: fs.existsSync(p.dbPath) ? 'ready' : 'warning', detail: fs.existsSync(p.dbPath) ? p.dbPath : 'created on first server run' },
    server: { status: p.exePath ? 'ready' : (fs.existsSync(p.csproj) ? 'warning' : 'missing'), detail: p.exePath ? p.exePath : (fs.existsSync(p.csproj) ? 'source only — will build on launch' : 'server project not found') },
    redirect: { status: fs.existsSync(p.redirectScript) ? 'ready' : 'missing', detail: fs.existsSync(p.redirectScript) ? p.redirectScript : 'redirect_server.py missing' },
  };
}

// ------------------------------------------------------------------- updates
//
// The repo is a git checkout of origin/main. "Check" fetches and reports how far
// behind we are plus the incoming changelog; "Apply" does a fast-forward-only
// pull (which NEVER overwrites uncommitted local edits — it refuses if it
// would), and "Rebuild" recompiles the .NET server that the pull may have
// changed.

const US = '';

const GH = { owner: 'Neoexm', repo: 'Shittim-Server', branch: 'main' };
const GH_UA = 'ShittimControlCenter';
const VERSION_FILE = 'shittim-version.json';

// Minimal HTTPS GET that follows redirects and buffers the whole body. GitHub's
// API and codeload both 30x-redirect, so redirect handling is mandatory.
function httpGet(url, { headers = {}, redirects = 5 } = {}) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, { headers: { 'User-Agent': GH_UA, ...headers } }, (res) => {
      const { statusCode } = res;
      if (statusCode >= 300 && statusCode < 400 && res.headers.location && redirects > 0) {
        res.resume();
        resolve(httpGet(new URL(res.headers.location, url).toString(), { headers, redirects: redirects - 1 }));
        return;
      }
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => resolve({ statusCode, headers: res.headers, body: Buffer.concat(chunks) }));
      res.on('error', reject);
    });
    req.on('error', reject);
    req.setTimeout(30000, () => req.destroy(new Error('request timed out')));
  });
}

async function githubApi(pathPart) {
  const res = await httpGet(`https://api.github.com/repos/${GH.owner}/${GH.repo}${pathPart}`, {
    headers: { Accept: 'application/vnd.github+json' },
  });
  if (res.statusCode === 403 || res.statusCode === 429) {
    throw new Error('GitHub API rate limit reached — try again in a little while.');
  }
  if (res.statusCode === 404) throw new Error('Not found on GitHub (branch or repo missing).');
  if (res.statusCode < 200 || res.statusCode >= 300) throw new Error(`GitHub API responded ${res.statusCode}`);
  return JSON.parse(res.body.toString('utf8'));
}

// Streaming download to a file, reporting progress; follows redirects.
function downloadFile(url, destPath, onProgress, redirects = 6) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, { headers: { 'User-Agent': GH_UA } }, (res) => {
      const { statusCode } = res;
      if (statusCode >= 300 && statusCode < 400 && res.headers.location && redirects > 0) {
        res.resume();
        resolve(downloadFile(new URL(res.headers.location, url).toString(), destPath, onProgress, redirects - 1));
        return;
      }
      if (statusCode !== 200) { res.resume(); reject(new Error(`download failed (HTTP ${statusCode})`)); return; }
      const total = Number(res.headers['content-length'] || 0);
      let received = 0;
      const out = fs.createWriteStream(destPath);
      res.on('data', (c) => { received += c.length; if (onProgress) onProgress(received, total); });
      res.on('error', reject);
      out.on('error', reject);
      out.on('finish', () => out.close(() => resolve({ total, received })));
      res.pipe(out);
    });
    req.on('error', reject);
    req.setTimeout(120000, () => req.destroy(new Error('download timed out')));
  });
}

function psQuote(s) { return `'${String(s).replace(/'/g, "''")}'`; }

// Extract a .zip into destDir. Uses PowerShell's Expand-Archive on Windows and
// `unzip` elsewhere — no third-party dependency is bundled.
function extractZip(zipPath, destDir) {
  return new Promise((resolve, reject) => {
    fs.mkdirSync(destDir, { recursive: true });
    if (process.platform === 'win32') {
      const ps = `$ProgressPreference='SilentlyContinue'; Expand-Archive -LiteralPath ${psQuote(zipPath)} -DestinationPath ${psQuote(destDir)} -Force`;
      execFile('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', ps],
        { windowsHide: true, maxBuffer: 64 * 1024 * 1024 },
        (err, _so, se) => err ? reject(new Error((se || '').toString().trim() || err.message)) : resolve());
    } else {
      execFile('unzip', ['-o', zipPath, '-d', destDir], { maxBuffer: 64 * 1024 * 1024 },
        (err, _so, se) => err ? reject(new Error((se || '').toString().trim() || err.message)) : resolve());
    }
  });
}

function readVersionMarker(repoRoot) {
  try { return JSON.parse(fs.readFileSync(path.join(repoRoot, VERSION_FILE), 'utf8')); } catch { return null; }
}
function writeVersionMarker(repoRoot, data) {
  try { fs.writeFileSync(path.join(repoRoot, VERSION_FILE), JSON.stringify(data, null, 2)); } catch { /* non-fatal */ }
}

function defaultDownloadDir() {
  let docs;
  try { docs = app.getPath('documents'); } catch { docs = os.homedir(); }
  return path.join(docs, 'Shittim-Server');
}

// Is a usable server project present at the currently-resolved location?
function projectStatus() {
  const p = resolvePaths();
  const hasCsproj = fs.existsSync(p.csproj);
  const hasExe = !!p.exePath;
  const marker = readVersionMarker(p.repoRoot);
  let source = marker ? marker.source : null;
  if (!source && fs.existsSync(path.join(p.repoRoot, '.git'))) source = 'git';
  return {
    found: hasCsproj || hasExe,
    repoRoot: p.repoRoot, serverDir: p.serverDir, csproj: p.csproj,
    hasCsproj, hasExe, source, version: marker, defaultDir: defaultDownloadDir(),
  };
}

// Point the control center at an existing folder. Accepts either the repo root
// (containing a Shittim-Server/ folder) or the Shittim-Server project folder
// itself, and normalises to the repo root that resolvePaths() expects.
function setProjectPath(dir) {
  if (!dir) return { ok: false, error: 'No folder selected.' };
  if (!fs.existsSync(dir)) return { ok: false, error: 'That folder does not exist.' };
  let repoRoot = null;
  if (fs.existsSync(path.join(dir, 'Shittim-Server', 'Shittim-Server.csproj'))) repoRoot = dir;
  else if (fs.existsSync(path.join(dir, 'Shittim-Server.csproj'))) repoRoot = path.dirname(dir);
  else if (fs.existsSync(path.join(dir, 'Shittim-Server'))) repoRoot = dir;
  else return { ok: false, error: 'No Shittim-Server project was found in that folder.' };
  saveSettings({ repoRoot });
  return { ok: true, repoRoot, status: projectStatus() };
}

// Download the latest commit of the repo as a zip and unpack it into targetDir.
// Used both for first-time setup (fresh, empty target) and for updates (merge
// over an existing copy — source files are overwritten, while build output, the
// database and Config/ live outside the archive and are left untouched).
async function downloadProject({ targetDir, branch } = {}) {
  branch = branch || GH.branch;
  targetDir = targetDir || defaultDownloadDir();
  const send = (phase, extra) => broadcast('project:progress', { phase, ...extra });
  let tmpRoot = null;
  try {
    send('resolve', { message: 'Resolving latest commit…' });
    const commit = await githubApi(`/commits/${encodeURIComponent(branch)}`);
    const sha = commit.sha;
    const subject = (commit.commit.message || '').split('\n')[0];
    const date = commit.commit.author && commit.commit.author.date;

    tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-proj-'));
    const zipPath = path.join(tmpRoot, 'project.zip');
    const url = `https://codeload.github.com/${GH.owner}/${GH.repo}/zip/${sha}`;
    send('download', { message: 'Downloading project…', recv: 0, total: 0 });
    await downloadFile(url, zipPath, (recv, total) => send('download', { message: 'Downloading project…', recv, total }));

    send('extract', { message: 'Extracting…' });
    const exDir = path.join(tmpRoot, 'x');
    await extractZip(zipPath, exDir);
    const top = fs.readdirSync(exDir)
      .map((n) => path.join(exDir, n))
      .find((q) => { try { return fs.statSync(q).isDirectory(); } catch { return false; } });
    if (!top) throw new Error('downloaded archive was empty');

    send('install', { message: 'Installing files…' });
    fs.mkdirSync(targetDir, { recursive: true });
    fs.cpSync(top, targetDir, { recursive: true, force: true });
    writeVersionMarker(targetDir, {
      sha, shortSha: sha.slice(0, 7), branch, subject,
      date: date || null, source: 'download', updatedAt: new Date().toISOString(),
    });

    saveSettings({ repoRoot: targetDir });
    send('done', { message: 'Done', repoRoot: targetDir, sha: sha.slice(0, 7) });
    return { ok: true, repoRoot: targetDir, sha: sha.slice(0, 7) };
  } catch (e) {
    send('error', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (tmpRoot) { try { fs.rmSync(tmpRoot, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

function isoToRel(iso) {
  if (!iso) return '';
  const then = new Date(iso).getTime();
  if (isNaN(then)) return '';
  const secs = Math.max(0, Math.floor((Date.now() - then) / 1000));
  const mins = Math.floor(secs / 60), hours = Math.floor(secs / 3600), days = Math.floor(secs / 86400);
  if (days > 30) { const mo = Math.floor(days / 30); return `${mo} month${mo === 1 ? '' : 's'} ago`; }
  if (days > 0) return `${days} day${days === 1 ? '' : 's'} ago`;
  if (hours > 0) return `${hours} hour${hours === 1 ? '' : 's'} ago`;
  if (mins > 0) return `${mins} minute${mins === 1 ? '' : 's'} ago`;
  return 'just now';
}

function execGit(args, cwd, timeout = 20000) {
  return new Promise((resolve) => {
    execFile('git', args, { cwd, timeout, windowsHide: true, maxBuffer: 16 * 1024 * 1024 }, (err, stdout, stderr) => {
      resolve({ ok: !err, out: (stdout || '').toString(), err: ((stderr || '').toString().trim()) || (err ? String(err.message) : '') });
    });
  });
}

async function checkUpdates() {
  const p = resolvePaths();
  const repoRoot = p.repoRoot;
  if (!fs.existsSync(p.csproj) && !p.exePath) {
    return { ok: false, error: 'Server project not found - download or locate it first.', noProject: true };
  }

  // Local identity: prefer the marker a download left; else a real git checkout.
  const marker = readVersionMarker(repoRoot);
  let branch = (marker && marker.branch) || GH.branch;
  let localSha = marker && marker.sha;
  let localSubject = marker && marker.subject;
  let localSource = marker ? 'download' : null;

  if (!localSha && fs.existsSync(path.join(repoRoot, '.git'))) {
    const head = await execGit(['rev-parse', 'HEAD'], repoRoot);
    if (head.ok && head.out.trim()) { localSha = head.out.trim(); localSource = 'git'; }
    const br = await execGit(['rev-parse', '--abbrev-ref', 'HEAD'], repoRoot);
    if (br.ok) { const b = br.out.trim(); if (b && b !== 'HEAD') branch = b; }
    const subj = await execGit(['log', '-1', '--pretty=%s'], repoRoot);
    if (subj.ok && subj.out.trim()) localSubject = subj.out.trim();
  }

  // Remote tip via the API (no fetch, no clone).
  let remote;
  try { remote = await githubApi(`/commits/${encodeURIComponent(branch)}`); }
  catch (e) { return { ok: false, error: `Could not reach GitHub: ${String(e.message || e)}` }; }
  const remoteSha = remote.sha;
  const base = {
    ok: true, branch, localSource, repoRoot,
    remoteSha, remoteShort: remoteSha.slice(0, 7),
    remoteSubject: (remote.commit.message || '').split('\n')[0],
    remoteWhen: isoToRel(remote.commit.author && remote.commit.author.date),
  };

  if (!localSha) return { ...base, versionKnown: false };

  const head = { head: localSha.slice(0, 7), headSubject: localSubject || '' };
  if (localSha === remoteSha) return { ...base, ...head, versionKnown: true, behind: 0, ahead: 0, commits: [] };

  // Diff local..remote through the compare API; its commit list is the changelog.
  try {
    const cmp = await githubApi(`/compare/${localSha}...${encodeURIComponent(branch)}`);
    const commits = (cmp.commits || []).map((c) => ({
      hash: c.sha.slice(0, 7),
      subject: (c.commit.message || '').split('\n')[0],
      author: (c.commit.author && c.commit.author.name) || (c.author && c.author.login) || '',
      when: isoToRel(c.commit.author && c.commit.author.date),
    })).reverse();
    return { ...base, ...head, versionKnown: true, behind: cmp.ahead_by || 0, ahead: cmp.behind_by || 0, commits, status: cmp.status };
  } catch (e) {
    // Local commit isn't an ancestor the API can diff (a local build, or a
    // diverged history). We still know the tip differs - offer a refresh.
    return { ...base, ...head, versionKnown: true, behind: null, compareFailed: true };
  }
}

async function applyUpdate() {
  const p = resolvePaths();
  const repoRoot = p.repoRoot;
  const marker = readVersionMarker(repoRoot);
  const isGit = fs.existsSync(path.join(repoRoot, '.git'));

  // A real git checkout (no download marker) keeps the safe ff-only pull.
  if (isGit && !marker) {
    const branch = ((await execGit(['rev-parse', '--abbrev-ref', 'HEAD'], repoRoot)).out || 'main').trim() || 'main';
    broadcast('proc:log', { source: 'server', line: `> git pull --ff-only origin ${branch}` });
    const r = await execGit(['pull', '--ff-only', 'origin', branch], repoRoot, 120000);
    const output = `${r.out}\n${r.err}`.trim();
    output.split('\n').filter(Boolean).forEach((line) => broadcast('proc:log', { source: 'server', line }));
    return { ok: r.ok, method: 'git', output, head: (await execGit(['rev-parse', '--short', 'HEAD'], repoRoot)).out.trim() };
  }

  // Otherwise re-download the latest commit and merge it over the folder. The
  // archive carries source only, so Config/, the database and build output stay.
  const branch = (marker && marker.branch) || GH.branch;
  broadcast('proc:log', { source: 'server', line: `> downloading ${GH.owner}/${GH.repo}@${branch} from GitHub...` });
  const res = await downloadProject({ targetDir: repoRoot, branch });
  if (res.ok) broadcast('proc:log', { source: 'server', line: `> project updated to ${res.sha}` });
  else broadcast('proc:log', { source: 'server', line: `> update failed: ${res.error}` });
  return { ok: res.ok, method: 'download', head: res.sha, error: res.error };
}

function rebuildServer() {
  const p = resolvePaths();
  if (!fs.existsSync(p.csproj)) return Promise.resolve({ ok: false, error: 'Server project not found' });
  return new Promise((resolve) => {
    broadcast('proc:log', { source: 'server', line: '> dotnet build -c Debug (rebuilding after update)…' });
    broadcast('proc:state', { rebuild: 'building' });
    const cmd = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    let child;
    try { child = spawn(cmd, ['build', '-c', 'Debug', p.csproj], { cwd: p.serverDir, windowsHide: true, env: { ...process.env } }); }
    catch (e) { resolve({ ok: false, error: String(e.message || e) }); return; }
    pipeLines(child, 'server');
    child.on('exit', (code) => { broadcast('proc:state', { rebuild: 'done' }); broadcast('proc:log', { source: 'server', line: `> build exited (code ${code})` }); resolve({ ok: code === 0, code }); });
    child.on('error', (err) => { broadcast('proc:state', { rebuild: 'failed' }); resolve({ ok: false, error: err.message }); });
  });
}

// ----------------------------------------------------------- toolchain setup
//
// One-click acquisition of the three host prerequisites the readiness card
// reports on: the .NET 10 SDK, mitmproxy, and a trusted mitmproxy CA cert.
// Everything installs per-user without admin EXCEPT trusting the CA into the
// machine root store — which the Steam client validates against — and which
// prompts once for elevation. Each installer is idempotent and streams progress
// to the renderer over the 'setup:progress' channel. Windows only; on other
// platforms each returns a message pointing at the manual install.

const TOOLS_DIR = () => path.join(app.getPath('userData'), 'tools');
const DOTNET_DIR = () => path.join(process.env.LOCALAPPDATA || os.homedir(), 'Microsoft', 'dotnet');
const MITM_DIR = () => path.join(TOOLS_DIR(), 'mitmproxy');
const CERT_PATH = () => path.join(os.homedir(), '.mitmproxy', 'mitmproxy-ca-cert.cer');

function setupLog(step, line) { broadcast('setup:progress', { step, line }); }
function setupPhase(step, status, extra) { broadcast('setup:progress', { step, status, ...(extra || {}) }); }

// Run a PowerShell snippet, streaming each output line to onLine. Resolves with
// the exit code — never rejects, so callers branch on `ok`.
function runPwsh(script, onLine) {
  return new Promise((resolve) => {
    let child;
    try {
      child = spawn('powershell.exe',
        ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', script],
        { windowsHide: true, env: { ...process.env } });
    } catch (e) { resolve({ ok: false, code: -1, out: String(e.message || e) }); return; }
    let out = '';
    const onData = (c) => {
      const s = c.toString(); out += s;
      if (onLine) s.split(/\r?\n/).forEach((l) => { if (l.trim()) onLine(l.replace(/\s+$/, '')); });
    };
    if (child.stdout) child.stdout.on('data', onData);
    if (child.stderr) child.stderr.on('data', onData);
    child.on('error', (e) => resolve({ ok: false, code: -1, out: `${out}\n${e.message}` }));
    child.on('exit', (code) => resolve({ ok: code === 0, code, out }));
  });
}

// Append a directory to the persistent per-user PATH (and to this process's live
// PATH so spawns in the current session resolve the tool without a restart).
async function addToUserPath(dir, step) {
  const cur = process.env.PATH || process.env.Path || '';
  if (!cur.split(path.delimiter).some((s) => s.toLowerCase() === dir.toLowerCase())) {
    process.env.PATH = cur ? `${cur}${path.delimiter}${dir}` : dir;
  }
  if (process.platform !== 'win32') return { ok: true };
  const ps = [
    `$dir = ${psQuote(dir)}`,
    `$cur = [Environment]::GetEnvironmentVariable('Path','User')`,
    `$parts = @(); if ($cur) { $parts = $cur -split ';' | Where-Object { $_ -ne '' } }`,
    `if ($parts -notcontains $dir) {`,
    `  $new = (@($parts) + $dir) -join ';'`,
    `  [Environment]::SetEnvironmentVariable('Path', $new, 'User')`,
    `  Write-Output "added to user PATH: $dir"`,
    `} else { Write-Output "already on user PATH: $dir" }`,
  ].join('\n');
  const r = await runPwsh(ps, (l) => setupLog(step, l));
  return { ok: r.ok };
}

// .NET 10 SDK via Microsoft's official dotnet-install.ps1 (per-user, no admin).
async function installDotnet() {
  const step = 'dotnet';
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Automated .NET install is wired up for Windows only — install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0.' };
  }
  setupPhase(step, 'running', { message: 'Installing .NET 10 SDK…' });
  let tmp = null;
  try {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-dotnet-'));
    const scriptPath = path.join(tmp, 'dotnet-install.ps1');
    setupLog(step, '> downloading dotnet-install.ps1');
    await downloadFile('https://dot.net/v1/dotnet-install.ps1', scriptPath);
    const dir = DOTNET_DIR();
    setupLog(step, `> dotnet-install.ps1 -Channel 10.0 -InstallDir "${dir}"`);
    // -NoPath: the script's session-only PATH edit is useless to us; we persist
    // it ourselves below. The install is a no-op if the SDK is already present.
    const ps = `& ${psQuote(scriptPath)} -Channel 10.0 -InstallDir ${psQuote(dir)} -Architecture x64 -NoPath`;
    const r = await runPwsh(ps, (l) => setupLog(step, l));
    if (!r.ok) { setupPhase(step, 'failed', { message: 'dotnet-install.ps1 failed' }); return { ok: false, error: 'dotnet-install.ps1 failed', out: r.out }; }
    await addToUserPath(dir, step);
    setupPhase(step, 'done', { message: '.NET 10 SDK installed' });
    return { ok: true, dir };
  } catch (e) {
    setupPhase(step, 'failed', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (tmp) { try { fs.rmSync(tmp, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

// Find the directory holding mitmweb.exe inside an extracted archive (flat or
// one level down).
function findMitmBinDir(root) {
  const hit = (d) => fs.existsSync(path.join(d, 'mitmweb.exe'));
  if (hit(root)) return root;
  try {
    for (const n of fs.readdirSync(root)) {
      const sub = path.join(root, n);
      try { if (fs.statSync(sub).isDirectory() && hit(sub)) return sub; } catch { /* ignore */ }
    }
  } catch { /* ignore */ }
  return null;
}

// mitmproxy: download the latest standalone Windows zip from GitHub releases,
// unpack it under userData/tools and put it on PATH.
async function installMitmproxy() {
  const step = 'mitmproxy';
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Automated mitmproxy install is wired up for Windows only — install it from https://mitmproxy.org/.' };
  }
  setupPhase(step, 'running', { message: 'Installing mitmproxy…' });
  let tmp = null;
  try {
    setupLog(step, '> resolving latest mitmproxy release');
    const rel = await httpGet('https://api.github.com/repos/mitmproxy/mitmproxy/releases/latest', {
      headers: { Accept: 'application/vnd.github+json' },
    });
    if (rel.statusCode < 200 || rel.statusCode >= 300) throw new Error(`GitHub API responded ${rel.statusCode}`);
    const data = JSON.parse(rel.body.toString('utf8'));
    const assets = data.assets || [];
    const asset = assets.find((a) => /windows/i.test(a.name) && /\.zip$/i.test(a.name) && /(x86_64|x64|amd64)/i.test(a.name))
      || assets.find((a) => /windows/i.test(a.name) && /\.zip$/i.test(a.name));
    if (!asset) throw new Error('no Windows .zip asset in the latest mitmproxy release');

    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
    const zipPath = path.join(tmp, asset.name);
    setupLog(step, `> downloading ${asset.name}`);
    await downloadFile(asset.browser_download_url, zipPath, (recv, total) => broadcast('setup:progress', { step, recv, total }));

    const dir = MITM_DIR();
    try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* ignore */ }
    setupLog(step, '> extracting…');
    await extractZip(zipPath, dir);

    const binDir = findMitmBinDir(dir);
    if (!binDir) throw new Error('mitmweb.exe not found after extraction');
    await addToUserPath(binDir, step);
    setupPhase(step, 'done', { message: 'mitmproxy installed' });
    return { ok: true, dir: binDir };
  } catch (e) {
    setupPhase(step, 'failed', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (tmp) { try { fs.rmSync(tmp, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

// Run mitmdump just long enough for it to write ~/.mitmproxy/*.cer on first
// start, then stop it. mitmproxy generates its CA lazily on proxy startup, so a
// brief launch on a throwaway port is the supported way to materialise the cert.
function generateMitmCert(step) {
  return new Promise((resolve, reject) => {
    const certPath = CERT_PATH();
    const exe = firstExisting([path.join(MITM_DIR(), 'mitmdump.exe')])
      || (process.platform === 'win32' ? 'mitmdump.exe' : 'mitmdump');
    let child;
    try { child = spawn(exe, ['--listen-port', '48080', '-q'], { windowsHide: true, env: { ...process.env } }); }
    catch (e) { reject(e); return; }
    let done = false;
    const finish = (err) => {
      if (done) return; done = true;
      clearInterval(poll); clearTimeout(timer);
      killTree(child);
      err ? reject(err) : resolve();
    };
    child.on('error', (e) => finish(e));
    const poll = setInterval(() => { if (fs.existsSync(certPath)) finish(null); }, 400);
    const timer = setTimeout(() => finish(fs.existsSync(certPath) ? null : new Error('timed out waiting for CA generation')), 25000);
  });
}

// Trust the mitmproxy CA. Generates it first if it has never been created, then
// adds it to the machine root store via certutil (one elevation prompt).
async function installCertificate() {
  const step = 'certificate';
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Automated certificate trust is wired up for Windows only.' };
  }
  setupPhase(step, 'running', { message: 'Preparing CA certificate…' });
  try {
    const certPath = CERT_PATH();
    if (!fs.existsSync(certPath)) {
      setupLog(step, '> generating mitmproxy CA (first run)…');
      await generateMitmCert(step);
    }
    if (!fs.existsSync(certPath)) throw new Error('mitmproxy CA certificate was not generated — install mitmproxy first.');
    setupLog(step, '> trusting CA in machine root store (certutil — approve the elevation prompt)…');
    // Machine root store is what the Steam client validates against, so it needs
    // admin. Start-Process -Verb RunAs raises the single UAC prompt.
    const ps = `$p = Start-Process -FilePath 'certutil.exe' -ArgumentList @('-addstore','-f','Root', ${psQuote(certPath)}) -Verb RunAs -PassThru -Wait; exit $p.ExitCode`;
    const r = await runPwsh(ps, (l) => setupLog(step, l));
    if (!r.ok) { setupPhase(step, 'failed', { message: 'certutil failed or elevation was declined' }); return { ok: false, error: 'certutil failed or elevation was declined', out: r.out }; }
    setupPhase(step, 'done', { message: 'CA certificate trusted', certPath });
    return { ok: true, certPath };
  } catch (e) {
    setupPhase(step, 'failed', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  }
}

// Orchestrate one step, or all three in dependency order (the cert step needs
// mitmproxy's mitmdump, so mitmproxy is installed before it).
async function runSetup(which) {
  const order = which === 'all' ? ['dotnet', 'mitmproxy', 'certificate'] : [which];
  const results = {};
  for (const step of order) {
    if (step === 'dotnet') results.dotnet = await installDotnet();
    else if (step === 'mitmproxy') results.mitmproxy = await installMitmproxy();
    else if (step === 'certificate') results.certificate = await installCertificate();
    else return { ok: false, error: `unknown setup step: ${step}` };
  }
  const ok = Object.values(results).every((r) => r && r.ok);
  broadcast('setup:progress', { step: which, status: ok ? 'all-done' : 'all-failed' });
  return { ok, results };
}

// ------------------------------------------------------------------- window

function appIcon() {
  const candidates = [
    path.join(APP_DIR, 'build', 'icon.png'),
    path.join(APP_DIR, 'build', 'icon.ico'),
  ];
  return firstExisting(candidates) || undefined;
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 940,
    minHeight: 600,
    show: false,
    frame: false,
    backgroundColor: '#0e121c',
    title: 'Shittim Control Center',
    icon: appIcon(),
    webPreferences: {
      preload: path.join(APP_DIR, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  win.loadFile(path.join(APP_DIR, 'src', 'index.html'));
  win.once('ready-to-show', () => win.show());

  win.on('maximize', () => broadcast('window:state', { maximized: true }));
  win.on('unmaximize', () => broadcast('window:state', { maximized: false }));
  return win;
}

// --------------------------------------------------------------------- ipc

ipcMain.handle('paths:resolve', () => resolvePaths());
ipcMain.handle('settings:read', () => loadSettings());
ipcMain.handle('settings:write', (_e, patch) => saveSettings(patch || {}));

ipcMain.handle('config:read', () => readConfig());
ipcMain.handle('config:write', (_e, payload) => writeConfig(payload));

ipcMain.handle('server:start', () => startServer());
ipcMain.handle('server:stop', () => stopServer());
ipcMain.handle('mitm:start', () => startMitm());
ipcMain.handle('mitm:stop', () => stopMitm());

// combined power control — one action drives both the server and the proxy
ipcMain.handle('system:start', () => {
  const server = startServer();
  const mitm = startMitm();
  return { ok: server.ok || mitm.ok, server, mitm };
});
ipcMain.handle('system:stop', () => {
  const server = procs.server ? stopServer() : { ok: true, error: 'Server not running' };
  const mitm = procs.mitm ? stopMitm() : { ok: true, error: 'mitmproxy not running' };
  return { ok: true, server, mitm };
});

// project location + first-run acquisition (download from GitHub or locate)
ipcMain.handle('project:status', () => projectStatus());
ipcMain.handle('project:download', (_e, opts) => downloadProject(opts || {}));
ipcMain.handle('project:setPath', (_e, dir) => setProjectPath(dir));

// git-free self-update (compares against GitHub via the REST API)
ipcMain.handle('updates:check', () => checkUpdates());
ipcMain.handle('updates:apply', () => applyUpdate());
ipcMain.handle('updates:rebuild', () => rebuildServer());
ipcMain.handle('proc:status', () => ({
  server: procs.server && !procs.server.killed ? 'running' : 'stopped',
  mitm: procs.mitm && !procs.mitm.killed ? 'running' : 'stopped',
  serverPid: procs.server && !procs.server.killed ? procs.server.pid : null,
  mitmPid: procs.mitm && !procs.mitm.killed ? procs.mitm.pid : null,
}));

ipcMain.handle('env:check', () => runEnvChecks());

// one-click toolchain setup: 'dotnet' | 'mitmproxy' | 'certificate' | 'all'
ipcMain.handle('setup:install', (_e, which) => runSetup(which || 'all'));

ipcMain.handle('dialog:pickFolder', async () => {
  const r = await dialog.showOpenDialog({ properties: ['openDirectory'] });
  return r.canceled ? null : r.filePaths[0];
});
ipcMain.handle('dialog:pickFile', async (_e, filters) => {
  const r = await dialog.showOpenDialog({ properties: ['openFile'], filters: filters || [] });
  return r.canceled ? null : r.filePaths[0];
});
ipcMain.handle('shell:openPath', (_e, p) => shell.openPath(p));
ipcMain.handle('shell:openExternal', (_e, url) => shell.openExternal(url));

ipcMain.on('window:control', (e, action) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (!win) return;
  if (action === 'minimize') win.minimize();
  else if (action === 'maximize') win.isMaximized() ? win.unmaximize() : win.maximize();
  else if (action === 'close') win.close();
});

// ------------------------------------------------------------------- bootstrap

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  killTree(procs.server);
  killTree(procs.mitm);
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
  killTree(procs.server);
  killTree(procs.mitm);
});
