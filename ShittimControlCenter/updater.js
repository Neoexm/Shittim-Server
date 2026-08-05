'use strict';

// Archive install for the .NET server source, and the writes around it that must not leave a half-state.
// Kept out of main.js because it has to be runnable without electron: an update that half-applies produces
// a tree whose handlers no longer compile against its models, and the only way to know this stays fixed is
// to drive it from a test.

const fs = require('fs');
const path = require('path');
const { execFile } = require('child_process');

const JOURNAL = '.shittim-update.json';
const BACKUP_DIR = '.shittim-update-backup';

function psQuote(s) { return `'${String(s).replace(/'/g, "''")}'`; }

// Extract a .zip into destDir - no third-party dependency is bundled. Windows prefers the in-box tar.exe (bsdtar, present since Win10 1803): it handles long paths and exits non-zero when ANY entry fails.
// The Expand-Archive fallback runs under $ErrorActionPreference='Stop' for the same reason - by default it reports per-file failures as non-terminating errors and still exits 0, which hands the caller a silently PARTIAL tree.
function extractZip(zipPath, destDir) {
  const run = (cmd, args) => new Promise((resolve, reject) => {
    execFile(cmd, args, { windowsHide: true, maxBuffer: 64 * 1024 * 1024 },
      (err, _so, se) => err ? reject(new Error((se || '').toString().trim() || err.message)) : resolve());
  });
  fs.mkdirSync(destDir, { recursive: true });
  if (process.platform !== 'win32') return run('unzip', ['-o', zipPath, '-d', destDir]);
  // -DestinationPath has no -Literal counterpart and is wildcard-interpreted, so an install under a folder like "Blue Archive [EN]" fails with "already exists" and extracts nothing, while "Games [SSD" fails as an invalid pattern. Escaping is the documented way out. tar needs none of this but loses non-ASCII in its argv - it converts to the ANSI codepage and then cannot chdir - so a Japanese path arrives here having already fallen through.
  const strictPs = `$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; ` +
    `try { Expand-Archive -LiteralPath ${psQuote(zipPath)} -DestinationPath ([System.Management.Automation.WildcardPattern]::Escape(${psQuote(destDir)})) -Force } ` +
    `catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }`;
  const psFallback = () => run('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', strictPs]);
  const tar = path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'tar.exe');
  if (!fs.existsSync(tar)) return psFallback();
  return run(tar, ['-xf', zipPath, '-C', destDir]).catch(psFallback);
}

// Files whose on-disk copy must survive an update merge: the gateway keypair is what the user's patched client metadata expects, so silently replacing it with the repo copy would strand the install until the next metadata re-patch.
const MERGE_PRESERVE = [/^Shittim-Server[\\/]config[\\/].*\.pem$/i];

function listFiles(root, rel, out) {
  for (const ent of fs.readdirSync(rel ? path.join(root, rel) : root, { withFileTypes: true })) {
    const entRel = rel ? path.join(rel, ent.name) : ent.name;
    if (ent.isDirectory()) listFiles(root, entRel, out);
    else if (ent.isFile()) out.push(entRel); // release archives carry no symlinks
  }
  return out;
}

// Can this file be replaced right now? A running server holds its own binaries with FILE_SHARE_NONE,
// which is the ordinary way an update used to strand a tree half-copied. Opening for write finds that
// before anything has been touched.
function blockedBy(dest) {
  let fd;
  try { fd = fs.openSync(dest, 'r+'); }
  catch (e) { return String(e.code || e.message); }
  finally { if (fd !== undefined) { try { fs.closeSync(fd); } catch { /* ignore */ } } }
  return null;
}

// EBUSY is the server still holding its own binaries and closing it is the whole answer. EPERM/EACCES is a folder this
// account cannot write - an install under Program Files, or one made by a different user - which closing things does not
// fix, and being told to close the server and try again is how someone ends up running the update five times.
function blockerAdvice(blockers) {
  const codes = new Set(blockers.map((b) => String(b.error)));
  if (codes.has('EPERM') || codes.has('EACCES')) {
    return 'Your account cannot write to that folder, so closing the server will not help. Either move the install somewhere under your own user profile, or give your account write access to it and update again.';
  }
  if (codes.has('ENOSPC')) return 'The drive is out of space. Free some up and update again.';
  if (codes.has('EROFS')) return 'That drive is mounted read-only.';
  return 'Close the server and anything else using the folder, then run the update again.';
}

function undo(journal) {
  const restored = [];
  const stuck = [];
  for (const rel of [...journal.replaced, ...(journal.removed || [])]) {
    try {
      fs.mkdirSync(path.dirname(path.join(journal.destRoot, rel)), { recursive: true });
      fs.copyFileSync(path.join(journal.backupDir, rel), path.join(journal.destRoot, rel));
      restored.push(rel);
    } catch (e) { stuck.push({ path: rel, error: String(e.code || e.message) }); }
  }
  for (const rel of journal.created) {
    try { fs.rmSync(path.join(journal.destRoot, rel), { force: true }); restored.push(rel); }
    catch (e) { stuck.push({ path: rel, error: String(e.code || e.message) }); }
  }
  return { restored: restored.length, stuck };
}

function discard(journal) {
  try { fs.rmSync(journal.backupDir, { recursive: true, force: true }); } catch { /* ignore */ }
  try { fs.rmSync(path.join(journal.destRoot, JOURNAL), { force: true }); } catch { /* ignore */ }
}

// Merge the extracted archive over the install so the tree ends up entirely on the new version or
// entirely on the old one. Every file the archive would overwrite is copied aside first and the list
// is written to disk before the first write, so a failure - or the app being killed mid-merge - can be
// undone. Only paths the archive itself carries are ever touched; the database, Config/ and build
// output are not in the archive and are never candidates for rollback.
function installTree(srcRoot, destRoot, { onProgress, previousManifest } = {}) {
  const manifest = listFiles(srcRoot, '', []);
  const carries = new Set(manifest);
  const files = manifest.filter((rel) =>
    !(MERGE_PRESERVE.some((re) => re.test(rel)) && fs.existsSync(path.join(destRoot, rel))));

  // Only files a previous archive put here are candidates for removal. A rename upstream otherwise leaves
  // the old .cs behind, the SDK globs both, and the build stops on CS0101. The database, Config/ and build
  // output have never been in a manifest, so nothing here can reach them.
  const obsolete = (previousManifest || []).filter((rel) => !carries.has(rel) && fs.existsSync(path.join(destRoot, rel)));

  const blockers = [];
  for (const rel of [...files, ...obsolete]) {
    const dest = path.join(destRoot, rel);
    if (!fs.existsSync(dest)) continue;
    const why = blockedBy(dest);
    if (why) blockers.push({ path: rel, error: why });
  }
  if (blockers.length) {
    return { ok: false, copied: 0, removed: 0, manifest, blockers, rolledBack: false, changed: false };
  }

  const backupDir = path.join(destRoot, BACKUP_DIR);
  fs.rmSync(backupDir, { recursive: true, force: true });
  const journal = { destRoot, backupDir, replaced: [], created: [], removed: [], startedAt: new Date().toISOString() };
  const journalPath = path.join(destRoot, JOURNAL);

  let copied = 0;
  try {
    fs.mkdirSync(backupDir, { recursive: true });
    fs.writeFileSync(journalPath, JSON.stringify(journal, null, 2));

    for (const rel of obsolete) {
      const keep = path.join(backupDir, rel);
      fs.mkdirSync(path.dirname(keep), { recursive: true });
      fs.copyFileSync(path.join(destRoot, rel), keep);
      journal.removed.push(rel);
      fs.writeFileSync(journalPath, JSON.stringify(journal, null, 2));
      fs.rmSync(path.join(destRoot, rel), { force: true });
    }

    for (const rel of files) {
      const dest = path.join(destRoot, rel);
      fs.mkdirSync(path.dirname(dest), { recursive: true });

      if (fs.existsSync(dest)) {
        const keep = path.join(backupDir, rel);
        fs.mkdirSync(path.dirname(keep), { recursive: true });
        fs.copyFileSync(dest, keep);
        journal.replaced.push(rel);
      } else {
        journal.created.push(rel);
      }
      // Re-written per file: a kill between the copy-aside and the overwrite must still leave a journal that names the file.
      fs.writeFileSync(journalPath, JSON.stringify(journal, null, 2));

      fs.copyFileSync(path.join(srcRoot, rel), dest);
      copied++;
      if (onProgress) onProgress(copied, files.length);
    }
  } catch (e) {
    const rollback = undo(journal);
    discard(journal);
    return {
      ok: false, copied, removed: journal.removed.length, manifest,
      blockers: [{ path: '(during copy)', error: String(e.code || e.message) }],
      rolledBack: rollback.stuck.length === 0, rollbackStuck: rollback.stuck, changed: rollback.stuck.length > 0,
    };
  }

  discard(journal);
  return { ok: true, copied, removed: journal.removed.length, manifest, blockers: [], rolledBack: false, changed: true };
}

// An update that was killed part-way leaves its journal behind. Roll it back at startup so the install
// is the version it was before, rather than a tree that no longer compiles.
function resumeInterruptedInstall(destRoot) {
  let journal;
  try { journal = JSON.parse(fs.readFileSync(path.join(destRoot, JOURNAL), 'utf8')); }
  catch { return null; }
  if (!journal || !Array.isArray(journal.replaced) || !Array.isArray(journal.created)) return null;

  // Trust this tree's own location, not the path recorded by whoever wrote the journal - the folder may have moved.
  journal.destRoot = destRoot;
  journal.backupDir = path.join(destRoot, BACKUP_DIR);

  const rollback = undo(journal);
  discard(journal);
  return { restored: rollback.restored, stuck: rollback.stuck, startedAt: journal.startedAt || null };
}

// Copy the database and Config/ aside before an update or a migration touches the tree. Backups go
// somewhere outside the install so an install-wide operation cannot take them with it, and the old
// ones are kept - nothing here is a recovery step that deletes account data.
function backupUserData(backupRoot, files, stamp) {
  const dir = path.join(backupRoot, stamp);
  const saved = [];
  const skipped = [];
  fs.mkdirSync(dir, { recursive: true });
  for (const src of files) {
    if (!fs.existsSync(src)) { skipped.push({ path: src, error: 'ENOENT' }); continue; }
    const dest = path.join(dir, path.basename(src));
    try {
      // Not cpSync: on node 24 it aborts the process outright - no exception to catch - when the source path contains a
      // non-ASCII character, which for a backup means the one operation that must never fail quietly kills the app instead.
      if (fs.statSync(src).isDirectory()) {
        for (const rel of listFiles(src, '', [])) {
          const to = path.join(dest, rel);
          fs.mkdirSync(path.dirname(to), { recursive: true });
          fs.copyFileSync(path.join(src, rel), to);
        }
      } else fs.copyFileSync(src, dest);
      saved.push(dest);
    } catch (e) {
      skipped.push({ path: src, error: String(e.code || e.message) });
    }
  }
  return { dir, saved, skipped };
}

// Replace a file's contents with no window in which it is truncated or half-written. Config.json carries the
// gateway key paths, the game version and the SQLCipher key, and a plain writeFileSync that dies partway through
// leaves the user with none of them.
function writeFileAtomic(dest, text) {
  const tmp = dest + '.tmp';
  try {
    fs.writeFileSync(tmp, text);
    fs.renameSync(tmp, dest);
  } catch (e) {
    try { fs.rmSync(tmp, { force: true }); } catch { /* ignore */ }
    throw e;
  }
}

// How the Control Center can update itself. A portable exe has no install for electron-updater to patch, and a
// build packaged without a publish config carries no app-update.yml - either one silently does nothing if it is
// handed to electron-updater, so both fall back to the version check plus a link to the releases page.
function selfUpdateMode({ packaged, portableDir, portableFile, resourcesPath }) {
  if (!packaged) return 'dev';
  if (portableDir || portableFile) return 'manual';
  try { return fs.existsSync(path.join(resourcesPath, 'app-update.yml')) ? 'in-place' : 'manual'; }
  catch { return 'manual'; }
}

module.exports = {
  JOURNAL, BACKUP_DIR, MERGE_PRESERVE,
  extractZip, installTree, resumeInterruptedInstall, backupUserData, writeFileAtomic, selfUpdateMode, blockerAdvice,
};
