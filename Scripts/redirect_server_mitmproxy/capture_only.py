"""
Passive capture addon for the OFFICIAL baseline test.

Does NOT rewrite, kill, or redirect anything — every request is forwarded to the
real destination so the game logs in against official Nexon servers. Small
responses (login/config/SDK) are captured in full; large bodies (asset/catalog
downloads) are streamed so they don't bloat the capture or stall the run.

Run with:
  mitmdump -m wireguard --no-http2 --mode local:BlueArchive.exe \
           -s capture_only.py -w official_capture.flows --set termlog_verbosity=warn
"""
from mitmproxy import http, ctx


def load(loader):
    # Only ignore the IRC chat port; everything else is observed + forwarded.
    ctx.options.ignore_hosts = [r".*:6667"]


def request(flow: http.HTTPFlow) -> None:
    # Lightweight progress line for the auth-relevant calls.
    p = flow.request.path
    if any(s in p for s in ("signInWithTicket", "enterToy", "getCountry", "/ias",
                            "/ims", "configurations", "signin", "toy/")):
        print(f"[CAP] {flow.request.method} {flow.request.pretty_host}{p.split('?')[0]}")


def response(flow: http.HTTPFlow) -> None:
    cl = flow.response.headers.get("content-length")
    if cl and cl.isdigit() and int(cl) > 524288:  # stream bodies > 512 KB (assets)
        flow.response.stream = True
