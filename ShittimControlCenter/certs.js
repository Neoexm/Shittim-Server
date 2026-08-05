'use strict';

const crypto = require('crypto');
const fs = require('fs');
const { execFile } = require('child_process');

// SHA-1 over the DER body, which is what Windows means by a thumbprint. mitmproxy writes PEM under a .cer extension so
// the base64 has to come back out of it first.
function thumbprint(file) {
  const raw = fs.readFileSync(file);
  const text = raw.toString('latin1');
  const der = text.includes('-----BEGIN') ? Buffer.from(text.replace(/-----[^-]+-----/g, '').replace(/\s+/g, ''), 'base64') : raw;
  return crypto.createHash('sha1').update(der).digest('hex').toUpperCase();
}

// Whether this exact CA is one the machine trusts. The file being on disk says nothing about that: trusting it is a
// separate elevated certutil call the user can decline, and a CA regenerated afterwards leaves the trusted one in the
// store under a different thumbprint. Both cases look identical to a check that only asks whether the file is there,
// and both fail every handshake the client makes.
async function trustedRoot(thumb, run = execFile) {
  const ask = (args) => new Promise((resolve) => run('certutil', args, { windowsHide: true }, (err) => resolve(!err)));
  if (await ask(['-verifystore', 'Root', thumb])) return true;
  return ask(['-user', '-verifystore', 'Root', thumb]);
}

module.exports = { thumbprint, trustedRoot };
