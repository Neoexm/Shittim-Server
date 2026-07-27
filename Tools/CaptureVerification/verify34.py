"""End-to-end verification of the two fixes against the running local gateway.

Issue 3: Clan_Check must report CanReceiveClanAttendanceReward (2048), OR with the mailbox
         baseline (2056), be cleared by Clan_Lobby (which delivers the reward as mail), and not
         re-deliver within the same game day.
Issue 4: Mission_List.ClearedOrignalMissionIds must be absent for the account-wide call and for a
         normal event, and present-but-empty for a rerun event.

Establishes a session by replaying the login prefix of our own capture, then drives its own probes.
"""
import json, re, os, gzip, struct, uuid, urllib.request, sqlite3, datetime, sys

CAP = r"c:\Users\tomda\Documents\Shittim-Server\captures\NetworkLog our server.txt"
DB = r"c:\Users\tomda\Documents\Shittim-Server\Shittim-Server\bin\Debug\net10.0\shittim.sqlite3"
GATEWAY = "http://127.0.0.1:5100/api/gateway"
XOR_KEY = 0xD9
FILLER_MAIL = 9002


def build_mx(plain: bytes) -> bytes:
    gz = gzip.compress(plain)
    xored = bytes(b ^ XOR_KEY for b in gz)
    payload = struct.pack("<i", len(plain)) + xored
    header = struct.pack("<I", 0) + struct.pack("<i", 0) + bytes([0, 0])
    return header + payload


def post_mx(body: bytes) -> str:
    boundary = "----shittimverify" + uuid.uuid4().hex
    part = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="mx"; filename="mx"\r\n'
        f"Content-Type: application/octet-stream\r\n\r\n"
    ).encode() + body + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(
        GATEWAY, data=part,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode("utf-8")


def send(packet: dict) -> dict:
    raw = post_mx(build_mx(json.dumps(packet, ensure_ascii=False, separators=(",", ":")).encode()))
    outer = json.loads(raw)
    inner = outer.get("packet")
    return json.loads(inner) if isinstance(inner, str) else (inner or {})


def login() -> dict:
    """Replay captured requests in order until one yields a SessionKey."""
    text = open(CAP, encoding="utf-8", errors="replace").read()
    parts = re.split(r"={5,}\s*(REQUEST|RESPONSE)\s*={5,}", text)
    reqs = [parts[i + 1].strip() for i in range(1, len(parts), 2) if parts[i] == "REQUEST"]

    session = None
    for raw in reqs:
        j = json.loads(raw)
        if session is not None and j.get("SessionKey") is not None:
            j["SessionKey"] = session
            if "AccountId" in j:
                j["AccountId"] = session["AccountServerId"]
        pk = send(j)
        if pk.get("SessionKey"):
            session = pk["SessionKey"]
        # Stop as soon as the session is usable; the rest of the capture would pollute clan state.
        if session is not None and j.get("Protocol") in (1006, 1013, 1019):
            return session
    if session is None:
        raise SystemExit("never obtained a SessionKey from the login replay")
    return session


# ---- DB helpers (the reward has no dedicated column; the delivered mail IS the record) ---------

def db():
    return sqlite3.connect(DB)


def reset_boundary(now: datetime.datetime) -> datetime.datetime:
    today = now.replace(hour=4, minute=0, second=0, microsecond=0)
    return today - datetime.timedelta(days=1) if now < today else today


def clean(acct: int):
    c = db()
    # EF Core stores SQLite DateTimes as TEXT with a space separator, and compares them as strings;
    # an ISO 'T' here sorts above every same-day time and silently matches nothing.
    boundary = reset_boundary(datetime.datetime.now()).strftime("%Y-%m-%d %H:%M:%S")
    n = c.execute("delete from Mails where AccountServerId=? and Type=11 and SendDate>=?",
                  (acct, boundary)).rowcount
    m = c.execute("delete from Mails where ServerId=?", (FILLER_MAIL,)).rowcount
    c.commit()
    print(f"  [db] removed {n} clan-attendance mail(s) since {boundary}, {m} filler mail(s)")


def seed_filler(acct: int):
    c = db()
    c.execute("delete from Mails where ServerId=?", (FILLER_MAIL,))
    c.execute(
        "insert into Mails (ServerId,AccountServerId,Type,UniqueId,Sender,Comment,"
        "LocalizedSender,LocalizedComment,SendDate,ReceiptDate,ExpireDate,ParcelInfos,"
        "RemainParcelInfos,IsRefresher) values (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        (FILLER_MAIL, acct, 0, 0, "Arona", "filler",
         '{"Kr":"Arona","En":"Arona","Th":"Arona","Tw":"Arona"}',
         '{"Kr":"","En":"","Th":"","Tw":""}',
         "2026-07-01T12:00:00", None, "2099-12-31T12:00:00", "[]", None, 0))
    c.commit()
    print("  [db] seeded unrelated unread mail 9002 (type 0)")


def clan_mails(acct: int):
    c = db()
    return c.execute(
        "select ServerId,Type,UniqueId,Sender,Comment,SendDate,ExpireDate,ParcelInfos,"
        "RemainParcelInfos,LocalizedSender from Mails where AccountServerId=? and Type=11 "
        "order by ServerId", (acct,)).fetchall()


# ---- probes ------------------------------------------------------------------------------------

FAILURES = []


def check(label, actual, expected):
    ok = actual == expected
    print(f"  {'OK  ' if ok else 'FAIL'} {label}: {actual!r}" + ("" if ok else f" (expected {expected!r})"))
    if not ok:
        FAILURES.append(label)


session = login()
acct = session["AccountServerId"]
print(f"session established for account {acct}\n")

print("--- issue 3: Clan_Check / Clan_Lobby -------------------------------------------")
clean(acct)

sn = send({"Protocol": 28019, "SessionKey": session}).get("ServerNotification")
check("Clan_Check over an empty mailbox == 2048", sn, 2048)

seed_filler(acct)
sn = send({"Protocol": 28019, "SessionKey": session}).get("ServerNotification")
check("Clan_Check with unread mail == 2056 (2048|8)", sn, 2056)

before = len(clan_mails(acct))
send({"Protocol": 28000, "SessionKey": session})
after = clan_mails(acct)
check("Clan_Lobby delivered one ClanAttendance mail", len(after) - before, 1)

sn = send({"Protocol": 28019, "SessionKey": session}).get("ServerNotification")
check("Clan_Check after the claim == 8 (2048 cleared, mail unread)", sn, 8)

send({"Protocol": 28000, "SessionKey": session})
check("second Clan_Lobby in the same game day delivers nothing", len(clan_mails(acct)), len(after))

mc = send({"Protocol": 7001, "SessionKey": session})
check("Mail_Check counts the reward + the filler", mc.get("CommonMailCount"), 2)

print("\n  delivered mail row:")
for row in clan_mails(acct):
    cols = ["ServerId", "Type", "UniqueId", "Sender", "Comment", "SendDate", "ExpireDate",
            "ParcelInfos", "RemainParcelInfos", "LocalizedSender"]
    for k, v in zip(cols, row):
        print(f"    {k:18} = {v}")

ml = send({"Protocol": 7000, "SessionKey": session})
wire = [m for m in (ml.get("MailDBs") or []) if m.get("Type") == 11]
ours = wire[0] if wire else {}
print("\n  Mail_List wire shape of the delivered mail:")
print("   ", json.dumps(ours, ensure_ascii=False))

# Field-by-field against official's own clan attendance mail, ignoring the per-account/per-day
# values (ids, account, and the two timestamps) but keeping their FORMAT under comparison.
OFFICIAL = {
    "ServerId": 70493846, "AccountServerId": 3218642, "Type": 11, "UniqueId": -1,
    "Sender": "UI_MAILBOX_POST_SENDER_ARONA",
    "LocalizedSender": {k: "UI_MAILBOX_POST_SENDER_ARONA" for k in ("Kr", "En", "Th", "Tw")},
    "Comment": "UI_MAILBOX_CLAN_ATTENDANCE_REWARD_MESSAGE_NORMAL",
    "LocalizedComment": {k: "UI_MAILBOX_CLAN_ATTENDANCE_REWARD_MESSAGE_NORMAL"
                         for k in ("Kr", "En", "Th", "Tw")},
    "SendDate": "2026-07-27T04:19:59", "ExpireDate": "2026-08-03T04:19:59",
    "ParcelInfos": [{"Key": {"Type": 2, "Id": 5}, "Amount": 10,
                     "Multiplier": {"rawValue": 10000}, "Probability": {"rawValue": 10000}}],
}
DYNAMIC = {"ServerId", "AccountServerId", "SendDate", "ExpireDate"}
TS = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$")

print("\n  vs official, field by field:")
check("key set matches official exactly", sorted(ours), sorted(OFFICIAL))
for k in sorted(set(OFFICIAL) & set(ours)):
    if k in DYNAMIC:
        continue
    check(f"  {k}", ours[k], OFFICIAL[k])
for k in ("SendDate", "ExpireDate"):
    if k in ours:
        check(f"  {k} format (whole seconds, no offset)", bool(TS.match(str(ours[k]))), True)

print("\n--- issue 4: Mission_List.ClearedOrignalMissionIds ----------------------------")
KEY = "ClearedOrignalMissionIds"
r = send({"Protocol": 8000, "SessionKey": session})
check("account-wide call omits the key", KEY in r, False)

r = send({"Protocol": 8000, "SessionKey": session, "EventContentId": 856})
check("normal event 856 omits the key", KEY in r, False)

r = send({"Protocol": 8000, "SessionKey": session, "EventContentId": 10842})
check("rerun event 10842 sends the key", KEY in r, True)
check("rerun event 10842 sends it empty", r.get(KEY), [])

print("\n--- cleanup -------------------------------------------------------------------")
clean(acct)

print()
if FAILURES:
    print(f"{len(FAILURES)} FAILURE(S): " + ", ".join(FAILURES))
    sys.exit(1)
print("all checks passed")
