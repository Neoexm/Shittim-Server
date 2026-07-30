"""Live verification of the NewMailArrived (bit 4) model against the running gateway.

Official model (from the reference captures):
  - a mid-session mail delivery makes the NEXT Mail_Check report 12 (HasUnreadMail|NewMailArrived),
    and only that one - the second Mail_Check is back to 8 while the mail is still unread;
  - unrelated protocols between delivery and Mail_Check stay at 8 (the bit is not baseline);
  - clan attendance mail raises the bit ONLY on the delivering Clan_Lobby response (official's
    recovered Clan_Lobby carries ServerNotification=4) and leaves nothing pending for Mail_Check.

Positive path uses POST /api/admin/mail/send (MailManager.SendSystemMail marks the account);
negative path uses Clan_Lobby (ClanHandler deliberately does not mark).
"""
import json, re, gzip, struct, uuid, urllib.request, sqlite3, datetime, sys

CAP = r"c:\Users\tomda\Documents\Shittim-Server\captures\NetworkLog our server.txt"
DB = r"c:\Users\tomda\Documents\Shittim-Server\Shittim-Server\bin\Debug\net10.0\shittim.sqlite3"
GATEWAY = "http://127.0.0.1:5100/api/gateway"
ADMIN_MAIL = "http://127.0.0.1:5100/api/admin/mail/send"
XOR_KEY = 0xD9
MARKER = "verify-newmail-bit4"


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
        if session is not None and j.get("Protocol") in (1006, 1013, 1019):
            return session
    if session is None:
        raise SystemExit("never obtained a SessionKey from the login replay")
    return session


def db():
    return sqlite3.connect(DB)


def reset_boundary(now: datetime.datetime) -> datetime.datetime:
    today = now.replace(hour=4, minute=0, second=0, microsecond=0)
    return today - datetime.timedelta(days=1) if now < today else today


def clean(acct: int):
    c = db()
    # EF stores DateTimes as TEXT with a space separator; strftime matches that, ISO 'T' doesn't.
    boundary = reset_boundary(datetime.datetime.now()).strftime("%Y-%m-%d %H:%M:%S")
    n = c.execute("delete from Mails where AccountServerId=? and Type=11 and SendDate>=?",
                  (acct, boundary)).rowcount
    m = c.execute("delete from Mails where AccountServerId=? and Comment=?",
                  (acct, MARKER)).rowcount
    c.commit()
    print(f"  [db] removed {n} clan-attendance mail(s) since {boundary}, {m} marker mail(s)")


def admin_send_mail(acct: int):
    body = json.dumps({
        "AccountServerId": acct,
        "Sender": "Plana",
        "Comment": MARKER,
        "Parcels": [{"Type": "Currency", "Id": 5, "Amount": 10}],
    }).encode()
    req = urllib.request.Request(ADMIN_MAIL, data=body,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode("utf-8"))


FAILURES = []


def check(label, actual, expected):
    ok = actual == expected
    print(f"  {'OK  ' if ok else 'FAIL'} {label}: {actual!r}" + ("" if ok else f" (expected {expected!r})"))
    if not ok:
        FAILURES.append(label)


session = login()
acct = session["AccountServerId"]
print(f"session established for account {acct}\n")
clean(acct)

print("--- clan path: bit 4 on the delivering response only ---------------------------")
cl = send({"Protocol": 28000, "SessionKey": session})   # Clan_Lobby delivers the clan mail
check("Clan_Lobby delivering response == 4 (mailbox was empty at entry)",
      cl.get("ServerNotification"), 4)
mc = send({"Protocol": 7001, "SessionKey": session})
check("Mail_Check after clan delivery == 8 (nothing left pending)", mc.get("ServerNotification"), 8)
check("clan mail is in the box", mc.get("CommonMailCount"), 1)

print("\n--- positive path: admin/system mail raises it on the NEXT Mail_Check only ----")
r = admin_send_mail(acct)
check("admin mail accepted", r.get("success"), True)

ml = send({"Protocol": 8000, "SessionKey": session})   # unrelated protocol in between
check("unrelated Mission_List stays == 8 (bit 4 is not baseline)",
      ml.get("ServerNotification"), 8)

mc1 = send({"Protocol": 7001, "SessionKey": session})
check("first Mail_Check == 12 (8|4)", mc1.get("ServerNotification"), 12)
check("both mails counted", mc1.get("CommonMailCount"), 2)

mc2 = send({"Protocol": 7001, "SessionKey": session})
check("second Mail_Check == 8 (consumed, mail still unread)", mc2.get("ServerNotification"), 8)
check("count unchanged", mc2.get("CommonMailCount"), 2)

print("\n--- cleanup -------------------------------------------------------------------")
clean(acct)

print()
if FAILURES:
    print(f"{len(FAILURES)} FAILURE(S): " + ", ".join(FAILURES))
    sys.exit(1)
print("all checks passed")
