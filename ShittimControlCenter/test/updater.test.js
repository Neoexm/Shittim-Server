'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn, spawnSync } = require('child_process');

const { extractZip, installTree, resumeInterruptedInstall, backupUserData, writeFileAtomic, selfUpdateMode, blockerAdvice, JOURNAL, BACKUP_DIR } = require('../updater');

const NAMES = ['aaa.cs', 'bbb.cs', 'ccc.cs', 'Model.cs', 'eee.cs', 'fff.cs', 'Handler.cs', 'hhh.cs'];

// An install at v1 next to an archive at v2, plus a database that is not in the archive.
function scratch() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-updater-'));
  const install = path.join(root, 'install');
  const archive = path.join(root, 'archive');
  for (const [dir, ver] of [[install, 'v1'], [archive, 'v2']]) {
    fs.mkdirSync(path.join(dir, 'Shittim-Server'), { recursive: true });
    for (const n of NAMES) fs.writeFileSync(path.join(dir, 'Shittim-Server', n), `// ${ver} ${n}\n`);
  }
  fs.writeFileSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3'), 'ACCOUNT DATA');
  return { root, install, archive };
}

function versions(install) {
  return NAMES.map((n) => fs.readFileSync(path.join(install, 'Shittim-Server', n), 'utf8').slice(3, 5));
}

function cleanup(root) {
  try { fs.rmSync(root, { recursive: true, force: true }); } catch { /* the lock holder may still be exiting */ }
}

test('a clean run puts every file on the new version', () => {
  const { root, install, archive } = scratch();
  try {
    const r = installTree(archive, install);
    assert.ok(r.ok);
    assert.deepEqual(versions(install), NAMES.map(() => 'v2'));
    assert.equal(fs.readFileSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3'), 'utf8'), 'ACCOUNT DATA');
    assert.ok(!fs.existsSync(path.join(install, JOURNAL)), 'journal is removed on success');
    assert.ok(!fs.existsSync(path.join(install, BACKUP_DIR)), 'backup copies are removed on success');
  } finally { cleanup(root); }
});

// The reported failure: the server is running, so it holds its own binaries with FILE_SHARE_NONE and one file cannot be replaced. An install that ends up part v1 and part v2 is what produces CS1061 on the next build.
test('a file locked by a running server stops the update with the install untouched', () => {
  const { root, install, archive } = scratch();
  const locked = path.join(install, 'Shittim-Server', 'Model.cs');
  const holder = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command',
    `$f=[System.IO.File]::Open('${locked}','Open','ReadWrite','None'); Start-Sleep -Seconds 20; $f.Close()`], { windowsHide: true });
  try {
    // Wait for the handle to actually be held rather than guessing at a delay.
    const deadline = Date.now() + 15000;
    while (Date.now() < deadline) {
      try { fs.closeSync(fs.openSync(locked, 'r+')); } catch { break; }
      spawnSync(process.execPath, ['-e', 'setTimeout(()=>{},100)']);
    }

    const r = installTree(archive, install);
    assert.equal(r.ok, false);
    assert.equal(r.changed, false, 'nothing may be written when a file cannot be replaced');
    assert.equal(r.copied, 0);
    assert.deepEqual(r.blockers.map((b) => b.path), [path.join('Shittim-Server', 'Model.cs')]);

    holder.kill();
    const settle = Date.now() + 5000;
    while (Date.now() < settle) {
      try { fs.closeSync(fs.openSync(locked, 'r+')); break; } catch { spawnSync(process.execPath, ['-e', 'setTimeout(()=>{},100)']); }
    }
    assert.deepEqual(versions(install), NAMES.map(() => 'v1'), 'the install stays entirely on the old version');
    assert.equal(fs.readFileSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3'), 'utf8'), 'ACCOUNT DATA');
  } finally { try { holder.kill(); } catch { /* already gone */ } cleanup(root); }
});

test('a failure part-way through the copy rolls the install back', () => {
  const { root, install, archive } = scratch();
  const realCopy = fs.copyFileSync;
  let writes = 0;
  fs.copyFileSync = (from, to) => {
    // Count only writes into the install, not the copies aside into the backup folder.
    if (String(to).startsWith(install) && !String(to).includes(BACKUP_DIR) && ++writes === 4) {
      const e = new Error('disk full'); e.code = 'ENOSPC'; throw e;
    }
    return realCopy(from, to);
  };
  try {
    const r = installTree(archive, install);
    assert.equal(r.ok, false);
    assert.equal(r.rolledBack, true);
    assert.deepEqual(r.rollbackStuck, []);
    assert.deepEqual(versions(install), NAMES.map(() => 'v1'), 'a mid-copy failure leaves the old version in place');
    assert.equal(fs.readFileSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3'), 'utf8'), 'ACCOUNT DATA');
    assert.ok(!fs.existsSync(path.join(install, JOURNAL)));
  } finally { fs.copyFileSync = realCopy; cleanup(root); }
});

// Killing the app mid-merge cannot run any rollback, so the journal on disk has to be enough to finish it at the next launch.
test('an update killed mid-merge is rolled back on the next launch', () => {
  const { root, install, archive } = scratch();
  try {
    const child = spawnSync(process.execPath, ['-e', `
      const { installTree } = require(${JSON.stringify(path.join(__dirname, '..', 'updater.js'))});
      installTree(${JSON.stringify(archive)}, ${JSON.stringify(install)}, {
        onProgress: (done) => { if (done === 3) process.exit(9); },
      });
    `], { windowsHide: true });

    assert.equal(child.status, 9, 'the child must die inside the merge');
    assert.ok(fs.existsSync(path.join(install, JOURNAL)), 'the interrupted merge left its journal');
    const mixed = versions(install);
    assert.ok(mixed.includes('v2') && mixed.includes('v1'), `the killed merge left a mixed tree, got ${mixed.join(',')}`);

    const undone = resumeInterruptedInstall(install);
    assert.ok(undone);
    assert.deepEqual(undone.stuck, []);
    assert.deepEqual(versions(install), NAMES.map(() => 'v1'), 'the next launch puts it back on the version it was');
    assert.ok(!fs.existsSync(path.join(install, JOURNAL)));
    assert.equal(fs.readFileSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3'), 'utf8'), 'ACCOUNT DATA');
  } finally { cleanup(root); }
});

test('nothing to resume when no update was interrupted', () => {
  const { root, install } = scratch();
  try { assert.equal(resumeInterruptedInstall(install), null); } finally { cleanup(root); }
});

test('the gateway keypair is not replaced by the archive copy', () => {
  const { root, install, archive } = scratch();
  try {
    for (const [dir, body] of [[install, 'THE KEY THE CLIENT WAS PATCHED WITH'], [archive, 'repo placeholder']]) {
      fs.mkdirSync(path.join(dir, 'Shittim-Server', 'config'), { recursive: true });
      fs.writeFileSync(path.join(dir, 'Shittim-Server', 'config', 'GatewayPrivateKey.pem'), body);
    }
    assert.ok(installTree(archive, install).ok);
    assert.equal(fs.readFileSync(path.join(install, 'Shittim-Server', 'config', 'GatewayPrivateKey.pem'), 'utf8'), 'THE KEY THE CLIENT WAS PATCHED WITH');
  } finally { cleanup(root); }
});

// A rename upstream leaves the old file behind, the SDK globs both, and the build stops on CS0101.
test('a file the new release no longer ships is removed', () => {
  const { root, install, archive } = scratch();
  try {
    const stale = path.join(install, 'Shittim-Server', 'OldArenaDataInfo.cs');
    fs.writeFileSync(stale, '// v1 renamed away upstream\n');
    const previousManifest = [...NAMES.map((n) => path.join('Shittim-Server', n)), path.join('Shittim-Server', 'OldArenaDataInfo.cs')];

    const r = installTree(archive, install, { previousManifest });
    assert.ok(r.ok);
    assert.equal(r.removed, 1);
    assert.ok(!fs.existsSync(stale));
    assert.deepEqual(versions(install), NAMES.map(() => 'v2'));
  } finally { cleanup(root); }
});

test('only files a previous release put there can be removed', () => {
  const { root, install, archive } = scratch();
  try {
    const mine = path.join(install, 'Shittim-Server', 'my-notes.txt');
    fs.writeFileSync(mine, 'not from any archive');

    const r = installTree(archive, install, { previousManifest: [path.join('Shittim-Server', 'gone.cs')] });
    assert.ok(r.ok);
    assert.equal(r.removed, 0);
    assert.ok(fs.existsSync(mine), 'a file no manifest listed is never a removal candidate');
    assert.ok(fs.existsSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3')));
  } finally { cleanup(root); }
});

test('a removal is undone when the update that made it fails', () => {
  const { root, install, archive } = scratch();
  const stale = path.join(install, 'Shittim-Server', 'OldArenaDataInfo.cs');
  fs.writeFileSync(stale, 'THE OLD FILE');
  const realCopy = fs.copyFileSync;
  let writes = 0;
  fs.copyFileSync = (from, to) => {
    if (String(to).startsWith(install) && !String(to).includes(BACKUP_DIR) && ++writes === 2) {
      const e = new Error('disk full'); e.code = 'ENOSPC'; throw e;
    }
    return realCopy(from, to);
  };
  try {
    const r = installTree(archive, install, { previousManifest: [path.join('Shittim-Server', 'OldArenaDataInfo.cs')] });
    assert.equal(r.ok, false);
    assert.deepEqual(r.rollbackStuck, []);
    assert.equal(fs.readFileSync(stale, 'utf8'), 'THE OLD FILE', 'the removed file comes back');
    assert.deepEqual(versions(install), NAMES.map(() => 'v1'));
  } finally { fs.copyFileSync = realCopy; cleanup(root); }
});

test('the database and config are copied outside the install before an update', () => {
  const { root, install } = scratch();
  try {
    const configDir = path.join(install, 'Shittim-Server', 'bin', 'Debug', 'net10.0', 'Config');
    fs.mkdirSync(configDir, { recursive: true });
    fs.writeFileSync(path.join(configDir, 'Config.json'), '{"ServerConfiguration":{}}');

    const backupRoot = path.join(root, 'userData', 'backups');
    const kept = backupUserData(backupRoot, [path.join(install, 'Shittim-Server', 'shittim.sqlite3'), configDir], 'pre-update-test');

    assert.equal(kept.skipped.length, 0);
    assert.ok(kept.dir.startsWith(backupRoot) && !kept.dir.startsWith(install), 'backups must not live inside the install');
    assert.equal(fs.readFileSync(path.join(kept.dir, 'shittim.sqlite3'), 'utf8'), 'ACCOUNT DATA');
    assert.equal(fs.readFileSync(path.join(kept.dir, 'Config', 'Config.json'), 'utf8'), '{"ServerConfiguration":{}}');
    assert.ok(fs.existsSync(path.join(install, 'Shittim-Server', 'shittim.sqlite3')), 'the original is copied, not moved');
  } finally { cleanup(root); }
});

// Config.json is the gateway key paths, the game version and the SQLCipher key. A save that dies partway leaves none of them, and the page had already said "Configuration saved".
test('a save that fails leaves the previous configuration where it was', () => {
  const { root } = scratch();
  const cfg = path.join(root, 'Config.json');
  fs.writeFileSync(cfg, '{"ServerConfiguration":{"GameVersion":"1.60"}}');
  writeFileAtomic(cfg, '{"ServerConfiguration":{"GameVersion":"1.61"}}');
  assert.equal(fs.readFileSync(cfg, 'utf8'), '{"ServerConfiguration":{"GameVersion":"1.61"}}');

  const realRename = fs.renameSync;
  fs.renameSync = () => { const e = new Error('disk full'); e.code = 'ENOSPC'; throw e; };
  try {
    assert.throws(() => writeFileAtomic(cfg, '{"ServerConfiguration":{}}'), /ENOSPC|disk full/);
    assert.equal(fs.readFileSync(cfg, 'utf8'), '{"ServerConfiguration":{"GameVersion":"1.61"}}');
    assert.ok(!fs.existsSync(cfg + '.tmp'), 'the half-written copy is not left lying next to it');
  } finally { fs.renameSync = realRename; cleanup(root); }
});

test('a missing database is reported rather than failing the backup', () => {
  const { root, install } = scratch();
  try {
    const kept = backupUserData(path.join(root, 'b'), [path.join(install, 'nope.sqlite3')], 'first-run');
    assert.deepEqual(kept.saved, []);
    assert.equal(kept.skipped[0].error, 'ENOENT');
  } finally { cleanup(root); }
});

// Handing a portable exe to electron-updater gets a download it cannot install: there is no NSIS install to patch. Both portable builds and any build packaged without a publish config have to fall back to the download-page notice.
test('only an installed build updates itself in place', () => {
  const resources = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-res-'));
  try {
    fs.writeFileSync(path.join(resources, 'app-update.yml'), 'provider: generic\n');

    assert.equal(selfUpdateMode({ packaged: true, resourcesPath: resources }), 'in-place');
    assert.equal(selfUpdateMode({ packaged: true, resourcesPath: resources, portableDir: 'D:\Portable' }), 'manual');
    assert.equal(selfUpdateMode({ packaged: true, resourcesPath: resources, portableFile: 'D:\Portable\cc.exe' }), 'manual');
    assert.equal(selfUpdateMode({ packaged: false, resourcesPath: resources }), 'dev');

    fs.rmSync(path.join(resources, 'app-update.yml'));
    assert.equal(selfUpdateMode({ packaged: true, resourcesPath: resources }), 'manual');
  } finally { fs.rmSync(resources, { recursive: true, force: true }); }
});

// app-update.yml is written by electron-builder only when a publish target is configured, and the portable artifact only exists if it is a declared target. Dropping either from package.json breaks self-update in a way nothing else notices.
test('the packaging config still produces both an installer and a portable build', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'package.json'), 'utf8'));
  const targets = pkg.build.win.target;

  assert.ok(targets.includes('nsis'), 'no installer target');
  assert.ok(targets.includes('portable'), 'no portable target');
  assert.ok((pkg.build.publish || []).length > 0, 'no publish config, so no app-update.yml and no in-place update');
});

// An install under Program Files, or one made by another account, fails the pre-flight with EPERM on every file, and being told to close the server and try again is the one piece of advice that cannot possibly work.
test('a folder we have no right to write is not blamed on the server being open', () => {
  const advice = blockerAdvice([{ path: 'Shittim-Server/Program.cs', error: 'EPERM' }]);
  assert.match(advice, /write access|user profile/);
  assert.ok(!/close the server/i.test(advice), `still telling them to close the server: ${advice}`);
});

test('a server holding its own binaries still gets told to close the server', () => {
  assert.match(blockerAdvice([{ path: 'Shittim-Server/bin/Debug/net10.0/Shittim-Server.exe', error: 'EBUSY' }]), /close the server/i);
});

// Mixed causes: one unwritable file is enough that closing the server cannot finish the update, so that is the advice.
test('one permission failure among busy files decides the advice', () => {
  const advice = blockerAdvice([{ path: 'a', error: 'EBUSY' }, { path: 'b', error: 'EACCES' }]);
  assert.match(advice, /write access|user profile/);
});

test('a full disk is not a permissions problem', () => {
  assert.match(blockerAdvice([{ path: 'a', error: 'ENOSPC' }]), /out of space/i);
});

// node 24's cpSync aborts the process outright, with nothing to catch, when the source path holds a non-ASCII character, and the step that exists to protect account data is the last one that should be taking the app down instead of reporting.
test('a config directory under a japanese path is backed up', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-i18n-'));
  try {
    const cfg = path.join(root, 'ブルアカ サーバー', 'Shittim-Server', 'Config');
    fs.mkdirSync(path.join(cfg, 'keys'), { recursive: true });
    fs.writeFileSync(path.join(cfg, 'Config.json'), '{"a":1}');
    fs.writeFileSync(path.join(cfg, 'keys', 'gateway.pem'), 'not a real key');

    const r = backupUserData(path.join(root, 'backups'), [cfg], '2026-08-03');

    assert.deepEqual(r.skipped, []);
    assert.equal(fs.readFileSync(path.join(r.dir, 'Config', 'Config.json'), 'utf8'), '{"a":1}');
    assert.equal(fs.readFileSync(path.join(r.dir, 'Config', 'keys', 'gateway.pem'), 'utf8'), 'not a real key', 'and it recursed');
  } finally { cleanup(root); }
});

function zipOf(dir, zip) {
  const q = (s) => `'${s.replace(/'/g, "''")}'`;
  spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command',
    `$ProgressPreference='SilentlyContinue'; Compress-Archive -LiteralPath ${q(dir)} -DestinationPath ${q(zip)} -Force`], { windowsHide: true });
}

// tar converts its argv to the ANSI codepage and then cannot chdir, so a non-ASCII destination always lands on the Expand-Archive fallback - whose -DestinationPath is wildcard-interpreted, which is the second name. A machine with no tar.exe at all takes that fallback for every path, brackets or not.
for (const name of ['ゲーム サーバー', 'ブルアカ [EN]']) {
  test(`an archive extracts into ${name}`, { skip: process.platform !== 'win32' }, async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-zipdest-'));
    try {
      const src = path.join(root, 'archive', 'Shittim-Server');
      fs.mkdirSync(src, { recursive: true });
      fs.writeFileSync(path.join(src, 'Program.cs'), '// v2\n');
      const zip = path.join(root, 'release.zip');
      zipOf(src, zip);

      const dest = path.join(root, name, 'staging');
      await extractZip(zip, dest);

      assert.equal(fs.readFileSync(path.join(dest, 'Shittim-Server', 'Program.cs'), 'utf8'), '// v2\n');
    } finally { cleanup(root); }
  });
}

// build.files is an allowlist that replaces the default **/*, so a new root module is absent from the asar until it is named here and the packaged app dies on require before it can show a window. Neither npm test nor a dev launch sees it, because both run from the source tree where the file is right there.
test('every root module main.js requires is packaged', () => {
  const dir = path.join(__dirname, '..');
  const pkg = JSON.parse(fs.readFileSync(path.join(dir, 'package.json'), 'utf8'));
  const files = pkg.build.files;
  const main = fs.readFileSync(path.join(dir, 'main.js'), 'utf8');

  const wanted = [...main.matchAll(/require\('\.\/([\w.-]+)'\)/g)].map((m) => (m[1].endsWith('.js') ? m[1] : m[1] + '.js'));
  assert.ok(wanted.length >= 4, 'require scan found nothing, so this test is not checking anything');

  for (const name of new Set(wanted)) {
    assert.ok(fs.existsSync(path.join(dir, name)), `main.js requires ${name} but it is not in the source tree`);
    const covered = files.some((f) => f === name || (f.endsWith('/**/*') && name.startsWith(f.slice(0, -5) + '/')));
    assert.ok(covered, `${name} is missing from build.files, so the packaged app cannot require it`);
  }
});
