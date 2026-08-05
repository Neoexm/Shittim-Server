'use strict';

// Where the server project and everything derived from it lives. Split out of main.js for the same reason updater.js was: it needs no electron, so the resolution rules can be driven from a test.

const fs = require('fs');
const path = require('path');

// A configured folder that has gone missing - unplugged drive, renamed folder, network share down - stays the answer and is flagged, rather than being quietly swapped for the fallback.
// In a packaged build appDir is resources/app.asar, so the fallback resolves to the Control Center's own resources directory and the user gets offered a fresh download into Documents while their database sits on the drive that is not plugged in.
function resolveRepoRoot(settings, appDir) {
  const configured = settings && settings.repoRoot ? settings.repoRoot : null;
  if (configured) {
    return { repoRoot: configured, configured, missing: !fs.existsSync(path.join(configured, 'Shittim-Server')) };
  }
  // ShittimControlCenter sits directly inside the repo.
  return { repoRoot: path.resolve(appDir, '..'), configured: null, missing: false };
}

function firstExisting(paths) {
  for (const p of paths) if (p && fs.existsSync(p)) return p;
  return null;
}

function pathsFor(repoRoot, platform = process.platform) {
  const serverDir = path.join(repoRoot, 'Shittim-Server');
  const csproj = path.join(serverDir, 'Shittim-Server.csproj');

  // dotnet publishes the apphost without a suffix everywhere except Windows. Looking only for Shittim-Server.exe finds
  // nothing on Linux, and then every path below it - config, gacha overrides, the gateway key directory - is derived
  // from a build directory that was only ever a guess.
  const exeName = platform === 'win32' ? 'Shittim-Server.exe' : 'Shittim-Server';
  const debugExe = path.join(serverDir, 'bin', 'Debug', 'net10.0', exeName);
  const releaseExe = path.join(serverDir, 'bin', 'Release', 'net10.0', exeName);
  const exePath = firstExisting([debugExe, releaseExe]);

  // The server reads its config from <exeBaseDir>/Config/Config.json and the gacha overrides from <exeBaseDir>/../gacha_config.json.
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

// Find the directory holding mitmweb.exe under `root` (the install root, or one of its sub-dirs like bin/), searching up to `depth` levels deep.
function findMitmBinDir(root, depth = 2) {
  const hit = (d) => fs.existsSync(path.join(d, 'mitmweb.exe'));
  if (hit(root)) return root;
  if (depth <= 0) return null;
  try {
    for (const n of fs.readdirSync(root)) {
      const sub = path.join(root, n);
      try {
        if (fs.statSync(sub).isDirectory()) {
          const found = findMitmBinDir(sub, depth - 1);
          if (found) return found;
        }
      } catch { /* ignore */ }
    }
  } catch { /* ignore */ }
  return null;
}

// Resolve a mitmproxy tool (mitmdump/mitmweb) from the install dir, else PATH. Walking PATH here rather than leaving the bare name to the spawn is what lets the environment check say WHICH mitmweb it found - a pip install under Scripts\ and our Program Files one are both normal and they are not the same program. The bare name is still the last resort, because an installer that has just finished writing to PATH has not necessarily reached this process's copy of it.
function mitmExe(name, installDir, platform = process.platform, pathVar = process.env.PATH || process.env.Path || '') {
  const dir = installDir ? findMitmBinDir(installDir) : null;
  const exe = platform === 'win32' ? `${name}.exe` : name;
  const onPath = pathVar.split(path.delimiter).filter(Boolean).map((d) => path.join(d, exe));
  return firstExisting([dir ? path.join(dir, exe) : null, ...onPath]) || exe;
}

// Our own install goes to perUserDir and is found by existence, so this is really about the other two: an SDK the user installed themselves before we ever ran, and one installed system-wide while we were running. The second is why PATH alone is not enough - our copy of it was taken when the process started.
function dotnetExe(perUserDir, platform = process.platform, pathVar = process.env.PATH || process.env.Path || '', programFiles = process.env.ProgramFiles) {
  const exe = platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const ours = perUserDir ? path.join(perUserDir, exe) : null;
  if (ours && fs.existsSync(ours)) return { cmd: ours, root: perUserDir };

  const dirs = pathVar.split(path.delimiter).filter(Boolean);
  if (platform === 'win32' && programFiles) dirs.push(path.join(programFiles, 'dotnet'));
  else if (platform !== 'win32') dirs.push('/usr/share/dotnet', '/usr/lib/dotnet');

  const found = firstExisting(dirs.map((d) => path.join(d, exe)));
  return { cmd: found || exe, root: null };
}

function declaredVersion(repoRoot) {
  try {
    const m = fs.readFileSync(path.join(repoRoot, 'Directory.Build.props'), 'utf8').match(/<Version>([^<]+)<\/Version>/);
    return m ? m[1].trim() : null;
  } catch { return null; }
}

// The version the exe next to it was compiled from. deps.json names the project as "Shittim-Server/<version>", which saves reading the PE version resource.
function builtVersion(exePath) {
  if (!exePath) return null;
  try {
    const deps = JSON.parse(fs.readFileSync(path.join(path.dirname(exePath), 'Shittim-Server.deps.json'), 'utf8'));
    for (const target of Object.values(deps.targets || {})) {
      const key = Object.keys(target).find((k) => k.startsWith('Shittim-Server/'));
      if (key) return key.slice('Shittim-Server/'.length);
    }
  } catch { /* no build, or a layout we do not know */ }
  return null;
}

// bin/ is not in the release archive, so an update replaces the sources, the patch definitions and the schema
// while leaving the compiled server exactly as it was. Launching that binary runs the old server against the new
// everything else, and the update looks like it did nothing.
function componentVersions(repoRoot, appVersion, marker) {
  const exePath = pathsFor(repoRoot).exePath;
  const sources = declaredVersion(repoRoot);
  const built = builtVersion(exePath);

  let buildStale = null;
  if (sources && built && sources !== built) {
    buildStale = `The server files here are ${sources}, but the built server is ${built}. Rebuild before starting it.`;
  } else if (exePath && marker && marker.updatedAt) {
    let mtime = null;
    try { mtime = fs.statSync(exePath).mtimeMs; } catch { /* it was there a moment ago */ }
    if (mtime != null && mtime < Date.parse(marker.updatedAt)) {
      buildStale = `The server was last built before the update to ${marker.shortSha || marker.sha || 'this version'}. Rebuild before starting it.`;
    }
  }

  // Not a block: a control center pointed at an older tree still drives it, and the only cure is updating one of them.
  const releaseSkew = appVersion && sources && appVersion !== sources
    ? `This control center is ${appVersion} and the server files are ${sources}.`
    : null;

  return { controlCenter: appVersion || null, sources, built, buildStale, releaseSkew };
}

module.exports = { resolveRepoRoot, pathsFor, firstExisting, componentVersions, declaredVersion, builtVersion, findMitmBinDir, mitmExe, dotnetExe };
