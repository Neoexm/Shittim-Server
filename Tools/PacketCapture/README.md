# Packet capture — decrypted server responses

Reads server responses out of the client's memory **after** it has decrypted
them, and writes them to a text file in `captures/`.

## Why from memory

The transport payload is encrypted, so a proxy capture (mitmproxy, HAR logs)
only ever yields ciphertext. But the plaintext has to exist in managed memory
for the client to deserialize it. Hooking the point where a decrypt returns —
or where a deserializer is handed a buffer — gives cleartext without having to
defeat the obfuscated crypto at all.

This is also why the hooks are resolved at runtime rather than from IDA.
IL2CPP loads string literals through a metadata table instead of referencing
them directly, so static xrefs to strings like `"Decrypt Exception"` dead-end
in IDA. The live runtime, by contrast, hands over real namespace/class/method
names via the `il2cpp_*` exports that `GameAssembly.dll` already provides.
Nothing here depends on the `.i64` database, and an ASLR rebase or a game
update cannot stale the addresses.

## Relationship to `HybridCryptor.EncryptSweep`

`Core/Crypto/HybridCryptor.cs` currently brute-forces AES mode/padding via
`sweep.txt` while trying to work out what the client's decryptor expects.
Capturing the client's decrypt output directly answers that question instead of
searching for it: hook the decrypt boundary, compare the plaintext the client
produced against what the server sent, and the mode/padding falls out. Once
that is settled the sweep scaffolding can go.

## Usage

```powershell
# 1. See what is hookable (installs nothing, just writes a candidate list)
.\Capture.ps1 -Discover
#    -> captures\discovered-methods.txt

# 2. Capture, attaching to a client that is already running
.\Capture.ps1

# 3. Capture from process start, so the login + gateway handshake is included
.\Capture.ps1 -Spawn
```

Run from an elevated shell if injection is refused.

Raw frida, if you prefer:

```powershell
$env:BA_MODE='discover'
python -m frida -n BlueArchive.exe -l ba-capture.js --runtime=v8
```

## Output

`captures\responses-<timestamp>.txt`, one block per payload:

```
[2026-07-26T14:31:07.842Z] #12 DECRYPT
hook   : <Namespace>.<Class>::Decrypt
length : 486 bytes (binary)
text   : AccountId · Nickname · Level · Exp · ParcelInfos
hex    :
  00000000  82 A9 41 63 63 6F 75 6E  74 49 64 CF 00 00 00 01  |..AccountId.....|
  ...
```

- `text` — for JSON this is the body; for MessagePack it is the printable runs,
  which is the part you actually need when matching field names against a
  server implementation.
- `length` — always the true length, even when the hex pane is truncated.
- `text` is escaped: every U+0000–U+001F and U+007F is rewritten as a
  lowercase 4-digit `\uXXXX` escape, so each capture stays on one line and a
  JSON body parses under a *strict* reader (`json.loads` with the default
  `strict=True`). Quotes and backslashes are left exactly as captured — a
  body's own `\"` / `\\` are already valid escapes, and re-escaping them would
  corrupt the payload.
- The file is flushed after every capture, so a client crash costs nothing.

## Tuning

Everything lives in the `CFG` and `TARGETS` blocks at the top of
`ba-capture.js`.

- Nothing captured? Run `-Discover`, find the real class/method names, and
  widen the `TARGETS` regexes.
- Too noisy? Tighten `TARGETS`, raise `CFG.minLength`, or set
  `CFG.hexDump = false` for text-only output.
- Frame stutter? `TARGETS` is matching too broadly — hooking a hot serializer
  used by non-network code will do that. Narrow the `cls` pattern.
- Duplicate blocks? A decrypt immediately followed by a deserialize of the same
  buffer. `CFG.dedupeWindowMs` collapses those; raise it if some slip through.

## Notes

- Hooks are keyed on method entry addresses; overloads sharing an entry point
  are installed once.
- Only reads memory — no patching, and `GameAssembly.dll` is not modified.
  `GameAssembly.patched.dll` and the `.orig-backup` in the game folder are from
  earlier work and are untouched by this.
