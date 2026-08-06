'use strict';

const test = require('node:test');
const assert = require('node:assert');
const { spawn, execFile } = require('child_process');
const { PassThrough } = require('stream');

const { alive, killTree, streamLines } = require('../procs');

// tasklist/taskkill stand-in. `living` is the set of pids the fake machine still has, and every call is recorded so a test can assert on the argv taskkill was actually given.
function fakeShell(living, onKill) {
  const calls = [];
  const run = (cmd, args, cb) => {
    calls.push([cmd, ...args].join(' '));
    if (cmd === 'tasklist') {
      const pid = args[1].split(' ').pop();
      return void cb(null, living.has(Number(pid)) ? `"node.exe","${pid}","Console","1","9,000 K"\r\n` : 'INFO: No tasks are running which match the specified criteria.\r\n');
    }
    const pid = Number(args[1]);
    const refused = onKill ? onKill(pid) : null;
    if (refused) return void cb(new Error('exit 255'), '', refused);
    living.delete(pid);
    cb(null, `SUCCESS: The process with PID ${pid} has been terminated.\r\n`, '');
  };
  return { run, calls };
}

const nap = () => Promise.resolve();

test('a pid that is already gone needs no kill at all', async () => {
  const { run, calls } = fakeShell(new Set());
  const r = await killTree(4242, { run, sleep: nap });
  assert.deepEqual(r, { ok: true, already: true });
  assert.ok(!calls.some((c) => c.startsWith('taskkill')), 'nothing was killed');
});

test('a kill that lands is reported once the pid has actually gone', async () => {
  const { run } = fakeShell(new Set([4242]));
  assert.deepEqual(await killTree(4242, { run, sleep: nap }), { ok: true });
});

// The whole point: taskkill's exit code arrives long before the process does, and an elevated server refuses outright. Saying "stopped" here is what leaves the next launch unable to bind the port.
test('a kill the machine refuses is a failure and carries what it said', async () => {
  const { run } = fakeShell(new Set([4242]), () => 'ERROR: The process with PID 4242 could not be terminated. Reason: Access is denied.');
  const r = await killTree(4242, { run, tries: 3, sleep: nap });
  assert.equal(r.ok, false);
  assert.match(r.error, /Access is denied/);
});

// "could not stop pid 4242 - Access is denied" is true and useless. The only thing that gets the port back is Task Manager or an elevated relaunch, and neither is guessable from the errno.
test('a process running with rights we do not have says what to do about it', async () => {
  const { run } = fakeShell(new Set([4242]), () => 'ERROR: The process with PID 4242 could not be terminated. Reason: Access is denied.');
  const r = await killTree(4242, { run, tries: 2, sleep: nap });
  assert.match(r.error, /administrator rights/);
  assert.match(r.error, /Task Manager/);
});

test('a kill that just takes too long is not blamed on privileges', async () => {
  const { run } = fakeShell(new Set([4242]), () => 'still shutting down');
  const r = await killTree(4242, { run, tries: 2, sleep: nap });
  assert.ok(!/administrator/i.test(r.error), `wrong advice for a slow shutdown: ${r.error}`);
});

test('the pid is given time to die rather than being read back immediately', async () => {
  let left = 4;
  const living = new Set([4242]);
  const { run } = fakeShell(living, () => 'still shutting down');
  const sleep = () => { if (--left <= 0) living.delete(4242); return Promise.resolve(); };
  assert.deepEqual(await killTree(4242, { run, tries: 10, sleep }), { ok: true });
});

test('kills go by pid and never by image name', async () => {
  const { run, calls } = fakeShell(new Set([4242]));
  await killTree(4242, { run, sleep: nap });
  const kill = calls.find((c) => c.startsWith('taskkill'));
  assert.equal(kill, 'taskkill /pid 4242 /T /F');
  assert.ok(!calls.some((c) => /\/IM/i.test(c)), 'no /IM anywhere');
});

function fakeChild() {
  const child = { stdout: new PassThrough(), stderr: new PassThrough() };
  const lines = [];
  streamLines(child, (l) => lines.push(l));
  return { child, lines };
}

// A server logging a path with kanji or Cyrillic in it emits a 3-byte sequence that a 64KB pipe read will eventually split, and that is not rare - it is once per couple of thousand lines, forever, on the machines that have such paths.
test('a character split across two reads survives', () => {
  const { child, lines } = fakeChild();
  const bytes = Buffer.from('reading D:\\ゲーム\\config\n', 'utf8');
  const cut = bytes.indexOf(Buffer.from('ゲ', 'utf8')) + 1;

  child.stdout.write(bytes.subarray(0, cut));
  child.stdout.write(bytes.subarray(cut));

  assert.deepEqual(lines, ['reading D:\\ゲーム\\config']);
  assert.ok(!lines[0].includes('\uFFFD'), 'and did not arrive as replacement characters');
});

test('stdout and stderr do not splice their half-written lines together', () => {
  const { child, lines } = fakeChild();
  child.stdout.write('now listening on ');
  child.stderr.write('warn: ');
  child.stdout.write('port 5000\n');
  child.stderr.write('no certificate\n');

  assert.deepEqual(lines, ['now listening on port 5000', 'warn: no certificate']);
});

test('CRLF from a windows child is not left on the line', () => {
  const { child, lines } = fakeChild();
  child.stdout.write('one\r\ntwo\r\n\r\nthree\r\n');
  assert.deepEqual(lines, ['one', 'two', 'three']);
});

// `dotnet run` holds the server as a grandchild, so this is the shape that matters: kill what we spawned, and check that the process which actually owns the port went with it.
test('a grandchild dies with the child we spawned', { skip: process.platform !== 'win32' }, async () => {
  const inner = 'setInterval(() => {}, 1000);';
  const outer = `const {spawn}=require('child_process');const c=spawn(process.execPath,['-e',${JSON.stringify(inner)}],{stdio:'ignore'});console.log(c.pid);setInterval(()=>{},1000);`;
  const child = spawn(process.execPath, ['-e', outer], { stdio: ['ignore', 'pipe', 'ignore'] });
  const grandPid = await new Promise((r) => child.stdout.once('data', (d) => r(parseInt(String(d).trim(), 10))));

  assert.ok(await alive(grandPid, execFile), 'the grandchild is up before we start');
  const r = await killTree(child.pid);

  assert.deepEqual(r, { ok: true });
  assert.equal(await alive(child.pid, execFile), false);
  assert.equal(await alive(grandPid, execFile), false, 'the process that would hold the port is gone too');
});
