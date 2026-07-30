"""Live verification of the 2026-07-27 feature batch against the running gateway.

Covers: attendance (books on Auth, 9002 claim -> mail + bit 4 + double-claim rejection),
the crafting chain end-to-end, Shop_BuyAP legality (999 AP cap -> bare Error 10006 envelope)
and success shape, and the Clan_Lobby structure against the recovered official reference.
"""
import json, re, gzip, struct, uuid, urllib.request, urllib.error, sqlite3, datetime, sys

CAP = r"c:\Users\tomda\Documents\Shittim-Server\captures\NetworkLog our server.txt"
DB = r"c:\Users\tomda\Documents\Shittim-Server\Shittim-Server\bin\Debug\net10.0\shittim.sqlite3"
GATEWAY = "http://127.0.0.1:5100/api/gateway"
ADMIN_CURRENCY = "http://127.0.0.1:5100/api/admin/currency/set"
XOR_KEY = 0xD9


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


def send_full(packet: dict):
    """Returns (outer_protocol_name, inner_packet_dict)."""
    raw = post_mx(build_mx(json.dumps(packet, ensure_ascii=False, separators=(",", ":")).encode()))
    outer = json.loads(raw)
    inner = outer.get("packet")
    return outer.get("protocol"), (json.loads(inner) if isinstance(inner, str) else (inner or {}))


def send(packet: dict) -> dict:
    return send_full(packet)[1]


def login():
    """Replay captured requests until a usable session; also grab the Account_Auth response."""
    text = open(CAP, encoding="utf-8", errors="replace").read()
    parts = re.split(r"={5,}\s*(REQUEST|RESPONSE)\s*={5,}", text)
    reqs = [parts[i + 1].strip() for i in range(1, len(parts), 2) if parts[i] == "REQUEST"]
    session, auth = None, None
    for raw in reqs:
        j = json.loads(raw)
        if session is not None and j.get("SessionKey") is not None:
            j["SessionKey"] = session
            if "AccountId" in j:
                j["AccountId"] = session["AccountServerId"]
        pk = send(j)
        if j.get("Protocol") == 1002:
            auth = pk
        if pk.get("SessionKey"):
            session = pk["SessionKey"]
        if session is not None and j.get("Protocol") in (1006, 1013, 1019):
            return session, auth
    raise SystemExit("never obtained a SessionKey from the login replay")


def db():
    return sqlite3.connect(DB)


def set_currency(acct: int, ctype: int, amount: int):
    body = json.dumps({"AccountServerId": acct, "CurrencyType": ctype, "Amount": amount}).encode()
    req = urllib.request.Request(ADMIN_CURRENCY, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode("utf-8"))


def reset_boundary(now: datetime.datetime) -> datetime.datetime:
    today = now.replace(hour=4, minute=0, second=0, microsecond=0)
    return today - datetime.timedelta(days=1) if now < today else today


def clean(acct: int):
    c = db()
    # EF stores DateTimes as TEXT with a space separator; strftime matches that, ISO 'T' doesn't.
    boundary = reset_boundary(datetime.datetime.now()).strftime("%Y-%m-%d %H:%M:%S")
    n1 = c.execute("delete from Mails where AccountServerId=? and Type in (1,11) and SendDate>=?",
                   (acct, boundary)).rowcount
    n2 = c.execute("delete from AttendanceHistories where AccountServerId=?", (acct,)).rowcount
    n3 = c.execute("delete from CraftInfos where AccountServerId=?", (acct,)).rowcount
    c.commit()
    print(f"  [db] removed {n1} attendance/clan mail(s), {n2} attendance history row(s), {n3} craft slot(s)")


FAILURES = []


def check(label, actual, expected):
    ok = actual == expected
    print(f"  {'OK  ' if ok else 'FAIL'} {label}: {actual!r}" + ("" if ok else f" (expected {expected!r})"))
    if not ok:
        FAILURES.append(label)


def check_true(label, cond, detail=""):
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + (f": {detail}" if detail else ""))
    if not cond:
        FAILURES.append(label)


session, auth = login()
acct = session["AccountServerId"]
print(f"session established for account {acct}\n")

print("--- attendance: Account_Auth carries books + histories -------------------------")
books = auth.get("AttendanceBookRewards")
hists = auth.get("AttendanceHistoryDBs")
print(f"  [auth] books={'ABSENT' if books is None else len(books)} histories={'ABSENT' if hists is None else len(hists)}")
check_true("Auth carries the attendance keys", books is not None and hists is not None)
if books:
    b0 = books[0]
    check_true("book definition has DailyRewards + TargetGroup",
               bool(b0.get("DailyRewards")) and "TargetGroup" in b0,
               f"UniqueId={b0.get('UniqueId')} days={len(b0.get('DailyRewards') or {})} TargetGroup={b0.get('TargetGroup')}")

print("\n--- attendance: 9002 claim -> book + history + mail + bit 4 --------------------")
clean(acct)
if books:
    book_id = books[0]["UniqueId"]
    r = send({"Protocol": 9002, "SessionKey": session,
              "DayByBookUniqueId": {str(book_id): 1}, "AttendanceBookUniqueId": 0, "Day": 0})
    check("claim response repeats the claimed book", len(r.get("AttendanceBookRewards") or []), 1)
    h = (r.get("AttendanceHistoryDBs") or [{}])[0]
    check_true("history row has AttendedDay day 1", "1" in (h.get("AttendedDay") or {}),
               json.dumps(h.get("AttendedDay"))[:60])
    check_true("claim response has NO ParcelResultDB", "ParcelResultDB" not in r)
    check_true("claim response carries bit 4", (r.get("ServerNotification") or 0) & 4 == 4,
               f"SN={r.get('ServerNotification')}")
    row = db().execute("select Type,UniqueId,Sender,Comment from Mails where AccountServerId=? "
                       "and Type=1 order by ServerId desc limit 1", (acct,)).fetchone()
    check_true("attendance mail delivered (Type 1, UniqueId=book)",
               row is not None and row[1] == book_id and row[2] == "UI_MAILBOX_POST_SENDER_ARONA"
               and row[3] == "UI_MAILBOX_ATTENDANCE_REWARD_MESSAGE_NORMAL", str(row))
    mc = send({"Protocol": 7001, "SessionKey": session})
    check("first Mail_Check == 12 (8|4, report-and-consume)", mc.get("ServerNotification"), 12)
    mc = send({"Protocol": 7001, "SessionKey": session})
    check("second Mail_Check == 8", mc.get("ServerNotification"), 8)
    name, err = send_full({"Protocol": 9002, "SessionKey": session,
                           "DayByBookUniqueId": {str(book_id): 2}, "AttendanceBookUniqueId": 0, "Day": 0})
    check("double claim rejected with Error", name, "Error")
    check("  ...code 9000 AttendanceInvalid", err.get("ErrorCode"), 9000)
else:
    print("  SKIP claim tests (no books - attendance excel empty?)")
    FAILURES.append("attendance books unavailable")

print("\n--- crafting chain -------------------------------------------------------------")
r = send({"Protocol": 21002, "SessionKey": session, "SlotId": 0, "CraftNodeType": 0,
          "ConsumeGoldAmount": 0,
          "ConsumeRequestDB": {"ConsumeItemServerIdAndCounts": {}, "ConsumeEquipmentServerIdAndCounts": {}, "ConsumeFurnitureServerIdAndCounts": {}}})
node = r.get("CraftNodeDB") or {}
check("UpdateNodeLevel creates the base node (level 1)", node.get("NodeLevel"), 1)
check_true("base node rolled 5 leaf choices", len(node.get("LeafNodeIds") or []) == 5,
           str(node.get("LeafNodeIds")))
check_true("base node has a random seed", (node.get("NodeRandomSeed") or 0) > 0)
check_true("pre-begin craft carries MaxValue sentinels",
           str((r.get("CraftInfoDB") or {}).get("StartTime", "")).startswith("9999-12-31"),
           str((r.get("CraftInfoDB") or {}).get("StartTime")))

if len(node.get("LeafNodeIds") or []) == 5:
    for tier in (1, 2, 3):
        s = send({"Protocol": 21001, "SessionKey": session, "SlotId": 0, "LeafNodeIndex": 1})
        sel = s.get("SelectedNodeDB") or {}
        if tier == 1:
            check("SelectNode -> tier-1 node with committed id",
                  (sel.get("NodeTier"), sel.get("NodeId") is not None), (1, True))
            check_true("selected node keeps its empty leaf list", sel.get("LeafNodeIds") == [],
                       str(sel.get("LeafNodeIds")))
        if tier < 3:
            lv = send({"Protocol": 21002, "SessionKey": session, "SlotId": 0, "CraftNodeType": tier,
                       "ConsumeGoldAmount": 0,
                       "ConsumeRequestDB": {"ConsumeItemServerIdAndCounts": {}, "ConsumeEquipmentServerIdAndCounts": {}, "ConsumeFurnitureServerIdAndCounts": {}}})
            got = len((lv.get("CraftNodeDB") or {}).get("LeafNodeIds") or [])
            check_true(f"tier-{tier} node leveled and rolled leaves", got == 5, f"leaves={got}")

    b = send({"Protocol": 21003, "SessionKey": session, "SlotId": 0})
    info = b.get("CraftInfoDB") or {}
    nodes = info.get("Nodes") or []
    check("BeginProcess resolves 4 nodes (base + 3 tiers)", len(nodes), 4)
    resolved = [n for n in nodes if n.get("CraftNodeResult")]
    check_true("selected tiers resolved results", len(resolved) == 3,
               f"resolved={len(resolved)} sample={json.dumps(resolved[0] if resolved else {})[:150]}")
    check_true("last-tier node dropped its leaf list", "LeafNodeIds" not in nodes[-1],
               str(nodes[-1].get("LeafNodeIds")))
    start = str(info.get("StartTime", "")); end = str(info.get("EndTime", ""))
    check_true("processing window is 7h30m", start and end and
               (datetime.datetime.fromisoformat(end) - datetime.datetime.fromisoformat(start)) == datetime.timedelta(hours=7, minutes=30),
               f"{start} -> {end}")

    c2 = send({"Protocol": 21004, "SessionKey": session, "SlotId": 0})
    check_true("CompleteProcess collapses EndTime to now",
               str((c2.get("CraftInfoDB") or {}).get("EndTime", "")) <= datetime.datetime.now().isoformat(),
               str((c2.get("CraftInfoDB") or {}).get("EndTime")))

    rw = send({"Protocol": 21005, "SessionKey": session, "SlotId": 0})
    check_true("Reward grants a ParcelResultDB", rw.get("ParcelResultDB") is not None,
               json.dumps(rw.get("ParcelResultDB") or {})[:150])
    lst = send({"Protocol": 21000, "SessionKey": session})
    check_true("post-reward Craft_List omits CraftInfos", "CraftInfos" not in lst)
    check_true("Craft_List never sends ShiftingCraftInfos", "ShiftingCraftInfos" not in lst)
else:
    print("  SKIP rest of chain (no leaf candidates - GachaCraftNode excel empty?)")
    FAILURES.append("craft leaf candidates unavailable")

print("\n--- Shop_BuyAP: cap + error envelope + success shape ---------------------------")
set_currency(acct, 5, 970)          # ActionPoint
for gem_type in (2, 3, 4):          # GemPaid / GemBonus / Gem, so the cost is affordable
    set_currency(acct, gem_type, 99999)
name, err = send_full({"Protocol": 10009, "SessionKey": session, "ShopUniqueId": 21, "PurchaseCount": 1})
check("over-cap purchase is an Error response", name, "Error")
check("  ...code 10006 ShopCannotPurchaseActionPointLimitOver", err.get("ErrorCode"), 10006)
check("  ...envelope keys exactly official's", sorted(err.keys()), ["ErrorCode", "Protocol", "ServerTimeTicks"])
check("  ...Protocol -1", err.get("Protocol"), -1)

set_currency(acct, 5, 100)
name, ok = send_full({"Protocol": 10009, "SessionKey": session, "ShopUniqueId": 21, "PurchaseCount": 1})
check("in-cap purchase succeeds", name, "Shop_BuyAP")
ap_after = ((ok.get("AccountCurrencyDB") or {}).get("CurrencyDict") or {}).get("ActionPoint")
check_true("AP increased past 100", isinstance(ap_after, int) and ap_after > 100, f"AP={ap_after}")
check_true("ParcelResultDB carries DisplaySequence", bool((ok.get("ParcelResultDB") or {}).get("DisplaySequence")))
prod = ok.get("ShopProductDB") or {}
check_true("ShopProductDB has ProductType and no Price", prod.get("ProductType") == 1 and "Price" not in prod,
           json.dumps(prod))

print("\n--- Clan_Lobby structure vs recovered official ---------------------------------")
cl = send({"Protocol": 28000, "SessionKey": session})
clan = cl.get("AccountClanDB") or {}
check("channel name is channel_<ClanDBId>", clan.get("ClanChannelName"), "channel_777")
check_true("no ClanPresidentRepresentCharacterCostumeId key",
           "ClanPresidentRepresentCharacterCostumeId" not in clan)
me = cl.get("AccountClanMemberDB") or {}
check_true("own member entry has no AttachmentDB", "AttachmentDB" not in me)
others = cl.get("ClanMemberDBs") or []
check_true("other members carry CafeComfortValue or omit-as-zero",
           all("AttachmentDB" in m or m.get("AccountId") == acct for m in others),
           f"members={len(others)}")

print("\n--- cleanup -------------------------------------------------------------------")
clean(acct)

print()
if FAILURES:
    print(f"{len(FAILURES)} FAILURE(S): " + ", ".join(FAILURES))
    sys.exit(1)
print("all checks passed")
