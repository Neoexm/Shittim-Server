'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');

const { findMitmBinDir } = require('../paths');

// The body of installMitmproxy, which cannot be require()d - main.js pulls in electron. main.js is CRLF, so the closing brace has to be matched loosely or this quietly returns the rest of the file and every assertion below finds whatever it was looking for somewhere else.
function installMitmSource() {
  const src = fs.readFileSync(path.join(__dirname, '..', 'main.js'), 'utf8');
  const from = src.indexOf('async function installMitmproxy()');
  assert.ok(from > 0, 'installMitmproxy has been renamed and this test is reading nothing');
  const end = src.slice(from).search(/\r?\n\}\r?\n/);
  assert.ok(end > 0, 'no end of function found');
  const body = src.slice(from, from + end);
  assert.ok(body.split('\n').length < 60, `read ${body.split('\n').length} lines, which is not one function`);
  return body;
}

// Inno Setup takes /VERYSILENT. Written --verysilent it is not an unknown-switch error, it is an unrecognised argument that Inno ignores, so the installer comes up as a normal GUI wizard behind the -Wait and the setup step hangs on a window nobody can see.
test('the mitmproxy installer is driven with Inno switches and not GNU ones', () => {
  const body = installMitmSource();
  const args = body.match(/const innoArgs = `([^`]+)`/);
  assert.ok(args, 'no installer argument list found');

  for (const flag of ['/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/DIR=']) {
    assert.ok(args[1].includes(flag), `${flag} is not being passed`);
  }
  assert.ok(!/--\w/.test(args[1]), `double-dash switch in the Inno arguments: ${args[1]}`);
});

// A silent installer that the user cancelled at the UAC prompt, or that failed part way, still leaves the step looking like it ran. Both halves are needed: the exit code, and then whether the thing it was supposed to install is there.
test('a finished installer is not taken as a working mitmproxy', () => {
  const body = installMitmSource();
  assert.match(body, /if \(!r\.ok\)[\s\S]*?return \{ ok: false/, 'the installer exit code is not checked');
  assert.match(body, /findMitmBinDir\(dir\);\s*\n\s*if \(!binDir\) throw/, 'the installed executable is never looked for');
});

// cmd.exe still expands %NAME% inside the double quotes, so a tool under a folder with a percent sign in it comes back as "The filename, directory name, or volume label syntax is incorrect" and the SDK is reported missing while it is there.
test('the environment checks run their tools without a shell', () => {
  const src = fs.readFileSync(path.join(__dirname, '..', 'main.js'), 'utf8');
  const imported = src.match(/const \{([^}]*)\} = require\('child_process'\)/);
  assert.ok(imported, 'the child_process import has moved and this test is reading nothing');
  assert.ok(!imported[1].split(',').map((s) => s.trim()).includes('exec'), 'exec is back in main.js');

  for (const m of src.matchAll(/\bexec(?:Check|Out)\(([^\n]*)/g)) {
    if (/^cmd, args/.test(m[1])) continue; // the two definitions
    assert.match(m[1], /,\s*\[/, `a version check is building a command string again: ${m[1].trim()}`);
  }
});

test('an install root the installer left empty resolves to nothing', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-inst-'));
  try {
    fs.mkdirSync(path.join(root, 'mitmproxy', 'bin'), { recursive: true });
    fs.writeFileSync(path.join(root, 'mitmproxy', 'unins000.exe'), '');

    assert.equal(findMitmBinDir(path.join(root, 'mitmproxy')), null);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

// Deeper than the two levels the search walks, so it is deliberately not found rather than being found slowly across a Program Files that may hold a lot of directories.
test('the search does not wander off down the whole install root', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-inst-'));
  try {
    const deep = path.join(root, 'mitmproxy', 'a', 'b', 'c');
    fs.mkdirSync(deep, { recursive: true });
    fs.writeFileSync(path.join(deep, 'mitmweb.exe'), '');

    assert.equal(findMitmBinDir(path.join(root, 'mitmproxy')), null);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});
