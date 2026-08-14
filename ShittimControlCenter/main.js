'use strict';

const { app, BrowserWindow, ipcMain, dialog, shell } = require('electron');
const path = require('path');
const fs = require('fs');
const os = require('os');
const https = require('https');
const { spawn, execFile } = require('child_process');
const { extractZip, installTree, resumeInterruptedInstall, backupUserData, writeFileAtomic, selfUpdateMode, blockerAdvice } = require('./updater');
const { resolveRepoRoot, pathsFor, firstExisting, componentVersions, findMitmBinDir, mitmExe, dotnetExe } = require('./paths');
const { killTree: killPidTree, streamLines } = require('./procs');
const { thumbprint, trustedRoot } = require('./certs');

// The control center lives at <repoRoot>/ShittimControlCenter. Everything it drives (the server project, the database, the mitm scripts) is resolved relative to <repoRoot> so the app is portable as long as the layout holds.

const APP_DIR = __dirname;
const SETTINGS_PATH = () => path.join(app.getPath('userData'), 'control-center.json');

function loadSettings() {
  try {
    return JSON.parse(fs.readFileSync(SETTINGS_PATH(), 'utf8'));
  } catch {
    return {};
  }
}
// Throws when the settings cannot be persisted. A silent failure here is how a located project folder is forgotten by the next launch after the app has already said it was set.
function saveSettings(patch) {
  const next = { ...loadSettings(), ...patch };
  fs.mkdirSync(app.getPath('userData'), { recursive: true });
  writeFileAtomic(SETTINGS_PATH(), JSON.stringify(next, null, 2));
  return next;
}

function resolvePaths() {
  return pathsFor(resolveRepoRoot(loadSettings(), APP_DIR).repoRoot);
}

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
    writeFileAtomic(configPath, text);
    return { ok: true, path: configPath };
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  }
}

const procs = { server: null, mitm: null };
// when each child was spawned, and how long the server gets to bind its port before silence counts as a symptom. A source run compiles first, so it gets no deadline at all - there is no telling a long build from a hang.
const started = { server: null, mitm: null, serverGraceMs: null };
const LISTEN_GRACE_MS = 90_000;
// a child that never spawned leaves nothing to ask about, so the failure has to outlive it or the next poll reports a tidy "stopped"
const spawnFailed = { server: false, mitm: false };

function broadcast(channel, payload) {
  for (const w of BrowserWindow.getAllWindows()) {
    if (!w.isDestroyed()) w.webContents.send(channel, payload);
  }
}

function pipeLines(child, source) {
  streamLines(child, (line) => broadcast('proc:log', { source, line }));
}

function killTree(child) {
  if (!child || child.killed) return Promise.resolve({ ok: true, already: true });
  return killPidTree(child.pid);
}

// Every child that is not the server or the proxy: builds, installers, the cert probe. Quit has to take these down too, or a rebuild's msbuild nodes outlive the window that started them.
const helpers = new Set();
function track(child) {
  helpers.add(child);
  const drop = () => helpers.delete(child);
  child.on('exit', drop);
  child.on('error', drop);
  return child;
}

// Resolve the dotnet host to launch. Prefer the per-user SDK our setup installs and launch it by ABSOLUTE PATH so machine-PATH ordering can't shadow it with a runtime-only host - the classic "SDK 'Microsoft.NET.Sdk.Web' could not be found" build failure on an otherwise-working machine.
// Falls back to whatever 'dotnet' is on PATH when we never installed our own.
function resolveDotnet() {
  return dotnetExe(DOTNET_DIR());
}

// Child env that forces the chosen dotnet: DOTNET_ROOT points at the install and its dir is prepended to PATH so nested 'dotnet' calls (run, build, exec) all resolve to the same host instead of a shadowing one earlier on PATH.
function dotnetEnv(root) {
  const env = { ...process.env };
  if (root) {
    env.DOTNET_ROOT = root;
    env['DOTNET_ROOT(x64)'] = root;
    const cur = env.PATH || env.Path || '';
    env.PATH = cur ? `${root}${path.delimiter}${cur}` : root;
  }
  env.DOTNET_CLI_TELEMETRY_OPTOUT = '1';
  return env;
}

// Does the SDK tree under `dir` contain the ASP.NET Core Web SDK the project's `<Project Sdk="Microsoft.NET.Sdk.Web">` header requires?
function hasWebSdk(dir) {
  const sdkRoot = path.join(dir, 'sdk');
  try {
    return fs.readdirSync(sdkRoot).some((v) =>
      fs.existsSync(path.join(sdkRoot, v, 'Sdks', 'Microsoft.NET.Sdk.Web', 'Sdk', 'Sdk.props')));
  } catch { return false; }
}

function startServer(offline) {
  if (procs.server && !procs.server.killed) return { ok: false, error: 'Server already running' };
  const p = resolvePaths();
  const dn = resolveDotnet();
  const env = dotnetEnv(dn.root);
  if (offline) env.SHITTIM_AUTO_PATCH_STEAM_OFFLINE = 'true';

  // An update replaces the sources, the patch definitions and the schema but never bin/, so the already-built server is still sitting there and is the thing this would launch. It runs against the new everything else and the update looks like it did nothing.
  const stale = componentVersions(p.repoRoot, null, readVersionMarker(p.repoRoot)).buildStale;
  if (p.exePath && stale) {
    broadcast('proc:log', { source: 'server', line: `> ${stale}` });
    return { ok: false, error: stale, staleBuild: true };
  }

  let cmd, args, cwd;
  if (p.exePath) {
    cmd = p.exePath;
    args = [];
    cwd = p.serverDir;
  } else if (fs.existsSync(p.csproj)) {
    cmd = dn.cmd;
    args = ['run', '--project', p.csproj];
    cwd = p.serverDir;
  } else {
    return { ok: false, error: 'No server executable or project found. Build the server first.' };
  }

  broadcast('proc:log', { source: 'server', line: `> launching ${path.basename(cmd)} (cwd: ${cwd})` });
  const child = spawn(cmd, args, { cwd, windowsHide: true, env });
  procs.server = child;
  started.server = Date.now();
  started.serverGraceMs = p.exePath ? LISTEN_GRACE_MS : null;
  spawnFailed.server = false;
  broadcast('proc:state', { server: 'starting', serverPid: child.pid });
  pipeLines(child, 'server');

  child.on('exit', (code) => {
    broadcast('proc:log', { source: 'server', line: `> server exited (code ${code})` });
    procs.server = null;
    started.server = null;
    broadcast('proc:state', { server: 'stopped' });
  });
  child.on('error', (err) => {
    broadcast('proc:log', { source: 'server', line: `> spawn error: ${err.message}` });
    procs.server = null;
    started.server = null;
    spawnFailed.server = true;
    broadcast('proc:state', { server: 'failed' });
  });

  return { ok: true, pid: child.pid };
}

async function stopServer() {
  if (!procs.server) return { ok: false, error: 'Server not running' };
  const child = procs.server;
  const r = await killTree(child);
  if (!r.ok) {
    broadcast('proc:log', { source: 'server', line: `> could not stop pid ${child.pid}: ${r.error}` });
    return { ok: false, error: `pid ${child.pid} is still running - ${r.error}` };
  }
  procs.server = null;
  broadcast('proc:state', { server: 'stopped' });
  return { ok: true };
}

function startMitm(offline) {
  if (procs.mitm && !procs.mitm.killed) return { ok: false, error: 'mitmproxy already running' };
  const p = resolvePaths();
  if (!fs.existsSync(p.redirectScript)) return { ok: false, error: 'redirect_server.py not found' };

  // local mode never sees these connections - the hosts file sends them to loopback and WinDivert does not divert loopback - so offline mitmproxy owns the ports itself. reverse:http:// still terminates the client's TLS, since next_layer inserts a client TLS layer when the first bytes are a ClientHello, where reverse:https:// would force TLS upstream onto a plaintext Kestrel; keep_host_header leaves the Host the addon routes on alone.
  const args = offline
    ? ['--no-http2', '-s', 'redirect_server.py', '--set', 'termlog_verbosity=warn', '--set', 'keep_host_header=true',
      '--mode', 'reverse:http://127.0.0.3:5000@127.0.0.2:443',
      '--mode', 'reverse:http://127.0.0.3:5000@127.0.0.2:5000',
      '--mode', 'reverse:http://127.0.0.3:5100@127.0.0.2:5100']
    : ['-m', 'wireguard', '--no-http2', '-s', 'redirect_server.py',
      '--set', 'termlog_verbosity=warn', '--mode', 'local:BlueArchive.exe'];
  const cmd = mitmTool('mitmweb');

  // the addon reads this at import time and answers anything it has no local route for with a 502 instead of forwarding it, so a missed host shows up as a logged failure rather than a connection that hangs until it times out.
  const env = { ...process.env };
  if (offline) env.SHITTIM_OFFLINE_MODE = '1';
  // otherwise Python picks the ANSI codepage for open() and for its own stdio, and on a Chinese or Japanese install that is cp936/cp932 - the addon then dies reading a UTF-8 file with "'gbk' codec can't decode byte ... illegal multibyte sequence"
  env.PYTHONUTF8 = '1';
  env.PYTHONIOENCODING = 'utf-8';

  broadcast('proc:log', { source: 'mitm', line: `> launching mitmweb (cwd: ${p.scriptsDir})${offline ? ' in offline mode' : ''}` });
  let child;
  try {
    child = spawn(cmd, args, { cwd: p.scriptsDir, windowsHide: true, env });
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  }
  procs.mitm = child;
  started.mitm = Date.now();
  spawnFailed.mitm = false;
  broadcast('proc:state', { mitm: 'starting' });
  pipeLines(child, 'mitm');

  child.on('exit', (code) => {
    broadcast('proc:log', { source: 'mitm', line: `> mitmproxy exited (code ${code})` });
    procs.mitm = null;
    started.mitm = null;
    broadcast('proc:state', { mitm: 'stopped' });
  });
  child.on('error', (err) => {
    broadcast('proc:log', { source: 'mitm', line: `> spawn error: ${err.message} (is mitmproxy installed?)` });
    procs.mitm = null;
    started.mitm = null;
    spawnFailed.mitm = true;
    broadcast('proc:state', { mitm: 'failed' });
  });

  return { ok: true, pid: child.pid };
}

async function stopMitm() {
  if (!procs.mitm) return { ok: false, error: 'mitmproxy not running' };
  const child = procs.mitm;
  const r = await killTree(child);
  if (!r.ok) {
    broadcast('proc:log', { source: 'mitm', line: `> could not stop pid ${child.pid}: ${r.error}` });
    return { ok: false, error: `pid ${child.pid} is still running - ${r.error}` };
  }
  procs.mitm = null;
  broadcast('proc:state', { mitm: 'stopped' });
  return { ok: true };
}

const HOSTS_PATH = () => path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'drivers', 'etc', 'hosts');
const HOSTS_MARK = '# shittim offline';

// every host the client reaches for across a full boot, read off the redirect log. mitmproxy's local mode can only divert a connection the client actually manages to open, and with no route out the name lookup fails before that - so these have to resolve on their own. 127.0.0.2 and not .1 because that is where the reverse listeners sit, and mitmproxy refuses to forward to a port it is itself listening on at 127.0.0.1.
const OFFLINE_HOSTS = [
  'public.api.nexon.com',
  'nxm-th-bagl.nexon.com',
  'nxm-eu-bagl.nexon.com',
  'config.na.nexon.com',
  'platform.gamescale.nexon.com',
  'toy.log.nexon.io',
  'psm-log.ngs.nexon.com',
  'd2vaidpni345rp.cloudfront.net',
  'bolo7yechd.execute-api.ap-northeast-1.amazonaws.com',
];

function offlineHostsApplied() {
  try { return fs.readFileSync(HOSTS_PATH(), 'utf8').includes(`${HOSTS_MARK} begin`); } catch { return false; }
}

function hostsWithBlock(text, on) {
  const kept = [];
  let inBlock = false;
  for (const line of text.split(/\r?\n/)) {
    const t = line.trim();
    if (t === `${HOSTS_MARK} begin`) { inBlock = true; continue; }
    if (t === `${HOSTS_MARK} end`) { inBlock = false; continue; }
    if (!inBlock) kept.push(line);
  }
  while (kept.length && !kept[kept.length - 1].trim()) kept.pop();
  if (!on) return `${kept.join('\r\n')}\r\n`;
  return `${[...kept, '', `${HOSTS_MARK} begin`, ...OFFLINE_HOSTS.map((h) => `127.0.0.2 ${h}`), `${HOSTS_MARK} end`].join('\r\n')}\r\n`;
}

// hosts sits under System32, so staging the finished file next to it and copying it across keeps the elevated half down to one copy and a resolver cache flush - the cache matters because a name the client already looked up stays pointed at the real address until it is dropped.
async function setOfflineHosts(on) {
  if (process.platform !== 'win32') return { ok: false, error: 'Offline hosts entries are wired up for Windows only.' };
  const hosts = HOSTS_PATH();
  let cur;
  try {
    cur = fs.readFileSync(hosts, 'utf8');
  } catch (e) {
    return { ok: false, error: `could not read ${hosts}: ${e.message}` };
  }

  const next = hostsWithBlock(cur, on);
  if (next === cur) return { ok: true, changed: false, applied: on };

  const staged = path.join(os.tmpdir(), `scc-hosts-${process.pid}.txt`);
  fs.writeFileSync(staged, next, 'utf8');
  const inner = `Copy-Item -LiteralPath ${psQuote(staged)} -Destination ${psQuote(hosts)} -Force; ipconfig /flushdns | Out-Null`;
  const ps = `$p = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-Command', ${psQuote(inner)}) -Verb RunAs -PassThru -Wait -WindowStyle Hidden; exit $p.ExitCode`;
  const r = await runPwsh(ps, (l) => broadcast('proc:log', { source: 'mitm', line: l }));
  try { fs.unlinkSync(staged); } catch { /* ignore */ }

  if (!r.ok) return { ok: false, error: 'the hosts file was not written - elevation was declined or the copy failed' };
  broadcast('proc:log', { source: 'mitm', line: on ? `> pointed ${OFFLINE_HOSTS.length} hosts at 127.0.0.2` : '> removed the offline hosts entries' });
  return { ok: true, changed: true, applied: on };
}

// execFile and not exec: the argument is a path we resolved, and putting it through cmd.exe means cmd gets an opinion about it. Double quotes stop it splitting on spaces but not expanding %NAME%, so an install under a directory with a percent sign in it reports the SDK as missing when it is sitting right there.
function execCheck(cmd, args, timeout = 6000) {
  return new Promise((resolve) => {
    execFile(cmd, args, { timeout, windowsHide: true }, (err, stdout, stderr) => {
      const out = (stdout || stderr || '').toString().trim();
      resolve({ ok: !err, detail: out.split('\n')[0] || (err ? String(err.message) : '') });
    });
  });
}

function execOut(cmd, args, timeout = 8000) {
  return new Promise((resolve) => {
    execFile(cmd, args, { timeout, windowsHide: true }, (err, stdout) => resolve({ ok: !err, out: (stdout || '').toString() }));
  });
}

// For a PATH-resolved dotnet (we didn't install our own), parse `--list-sdks` and check each listed SDK dir for the Web SDK.
async function pathDotnetHasWeb(dn) {
  const r = await execOut(dn.cmd, ['--list-sdks']);
  if (!r.ok) return false;
  for (const line of r.out.split(/\r?\n/)) {
    const m = line.match(/^(\S+)\s+\[(.+)\]\s*$/); // "10.0.301 [C:\...\sdk]"
    if (m && fs.existsSync(path.join(m[2], m[1], 'Sdks', 'Microsoft.NET.Sdk.Web', 'Sdk', 'Sdk.props'))) return true;
  }
  return false;
}

// Report on the exact dotnet host the server will launch, including whether its SDK carries the ASP.NET Core Web SDK - a base SDK alone yields the "Sdk.Web could not be found" build failure even though `dotnet --version` works.
async function checkDotnet() {
  const dn = resolveDotnet();
  const ver = await execCheck(dn.cmd, ['--version']);
  if (!ver.ok) return { status: 'missing', detail: '.NET SDK not found - click Install' };
  const where = dn.root ? dn.cmd : `${dn.cmd} on PATH`;
  const webOk = dn.root ? hasWebSdk(dn.root) : await pathDotnetHasWeb(dn);
  if (!webOk) return { status: 'warning', detail: `SDK ${ver.detail} (${where}) - ASP.NET Core Web SDK missing; click Install` };
  return { status: 'ready', detail: `SDK ${ver.detail} (${where})` };
}

// The CA the client has to accept, and whether the machine actually accepts it. A file check passes in two states that break every handshake: the certutil step was declined, or mitmproxy regenerated its CA after that step ran and the store still holds the old one.
async function checkCertificate() {
  const certPath = CERT_PATH();
  if (!fs.existsSync(certPath)) return { status: 'warning', detail: 'CA certificate not generated yet' };
  if (process.platform !== 'win32') return { status: 'warning', detail: `${certPath} - trust it in the system store yourself on this platform` };
  const thumb = thumbprint(certPath);
  if (await trustedRoot(thumb)) return { status: 'ready', detail: `${certPath} (${thumb.slice(0, 8)}, trusted)` };
  return { status: 'warning', detail: `${certPath} exists but ${thumb.slice(0, 8)} is not in the root store - click Trust cert` };
}

async function runEnvChecks() {
  const p = resolvePaths();

  const mitmPath = mitmTool('mitmweb');
  const [dotnet, mitm, certificate] = await Promise.all([
    checkDotnet(),
    execCheck(mitmPath, ['--version']),
    checkCertificate(),
  ]);

  let ccVersion = null;
  try { ccVersion = app.getVersion(); } catch { /* not packaged */ }
  const vs = componentVersions(p.repoRoot, ccVersion, readVersionMarker(p.repoRoot));
  const serverDetail = vs.buildStale ? vs.buildStale
    : p.exePath ? (vs.releaseSkew ? `${p.exePath} - ${vs.releaseSkew}` : p.exePath)
    : fs.existsSync(p.csproj) ? 'source only - will build on launch'
    : 'server project not found';

  // The gateway RSA pair is not generated - it is shipped and copied next to the exe by the csproj. A clean rebuild wipes bin/Config, and without the private key the 50001 handshake cannot be decrypted, which the client shows as a hang on "Unpacking game resources" rather than as anything to do with keys.
  const keyDir = path.dirname(p.configPath);
  const gatewayKey = path.join(keyDir, 'GatewayPrivateKey.pem');
  const shippedKey = path.join(p.serverDir, 'config', 'GatewayPrivateKey.pem');
  const gateway = fs.existsSync(gatewayKey) ? { status: 'ready', detail: gatewayKey }
    : fs.existsSync(shippedKey) ? { status: 'ready', detail: `${shippedKey} - copied next to the exe on build` }
    : { status: 'missing', detail: `GatewayPrivateKey.pem is in neither ${keyDir} nor ${path.dirname(shippedKey)} - login cannot be decrypted` };

  return {
    dotnet,
    mitmproxy: { status: mitm.ok ? 'ready' : 'missing', detail: mitm.ok ? `${mitm.detail} - ${mitmPath}` : `${mitmPath} did not answer --version - click Install` },
    certificate,
    gateway,
    database: { status: fs.existsSync(p.dbPath) ? 'ready' : 'warning', detail: fs.existsSync(p.dbPath) ? p.dbPath : 'created on first server run' },
    server: { status: vs.buildStale ? 'warning' : p.exePath ? 'ready' : (fs.existsSync(p.csproj) ? 'warning' : 'missing'), detail: serverDetail },
    redirect: { status: fs.existsSync(p.redirectScript) ? 'ready' : 'missing', detail: fs.existsSync(p.redirectScript) ? p.redirectScript : 'redirect_server.py missing' },
  };
}

// The repo is a git checkout of origin/main. "Check" fetches and reports how far behind we are plus the incoming changelog; "Apply" does a fast-forward-only pull (which NEVER overwrites uncommitted local edits - it refuses if it would), and "Rebuild" recompiles the .NET server that the pull may have changed.

const US = '';

const GH = { owner: 'Neoexm', repo: 'Shittim-Server', branch: 'main' };
const GH_UA = 'ShittimControlCenter';
const VERSION_FILE = 'shittim-version.json';

// Minimal HTTPS GET that follows redirects and buffers the whole body. GitHub's API and codeload both 30x-redirect, so redirect handling is mandatory.
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
    throw new Error('GitHub API rate limit reached - try again in a little while.');
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
      // A connection cut mid-body still ends the stream cleanly, so the short read has to be caught here or a truncated archive gets treated as a download.
      out.on('finish', () => out.close(() => {
        if (total > 0 && received !== total) reject(new Error(`download was cut short (${received} of ${total} bytes)`));
        else resolve({ total, received });
      }));
      res.pipe(out);
    });
    req.on('error', reject);
    req.setTimeout(120000, () => req.destroy(new Error('download timed out')));
  });
}

function psQuote(s) { return `'${String(s).replace(/'/g, "''")}'`; }

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

// Backups live outside the install so that whatever an update, a migration or a repair does to the folder, the copy of the database is somewhere else.
function userBackupRoot() {
  return path.join(app.getPath('userData'), 'backups');
}

// Is a usable server project present at the currently-resolved location?
function projectStatus() {
  const where = resolveRepoRoot(loadSettings(), APP_DIR);
  const p = pathsFor(where.repoRoot);
  const hasCsproj = fs.existsSync(p.csproj);
  const hasExe = !!p.exePath;
  const marker = readVersionMarker(p.repoRoot);
  let source = marker ? marker.source : null;
  if (!source && fs.existsSync(path.join(p.repoRoot, '.git'))) source = 'git';
  return {
    found: hasCsproj || hasExe,
    repoRoot: p.repoRoot, serverDir: p.serverDir, csproj: p.csproj,
    hasCsproj, hasExe, source, version: marker, defaultDir: defaultDownloadDir(),
    configured: where.configured, configuredMissing: where.missing,
  };
}

// Point the control center at an existing folder. Accepts either the repo root (containing a Shittim-Server/ folder) or the Shittim-Server project folder itself, and normalises to the repo root that resolvePaths() expects.
function setProjectPath(dir) {
  if (!dir) return { ok: false, error: 'No folder selected.' };
  if (!fs.existsSync(dir)) return { ok: false, error: 'That folder does not exist.' };
  let repoRoot = null;
  if (fs.existsSync(path.join(dir, 'Shittim-Server', 'Shittim-Server.csproj'))) repoRoot = dir;
  else if (fs.existsSync(path.join(dir, 'Shittim-Server.csproj'))) repoRoot = path.dirname(dir);
  else if (fs.existsSync(path.join(dir, 'Shittim-Server'))) repoRoot = dir;
  else return { ok: false, error: 'No Shittim-Server project was found in that folder.' };
  try { saveSettings({ repoRoot }); }
  catch (e) { return { ok: false, error: `Found the project, but could not remember the folder: ${e.message}` }; }
  return { ok: true, repoRoot, status: projectStatus() };
}

// Download the latest commit of the repo as a zip and unpack it into targetDir.
// Used both for first-time setup (fresh, empty target) and for updates (merge over an existing copy - source files are overwritten, while build output, the database and Config/ live outside the archive and are left untouched).
async function downloadProject({ targetDir, branch } = {}) {
  branch = branch || GH.branch;
  targetDir = targetDir || defaultDownloadDir();
  const send = (phase, extra) => broadcast('project:progress', { phase, ...extra });
  let tmpRoot = null;
  try {
    send('resolve', { message: 'Resolving latest commit...' });
    const commit = await githubApi(`/commits/${encodeURIComponent(branch)}`);
    const sha = commit.sha;
    const subject = (commit.commit.message || '').split('\n')[0];
    const date = commit.commit.author && commit.commit.author.date;

    tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-proj-'));
    const zipPath = path.join(tmpRoot, 'project.zip');
    const url = `https://codeload.github.com/${GH.owner}/${GH.repo}/zip/${sha}`;
    send('download', { message: 'Downloading project...', recv: 0, total: 0 });
    await downloadFile(url, zipPath, (recv, total) => send('download', { message: 'Downloading project...', recv, total }));

    send('extract', { message: 'Extracting...' });
    const exDir = path.join(tmpRoot, 'x');
    await extractZip(zipPath, exDir);
    const top = fs.readdirSync(exDir)
      .map((n) => path.join(exDir, n))
      .find((q) => { try { return fs.statSync(q).isDirectory(); } catch { return false; } });
    if (!top) throw new Error('downloaded archive was empty');

    // Refuse to merge anything that doesn't look like a complete checkout - nothing in the install has been touched up to this point.
    for (const probe of ['Shittim-Server/Shittim-Server.csproj', 'Schale/Schale.csproj']) {
      if (!fs.existsSync(path.join(top, probe))) {
        throw new Error(`downloaded archive is incomplete (missing ${probe}) - nothing was changed`);
      }
    }

    fs.mkdirSync(targetDir, { recursive: true });

    // An update over an existing install copies the database and Config/ aside first. Neither is in the archive, so neither can be overwritten by the merge, but a migration on the next launch can rewrite the database and there is otherwise nothing to go back to.
    if (fs.existsSync(path.join(targetDir, 'Shittim-Server'))) {
      send('backup', { message: 'Backing up your database and config...' });
      const p = pathsFor(targetDir);
      const kept = backupUserData(userBackupRoot(), [p.dbPath, path.dirname(p.configPath), p.gachaConfigPath], `pre-update-${sha.slice(0, 7)}-${Date.now()}`);
      broadcast('proc:log', { source: 'server', line: `> backed up ${kept.saved.length} item(s) to ${kept.dir}` });
    }

    send('install', { message: 'Installing files...' });
    const previous = readVersionMarker(targetDir);
    const merged = installTree(top, targetDir, {
      previousManifest: previous && previous.manifest,
      onProgress: (done, total) => send('install', { message: 'Installing files...', recv: done, total }),
    });
    if (!merged.ok) {
      const shown = merged.blockers.slice(0, 5).map((f) => `${f.path} (${f.error})`).join(', ');
      const more = merged.blockers.length > 5 ? ` and ${merged.blockers.length - 5} more` : '';
      const state = merged.changed
        ? 'The install is in a MIXED state and could not be rolled back fully - restore from a backup before running the server.'
        : 'Nothing in your install was changed; it is still on the previous version.';
      throw new Error(`Update stopped - ${merged.blockers.length} file(s) could not be replaced: ${shown}${more}. ` +
        `${state} ${blockerAdvice(merged.blockers)}`);
    }
    if (merged.removed) broadcast('proc:log', { source: 'server', line: `> removed ${merged.removed} file(s) this release no longer ships` });
    writeVersionMarker(targetDir, {
      sha, shortSha: sha.slice(0, 7), branch, subject,
      date: date || null, source: 'download', updatedAt: new Date().toISOString(),
      manifest: merged.manifest,
    });

    // The files are installed by this point, so a settings write that fails is worth saying out loud but is not a failed update.
    let remembered = true;
    try { saveSettings({ repoRoot: targetDir }); }
    catch (e) {
      remembered = false;
      broadcast('proc:log', { source: 'server', line: `> installed to ${targetDir}, but the folder could not be remembered: ${e.message}` });
    }
    send('done', { message: 'Done', repoRoot: targetDir, sha: sha.slice(0, 7) });
    return { ok: true, repoRoot: targetDir, sha: sha.slice(0, 7), remembered };
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
  // Whether an update exists is decided by the two commit ids differing, never by the commit count. This branch has been rewritten and force-pushed before, and after that the installed commit is not an ancestor of the tip - the count below is a changelog, not the test.
  if (localSha === remoteSha) return { ...base, ...head, versionKnown: true, differs: false, behind: 0, ahead: 0, commits: [] };

  // Diff local..remote through the compare API; its commit list is the changelog.
  try {
    const cmp = await githubApi(`/compare/${localSha}...${encodeURIComponent(branch)}`);
    const commits = (cmp.commits || []).map((c) => ({
      hash: c.sha.slice(0, 7),
      subject: (c.commit.message || '').split('\n')[0],
      author: (c.commit.author && c.commit.author.name) || (c.author && c.author.login) || '',
      when: isoToRel(c.commit.author && c.commit.author.date),
    })).reverse();
    return {
      ...base, ...head, versionKnown: true, differs: true,
      behind: cmp.ahead_by || 0, ahead: cmp.behind_by || 0, commits, status: cmp.status,
      rewritten: cmp.status === 'diverged',
    };
  } catch (e) {
    // The installed commit is not something the compare API can reach from the branch - a rewritten history, or a local build. The changelog is gone but the tip still differs.
    return { ...base, ...head, versionKnown: true, differs: true, behind: null, compareFailed: true, rewritten: true };
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

    // Upstream gets rewritten, and after that a fast-forward is impossible - git exits 128 and the useful part of the message is buried under merge advice. Recovering means discarding local commits, so it is described here and left to the user rather than run.
    await execGit(['fetch', 'origin', branch], repoRoot, 120000);
    const ancestor = await execGit(['merge-base', '--is-ancestor', 'HEAD', `origin/${branch}`], repoRoot);
    if (!ancestor.ok) {
      const local = (await execGit(['rev-parse', '--short', 'HEAD'], repoRoot)).out.trim();
      const upstream = (await execGit(['rev-parse', '--short', `origin/${branch}`], repoRoot)).out.trim();
      const message = `This checkout is at ${local}, which is not in origin/${branch} (now ${upstream}) - the branch was rewritten, or you have local commits. ` +
        `An update cannot fast-forward past that. Keep anything you want from this tree first, then run: git fetch origin && git reset --hard origin/${branch}`;
      broadcast('proc:log', { source: 'server', line: `> ${message}` });
      return { ok: false, method: 'git', rewritten: true, error: message, head: local };
    }

    broadcast('proc:log', { source: 'server', line: `> git pull --ff-only origin ${branch}` });
    const r = await execGit(['pull', '--ff-only', 'origin', branch], repoRoot, 120000);
    const output = `${r.out}\n${r.err}`.trim();
    output.split('\n').filter(Boolean).forEach((line) => broadcast('proc:log', { source: 'server', line }));
    return { ok: r.ok, method: 'git', output, head: (await execGit(['rev-parse', '--short', 'HEAD'], repoRoot)).out.trim() };
  }

  // Otherwise re-download the latest commit and merge it over the folder. The archive carries source only, so Config/, the database and build output stay.
  const branch = (marker && marker.branch) || GH.branch;
  broadcast('proc:log', { source: 'server', line: `> downloading ${GH.owner}/${GH.repo}@${branch} from GitHub...` });
  const res = await downloadProject({ targetDir: repoRoot, branch });
  if (res.ok) broadcast('proc:log', { source: 'server', line: `> project updated to ${res.sha}` });
  else broadcast('proc:log', { source: 'server', line: `> update failed: ${res.error}` });
  return { ok: res.ok, method: 'download', head: res.sha, error: res.error };
}

// Check-and-notify only: runs once shortly after launch and every few hours while the app stays open, toasting the renderer when the server source is behind origin.
// Applying stays a user action on the Updates page - the server may be live mid-session, and an apply can need files closed.

const SERVER_UPDATE_CHECK_EVERY_MS = 4 * 60 * 60 * 1000;
let lastNotifiedServerSha = null;

async function watchServerUpdates() {
  try {
    const r = await checkUpdates();
    if (!r || !r.ok || !r.versionKnown) return;        // no project or offline - stay quiet
    if (!r.differs) return;                            // same commit id - up to date
    if (r.remoteSha === lastNotifiedServerSha) return;  // already announced this tip this session
    lastNotifiedServerSha = r.remoteSha;
    broadcast('update:server', {
      behind: r.behind,
      rewritten: !!r.rewritten,
      remoteShort: r.remoteShort,
      remoteSubject: r.remoteSubject,
      remoteWhen: r.remoteWhen,
    });
    const how = r.behind > 0 ? `${r.behind} commit(s) behind` : 'a different version of the branch';
    broadcast('proc:log', { source: 'server', line: `> server update available: ${how} - ${r.remoteShort} "${r.remoteSubject}" (apply it from the Updates page)` });
  } catch { /* offline or rate-limited - the next cycle retries */ }
}

async function rebuildServer() {
  const p = resolvePaths();
  if (!fs.existsSync(p.csproj)) return { ok: false, error: 'Server project not found' };

  // The build can't replace Shittim-Server.exe or Schale.dll while the server holds them open, so a live one comes down for it and goes back up after. Under `dotnet run` the server is a grandchild of the process we wait on and outlives it slightly, hence the settle.
  const running = procs.server && !procs.server.killed ? procs.server : null;
  if (running) {
    broadcast('proc:log', { source: 'server', line: '> stopping the server so the build can replace its binaries...' });
    const down = await stopServer();
    if (!down.ok) return { ok: false, error: `the server is still running, so the build would fail to replace its binaries: ${down.error}` };
    await new Promise((resolve) => {
      const bail = setTimeout(resolve, 10000);
      running.once('exit', () => { clearTimeout(bail); setTimeout(resolve, 500); });
    });
  }

  const res = await new Promise((resolve) => {
    broadcast('proc:log', { source: 'server', line: '> dotnet build -c Debug (rebuilding after update)...' });
    broadcast('proc:state', { rebuild: 'building' });
    const dn = resolveDotnet();
    let child;
    try { child = track(spawn(dn.cmd, ['build', '-c', 'Debug', p.csproj], { cwd: p.serverDir, windowsHide: true, env: dotnetEnv(dn.root) })); }
    catch (e) { resolve({ ok: false, error: String(e.message || e) }); return; }
    pipeLines(child, 'server');
    child.on('exit', (code) => { broadcast('proc:state', { rebuild: 'done' }); broadcast('proc:log', { source: 'server', line: `> build exited (code ${code})` }); resolve({ ok: code === 0, code }); });
    child.on('error', (err) => { broadcast('proc:state', { rebuild: 'failed' }); resolve({ ok: false, error: err.message }); });
  });

  // Back up even on a failed build, on the old binaries, rather than leaving the server down.
  if (running) {
    const back = startServer();
    if (!back.ok) broadcast('proc:log', { source: 'server', line: `> could not restart the server: ${back.error}` });
  }

  return { ...res, restarted: !!running };
}

// One-click acquisition of the three host prerequisites the readiness card reports on: the .NET 10 SDK, mitmproxy, and a trusted mitmproxy CA cert.
// The .NET SDK installs per-user (no admin); mitmproxy's official installer and trusting the CA into the machine root store both require admin and prompt once for elevation. Each installer is idempotent and streams progress to the renderer over 'setup:progress'.
// Windows only; elsewhere each returns a message pointing at the manual install.

const DOTNET_DIR = () => path.join(process.env.LOCALAPPDATA || os.homedir(), 'Microsoft', 'dotnet');
const CERT_PATH = () => path.join(os.homedir(), '.mitmproxy', 'mitmproxy-ca-cert.cer');

// mitmproxy ships an official, per-version Windows installer (Inno Setup, admin-only - its manifest is requireAdministrator). We pin a known-good version, install it into Program Files silently+elevated, then add it to PATH ourselves because the installer does not modify PATH.
const MITM_VERSION = '12.2.3';
const MITM_INSTALLER_URL = (v) => `https://downloads.mitmproxy.org/${v}/mitmproxy-${v}-windows-x86_64-installer.exe`;
const MITM_INSTALL_DIR = () => path.join(process.env.ProgramFiles || 'C:\\Program Files', 'mitmproxy');

const mitmTool = (name) => mitmExe(name, MITM_INSTALL_DIR());

function setupLog(step, line) { broadcast('setup:progress', { step, line }); }
function setupPhase(step, status, extra) { broadcast('setup:progress', { step, status, ...(extra || {}) }); }

// Run a PowerShell snippet, streaming each output line to onLine. Resolves with the exit code - never rejects, so callers branch on `ok`.
function runPwsh(script, onLine) {
  return new Promise((resolve) => {
    let child;
    try {
      child = track(spawn('powershell.exe',
        ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', script],
        { windowsHide: true, env: { ...process.env } }));
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

// Append a directory to the persistent per-user PATH (and to this process's live PATH so spawns in the current session resolve the tool without a restart).
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
    return { ok: false, error: 'Automated .NET install is wired up for Windows only - install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0.' };
  }
  setupPhase(step, 'running', { message: '~250 MB, this can take a few minutes' });
  let tmp = null;
  let beat = null;
  try {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-dotnet-'));
    const scriptPath = path.join(tmp, 'dotnet-install.ps1');
    setupLog(step, '> downloading dotnet-install.ps1');
    await downloadFile('https://dot.net/v1/dotnet-install.ps1', scriptPath);
    const dir = DOTNET_DIR();
    setupLog(step, `> dotnet-install.ps1 -Channel 10.0 -InstallDir "${dir}"`);
    // dotnet-install.ps1 emits almost nothing during the big transfer, so keep a heartbeat going to prove the step is still alive in the log/UI.
    beat = setInterval(() => setupPhase(step, 'running', { message: 'downloading in the background' }), 3000);
    // -NoPath: the script's session-only PATH edit is useless to us; we persist it ourselves below. The install is a no-op if the SDK is already present.
    const ps = `& ${psQuote(scriptPath)} -Channel 10.0 -InstallDir ${psQuote(dir)} -Architecture x64 -NoPath`;
    const r = await runPwsh(ps, (l) => setupLog(step, l));
    clearInterval(beat); beat = null;
    if (!r.ok) { setupPhase(step, 'failed', { message: 'dotnet-install.ps1 failed' }); return { ok: false, error: 'dotnet-install.ps1 failed', out: r.out }; }
    // A base SDK without the ASP.NET Core Web SDK still builds far enough to fail with "SDK 'Microsoft.NET.Sdk.Web' could not be found" - catch that here rather than letting the first server launch surface it.
    if (!hasWebSdk(dir)) {
      setupPhase(step, 'failed', { message: 'Install finished but the ASP.NET Core Web SDK is missing - re-run install.' });
      return { ok: false, error: `Microsoft.NET.Sdk.Web not found under ${path.join(dir, 'sdk')} - the SDK install looks incomplete.` };
    }
    await addToUserPath(dir, step);
    setupLog(step, `> verified ASP.NET Core Web SDK is present in ${dir}`);
    setupPhase(step, 'done', { message: '.NET 10 SDK installed' });
    return { ok: true, dir };
  } catch (e) {
    setupPhase(step, 'failed', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (beat) { clearInterval(beat); beat = null; }
    if (tmp) { try { fs.rmSync(tmp, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

// mitmproxy: download the official pinned Windows installer and run it silently+elevated into Program Files, then add the install dir to PATH (the installer itself never modifies PATH).
async function installMitmproxy() {
  const step = 'mitmproxy';
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Automated mitmproxy install is wired up for Windows only - install it from https://mitmproxy.org/.' };
  }
  setupPhase(step, 'running', { message: `Downloading mitmproxy ${MITM_VERSION}...` });
  let tmp = null;
  try {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
    const installer = path.join(tmp, `mitmproxy-${MITM_VERSION}-installer.exe`);
    const url = MITM_INSTALLER_URL(MITM_VERSION);
    setupLog(step, `> downloading ${url}`);
    await downloadFile(url, installer, (recv, total) => broadcast('setup:progress', { step, recv, total }));

    const dir = MITM_INSTALL_DIR();
    setupPhase(step, 'running', { message: 'Approve the elevation prompt' });
    setupLog(step, `> running installer silently into ${dir} (requires elevation)`);
    // Inno Setup silent switches; -Verb RunAs raises the one UAC prompt the requireAdministrator manifest forces.
    // ArgumentList as a single string is passed verbatim so /DIR="...with spaces..." reaches Inno intact.
    const innoArgs = `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="${dir}"`;
    const ps = `$p = Start-Process -FilePath ${psQuote(installer)} -ArgumentList ${psQuote(innoArgs)} -Verb RunAs -PassThru -Wait; exit $p.ExitCode`;
    const r = await runPwsh(ps, (l) => setupLog(step, l));
    if (!r.ok) { setupPhase(step, 'failed', { message: 'installer failed or elevation was declined' }); return { ok: false, error: 'mitmproxy installer failed or elevation was declined', out: r.out }; }

    const binDir = findMitmBinDir(dir);
    if (!binDir) throw new Error(`mitmweb.exe not found under ${dir} after install`);
    await addToUserPath(binDir, step);
    setupPhase(step, 'done', { message: `mitmproxy ${MITM_VERSION} installed` });
    return { ok: true, dir: binDir };
  } catch (e) {
    setupPhase(step, 'failed', { message: String(e.message || e) });
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (tmp) { try { fs.rmSync(tmp, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

// Run mitmdump just long enough for it to write ~/.mitmproxy/*.cer on first start, then stop it. mitmproxy generates its CA lazily on proxy startup, so a brief launch on a throwaway port is the supported way to materialise the cert.
function generateMitmCert(step) {
  return new Promise((resolve, reject) => {
    const certPath = CERT_PATH();
    const exe = mitmTool('mitmdump');
    let child;
    try { child = track(spawn(exe, ['--listen-port', '48080', '-q'], { windowsHide: true, env: { ...process.env } })); }
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

// Trust the mitmproxy CA. Generates it first if it has never been created, then adds it to the machine root store via certutil (one elevation prompt).
async function installCertificate() {
  const step = 'certificate';
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Automated certificate trust is wired up for Windows only.' };
  }
  setupPhase(step, 'running', { message: 'Preparing CA certificate...' });
  try {
    const certPath = CERT_PATH();
    if (!fs.existsSync(certPath)) {
      setupLog(step, '> generating mitmproxy CA (first run)...');
      await generateMitmCert(step);
    }
    if (!fs.existsSync(certPath)) throw new Error('mitmproxy CA certificate was not generated - install mitmproxy first.');
    setupLog(step, '> trusting CA in machine root store (certutil - approve the elevation prompt)...');
    // Machine root store is what the Steam client validates against, so it needs admin. Start-Process -Verb RunAs raises the single UAC prompt.
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

// Orchestrate one step, or all three in dependency order (the cert step needs mitmproxy's mitmdump, so mitmproxy is installed before it).
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

// Bundle the server logs plus a short diagnostic snapshot into a single .zip the user can attach when reporting an issue. The classic "stuck on Unpacking game resources" hang, for instance, shows up plainly in the server log (a failed gateway-key handshake, or a client metadata file that was never found).
// Uses PowerShell's Compress-Archive on Windows - mirroring extractZip's Expand-Archive - so no zip dependency is bundled.

function logsDir() {
  // ConfigLogger writes to <exeBaseDir>/logs/log.txt (+ log-prev.txt).
  return path.join(resolvePaths().exeBaseDir, 'logs');
}

function documentsDir() {
  try { return app.getPath('documents'); } catch { return os.homedir(); }
}

function compressArchive(items, destZip) {
  return new Promise((resolve, reject) => {
    if (process.platform === 'win32') {
      const list = items.map(psQuote).join(',');
      const ps = `$ProgressPreference='SilentlyContinue'; Compress-Archive -LiteralPath ${list} -DestinationPath ${psQuote(destZip)} -Force`;
      execFile('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', ps],
        { windowsHide: true, maxBuffer: 64 * 1024 * 1024 },
        (err, _so, se) => err ? reject(new Error((se || '').toString().trim() || err.message)) : resolve());
    } else {
      execFile('zip', ['-j', destZip, ...items], { maxBuffer: 64 * 1024 * 1024 },
        (err, _so, se) => err ? reject(new Error((se || '').toString().trim() || err.message)) : resolve());
    }
  });
}

async function buildDiagnosticInfo() {
  const p = resolvePaths();
  let env = null;
  try { env = await runEnvChecks(); } catch (e) { env = { error: String(e.message || e) }; }
  let appVersion = '';
  try { appVersion = app.getVersion(); } catch { /* ignore */ }
  const vs = componentVersions(p.repoRoot, appVersion, readVersionMarker(p.repoRoot));

  return [
    '# Shittim Control Center diagnostic snapshot',
    `generated:     ${new Date().toISOString()}`,
    `controlCenter: ${appVersion}`,
    `serverFiles:   ${vs.sources || '(no Directory.Build.props)'}`,
    `serverBuild:   ${vs.built || '(not built)'}`,
    `mismatch:      ${vs.buildStale || vs.releaseSkew || 'none'}`,
    `packaged:      ${app.isPackaged}`,
    `electron:      ${process.versions.electron}`,
    `node:          ${process.versions.node}`,
    `os:            ${os.type()} ${os.release()} (${process.arch})`,
    '',
    '## Paths',
    `repoRoot:      ${p.repoRoot}`,
    `serverDir:     ${p.serverDir}`,
    `exePath:       ${p.exePath || '(not built)'}`,
    `configPath:    ${p.configPath}`,
    `dbPath:        ${p.dbPath}`,
    `logsDir:       ${logsDir()}`,
    '',
    '## Processes',
    `server:        ${procs.server && !procs.server.killed ? 'running' : 'stopped'}`,
    `mitmproxy:     ${procs.mitm && !procs.mitm.killed ? 'running' : 'stopped'}`,
    '',
    '## Environment checks',
    JSON.stringify(env, null, 2),
    '',
  ].join('\r\n');
}

// Returns the chosen profile's name and content; the renderer uploads it, since the server only loads from its own folder and may not be on this machine.
async function pickAccountDataFile() {
  const res = await dialog.showOpenDialog({
    title: 'Import profile',
    defaultPath: documentsDir(),
    filters: [{ name: 'Account profile', extensions: ['json'] }],
    properties: ['openFile'],
  });
  if (res.canceled || !res.filePaths?.length) return { ok: false, canceled: true };

  const file = res.filePaths[0];
  try {
    const content = fs.readFileSync(file, 'utf8');
    try { JSON.parse(content); }
    catch { return { ok: false, error: `${path.basename(file)} is not valid JSON` }; }
    return { ok: true, name: path.basename(file), content };
  } catch (e) {
    return { ok: false, error: e.message };
  }
}

async function exportLogs() {
  const p = resolvePaths();
  const stamp = new Date().toISOString().replace(/[:T]/g, '-').replace(/\..+$/, '');
  const res = await dialog.showSaveDialog({
    title: 'Export logs',
    defaultPath: path.join(documentsDir(), `shittim-logs-${stamp}.zip`),
    filters: [{ name: 'Zip archive', extensions: ['zip'] }],
  });
  if (res.canceled || !res.filePath) return { ok: false, canceled: true };
  const destZip = res.filePath;

  let staging = null;
  try {
    staging = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-logs-'));
    let count = 0;

    const ld = logsDir();
    for (const name of ['log.txt', 'log-prev.txt']) {
      const src = path.join(ld, name);
      try { if (fs.existsSync(src)) { fs.copyFileSync(src, path.join(staging, name)); count++; } } catch { /* skip */ }
    }

    // server config - toggles + client paths (no private key material by default)
    try { if (fs.existsSync(p.configPath)) { fs.copyFileSync(p.configPath, path.join(staging, 'Config.json')); count++; } } catch { /* skip */ }

    // diagnostic snapshot - always present so the bundle is never empty
    try { fs.writeFileSync(path.join(staging, 'controlcenter-info.txt'), await buildDiagnosticInfo()); count++; } catch { /* skip */ }

    const entries = fs.readdirSync(staging).map((n) => path.join(staging, n));
    if (!entries.length) return { ok: false, error: 'No logs were found to export.' };

    try { if (fs.existsSync(destZip)) fs.rmSync(destZip, { force: true }); } catch { /* -Force handles overwrite */ }
    await compressArchive(entries, destZip);

    return { ok: true, path: destZip, name: path.basename(destZip), count };
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  } finally {
    if (staging) { try { fs.rmSync(staging, { recursive: true, force: true }); } catch { /* ignore */ } }
  }
}

// The Control Center is a packaged Electron app, so its own exe/files can only be replaced by a newer packaged build - the source pull on the Updates page only updates the .NET server's source.
// electron-updater pulls new Control Center builds from this repo's GitHub Releases (configured by the `publish` block in package.json), prompts before downloading, and installs on quit. Releases must carry the latest.yml + blockmap + installer assets that `npm run publish` uploads (set a GH_TOKEN env var first).
// This repo's releases mix SERVER builds and Control Center builds, and electron-updater's GitHub provider only ever inspects the newest release - a server release on top would 404 the feed.
// Portable builds (and pre-2026.7.27 installs, which shipped without an embedded feed) cannot be swapped in place: they get a version check plus a link to the release page instead.

let cachedAutoUpdater = null;
let updaterWired = false;

const SELF_RELEASES_PAGE = `https://github.com/${GH.owner}/${GH.repo}/releases`;

// Newest non-draft release that carries updater metadata. Returns { url, tag, htmlUrl } or null when none is published yet.
async function resolveSelfUpdateFeed() {
  const releases = await githubApi('/releases?per_page=20');
  for (const r of releases) {
    if (r.draft) continue;
    if ((r.assets || []).some((a) => a.name === 'latest.yml')) {
      return {
        url: `https://github.com/${GH.owner}/${GH.repo}/releases/download/${encodeURIComponent(r.tag_name)}/`,
        tag: r.tag_name,
        htmlUrl: r.html_url || SELF_RELEASES_PAGE,
      };
    }
  }
  return null;
}

function hasInPlaceUpdater() {
  return selfUpdateMode({
    packaged: app.isPackaged,
    portableDir: process.env.PORTABLE_EXECUTABLE_DIR,
    portableFile: process.env.PORTABLE_EXECUTABLE_FILE,
    resourcesPath: process.resourcesPath,
  }) === 'in-place';
}

// Manual-download flow for builds electron-updater can't patch: read the feed's latest.yml, compare versions, and offer the release page.
async function checkManualSelfUpdate() {
  const feed = await resolveSelfUpdateFeed();
  if (!feed) return { ok: true, portable: true, current: app.getVersion(), version: null, available: false };
  const res = await httpGet(feed.url + 'latest.yml');
  if (res.statusCode !== 200) throw new Error(`update feed responded HTTP ${res.statusCode}`);
  const m = res.body.toString('utf8').match(/^version:\s*(\S+)/m);
  const latest = m ? m[1] : null;
  if (!latest || cmpVer(latest, app.getVersion()) <= 0) {
    return { ok: true, portable: true, current: app.getVersion(), version: latest, available: false };
  }
  broadcast('update:self', { phase: 'available', version: latest, portable: true });
  const win = BrowserWindow.getAllWindows()[0] || null;
  const { response } = await dialog.showMessageBox(win, {
    type: 'info',
    buttons: ['Open download page', 'Later'],
    defaultId: 0,
    cancelId: 1,
    title: 'Control Center update available',
    message: `Shittim Control Center ${latest} is available.`,
    detail: `You are running ${app.getVersion()} as a build that cannot update itself in place. ` +
      'Grab the new installer or portable exe from the releases page.',
  });
  if (response === 0) shell.openExternal(feed.htmlUrl);
  return { ok: true, portable: true, current: app.getVersion(), version: latest, available: true };
}

// Compare dotted numeric versions: 1 if a>b, -1 if a<b, 0 if equal.
function cmpVer(a, b) {
  const pa = String(a || '0').split('.').map((n) => parseInt(n, 10) || 0);
  const pb = String(b || '0').split('.').map((n) => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d) return d > 0 ? 1 : -1;
  }
  return 0;
}

// Lazily require + wire electron-updater. Returns the configured autoUpdater, or null when running from source (no packaged feed) or if the module is absent.
function setupAutoUpdate(win) {
  if (!app.isPackaged) return null;

  if (!cachedAutoUpdater) {
    try { ({ autoUpdater: cachedAutoUpdater } = require('electron-updater')); }
    catch (e) {
      broadcast('proc:log', { source: 'server', line: `> auto-update unavailable (electron-updater not bundled): ${e.message}` });
      return null;
    }
    cachedAutoUpdater.autoDownload = false;        // prompt before pulling the build
    cachedAutoUpdater.autoInstallOnAppQuit = true; // if declined now, install on quit
    try { cachedAutoUpdater.logger = null; } catch { /* ignore */ }
  }

  const autoUpdater = cachedAutoUpdater;

  if (!updaterWired) {
    updaterWired = true;

    autoUpdater.on('update-available', async (info) => {
      broadcast('update:self', { phase: 'available', version: info.version });
      const { response } = await dialog.showMessageBox(win, {
        type: 'info',
        buttons: ['Download && install', 'Later'],
        defaultId: 0,
        cancelId: 1,
        title: 'Control Center update available',
        message: `Shittim Control Center ${info.version} is available.`,
        detail: `You are running ${app.getVersion()}. The update installs when you close the app.`,
      });
      if (response === 0) {
        broadcast('update:self', { phase: 'downloading', percent: 0 });
        autoUpdater.downloadUpdate().catch((e) => broadcast('proc:log', { source: 'server', line: `> update download failed: ${e.message}` }));
      }
    });

    autoUpdater.on('download-progress', (p) => broadcast('update:self', { phase: 'downloading', percent: Math.round(p.percent || 0) }));

    autoUpdater.on('update-downloaded', async (info) => {
      broadcast('update:self', { phase: 'downloaded', version: info.version });
      const { response } = await dialog.showMessageBox(win, {
        type: 'info',
        buttons: ['Restart now', 'On next quit'],
        defaultId: 0,
        cancelId: 1,
        title: 'Update ready',
        message: `Shittim Control Center ${info.version} downloaded.`,
      });
      if (response === 0) setImmediate(() => autoUpdater.quitAndInstall());
    });

    autoUpdater.on('error', (err) => {
      let line = `> auto-update error: ${(err && err.message) || err}`;
      if (/404/.test(line) && /latest\.yml|update.*metadata|feed/i.test(line)) {
        line += ' (the newest GitHub release carries no updater metadata - publish with `npm run publish` so latest.yml is attached)';
      }
      broadcast('proc:log', { source: 'server', line });
    });
  }

  return autoUpdater;
}

// Manual "check now": triggers a check (the update-available handler still drives the prompt) and reports the result so the renderer can toast up-to-date / dev.
// Portable/feedless builds go through the manual-download flow instead.
async function checkSelfUpdate() {
  if (!app.isPackaged) return { ok: true, dev: true, current: app.getVersion() };
  if (!hasInPlaceUpdater()) {
    try { return await checkManualSelfUpdate(); }
    catch (e) { return { ok: false, error: String(e.message || e) }; }
  }
  const autoUpdater = setupAutoUpdate(BrowserWindow.getAllWindows()[0] || null);
  if (!autoUpdater) return { ok: false, error: 'Updater is not available in this build.' };
  try {
    // Aim electron-updater at the newest release that actually has a feed; the embedded app-update.yml remains the fallback if the API is unreachable.
    const feed = await resolveSelfUpdateFeed().catch(() => null);
    if (feed) autoUpdater.setFeedURL({ provider: 'generic', url: feed.url });
    const r = await autoUpdater.checkForUpdates();
    const version = r && r.updateInfo ? r.updateInfo.version : null;
    return { ok: true, current: app.getVersion(), version, available: version ? cmpVer(version, app.getVersion()) > 0 : false };
  } catch (e) {
    return { ok: false, error: String(e.message || e) };
  }
}

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
    backgroundColor: '#0d1826',
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

ipcMain.handle('accountdata:pick', () => pickAccountDataFile());
ipcMain.handle('paths:resolve', () => resolvePaths());
ipcMain.handle('settings:read', () => loadSettings());
// The renderer writes window geometry through here and does not wait on it, so a failure is reported rather than thrown back at it.
ipcMain.handle('settings:write', (_e, patch) => {
  try { return saveSettings(patch || {}); }
  catch (e) { return { error: String(e.message || e) }; }
});

ipcMain.handle('config:read', () => readConfig());
ipcMain.handle('config:write', (_e, payload) => writeConfig(payload));

ipcMain.handle('server:start', () => startServer());
ipcMain.handle('server:stop', () => stopServer());
ipcMain.handle('mitm:start', () => startMitm());
ipcMain.handle('mitm:stop', () => stopMitm());

ipcMain.handle('system:start', () => {
  const server = startServer();
  const mitm = startMitm();
  return { ok: server.ok || mitm.ok, server, mitm };
});
ipcMain.handle('system:stop', async () => {
  const server = procs.server ? await stopServer() : { ok: true, error: 'Server not running' };
  const mitm = procs.mitm ? await stopMitm() : { ok: true, error: 'mitmproxy not running' };
  const hosts = offlineHostsApplied() ? await setOfflineHosts(false) : { ok: true, changed: false };
  return { ok: server.ok && mitm.ok, server, mitm, hosts };
});

// offline start: the hosts entries have to land before anything else, since the client resolves its first name within seconds of launch and a stale cache entry would send it out to the real address.
ipcMain.handle('system:startOffline', async () => {
  const hosts = await setOfflineHosts(true);
  if (!hosts.ok) return { ok: false, error: hosts.error, hosts };
  const server = startServer(true);
  const mitm = startMitm(true);
  return { ok: server.ok || mitm.ok, hosts, server, mitm };
});
ipcMain.handle('offline:status', () => ({ hosts: offlineHostsApplied(), hostnames: OFFLINE_HOSTS }));
ipcMain.handle('offline:hosts', (_e, on) => setOfflineHosts(!!on));

ipcMain.handle('project:status', () => projectStatus());
ipcMain.handle('project:download', (_e, opts) => downloadProject(opts || {}));
ipcMain.handle('project:setPath', (_e, dir) => setProjectPath(dir));

ipcMain.handle('updates:check', () => checkUpdates());
ipcMain.handle('updates:apply', () => applyUpdate());
ipcMain.handle('updates:rebuild', () => rebuildServer());
ipcMain.handle('updates:checkSelf', () => checkSelfUpdate());
ipcMain.handle('proc:status', () => ({
  server: procs.server && !procs.server.killed ? 'running' : (spawnFailed.server ? 'failed' : 'stopped'),
  mitm: procs.mitm && !procs.mitm.killed ? 'running' : (spawnFailed.mitm ? 'failed' : 'stopped'),
  serverPid: procs.server && !procs.server.killed ? procs.server.pid : null,
  mitmPid: procs.mitm && !procs.mitm.killed ? procs.mitm.pid : null,
  serverStartedAt: started.server,
  serverGraceMs: started.serverGraceMs,
  mitmStartedAt: started.mitm,
}));

ipcMain.handle('env:check', () => runEnvChecks());

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
ipcMain.handle('shell:showItem', (_e, p) => { try { shell.showItemInFolder(p); return true; } catch { return false; } });

ipcMain.handle('logs:export', () => exportLogs());

ipcMain.on('window:control', (e, action) => {
  const win = BrowserWindow.fromWebContents(e.sender);
  if (!win) return;
  if (action === 'minimize') win.minimize();
  else if (action === 'maximize') win.isMaximized() ? win.unmaximize() : win.maximize();
  else if (action === 'close') win.close();
});

app.whenReady().then(() => {
  createWindow();
  try {
    const undone = resumeInterruptedInstall(resolvePaths().repoRoot);
    if (undone) {
      broadcast('proc:log', { source: 'server', line: `> a previous update was interrupted - rolled ${undone.restored} file(s) back to the version you were on` });
      if (undone.stuck.length) broadcast('proc:log', { source: 'server', line: `> ${undone.stuck.length} file(s) could not be rolled back: ${undone.stuck.slice(0, 5).map((f) => f.path).join(', ')}` });
    }
  } catch (e) {
    broadcast('proc:log', { source: 'server', line: `> could not check for an interrupted update: ${e.message}` });
  }
  // On launch, check GitHub Releases for a newer packaged Control Center build and prompt to install. Installed builds go through electron-updater; portable/feedless builds get a manual-download notice.
  // Dev runs skip this, and offline errors stay quiet (manual checks still surface them).
  checkSelfUpdate().catch(() => { /* offline or no releases yet - stay quiet */ });
  // Server-source update check: once after the renderer has had time to come up (so the toast lands), then periodically while the app stays open.
  setTimeout(watchServerUpdates, 8000);
  setInterval(watchServerUpdates, SERVER_UPDATE_CHECK_EVERY_MS);
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

// Quit has to wait for the kills. Firing them and returning lets Electron tear this process down with taskkill still in flight, and then dotnet and mitmdump outlive the window that started them - which is the state people find when the next launch cannot bind the port. The 8s cap is so a child that refuses to die cannot hold the app open forever.
let quitting = false;
app.on('before-quit', (e) => {
  if (quitting) return;
  quitting = true;
  e.preventDefault();
  const all = [killTree(procs.server), killTree(procs.mitm), ...[...helpers].map((c) => killPidTree(c.pid))];
  Promise.race([Promise.all(all), new Promise((r) => setTimeout(r, 8000))]).then(() => app.quit());
});
