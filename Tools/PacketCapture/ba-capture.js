'use strict';
/* ============================================================================
   Blue Archive - decrypted response capture

   Reads server responses out of the client's memory AFTER it has decrypted
   them, and appends them to a text file.

   The payload is encrypted on the wire, so a proxy only ever yields ciphertext.
   The plaintext has to be in managed memory for the client to deserialize it,
   so hooking where a decrypt returns gets it without touching the crypto.

   Self-contained on purpose: `frida -l` does not resolve require(), so there
   is no module to bundle and no frida-compile step.

   Usage
     Discovery (no hooks, writes a candidate list you can eyeball):
       set BA_MODE=discover && frida -n BlueArchive.exe -l ba-capture.js

     Capture (default):
       frida -n BlueArchive.exe -l ba-capture.js

     Spawn instead of attach, so early login traffic is not missed:
       frida -f "F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive.exe" -l ba-capture.js

   Output lands in ..\..\captures\
   ========================================================================== */

const CFG = {
  mode: 'capture',          // 'capture' | 'discover'  (env BA_MODE overrides)
  outDir: 'C:\\Users\\tomda\\Documents\\Shittim-Server\\captures',
  module: 'GameAssembly.dll',

  hexDump: true,            // include a hex+ascii pane per capture
  maxHexBytes: 4096,        // cap the pane; full length is always recorded
  maxTextChars: 8000,
  minLength: 2,             // ignore trivial buffers
  dedupeWindowMs: 60,       // identical buffer inside this window logs once
  logToConsole: true,       // one-line summary per capture in the terminal
};

try {
  const m = Process.env && Process.env.BA_MODE;
  if (m) CFG.mode = m.toLowerCase();
} catch (e) { /* env unavailable - keep default */ }

// Only these (class, method) shapes get hooked. Narrow on purpose: hooking every method named "Deserialize" in a 215 MB binary would attach thousands of probes and stutter the game.
const TARGETS = [
  // crypto boundaries - the return value is the plaintext
  { cls: /crypt|cipher|aes|rijndael|xor|security|obfusc/i, m: /^decrypt/i, capture: 'ret', kind: 'decrypt' },
  { cls: /packet|protocol|session|gateway|network/i, m: /decrypt/i, capture: 'ret', kind: 'decrypt' },

  // serialization boundaries - the incoming buffer is already plaintext
  { cls: /messagepack|serializer|formatter/i, m: /^deserialize$/i, capture: 'arg0', kind: 'deserialize' },
  { cls: /packet|protocol/i, m: /^(deserialize|unpack|fromjson|parse)$/i, capture: 'arg0', kind: 'deserialize' },

  // receive path - whatever the transport just handed upward
  { cls: /packet|protocol|network|session|transport|webrequest/i,
    m: /^(onreceive|onresponse|handleresponse|parseresponse|processpacket|setresponse)$/i,
    capture: 'arg0', kind: 'receive' },
];

const mod = Process.getModuleByName(CFG.module);

function api(name, ret, args) {
  const addr = mod.findExportByName(name);
  if (!addr) throw new Error('export not found: ' + name);
  return new NativeFunction(addr, ret, args);
}

let il2cpp;
try {
  il2cpp = {
    domain_get:             api('il2cpp_domain_get', 'pointer', []),
    domain_get_assemblies:  api('il2cpp_domain_get_assemblies', 'pointer', ['pointer', 'pointer']),
    assembly_get_image:     api('il2cpp_assembly_get_image', 'pointer', ['pointer']),
    image_get_name:         api('il2cpp_image_get_name', 'pointer', ['pointer']),
    image_get_class_count:  api('il2cpp_image_get_class_count', 'uint32', ['pointer']),
    image_get_class:        api('il2cpp_image_get_class', 'pointer', ['pointer', 'uint32']),
    class_get_name:         api('il2cpp_class_get_name', 'pointer', ['pointer']),
    class_get_namespace:    api('il2cpp_class_get_namespace', 'pointer', ['pointer']),
    class_get_methods:      api('il2cpp_class_get_methods', 'pointer', ['pointer', 'pointer']),
    method_get_name:        api('il2cpp_method_get_name', 'pointer', ['pointer']),
    method_get_param_count: api('il2cpp_method_get_param_count', 'uint32', ['pointer']),
    thread_attach:          api('il2cpp_thread_attach', 'pointer', ['pointer']),
  };
} catch (e) {
  console.log('[!] ' + e.message);
  console.log('[!] GameAssembly.dll is not loaded yet - attach once the game is at the title screen,');
  console.log('[!] or use `frida -f <exe>` to spawn and hold at entry.');
  throw e;
}

function cstr(p) { return (!p || p.isNull()) ? '' : p.readUtf8String(); }

// 64-bit IL2CPP layouts. MethodInfo.methodPointer is at offset 0, so the native entry point is a straight pointer read.
const OFF = {
  methodPointer: 0x00,
  arrayLength:   0x18,   // Il2CppArray.max_length
  arrayData:     0x20,   // Il2CppArray.vector
  stringLength:  0x10,   // Il2CppString.length
  stringChars:   0x14,   // Il2CppString.chars (UTF-16)
};

function readByteArray(p) {
  if (!p || p.isNull()) return null;
  const len = p.add(OFF.arrayLength).readS32();
  if (len < 0 || len > 0x4000000) return null;          // reject > 64 MB
  return { length: len, bytes: p.add(OFF.arrayData).readByteArray(len) };
}

function readManagedString(p) {
  if (!p || p.isNull()) return null;
  const len = p.add(OFF.stringLength).readS32();
  if (len < 0 || len > 0x1000000) return null;
  if (len === 0) return '';
  return p.add(OFF.stringChars).readUtf16String(len);
}

function attachThread() {
  try { il2cpp.thread_attach(il2cpp.domain_get()); } catch (e) { /* already attached */ }
}

function eachClass(fn) {
  const domain = il2cpp.domain_get();
  const countPtr = Memory.alloc(Process.pointerSize);
  const assemblies = il2cpp.domain_get_assemblies(domain, countPtr);
  const asmCount = countPtr.readU32();

  for (let a = 0; a < asmCount; a++) {
    const asm = assemblies.add(a * Process.pointerSize).readPointer();
    if (asm.isNull()) continue;
    const image = il2cpp.assembly_get_image(asm);
    if (image.isNull()) continue;
    const imageName = cstr(il2cpp.image_get_name(image));
    const n = il2cpp.image_get_class_count(image);
    for (let i = 0; i < n; i++) {
      const klass = il2cpp.image_get_class(image, i);
      if (klass.isNull()) continue;
      fn({
        imageName, klass,
        ns: cstr(il2cpp.class_get_namespace(klass)),
        name: cstr(il2cpp.class_get_name(klass)),
      });
    }
  }
}

function eachMethod(klass, fn) {
  const iter = Memory.alloc(Process.pointerSize);
  iter.writePointer(NULL);
  let method;
  while (!(method = il2cpp.class_get_methods(klass, iter)).isNull()) {
    let ptr;
    try { ptr = method.add(OFF.methodPointer).readPointer(); } catch (e) { continue; }
    if (!ptr || ptr.isNull()) continue;
    fn({
      name: cstr(il2cpp.method_get_name(method)),
      paramCount: il2cpp.method_get_param_count(method),
      ptr,
    });
  }
}

function ts() { return new Date().toISOString(); }
function fileStamp() { return ts().replace(/[:.]/g, '-').slice(0, 19); }

function openOut(name) {
  const path = CFG.outDir + '\\' + name;
  const f = new File(path, 'w');
  return { f, path };
}

function hexPane(bytes, total) {
  const u8 = new Uint8Array(bytes);
  const n = Math.min(u8.length, CFG.maxHexBytes);
  const rows = [];
  for (let i = 0; i < n; i += 16) {
    const s = Array.from(u8.slice(i, i + 16));
    const hex = s.map((b) => b.toString(16).padStart(2, '0').toUpperCase());
    while (hex.length < 16) hex.push('  ');
    const ascii = s.map((b) => (b >= 32 && b < 127 ? String.fromCharCode(b) : '.')).join('');
    rows.push('  ' + i.toString(16).padStart(8, '0').toUpperCase() + '  ' +
              hex.slice(0, 8).join(' ') + '  ' + hex.slice(8).join(' ') + '  |' + ascii + '|');
  }
  if (total > n) rows.push('  ... ' + (total - n) + ' more bytes truncated from hex pane');
  return rows.join('\r\n');
}

// MessagePack and JSON both survive a UTF-8 decode well enough to be useful. For MessagePack the printable runs give you the field names, which is the part that matters when matching against a server implementation.
function decodeText(bytes) {
  const u8 = new Uint8Array(bytes);
  let printable = 0;
  for (let i = 0; i < u8.length; i++) if (u8[i] >= 32 && u8[i] < 127) printable++;
  const ratio = u8.length ? printable / u8.length : 0;

  if (ratio < 0.75) {
    const runs = [];
    let cur = '';
    for (let i = 0; i < u8.length; i++) {
      const b = u8[i];
      if (b >= 32 && b < 127) cur += String.fromCharCode(b);
      else { if (cur.length >= 3) runs.push(cur); cur = ''; }
    }
    if (cur.length >= 3) runs.push(cur);
    return { mode: 'binary', text: runs.join(' - ') };
  }

  let text = '';
  try { text = new TextDecoder('utf-8', { fatal: false }).decode(u8); } catch (e) { text = ''; }
  return { mode: 'text', text };
}

// Official payloads carry raw control bytes inside user text (clan notices, localized mail bodies); escaping them keeps a body on one line and readable with strict=True.
// Quotes and backslashes stay as captured - a body's own \" / \\ are already escapes.
const CONTROL_CHARS = /[\u0000-\u001f\u007f]/g;

function escapeControls(s) {
  return s.replace(CONTROL_CHARS, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
}

// A decrypt that is immediately deserialized would otherwise log twice.
const recent = new Map();
function isDuplicate(key) {
  const now = Date.now();
  if (recent.size > 512) {
    for (const [k, t] of recent) if (now - t > CFG.dedupeWindowMs * 20) recent.delete(k);
  }
  const prev = recent.get(key);
  recent.set(key, now);
  return prev !== undefined && (now - prev) < CFG.dedupeWindowMs;
}

function fingerprint(bytes, len) {
  const u8 = new Uint8Array(bytes);
  let h = 2166136261 >>> 0;
  const step = Math.max(1, Math.floor(u8.length / 64));
  for (let i = 0; i < u8.length; i += step) { h ^= u8[i]; h = (h * 16777619) >>> 0; }
  return len + ':' + (h >>> 0).toString(16);
}

// A hooked value may be a managed byte[] or a managed string; try both and keep whichever reads back sanely.
function coerce(p) {
  if (!p || p.isNull()) return null;
  try {
    const arr = readByteArray(p);
    if (arr && arr.length >= CFG.minLength && arr.bytes) return arr;
  } catch (e) { /* not an array */ }
  try {
    const s = readManagedString(p);
    if (s && s.length >= CFG.minLength) {
      const enc = new TextEncoder().encode(s);
      return { length: enc.length, bytes: enc.buffer };
    }
  } catch (e) { /* not a string */ }
  return null;
}

function runCapture() {
  const { f: out, path } = openOut('responses-' + fileStamp() + '.txt');
  out.write('# Blue Archive - decrypted server responses (read from process memory)\r\n' +
            '# session started ' + ts() + '\r\n' +
            '# block format: timestamp, hook, byte length, decoded text, hex pane\r\n' +
            '#' + '='.repeat(78) + '\r\n\r\n');
  out.flush();

  let count = 0;
  function record(label, kind, payload) {
    if (!payload || payload.length < CFG.minLength || !payload.bytes) return;
    const fp = fingerprint(payload.bytes, payload.length);
    if (isDuplicate(fp)) return;

    const { mode, text } = decodeText(payload.bytes);
    count++;

    const shown = text.length > CFG.maxTextChars
      ? text.slice(0, CFG.maxTextChars) + '...[truncated]'
      : text;

    let b = '';
    b += '[' + ts() + '] #' + count + ' ' + kind.toUpperCase() + '\r\n';
    b += 'hook   : ' + label + '\r\n';
    b += 'length : ' + payload.length + ' bytes (' + mode + ')\r\n';
    b += 'text   : ' + escapeControls(shown) + '\r\n';
    if (CFG.hexDump) b += 'hex    :\r\n' + hexPane(payload.bytes, payload.length) + '\r\n';
    b += '-'.repeat(79) + '\r\n\r\n';

    out.write(b);
    out.flush();                        // never lose a capture to a crash

    if (CFG.logToConsole) {
      console.log('[' + count + '] ' + kind + ' ' + payload.length + 'B  ' + label);
    }
  }

  attachThread();

  const hooked = [];
  const seen = new Set();

  eachClass(({ klass, ns, name }) => {
    const full = (ns ? ns + '.' : '') + name;
    eachMethod(klass, ({ name: mName, paramCount, ptr }) => {
      const t = TARGETS.find((x) => x.cls.test(full) && x.m.test(mName));
      if (!t) return;
      if (t.capture === 'arg0' && paramCount < 1) return;

      const key = ptr.toString();
      if (seen.has(key)) return;        // overloads can share an entry point
      seen.add(key);

      const label = full + '::' + mName;
      try {
        Interceptor.attach(ptr, {
          onEnter(args) {
            if (t.capture !== 'arg0') return;
            // instance methods carry `this` in args[0], so the first real
            // parameter is args[1]; try both, keep whichever reads.
            const got = coerce(args[1]) || coerce(args[0]);
            if (got) record(label, t.kind, got);
          },
          onLeave(retval) {
            if (t.capture !== 'ret') return;
            const got = coerce(retval);
            if (got) record(label, t.kind, got);
          },
        });
        hooked.push(label);
      } catch (e) {
        console.log('[dump] could not hook ' + label + ': ' + e.message);
      }
    });
  });

  out.write('# hooks installed: ' + hooked.length + '\r\n' +
            hooked.map((h) => '#   ' + h).join('\r\n') + '\r\n\r\n');
  out.flush();

  console.log('[capture] ' + hooked.length + ' hooks installed');
  hooked.slice(0, 40).forEach((h) => console.log('    ' + h));
  if (hooked.length > 40) console.log('    ... and ' + (hooked.length - 40) + ' more');
  console.log('[capture] writing to ' + path);
  if (!hooked.length) {
    console.log('[capture] nothing matched - run BA_MODE=discover and widen TARGETS');
  }
}

function runDiscover() {
  const CLASS_HINTS = ['networkprotocol', 'packet', 'crypt', 'cipher', 'aes', 'rijndael',
    'xor', 'protocol', 'session', 'gateway', 'messagepack', 'serializer', 'formatter',
    'transport', 'webrequest', 'httpclient', 'response', 'obfusc'];
  const METHOD_HINTS = ['decrypt', 'encrypt', 'deserialize', 'serialize', 'transform',
    'onreceive', 'receive', 'handleresponse', 'parseresponse', 'setresponse',
    'processpacket', 'onresponse', 'fromjson', 'tojson', 'unpack', 'parse'];

  const hit = (s, arr) => { const h = s.toLowerCase(); return arr.some((n) => h.indexOf(n) !== -1); };

  attachThread();

  const lines = [
    '# IL2CPP discovery - networking / crypto / serialization surface',
    '# generated ' + ts(),
    '# format: <image> | <namespace>.<class> :: <method>(<params>) @ <address>',
    '',
  ];
  let classes = 0, mClasses = 0, mMethods = 0;

  eachClass(({ imageName, klass, ns, name }) => {
    classes++;
    const full = (ns ? ns + '.' : '') + name;
    if (!hit(full, CLASS_HINTS)) return;
    mClasses++;

    const rows = [];
    eachMethod(klass, ({ name: mName, paramCount, ptr }) => {
      if (!hit(mName, METHOD_HINTS)) return;
      mMethods++;
      rows.push('  ' + imageName + ' | ' + full + ' :: ' + mName + '(' + paramCount + ') @ ' + ptr);
    });
    if (rows.length) { lines.push('--- ' + full + '  [' + imageName + ']'); lines.push.apply(lines, rows); lines.push(''); }
  });

  lines.push('', '# classes walked: ' + classes, '# classes matched: ' + mClasses, '# methods matched: ' + mMethods);

  const { f, path } = openOut('discovered-methods.txt');
  f.write(lines.join('\r\n'));
  f.flush();
  f.close();

  console.log('[discover] walked ' + classes + ' classes; matched ' + mClasses + ' classes / ' + mMethods + ' methods');
  console.log('[discover] wrote ' + path);
}

setImmediate(function () {
  console.log('[ba-capture] mode=' + CFG.mode + '  module=' + CFG.module + ' @ ' + mod.base);
  if (CFG.mode === 'discover') runDiscover();
  else runCapture();
});
