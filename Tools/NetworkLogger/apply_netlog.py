#!/usr/bin/env python3
#  Blue Archive client Network Logger  -  self-contained in-place patcher
#  Logs EVERY server request and response (full, untruncated plaintext JSON,
#  as the client sees it) to:
#     <GameDir>\DiagnosticLogs\NetworkLog.txt
#
#  HOW TO USE
#    1. Steam -> Blue Archive -> Properties -> Installed Files
#         -> "Verify integrity of game files"   (gives you a clean DLL)
#    2. Fully close the game.
#    3. Double-click  Patch-NetworkLogger.bat   (or:  python apply_netlog.py)
#    4. Launch the game and play/log in. Watch the log file fill up.
#
#  To undo:  python apply_netlog.py revert     (or just verify integrity again)
#
#  Pure Python standard library - no pip installs, no keystone, nothing extra.
import struct, os, sys, hashlib, base64

GAMEDIR = r"F:\SteamLibrary\steamapps\common\BlueArchive"
DLL     = GAMEDIR + r"\GameAssembly.dll"
BACKUP  = GAMEDIR + r"\GameAssembly.dll.orig-backup"
LOGFILE = GAMEDIR + r"\DiagnosticLogs\NetworkLog.txt"

IMAGEBASE               = 0x180000000
PRISTINE_MD5            = "0f968d7df1b40dbb509564a2638f27b5"
PATCHED_MD5             = "d0e9766e5109e742e294883f08152fb3"
SECTION_NAME            = b".netlog\x00"
CAVE_VSIZE              = 0x8000
EXPECTED_CAVE_RVA       = 0xda0e000
SECTION_CHARACTERISTICS = 0xE0000020        # RWX + code
HOOK_EDITS              = [(6456689696, '4156565753', 'e9db9bc70c'), (6456717603, 'e8682f8802', 'e8362fc70c')]          # (VA, orig5_hex, new5_hex)
SECTION_BLOB            = base64.b64decode("UFFSQVBBUUFSQVNTVldVQVRBVUFWQVecSInlSIPsQEiD5PBIi01wSI0V4hAAAEG4IgAAAOiHAAAASInsnUFfQV5BXUFcXV9eW0FbQVpBWUFYWllYQVZWV1Ppx2M480iD7CjoKQDB9UiDxChQUVJBUEFRQVJBU1NWV1VBVEFVQVZBV5xIieVIg+xASIPk8EiLTXhIjRWfEAAAQbghAAAA6BwAAABIieydQV9BXkFdQVxdX15bQVtBWkFZQVhaWVjDVUiJ5VNWV0FUQVVBVkFXSYnMSYnVTYnGSIHsAIgAAEiD5PBNheQPhD8BAABIjQ2lDwAAMdL/Fc0QZPxIjQ3+DgAAugAAAEBBuAMAAABFMclIx0QkIAQAAABIx0QkKIAAAABIx0QkMAAAAAD/FZ8SZPxIg/j/D4TwAAAASYnHTIn5MdJFMcBBuQIAAAD/FRYQZPxMiflMiepFifBMjYwkAIcAAEjHRCQgAAAAAP8VBhBk/EWLbCQQSY10JBRFhe0PjmsAAABEiemB+QCAAAAPjgUAAAC5AIAAAEGJzkiNvCQAAQAAMdI5yg+DDgAAAA+3BFaIBBf/wunq////TIn5SI2UJAABAABFifBMjYwkAIcAAEjHRCQgAAAAAP8Vlw9k/EljxkiNNEZFKfXpjP///2bHhCQAAQAADQpMiflIjZQkAAEAAEG4AgAAAEyNjCQAhwAASMdEJCAAAAAA/xVWD2T8TIn5/xVtEmT8SI1lyEFfQV5BXUFcX15bXcPMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzEYAOgBcAFMAdABlAGEAbQBMAGkAYgByAGEAcgB5AFwAcwB0AGUAYQBtAGEAcABwAHMAXABjAG8AbQBtAG8AbgBcAEIAbAB1AGUAQQByAGMAaABpAHYAZQBcAEQAaQBhAGcAbgBvAHMAdABpAGMATABvAGcAcwBcAE4AZQB0AHcAbwByAGsATABvAGcALgB0AHgAdAAAAAAARgA6AFwAUwB0AGUAYQBtAEwAaQBiAHIAYQByAHkAXABzAHQAZQBhAG0AYQBwAHAAcwBcAGMAbwBtAG0AbwBuAFwAQgBsAHUAZQBBAHIAYwBoAGkAdgBlAFwARABpAGEAZwBuAG8AcwB0AGkAYwBMAG8AZwBzAAAADQo9PT09PT09PT09IFJFU1BPTlNFID09PT09PT09PT0NCgAAAAAAAA0KPT09PT09PT09PSBSRVFVRVNUID09PT09PT09PT0NCgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")

md5 = lambda b: hashlib.md5(b).hexdigest()
al  = lambda x, a: (x + a - 1) & ~(a - 1)

def rva_to_off(raw, rva):
    e = struct.unpack_from("<I", raw, 0x3C)[0]
    num = struct.unpack_from("<H", raw, e + 6)[0]
    szopt = struct.unpack_from("<H", raw, e + 0x14)[0]
    soff = e + 0x18 + szopt
    for i in range(num):
        vs, va, rs, rp = struct.unpack_from("<IIII", raw, soff + i * 0x28 + 8)
        if va <= rva < va + max(vs, rs):
            return rva - va + rp
    raise ValueError("rva not found: " + hex(rva))

def do_revert():
    if not os.path.exists(BACKUP):
        print("[!] No backup found. Use Steam 'Verify integrity of game files' instead.")
        return 1
    if md5(open(BACKUP, "rb").read()) != PRISTINE_MD5:
        print("[!] Backup is not the expected clean version; refusing to restore it.")
        return 1
    open(DLL, "wb").write(open(BACKUP, "rb").read())
    print("[OK] Restored clean GameAssembly.dll from backup.")
    return 0

def do_apply():
    if not os.path.exists(DLL):
        print("[!] Not found: " + DLL)
        return 1
    raw = open(DLL, "rb").read()
    h = md5(raw)
    if h == PATCHED_MD5:
        print("[=] Already patched - nothing to do.")
        print("    Log file: " + LOGFILE)
        return 0
    if h != PRISTINE_MD5:
        print("[!] This GameAssembly.dll is not the clean version this patch was built for.")
        print("    (md5=" + h + ")")
        print("    In Steam: Properties -> Installed Files -> Verify integrity of game files,")
        print("    then run this again.")
        return 1

    if not os.path.exists(BACKUP) or md5(open(BACKUP, "rb").read()) != PRISTINE_MD5:
        open(BACKUP, "wb").write(raw)
        print("[+] Saved clean backup -> GameAssembly.dll.orig-backup")

    e = struct.unpack_from("<I", raw, 0x3C)[0]
    num = struct.unpack_from("<H", raw, e + 6)[0]
    szopt = struct.unpack_from("<H", raw, e + 0x14)[0]
    oo = e + 0x18
    SA = struct.unpack_from("<I", raw, oo + 0x20)[0]
    FA = struct.unpack_from("<I", raw, oo + 0x24)[0]
    soff = oo + szopt
    lvs, lva, lrs, lrp = struct.unpack_from("<IIII", raw, soff + (num - 1) * 0x28 + 8)
    cave_rva = al(lva + lvs, SA)
    if cave_rva != EXPECTED_CAVE_RVA:
        print("[!] Section layout mismatch (cave_rva=" + hex(cave_rva) + "). Aborting.")
        return 1
    new_hdr_off = soff + num * 0x28
    first_raw = struct.unpack_from("<IIII", raw, soff + 8)[3]
    if new_hdr_off + 0x28 > first_raw:
        print("[!] No room for a new section header. Aborting.")
        return 1
    file_raw_ptr = al(len(raw), FA)
    raw_size = len(SECTION_BLOB)

    out = bytearray(raw)
    out[new_hdr_off:new_hdr_off + 0x28] = struct.pack(
        "<8sIIIIIIHHI", SECTION_NAME, CAVE_VSIZE, cave_rva, raw_size,
        file_raw_ptr, 0, 0, 0, 0, SECTION_CHARACTERISTICS)
    struct.pack_into("<H", out, e + 6, num + 1)
    struct.pack_into("<I", out, oo + 0x38, al(cave_rva + CAVE_VSIZE, SA))  # SizeOfImage

    for va, orig_hex, new_hex in HOOK_EDITS:
        fo = rva_to_off(raw, va - IMAGEBASE)
        if bytes(out[fo:fo + 5]) != bytes.fromhex(orig_hex):
            print("[!] Hook site " + hex(va) + " bytes unexpected. Aborting.")
            return 1
        out[fo:fo + 5] = bytes.fromhex(new_hex)

    if len(out) < file_raw_ptr:
        out.extend(b"\x00" * (file_raw_ptr - len(out)))
    out.extend(SECTION_BLOB)

    final = bytes(out)
    if md5(final) != PATCHED_MD5:
        print("[!] Internal error: result md5 mismatch. Not writing.")
        return 1
    open(DLL, "wb").write(final)
    print("[OK] Patched GameAssembly.dll.")
    print("     Launch the game; every request/response is logged (full, untruncated) to:")
    print("     " + LOGFILE)
    print("     Undo with:  python apply_netlog.py revert   (or Steam verify integrity)")
    return 0

if __name__ == "__main__":
    mode = sys.argv[1].lower() if len(sys.argv) > 1 else "apply"
    rc = do_revert() if mode == "revert" else do_apply()
    sys.exit(rc)
