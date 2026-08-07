'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');

const { resolveRepoRoot, pathsFor, componentVersions, mitmExe, dotnetExe } = require('../paths');

// An install on some other drive, and the Control Center packaged into %LOCALAPPDATA%\Programs where __dirname is resources/app.asar.
function scratch() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-paths-'));
  const install = path.join(root, 'D_drive', 'Shittim-Server-install');
  fs.mkdirSync(path.join(install, 'Shittim-Server'), { recursive: true });
  const appDir = path.join(root, 'AppData', 'Local', 'Programs', 'shittim-control-center', 'resources', 'app.asar');
  fs.mkdirSync(path.dirname(appDir), { recursive: true });
  return { root, install, appDir };
}

test('a configured folder that is there is the one used', () => {
  const { root, install, appDir } = scratch();
  try {
    const r = resolveRepoRoot({ repoRoot: install }, appDir);
    assert.equal(r.repoRoot, install);
    assert.equal(r.missing, false);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// The folder the user set goes away - drive unplugged, folder renamed, share offline. Falling back silently points the app at its own resources directory and offers a fresh download while the database is still on the other drive.
test('a configured folder that has gone missing is still the answer, and is flagged', () => {
  const { root, install, appDir } = scratch();
  try {
    fs.renameSync(install, install + '-renamed');
    const r = resolveRepoRoot({ repoRoot: install }, appDir);

    assert.equal(r.repoRoot, install, 'the configured folder is not discarded');
    assert.equal(r.configured, install);
    assert.equal(r.missing, true);
    assert.ok(!r.repoRoot.startsWith(path.dirname(appDir)), 'and it never resolves into the packaged app');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('with nothing configured the project is the folder the app sits in', () => {
  const { root, appDir } = scratch();
  try {
    const r = resolveRepoRoot({}, appDir);
    assert.equal(r.repoRoot, path.dirname(appDir));
    assert.equal(r.configured, null);
    assert.equal(r.missing, false);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('every derived path moves when the project folder changes', () => {
  const a = pathsFor(path.join('X:', 'one'));
  const b = pathsFor(path.join('Y:', 'two'));

  for (const key of Object.keys(a)) {
    if (a[key] === null) continue;
    assert.ok(a[key].startsWith(path.join('X:', 'one')), `${key} is not under the project folder: ${a[key]}`);
    assert.notEqual(a[key], b[key], `${key} did not follow the project folder`);
  }
});

// dotnet only appends .exe on Windows, so a resolver looking for one finds no build at all on Linux and falls back to a Debug directory that may not be the one the server was published into.
test('the built server is found on a platform that does not suffix executables', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-nix-'));
  try {
    const bin = path.join(root, 'Shittim-Server', 'bin', 'Release', 'net10.0');
    fs.mkdirSync(bin, { recursive: true });
    fs.writeFileSync(path.join(bin, 'Shittim-Server'), '');

    assert.equal(pathsFor(root, 'linux').exePath, path.join(bin, 'Shittim-Server'));
    assert.equal(pathsFor(root, 'win32').exePath, null, 'and a Windows host is not fooled by it either');
    assert.equal(pathsFor(root, 'linux').configPath, path.join(bin, 'Config', 'Config.json'));
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

function mitmAt(dir) {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'mitmweb.exe'), '');
  return path.join(dir, 'mitmweb.exe');
}

// The installer writes PATH into the registry and broadcasts a settings change, which this process, started before it, never sees. Asking PATH alone therefore says "mitmproxy is missing" straight after installing mitmproxy, and the only way out anyone finds is restarting the whole Control Center.
test('an install that PATH has not caught up with is still found', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
  try {
    const exe = mitmAt(path.join(root, 'Program Files', 'mitmproxy'));
    assert.equal(mitmExe('mitmweb', path.join(root, 'Program Files', 'mitmproxy'), 'win32', ''), exe);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('the installer layout is found when it puts the tools in a sub-directory', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
  try {
    const exe = mitmAt(path.join(root, 'mitmproxy', 'bin'));
    assert.equal(mitmExe('mitmweb', path.join(root, 'mitmproxy'), 'win32', ''), exe);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// A pip install under Scripts\ and our Program Files one are both normal and they are not the same program, so the answer has to be a path rather than a bare name that the spawn resolves out of sight.
test('a mitmproxy only on PATH is found and named', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
  try {
    const scripts = path.join(root, 'Python313', 'Scripts');
    const exe = mitmAt(scripts);
    const PATH = [path.join(root, 'nothing-here'), scripts].join(path.delimiter);

    assert.equal(mitmExe('mitmweb', path.join(root, 'never-installed'), 'win32', PATH), exe);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('the version we installed wins over whatever else is on PATH', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-mitm-'));
  try {
    const scripts = path.join(root, 'Python313', 'Scripts');
    const ours = mitmAt(path.join(root, 'Program Files', 'mitmproxy'));
    mitmAt(scripts);
    assert.equal(mitmExe('mitmweb', path.dirname(ours), 'win32', scripts), ours);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// Not a resolution failure to report - the spawn may still find it through a shell or an environment we cannot see - but it is what the environment check prints, so it has to stay a name and not become null.
test('with nothing anywhere the bare name comes back', () => {
  assert.equal(mitmExe('mitmdump', path.join('X:', 'nope'), 'win32', ''), 'mitmdump.exe');
  assert.equal(mitmExe('mitmdump', path.join('X:', 'nope'), 'linux', ''), 'mitmdump');
});

function dotnetAt(dir) {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'dotnet.exe'), '');
  return path.join(dir, 'dotnet.exe');
}

test('our own per-user SDK is preferred and reported as a directory we own', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-dn-'));
  try {
    const ours = path.join(root, 'AppData', 'Local', 'Microsoft', 'dotnet');
    const exe = dotnetAt(ours);
    dotnetAt(path.join(root, 'Program Files', 'dotnet'));

    const r = dotnetExe(ours, 'win32', '', path.join(root, 'Program Files'));
    assert.equal(r.cmd, exe);
    assert.equal(r.root, ours, 'and the SDK layout can be read off disk rather than by invoking it');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// The .NET installer someone runs from Microsoft's page goes to Program Files and puts it on the machine PATH, and none of that reaches the environment this process was started with. Asking PATH alone says the SDK is missing while it is sitting right there.
test('a system-wide SDK installed while we were running is found', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-dn-'));
  try {
    const exe = dotnetAt(path.join(root, 'Program Files', 'dotnet'));
    const r = dotnetExe(path.join(root, 'never-installed'), 'win32', '', path.join(root, 'Program Files'));

    assert.equal(r.cmd, exe);
    assert.equal(r.root, null, 'not ours, so its SDK list has to come from asking it');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('an SDK on PATH is found ahead of the standard location', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-dn-'));
  try {
    const onPath = dotnetAt(path.join(root, 'tools', 'dotnet'));
    dotnetAt(path.join(root, 'Program Files', 'dotnet'));

    assert.equal(dotnetExe(null, 'win32', path.join(root, 'tools', 'dotnet'), path.join(root, 'Program Files')).cmd, onPath);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('with no SDK anywhere the bare name comes back for the check to fail on', () => {
  const r = dotnetExe(path.join('X:', 'nope'), 'win32', '', path.join('X:', 'nope'));
  assert.deepEqual(r, { cmd: 'dotnet.exe', root: null });
});

// An install whose sources are at `sources` and whose compiled server was built from `built`.
function versioned(sources, built) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-ver-'));
  fs.writeFileSync(path.join(root, 'Directory.Build.props'),
    `<Project>\n  <PropertyGroup>\n    <Version>${sources}</Version>\n  </PropertyGroup>\n</Project>\n`);
  const bin = path.join(root, 'Shittim-Server', 'bin', 'Debug', 'net10.0');
  fs.mkdirSync(bin, { recursive: true });
  if (built) {
    fs.writeFileSync(path.join(bin, 'Shittim-Server.exe'), '');
    fs.writeFileSync(path.join(bin, 'Shittim-Server.deps.json'), JSON.stringify({
      targets: { '.NETCoreApp,Version=v10.0': { [`Shittim-Server/${built}`]: {} } },
    }));
  }
  return { root, bin };
}

// bin/ is not in the release archive, so an update leaves the old compiled server in place and starting it runs the previous release against the new config, schema and patch definitions.
test('a server built from a different release than the sources is not started', () => {
  const { root } = versioned('2026.7.31', '2026.6.1');
  try {
    const v = componentVersions(root, '2026.7.31', null);
    assert.equal(v.sources, '2026.7.31');
    assert.equal(v.built, '2026.6.1');
    assert.match(v.buildStale, /2026\.7\.31.*2026\.6\.1|2026\.6\.1.*2026\.7\.31/);
    assert.equal(v.releaseSkew, null);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('a build made from these sources is fine', () => {
  const { root } = versioned('2026.7.31', '2026.7.31');
  try {
    const v = componentVersions(root, '2026.7.31', null);
    assert.equal(v.buildStale, null);
    assert.equal(v.releaseSkew, null);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// Most updates land between version bumps, so the declared versions match and the only evidence is that the exe predates the merge.
test('a build older than the last update is stale even at the same version', () => {
  const { root, bin } = versioned('2026.7.31', '2026.7.31');
  try {
    const exe = path.join(bin, 'Shittim-Server.exe');
    const old = new Date('2026-07-01T00:00:00Z');
    fs.utimesSync(exe, old, old);

    const v = componentVersions(root, '2026.7.31', { updatedAt: '2026-07-20T00:00:00Z', shortSha: 'abc1234' });
    assert.match(v.buildStale, /abc1234/);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('nothing to complain about when the server has never been built', () => {
  const { root } = versioned('2026.7.31', null);
  try {
    const v = componentVersions(root, '2026.7.31', { updatedAt: '2026-07-20T00:00:00Z' });
    assert.equal(v.built, null);
    assert.equal(v.buildStale, null);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('a control center from another release is reported but does not stop anything', () => {
  const { root } = versioned('2026.6.1', '2026.6.1');
  try {
    const v = componentVersions(root, '2026.7.31', null);
    assert.equal(v.buildStale, null);
    assert.match(v.releaseSkew, /2026\.7\.31/);
    assert.match(v.releaseSkew, /2026\.6\.1/);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('the release build is used when there is no debug build', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-paths-'));
  try {
    const rel = path.join(root, 'Shittim-Server', 'bin', 'Release', 'net10.0');
    fs.mkdirSync(rel, { recursive: true });
    fs.writeFileSync(path.join(rel, 'Shittim-Server.exe'), '');

    const p = pathsFor(root);
    assert.equal(p.exePath, path.join(rel, 'Shittim-Server.exe'));
    assert.equal(p.configPath, path.join(rel, 'Config', 'Config.json'));
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});
