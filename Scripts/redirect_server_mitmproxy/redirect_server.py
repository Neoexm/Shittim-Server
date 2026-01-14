import gzip
import json
import socket
from mitmproxy import http, ctx

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

SERVER_HOST = get_local_ip()  # or '192...'
SERVER_PORT = 5000
IRC_PORT = 6667

def load(loader):
    ctx.options.ignore_hosts = [
        f"{SERVER_HOST}:{IRC_PORT}",
        # "gtable.inface.nexon.com",
        # "public.api.nexon.com",
        # "signin.nexon.com",
        # "toy.log.nexon.io"
    ]

print(f"Using IP Address: {SERVER_HOST}:{SERVER_PORT}")
print(f"Using IRC Address: {SERVER_HOST}:{IRC_PORT}")
print("If this is incorrect, please run the server setup manually")

REWRITE_HOST_LIST = [
    'd2vaidpni345rp.cloudfront.net',
    'nxm-eu-bagl.nexon.com',
    'nxm-ios-bagl.nexon.com',
    'nxm-kr-bagl.nexon.com',
    'nxm-tw-bagl.nexon.com',
    'nxm-th-bagl.nexon.com',
    'nxm-or-bagl.nexon.com',

    # Accounts
    # 'public.api.nexon.com',
    # 'signin.nexon.com',

    # Other
    'psm-log.ngs.nexon.com',
    'gtable.inface.nexon.com'
]

KILL_HOST_LIST = [
    'sdk-push.mp.nexon.com',
    'config.na.nexon.com'
]

PING_HOST_REDIRECT = [
    'toy.log.nexon.io',
    'x-init.ngs.nexon.com',
    'x-phaethon.ngs.nexon.com',
    'x-csauth.ngs.nexon.com'
]

OTHER_KILL_HOST = [
    'blacklist.csv',
    'chattingblacklist.csv',
    'whitelist.csv'
]

def request(flow: http.HTTPFlow) -> None:
    if any(flow.request.pretty_host.endswith(host) for host in KILL_HOST_LIST):
        flow.kill()
        return
    if any(flow.request.url.endswith(item) for item in OTHER_KILL_HOST):
        flow.kill()
        return
    if any(flow.request.pretty_host.endswith(host) for host in PING_HOST_REDIRECT):
        flow.response = http.Response.make(
            200,
            b"",
            {"Content-Type": "text/plain"}
        )
        return
    if flow.request.url.endswith("client.all.secure"):
        flow.kill()
        return
    if flow.request.url.endswith("sdk-api/user-meta/last-login"):
        flow.kill()
        return
    if flow.request.pretty_host in REWRITE_HOST_LIST:
        flow.request.scheme = 'http'
        flow.request.host = SERVER_HOST
        flow.request.port = SERVER_PORT
        return
        
def response(flow: http.HTTPFlow) -> None:
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