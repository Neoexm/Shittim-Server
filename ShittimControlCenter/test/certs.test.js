'use strict';

const test = require('node:test');
const assert = require('node:assert');
const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');

const { thumbprint, trustedRoot } = require('../certs');

// A stand-in for what mitmproxy leaves in ~/.mitmproxy: DER bytes wrapped in PEM under a .cer name. Nothing here parses
// the certificate, so the bytes only have to be stable.
function fakeCa(der) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'scc-ca-'));
  const file = path.join(dir, 'mitmproxy-ca-cert.cer');
  const b64 = der.toString('base64').replace(/(.{64})/g, '$1\n');
  fs.writeFileSync(file, `-----BEGIN CERTIFICATE-----\n${b64}\n-----END CERTIFICATE-----\n`);
  return { dir, file };
}

test('the thumbprint of a PEM file is the sha1 of the certificate inside it', () => {
  const der = crypto.randomBytes(600);
  const { dir, file } = fakeCa(der);
  try {
    assert.equal(thumbprint(file), crypto.createHash('sha1').update(der).digest('hex').toUpperCase());
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

// mitmproxy regenerates its CA whenever ~/.mitmproxy is wiped. The file is back, the card was happy with it, and the
// store still holds the CA from before - so the thumbprint has to be what identifies it, not the path.
test('a regenerated CA does not have the thumbprint of the one that was trusted', () => {
  const der = crypto.randomBytes(600);
  const first = fakeCa(der);
  const second = fakeCa(Buffer.concat([der.subarray(0, 599), Buffer.from([der[599] ^ 1])]));
  try {
    assert.notEqual(thumbprint(first.file), thumbprint(second.file));
  } finally {
    fs.rmSync(first.dir, { recursive: true, force: true });
    fs.rmSync(second.dir, { recursive: true, force: true });
  }
});

test('a CA the machine root store knows about is trusted', async () => {
  const asked = [];
  const run = (cmd, args, opts, cb) => { asked.push([cmd, ...args].join(' ')); cb(null, '', ''); };
  assert.equal(await trustedRoot('AABB', run), true);
  assert.deepEqual(asked, ['certutil -verifystore Root AABB']);
});

// certutil answers 0x800b010a on a thumbprint that is not in the store, which is what a declined elevation prompt
// leaves behind: the file written, the trust step never run.
test('a CA that was never trusted is not trusted just because the file is there', async () => {
  const asked = [];
  const run = (cmd, args, opts, cb) => { asked.push(args.join(' ')); cb(new Error('exit 0x800b010a'), '', ''); };
  assert.equal(await trustedRoot('AABB', run), false);
  assert.equal(asked.length, 2, 'the per-user store is asked as well before giving up');
});

test('a CA trusted per-user rather than machine-wide still counts', async () => {
  const run = (cmd, args, opts, cb) => (args[0] === '-user' ? cb(null, '', '') : cb(new Error('not in machine store'), '', ''));
  assert.equal(await trustedRoot('AABB', run), true);
});

test('the console window stays hidden', async () => {
  let seen = null;
  await trustedRoot('AABB', (cmd, args, opts, cb) => { seen = opts; cb(null, '', ''); });
  assert.equal(seen.windowsHide, true);
});
