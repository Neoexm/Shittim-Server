import gzip
import json
import os
import socket
import datetime
from pathlib import Path
from mitmproxy import http, ctx

# --- Diagnostic request logger -------------------------------------------------
# Logs EVERY request the game makes (regardless of host/disposition) to a flat
# file we can read. This is the only way to observe requests that go to real
# Nexon hosts (via the WireGuard tunnel) rather than our loopback server, which
# our own Serilog already captures. Used to find the request the game issues
# after get_primary_link that never gets served (the 21s-timeout gap).
RLOG_PATH = Path(r"C:\Users\tomda\Documents\Shittim-Server\redirect_requests.log")

def rlog(msg: str) -> None:
    try:
        ts = datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]
        with RLOG_PATH.open("a", encoding="utf-8") as f:
            f.write(f"{ts} {msg}\n")
    except Exception:
        pass

def get_local_ip():
    try:
        # Create a socket that connects to an external server (doesn't actually send data)
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        local_ip = s.getsockname()[0]
        s.close()
        return local_ip
    except Exception:
        return "127.0.0.1"  # Fallback to localhost if detection fails

SERVER_HOST = "127.0.0.1"
SERVER_PORT = 5000
GATEWAY_PORT = 5100
IRC_PORT = 6667
NGS_PROBE_PORT = 58880
X_INIT_RESPONSE_PATH = Path(__file__).with_name("ngs_x_init_response.bin")
NGS_PASSTHROUGH = os.environ.get("SHITTIM_NGS_PASSTHROUGH") == "1"
NGS_HOSTS = [
    'x-init.ngs.nexon.com',
    'x-phaethon.ngs.nexon.com',
    'x-csauth.ngs.nexon.com'
]

def load(loader):
    rlog("==== redirect_server.py (re)loaded — new logging session ====")
    ctx.options.ignore_hosts = [
        f"{SERVER_HOST}:{SERVER_PORT}",
        f"{SERVER_HOST}:{GATEWAY_PORT}",
        f"{SERVER_HOST}:{IRC_PORT}",
        f".*:{NGS_PROBE_PORT}",
        # "gtable.inface.nexon.com",
        # "public.api.nexon.com",
        # "signin.nexon.com",
        # "toy.log.nexon.io"
    ]

print(f"Using IP Address: {SERVER_HOST}:{SERVER_PORT}")
print(f"Using IRC Address: {SERVER_HOST}:{IRC_PORT}")
print(f"NGS passthrough: {'enabled' if NGS_PASSTHROUGH else 'disabled'}")
print("If this is incorrect, please run the server setup manually")

REWRITE_HOST_LIST = [
    'd2vaidpni345rp.cloudfront.net',
    'nxm-eu-bagl.nexon.com',
    'nxm-ios-bagl.nexon.com',
    'nxm-kr-bagl.nexon.com',
    'nxm-tw-bagl.nexon.com',
    'nxm-th-bagl.nexon.com',
    'nxm-or-bagl.nexon.com',

    # Accounts / Auth / SDK backends
    'public.api.nexon.com',
    'sandbox.api.nexon.com',
    'signin.nexon.com',
    'pre-signin.nexon.com',
    'test-signin.nexon.com',
    'dev-signin.nexon.com',

    # Other
    'psm-log.ngs.nexon.com',
    # Nexon config service (na_time_sync / na_grclist_query) — gamescale/Inface SDK
    # init gates on this; must be served, not killed.
    'config.na.nexon.com',
    # GTable (all env variants)
    'gtable.inface.nexon.com',
    'test-gtable.inface.nexon.com',
    'dev-gtable.inface.nexon.com'
]

KILL_HOST_LIST = [
    'sdk-push.mp.nexon.com'
    # config.na.nexon.com: do NOT kill — the gamescale/Inface SDK gates its init on
    # na_time_sync (config.na). Killing it leaves the SDK uninitialized so the Bolt
    # sign-in base URL is never set and /signInWithTicket.nx never fires. Redirect it
    # to our server (REWRITE_HOST_LIST) and serve the config endpoints instead.
]

PING_HOST_REDIRECT = [
    'toy.log.nexon.io',
    'x-phaethon.ngs.nexon.com',
    'x-csauth.ngs.nexon.com'
]

OTHER_KILL_HOST = [
    'blacklist.csv',
    'chattingblacklist.csv',
    'whitelist.csv'
]

def request_host(flow: http.HTTPFlow) -> str:
    host = flow.request.headers.get("Host") or flow.request.pretty_host or flow.request.host
    return host.split(":", 1)[0].lower()

def host_endswith(host: str, hosts: list[str]) -> bool:
    return any(host.endswith(item) for item in hosts)

def request(flow: http.HTTPFlow) -> None:
    host = request_host(flow)
    rlog(f"REQ  {flow.request.method:4} {flow.request.scheme}://{flow.request.pretty_host}:{flow.request.port}{flow.request.path}")

    if '/gid/' in flow.request.path:
        print(f"[GTable] {flow.request.method} {host}{flow.request.path}")

    if NGS_PASSTHROUGH and host_endswith(host, NGS_HOSTS):
        print(f"[NGS passthrough] {flow.request.method} {host}{flow.request.path}")
        rlog(f"  -> NGS-PASSTHROUGH {host}")
        return

    if host_endswith(host, KILL_HOST_LIST):
        rlog(f"  -> KILL (host list) {host}")
        flow.kill()
        return
    if any(flow.request.url.endswith(item) for item in OTHER_KILL_HOST):
        rlog(f"  -> KILL (url list) {flow.request.path}")
        flow.kill()
        return
    if host == 'bolo7yechd.execute-api.ap-northeast-1.amazonaws.com' and flow.request.path.startswith('/prod/crexception-prop'):
        flow.response = http.Response.make(
            200,
            b'{"propCheck": false, "period": 10, "ratio": 10}',
            {"Content-Type": "application/json"}
        )
        return
    if host == 'x-init.ngs.nexon.com' and flow.request.path == '/v1':
        body = X_INIT_RESPONSE_PATH.read_bytes() if X_INIT_RESPONSE_PATH.exists() else b""
        rlog(f"  -> STUB x-init.ngs /v1 ({len(body)} bytes)")
        flow.response = http.Response.make(
            200,
            body,
            {"Content-Type": "application/json", "CE": "G"}
        )
        return
    # gamescale platform config (pcc/gid/<id>.json). The game requests it over HTTP and the
    # real host answers 301 -> HTTPS, which the game's gamescale client does NOT follow, so it
    # never gets this config and the gamescale SDK init can stall the login. Serve it directly.
    if host == 'platform.gamescale.nexon.com' and '/pcc/gid/' in flow.request.path:
        pcc = ('{"toy_service_id":2079,"arena_product_id":59754,"game_client_id":null,'
               '"portal_game_code":"1000158","krpc_game_code":74280,"jppc_game_code":null,'
               '"na_service_id":1050768977,"na_region_host":null,"krpc_service_code":null,'
               '"eve_gameinfo_id":null,"twitch_game_id":"1571205119","chzzk_game_id":"Blue_Archive",'
               '"project_id":"d8e6e343","guss_service_code":null,"guid":"guid","world_id":null,'
               '"gcid":null,"krpc_member_access_code":"2000088010","jppc_gm":null,'
               '"google_oauth_billing_client_redirect_uri":null,"krpc_product_type":null,'
               '"jppc_product_type":null,"coin_type":null,"alltem_code":"bluearchive","nisms_code":null,'
               '"nxshop_code":null,"google_oauth_billing_client_id":null,'
               '"google_oauth_billing_client_secret":null,"arena_service_code":null,'
               '"str_env_type":"LIVE","game_release_status":"released","nemo_service_id":null,'
               '"game_name_ko":"Blue Archive","game_name_en":"Blue Archive","gid":"2079",'
               '"last_modified":{"modify_date":"2026-02-26T10:07:48.478Z","admin_no":333},'
               '"krpc_alltem_code":"bluearchive",'
               '"created":{"create_date":"2021-10-28T07:35:22.366Z","admin_no":2}}').encode('utf-8')
        rlog(f"  -> SERVE gamescale pcc ({len(pcc)} bytes)")
        flow.response = http.Response.make(200, pcc, {"Content-Type": "application/json"})
        return
    if host_endswith(host, PING_HOST_REDIRECT):
        rlog(f"  -> STUB empty-200 (ping redirect) {host}")
        flow.response = http.Response.make(
            200,
            b"",
            {"Content-Type": "text/plain"}
        )
        return
    if flow.request.url.endswith("client.all.secure"):
        rlog("  -> KILL client.all.secure")
        flow.kill()
        return
    if flow.request.url.endswith("sdk-api/user-meta/last-login"):
        rlog("  -> KILL sdk-api/user-meta/last-login")
        flow.kill()
        return
    # Redirect any connection on port 5100 (game gateway) to our local gateway,
    # regardless of the destination host. This catches the case where the server config
    # has an empty GatewayUrl and the game uses its hardcoded Nexon gateway fallback.
    if flow.request.port == GATEWAY_PORT or (flow.request.pretty_host and ':5100' in flow.request.pretty_host):
        print(f"[Gateway Rewrite] {flow.request.method} {host}:{flow.request.port}{flow.request.path} -> {SERVER_HOST}:{GATEWAY_PORT}")
        rlog(f"  -> GATEWAY-REWRITE {host} -> {SERVER_HOST}:{GATEWAY_PORT}")
        flow.request.scheme = 'http'
        flow.request.host = SERVER_HOST
        flow.request.port = GATEWAY_PORT
        return

    if host_endswith(host, REWRITE_HOST_LIST):
        # For CloudFront (d2vaidpni345rp.cloudfront.net), only redirect server config paths.
        # Asset bundle / catalog downloads from CloudFront must go to the real CDN.
        if host == 'd2vaidpni345rp.cloudfront.net':
            is_server_config = (
                '/server_config/' in flow.request.path
                or flow.request.path.endswith('.json')
            ) and '_Live' in flow.request.path
            if not is_server_config:
                # Let CloudFront asset requests pass through (do not redirect)
                rlog(f"  -> CLOUDFRONT-PASSTHROUGH {flow.request.path}")
                return
        print(f"[Rewrite] {flow.request.method} {host}{flow.request.path} -> {SERVER_HOST}:{SERVER_PORT}")
        rlog(f"  -> REWRITE {host} -> {SERVER_HOST}:{SERVER_PORT}")
        flow.request.scheme = 'http'
        flow.request.host = SERVER_HOST
        flow.request.port = SERVER_PORT
        return

    # No rule matched: mitmproxy forwards this to the REAL host over the tunnel.
    rlog(f"  -> FORWARD-REAL {host}{flow.request.path}")

def response(flow: http.HTTPFlow) -> None:
    rlog(f"RESP {flow.request.method:4} {flow.request.pretty_host}:{flow.request.port}{flow.request.path} -> {flow.response.status_code}")
    flow.response.stream = True
    # if flow.request.url.endswith('/api/gateway'):
    #     try:
    #         req = flow.request.raw_content
    #         res = json.loads(flow.response.text)
    #         protocol = res['protocol']

    #         mx_end = req.rfind(b'\r\n', 0, len(req) - 1)
    #         mx_start = req.rfind(b'\r\n\r\n')
    #         req_mx = req[mx_start + 4:mx_end]
    #         req_bytes = req_mx[12:]
    #         req_bytes = bytearray([x ^ 0xD9 for x in req_bytes])
    #         req_bytes = gzip.decompress(req_bytes)
    #         print(f'Protocol: {protocol}')
    #         print(f'[OUT]->{json.loads(req_bytes)}')
    #         print(f'[IN]<--{json.loads(res["packet"])}')
    #         print('')
    #     except Exception as e:
    #         print('Failed to dump packet', e)
    #     return
