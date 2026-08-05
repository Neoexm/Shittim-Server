'use strict';

const { execFile } = require('child_process');
const { StringDecoder } = require('string_decoder');

const WIN = process.platform === 'win32';

function alive(pid, run) {
  if (!pid) return Promise.resolve(false);
  if (!WIN) {
    try { process.kill(pid, 0); return Promise.resolve(true); } catch { return Promise.resolve(false); }
  }
  return new Promise((resolve) => {
    run('tasklist', ['/FI', `PID eq ${pid}`, '/NH', '/FO', 'CSV'], (err, stdout) => resolve(!err && String(stdout || '').includes(`"${pid}"`)));
  });
}

// Always by pid and never by image name: /IM dotnet.exe would take out whatever else on the machine happens to be a
// dotnet process, and this runs on the user's own desktop. /T because `dotnet run` holds the actual server as a
// grandchild, so killing what we spawned leaves the thing that owns port 5000 behind.
// taskkill can also simply refuse - an elevated server cannot be killed by a Control Center running as the user - and
// its exit code arrives long before the process is gone either way, so the answer comes from looking afterwards.
async function killTree(pid, opts = {}) {
  const run = opts.run || execFile;
  const tries = opts.tries || 12;
  const gap = opts.gap == null ? 250 : opts.gap;
  const sleep = opts.sleep || ((ms) => new Promise((r) => setTimeout(r, ms)));

  if (!(await alive(pid, run))) return { ok: true, already: true };

  const said = await new Promise((resolve) => {
    if (!WIN) {
      try { process.kill(pid, 'SIGTERM'); resolve(''); } catch (e) { resolve(String(e.message || e)); }
      return;
    }
    run('taskkill', ['/pid', String(pid), '/T', '/F'], (err, stdout, stderr) => resolve(`${stdout || ''}${stderr || ''}`.trim()));
  });

  for (let i = 0; i < tries; i++) {
    if (!(await alive(pid, run))) return { ok: true };
    await sleep(gap);
  }
  // Access is denied from taskkill means the target is running with rights we do not have, which is what a server
  // started from an elevated shell looks like. There is nothing to retry and nothing in here that fixes it, so the
  // message has to send the user somewhere that does rather than repeating that the pid is still running.
  if (/access is denied/i.test(said)) {
    return { ok: false, error: `${said} - it is running with administrator rights this Control Center does not have. End it from Task Manager, or start the Control Center as administrator.` };
  }
  return { ok: false, error: said || `pid ${pid} is still running` };
}

// A decoder and a buffer per stream, not chunk.toString() per chunk: a child writing a path with non-ASCII in it hands us a multi-byte sequence that lands across a chunk boundary often enough to matter, and decoding each half on its own turns it into replacement characters that are then in the log forever. Sharing one buffer between stdout and stderr splices their partial lines together the same way.
function streamLines(child, onLine) {
  const reader = () => {
    const dec = new StringDecoder('utf8');
    let buf = '';
    return (chunk) => {
      buf += dec.write(chunk);
      let idx;
      while ((idx = buf.indexOf('\n')) >= 0) {
        const line = buf.slice(0, idx).replace(/\r$/, '');
        buf = buf.slice(idx + 1);
        if (line.length) onLine(line);
      }
    };
  };
  if (child.stdout) child.stdout.on('data', reader());
  if (child.stderr) child.stderr.on('data', reader());
}

module.exports = { alive, killTree, streamLines };
